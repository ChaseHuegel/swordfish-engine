# Code review

This is the lens applied to a change set before it is final. Use it for
self-review and peer review. It does not restate the style tables in
[development](../development.md). It states what to look for and why.

## Scope

A change is one subject. It is surgical. It stays within one module and its
direct consumers. It does not mix the engine and game sets. It is commitable
alone and builds standalone.

## Checklist

Run each check. Stop when a check fails.

- House vocabulary. Match the change to sibling modules and the naming spec:
  [specs/naming](specs/naming.md).
- Reuse the base. Delete members that shadow a base capability. If
  `IAssetDatabase<T>.Get` covers lookup and unknown ids, do not redeclare it
  and do not add a `Contains`. Keep only the API the base cannot provide.
- No parallel state. Keep one representation of loaded data. Do not hold the
  source objects just to enumerate their keys. Hold the ids you enumerate and
  nothing more.
- Minimal public surface. Make types `internal` by default. Make a type public
  only for a specific cross-assembly need. Seal leaf types.
- Concrete types. Declare the most concrete type that fits. Abstract only when
  it earns its cost: public, churn-prone, multi-implementation, or
  heterogeneous. Mutable collections are allowed, but avoid them in public
  immutable APIs. A database's loaded asset view is an immutable API; it does
  not hand out a mutable collection.
- Docs single-source. Write prose on the defining type only. Reference it on
  implementers and promoted fields with `<inheritdoc>` or
  `<inheritdoc cref="..."/>`. Keep summaries to one line.
- Normalize on one side. Enforce case-insensitivity on the data structure with
  `StringComparer.*IgnoreCase`, not with `ToLowerInvariant()` at every call
  site. Document the rule where the structure is declared.
- Docs pass. Map the change to its doc. Update the doc in the same change.
  Stale docs are a bug.