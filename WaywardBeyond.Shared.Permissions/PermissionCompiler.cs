using System;
using System.Collections.Generic;
using System.Linq;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>
///     Merges loaded permission files into an immutable <see cref="PermissionPolicy"/>. Same-named groups
///     and users merge by union across files. Inheritance resolves iteratively, so a cycle contributes
///     every acyclic edge and is reported once. Compilation never recurses and never throws.
/// </summary>
internal static class PermissionCompiler
{
    private const int MAX_INHERITANCE_DEPTH = 64;
    private const int MAX_GROUPS_PER_USER = 256;

    public static PermissionPolicy Compile(IReadOnlyList<SourcedPermissionFile> files, PermissionDiagnostics diagnostics)
    {
        Dictionary<string, MergedGroup> groups = MergeGroups(files, diagnostics);
        Dictionary<string, MergedUser> users = MergeUsers(files, diagnostics);

        var graph = new GroupGraph();
        foreach (KeyValuePair<string, MergedGroup> entry in groups)
        {
            graph.Add(entry.Key, [.. entry.Value.Inherits.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)]);
        }

        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasDefault = graph.Contains(PermissionPolicy.DEFAULT_ROLE);
        string[] defaultClosure = hasDefault
            ? ResolveClosure(graph, groups, [PermissionPolicy.DEFAULT_ROLE], reportedCycles, diagnostics)
            : [];
        PermissionSet defaultSet = CompileSet(defaultClosure, CollectNodes(defaultClosure, groups, null), diagnostics);

