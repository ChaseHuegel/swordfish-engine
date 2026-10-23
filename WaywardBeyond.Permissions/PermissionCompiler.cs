using System;
using System.Collections.Generic;
using System.Linq;
using Swordfish.Library.Util;

namespace WaywardBeyond.Permissions;

/// <summary>
///     Merges loaded permission files into an immutable <see cref="PermissionPolicy"/>. Same-named groups
///     and users merge by union across files. A user's own nodes override the group baseline. Inheritance
///     resolves depth-capped, so a cycle contributes every acyclic edge and is reported once. Compilation
///     never throws.
/// </summary>
internal static class PermissionCompiler
{
    private const int MAX_INHERITANCE_DEPTH = 64;
    private const int MAX_GROUPS_PER_USER = 256;

    public static PermissionPolicy Compile(IReadOnlyList<SourcedPermissionFile> files, PermissionDiagnostics diagnostics)
    {
        Dictionary<string, MergedGroup> groups = MergeGroups(files, diagnostics);
        Dictionary<string, MergedUser> users = MergeUsers(files, diagnostics);

        var graph = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, MergedGroup> entry in groups)
        {
            graph[entry.Key] = [.. entry.Value.Inherits.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)];
        }

        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasDefault = graph.ContainsKey(PermissionPolicy.DEFAULT_ROLE);
        string[] defaultClosure = hasDefault
            ? ResolveClosure(graph, groups, [PermissionPolicy.DEFAULT_ROLE], reportedCycles, diagnostics)
            : [];
        PermissionSet defaultSet = CompileSet(CollectNodes(defaultClosure, groups));

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
            PermissionSet groupSet = CompileSet(CollectNodes(closure, groups));

            //  The user's own nodes are a separate layer, so a user entry always overrides a group entry.
            compiledUsers[entry.Key] = user.Permissions.Count == 0
                ? groupSet
                : CompileSet(user.Permissions, groupSet);
        }

        return new PermissionPolicy(compiledUsers, defaultSet, groups.Count, users.Count, diagnostics);
    }

    private static Dictionary<string, MergedGroup> MergeGroups(IReadOnlyList<SourcedPermissionFile> files, PermissionDiagnostics diagnostics)
    {
        var groups = new Dictionary<string, MergedGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (SourcedPermissionFile sourced in files)
        {
            foreach (KeyValuePair<string, PermissionGroupGrant> entry in sourced.File.Groups)
            {
                string name = entry.Key == null ? string.Empty : entry.Key.Trim();
                if (!PermissionSyntax.IsValidName(name))
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
                if (!PermissionSyntax.IsValidName(userId))
                {
                    diagnostics.Warn($"File \"{sourced.Source}\" declares the invalid user id \"{entry.Key}\"; the entry is ignored.");
                    continue;
                }

                if (!users.TryGetValue(userId, out MergedUser? user))
                {
                    user = new MergedUser();
                    users[userId] = user;
                }

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
            if (!PermissionSyntax.IsValidName(name))
            {
                diagnostics.Warn($"File \"{source}\" declares the invalid role \"{raw}\" on {kind} \"{owner}\"; the role is ignored.");
                continue;
            }

            target.Add(name);
        }
    }

    private static void MergePermissions(
        List<PermissionNode> nodes,
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
            Result<PermissionNode> parsed = PermissionSyntax.Parse(node);
            if (!parsed.Success)
            {
                diagnostics.Warn($"File \"{source}\" declares the invalid permission node \"{node}\" on {kind} \"{owner}\"; the node is ignored.");
                continue;
            }

            if (seen.Add(node))
            {
                nodes.Add(parsed.Value);
            }
        }
    }

    private static string[] ResolveClosure(
        Dictionary<string, string[]> graph,
        Dictionary<string, MergedGroup> groups,
        IReadOnlyList<string> roots,
        HashSet<string> reportedCycles,
        PermissionDiagnostics diagnostics
    ) {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var onPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();
        var closure = new List<string>();
        var depthReported = false;
        var countReported = false;

        foreach (string root in roots)
        {
            if (root.Length == 0 || visited.Contains(root))
            {
                continue;
            }

            if (!graph.ContainsKey(root))
            {
                diagnostics.Warn($"Role \"{root}\" does not match any declared group.");
                continue;
            }

            Visit(root, depth: 0);
        }

        return [.. closure];

        void Visit(string group, int depth)
        {
            visited.Add(group);
            onPath.Add(group);
            path.Add(group);
            closure.Add(group);

            string[] parents = graph.TryGetValue(group, out string[]? resolved) ? resolved : [];
            foreach (string parent in parents)
            {
                if (!graph.ContainsKey(parent))
                {
                    diagnostics.Warn($"Group \"{group}\" inherits unknown group \"{parent}\".");
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

                if (depth + 1 >= MAX_INHERITANCE_DEPTH)
                {
                    if (!depthReported)
                    {
                        diagnostics.Error($"Group \"{group}\" inherits \"{parent}\", which exceeds the maximum inheritance depth of {MAX_INHERITANCE_DEPTH}; the branch is truncated.");
                        depthReported = true;
                    }

                    continue;
                }

                if (closure.Count >= MAX_GROUPS_PER_USER)
                {
                    if (!countReported)
                    {
                        diagnostics.Error($"Group \"{group}\" inherits \"{parent}\", which exceeds the maximum of {MAX_GROUPS_PER_USER} groups per role set; the branch is truncated.");
                        countReported = true;
                    }

                    continue;
                }

                Visit(parent, depth + 1);
            }

            onPath.Remove(group);
            path.RemoveAt(path.Count - 1);
        }
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

        string sourceList = string.Join(", ", sources);
        diagnostics.Warn($"Inheritance cycle detected: {string.Join(" -> ", cycle)}. The closing edge is ignored. Sources: {sourceList}.");
    }

    private static List<PermissionNode> CollectNodes(string[] closure, Dictionary<string, MergedGroup> groups)
    {
        var nodes = new List<PermissionNode>();
        foreach (string name in closure)
        {
            if (groups.TryGetValue(name, out MergedGroup? group))
            {
                nodes.AddRange(group.Permissions);
            }
        }

        return nodes;
    }

    private static PermissionSet CompileSet(List<PermissionNode> nodes, PermissionSet? fallback = null)
    {
        var exact = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var wildcards = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool? global = null;

        foreach (PermissionNode node in nodes)
        {
            if (node.IsGlobal)
            {
                global = (global ?? true) && !node.IsDeny;
                continue;
            }

            //  Deny wins inside one layer. Across layers, the more specific layer decides.
            Dictionary<string, bool> map = node.IsWildcard ? wildcards : exact;
            bool allow = !node.IsDeny;
            map[node.Key] = map.TryGetValue(node.Key, out bool existing) ? existing && allow : allow;
        }

        return new PermissionSet(exact, wildcards, global, fallback);
    }

    private sealed class MergedGroup
    {
        public readonly HashSet<string> Inherits = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<PermissionNode> Permissions = [];
        public readonly HashSet<string> SeenPermissions = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Sources = new(StringComparer.Ordinal);
    }

    private sealed class MergedUser
    {
        public readonly HashSet<string> Roles = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<PermissionNode> Permissions = [];
        public readonly HashSet<string> SeenPermissions = new(StringComparer.OrdinalIgnoreCase);
    }
}