        var compiledUsers = new Dictionary<string, PermissionSet>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, MergedUser> entry in users)
        {
            MergedUser user = entry.Value;
            if (user.Roles.Count == 0 && user.Permissions.Count == 0)
            {
                compiledUsers[entry.Key] = defaultSet;
                continue;
            }

            var roots = new List<string>(user.Roles);
            roots.Sort(StringComparer.OrdinalIgnoreCase);
            if (hasDefault)
            {
                roots.Add(PermissionPolicy.DEFAULT_ROLE);
            }

            string[] closure = ResolveClosure(graph, groups, roots, reportedCycles, diagnostics);
            compiledUsers[entry.Key] = CompileSet(closure, CollectNodes(closure, groups, user.Permissions), diagnostics);
        }

        return new PermissionPolicy(
            compiledUsers,
            defaultSet,
            [.. groups.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)],
            [.. users.Keys.OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)],
            diagnostics
        );
    }

    private static Dictionary<string, MergedGroup> MergeGroups(IReadOnlyList<SourcedPermissionFile> files, PermissionDiagnostics diagnostics)
    {
        var groups = new Dictionary<string, MergedGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (SourcedPermissionFile sourced in files)
        {
            foreach (KeyValuePair<string, PermissionGroupGrant> entry in sourced.File.Groups)
            {
                string name = entry.Key == null ? string.Empty : entry.Key.Trim();
                if (!PermissionKey.IsValidName(name))
                {
                    diagnostics.Warn($"File \"{sourced.Source}\" declares the invalid group name \"{entry.Key}\"; the entry is ignored.");
                    continue;
                }

                if (!groups.TryGetValue(name, out MergedGroup? group))
                {
                    group = new MergedGroup();
                    groups[name] = group;
                }

                group.Sources.Add(sourced.Source);
                MergeNames(group.Inherits, entry.Value?.Inherits, "group", name, sourced.Source, diagnostics);
                MergePermissions(group.Permissions, group.SeenPermissions, entry.Value?.Permissions, "group", name, sourced.Source, diagnostics);
            }
        }

        return groups;
    }

    private static Dictionary<string, MergedUser> MergeUsers(IReadOnlyList<SourcedPermissionFile> files, PermissionDiagnostics diagnostics)
    {
        var users = new Dictionary<string, MergedUser>(StringComparer.OrdinalIgnoreCase);
        foreach (SourcedPermissionFile sourced in files)
        {
            foreach (KeyValuePair<string, PermissionUserGrant> entry in sourced.File.Users)
            {
                string userId = entry.Key == null ? string.Empty : entry.Key.Trim();
                if (!PermissionKey.IsValidName(userId))
                {
                    diagnostics.Warn($"File \"{sourced.Source}\" declares the invalid user id \"{entry.Key}\"; the entry is ignored.");
                    continue;
                }

                if (!users.TryGetValue(userId, out MergedUser? user))
                {
                    user = new MergedUser();
                    users[userId] = user;
                }

                user.Sources.Add(sourced.Source);
                MergeNames(user.Roles, entry.Value?.Roles, "user", userId, sourced.Source, diagnostics);
                MergePermissions(user.Permissions, user.SeenPermissions, entry.Value?.Permissions, "user", userId, sourced.Source, diagnostics);
            }
        }

        return users;
    }

    private static void MergeNames(
        HashSet<string> target,
        List<string>? values,
        string kind,
        string owner,
        string source,
        PermissionDiagnostics diagnostics
    ) {
        if (values == null)
        {
            return;
        }

        foreach (string raw in values)
        {
            string name = raw == null ? string.Empty : raw.Trim();
            if (!PermissionKey.IsValidName(name))
            {
                diagnostics.Warn($"File \"{source}\" declares the invalid role \"{raw}\" on {kind} \"{owner}\"; the role is ignored.");
                continue;
            }

            target.Add(name);
        }
    }

    private static void MergePermissions(
        List<string> nodes,
        HashSet<string> seen,
        List<string>? values,
        string kind,
        string owner,
        string source,
        PermissionDiagnostics diagnostics
    ) {
        if (values == null)
        {
            return;
        }

        foreach (string raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                diagnostics.Warn($"File \"{source}\" declares an empty permission node on {kind} \"{owner}\"; the node is ignored.");
                continue;
            }

            string node = raw.Trim();
            if (seen.Add(node))
            {
                nodes.Add(node);
            }
        }
    }

    private static string[] ResolveClosure(
        GroupGraph graph,
        Dictionary<string, MergedGroup> groups,
        IReadOnlyList<string> roots,
        HashSet<string> reportedCycles,
        PermissionDiagnostics diagnostics
    ) {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var onPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();
        var closure = new List<string>();
        var stack = new List<Frame>();

        foreach (string root in roots)
        {
            if (root.Length == 0 || visited.Contains(root))
            {
                continue;
            }

            if (!graph.Contains(root))
            {
                diagnostics.Warn($"Role \"{root}\" does not match any declared group.");
                continue;
            }

            visited.Add(root);
            onPath.Add(root);
            path.Add(root);
            closure.Add(root);
            stack.Add(new Frame(root, graph.GetParents(root)));

            while (stack.Count > 0)
            {
                int top = stack.Count - 1;
                Frame frame = stack[top];

                if (frame.Next >= frame.Parents.Length)
                {
                    onPath.Remove(frame.Group);
                    path.RemoveAt(path.Count - 1);
                    stack.RemoveAt(top);
                    continue;
                }

                string parent = frame.Parents[frame.Next];
                frame.Next++;
                stack[top] = frame;

                if (!graph.Contains(parent))
                {
                    diagnostics.Warn($"Group \"{frame.Group}\" inherits unknown group \"{parent}\".");
                    continue;
                }

                if (onPath.Contains(parent))
                {
                    ReportCycle(path, parent, groups, reportedCycles, diagnostics);
                    continue;
                }

                if (visited.Contains(parent))
                {
                    //  Already expanded through another branch; a diamond is not a cycle.
                    continue;
                }

                if (path.Count >= MAX_INHERITANCE_DEPTH)
                {
                    diagnostics.Error($"Inheritance below group \"{parent}\" exceeds the maximum depth of {MAX_INHERITANCE_DEPTH}; the branch is truncated.");
                    continue;
                }

                if (closure.Count >= MAX_GROUPS_PER_USER)
                {
                    diagnostics.Error($"Group expansion exceeded the maximum of {MAX_GROUPS_PER_USER} groups; the branch is truncated.");
                    continue;
                }

                visited.Add(parent);
                onPath.Add(parent);
                path.Add(parent);
                closure.Add(parent);
                stack.Add(new Frame(parent, graph.GetParents(parent)));
            }
        }

        return [.. closure];
    }

    private static void ReportCycle(
        List<string> path,
        string parent,
        Dictionary<string, MergedGroup> groups,
        HashSet<string> reportedCycles,
        PermissionDiagnostics diagnostics
    ) {
        int start = -1;
        for (var i = 0; i < path.Count; i++)
        {
            if (string.Equals(path[i], parent, StringComparison.OrdinalIgnoreCase))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            return;
        }

        string[] members = path.Skip(start).ToArray();
        string[] sorted = [.. members.OrderBy(static member => member, StringComparer.OrdinalIgnoreCase)];
        if (!reportedCycles.Add(string.Join('|', sorted)))
        {
            return;
        }

        var cycle = new List<string>(members) { parent };
        var sources = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string member in members)
        {
            if (groups.TryGetValue(member, out MergedGroup? group))
            {
                sources.UnionWith(group.Sources);
            }
        }

        string sourceList = sources.Count > 0 ? string.Join(", ", sources) : "unknown";
        diagnostics.Warn($"Inheritance cycle detected: {string.Join(" -> ", cycle)}. The closing edge is ignored. Sources: {sourceList}.");
    }

    private static List<string> CollectNodes(string[] closure, Dictionary<string, MergedGroup> groups, IReadOnlyList<string>? userNodes)
    {
        var nodes = new List<string>();
        foreach (string name in closure)
        {
            if (groups.TryGetValue(name, out MergedGroup? group))
            {
                nodes.AddRange(group.Permissions);
            }
        }

        if (userNodes != null)
        {
            nodes.AddRange(userNodes);
        }

        return nodes;
    }

    private static PermissionSet CompileSet(string[] roles, List<string> rawNodes, PermissionDiagnostics diagnostics)
    {
        var exact = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var wildcards = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var declared = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasGlobal = false;
        var global = true;

        foreach (string raw in rawNodes)
        {
            if (!PermissionKey.TryParse(raw, out PermissionNode node))
            {
                diagnostics.Warn($"Ignoring invalid permission node \"{raw}\".");
                continue;
            }

            if (seen.Add(raw))
            {
                declared.Add(raw);
            }

            if (node.IsGlobal)
            {
                hasGlobal = true;
                global = global && !node.IsDeny;
                continue;
            }

            //  Deny wins when both a grant and a deny declare the same node.
            Dictionary<string, bool> map = node.IsWildcard ? wildcards : exact;
            bool allow = !node.IsDeny;
            map[node.Key] = map.TryGetValue(node.Key, out bool existing) ? existing && allow : allow;
        }

        return new PermissionSet(
            allowAll: false,
            exact,
            wildcards,
            hasGlobal,
            global,
            roles,
            [.. declared]
        );
    }

    private struct Frame(string group, string[] parents)
    {
        public readonly string Group = group;
        public readonly string[] Parents = parents;
        public int Next;
    }

    private sealed class GroupGraph
    {
        private readonly Dictionary<string, string[]> _parents = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string group, string[] parents)
        {
            _parents[group] = parents;
        }

        public bool Contains(string group)
        {
            return _parents.ContainsKey(group);
        }

        public string[] GetParents(string group)
        {
            return _parents.TryGetValue(group, out string[]? parents) ? parents : [];
        }
    }

    private sealed class MergedGroup
    {
        public readonly HashSet<string> Inherits = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Permissions = [];
        public readonly HashSet<string> SeenPermissions = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Sources = new(StringComparer.Ordinal);
    }

    private sealed class MergedUser
    {
        public readonly HashSet<string> Roles = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Permissions = [];
        public readonly HashSet<string> SeenPermissions = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Sources = new(StringComparer.Ordinal);
    }
}
