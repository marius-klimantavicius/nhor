# C05: typed controlled lazy trees

`TreeController<TItem>` in native Winter owns data identity, selection, expansion,
and lazy-load state without requiring a Window or Blazor. `DataTree<TItem>` renders
that controller. The original native `TreeView`/`TreeNode` and Razor
`TreeView`/`TreeViewNode`, string-tag callbacks, and `OnNativeTreeView` remain available.

```csharp
using var data = new TreeController<Project>(
    keySelector: p => p.Id,
    textSelector: p => p.Name,
    childrenSelector: p => p.Children,
    iconSelector: p => p.IconSvg,
    mayHaveChildren: p => p.IsLazy,
    loadChildren: LoadProjectsAsync); // Task<IReadOnlyList<Project>>, CancellationToken
data.SetItems(projects);
data.Synchronize(selectedKey, expandedKeys);
using var view = new DataTree<Project>(data);
view.Requested += request => HandleProjectAction(request);
```

Keys are non-null, globally unique **ordinal, case-sensitive strings**. They are
independent of labels; duplicate names are supported. Duplicate keys reject an
entire replacement atomically. `SetItems` replaces the known hierarchy, invalidates
all old loads and lazy caches, and retains selected/expanded keys still present in
the new hierarchy, including reparented keys. Selection clears when its key is
removed. To retain a previously loaded child during replacement, include it in
the replacement's children. Mutating an item in place requires `SetItems` again.
Use `SetItems(items, selectedKey, expandedKeys)` to replace data and controlled
state atomically, including Reset without briefly reloading old expansion.

`Select`, `ExpandAsync`, `Collapse`, `RetryAsync`, `RefreshAsync`, `Activate`, and
`Reveal` operate on keys. Reveal expands ancestors and scrolls a **known** key into
view; it does not search unloaded folders. Collapse removes descendant expansion
and cancels descendant loads. Successful loads, including empty results, are cached
until refresh/replacement. Refresh invalidates a lazy node's children and reloads
it only when expanded and visible. Ordinary loader/selector/duplicate errors become
retryable node errors. Exceptions from application notification subscribers remain
application errors.

Snapshots (`Roots`, `GetNode`, `ExpandedKeys`) are read-only. Nodes expose typed
`Item`, `Text`, `Icon`, `IsExpanded`, `MayHaveChildren`, `LoadState`, `Error`, and
`Children`. `Changed` notifies presentation updates; `Requested` carries an immutable
`TreeRequest<TItem>` with `Kind`, `Key`, `Item`, selected key and expanded-key snapshot.
Programmatic `Synchronize` and data/load updates do not emit user requests. Request
notifications preserve every expand/collapse/retry/reveal/activation identity.
Retry revalidates the controller lifetime, data revision, entry and visible
expansion after application notification. A listener may collapse, replace data
or dispose the controller there; rejected work does not start. Loading-state
notifications have the same guard before invoking application loaders.

Loads use cancellation **and** per-entry generation/reference checks. Collapse,
refresh, replacement, removal and controller disposal invalidate older completions,
including loaders that ignore cancellation or finish out of order. The controller
serializes state operations; notifications run on the mutating thread. Native
presentation marshals background completion to its existing Window dispatcher.
UI callers must perform view operations on that owner thread.

A supplied controller is caller-owned; disposing a view detaches subscriptions
without disposing that controller or its loader/service. Pass `ownsController: true`
to native `DataTree` to transfer controller ownership. Detached views stop observing
loads and resynchronize on attachment. A caller-owned controller may continue its
work while detached; its owner controls cancellation/disposal. Owned row paints are
released on detach and rebuilt on attachment; logical nodes and keys survive.

## Razor

```razor
<DataTree TItem="Project" Items="projects"
          KeySelector="@(p => p.Id)" TextSelector="@(p => p.Name)"
          ChildrenSelector="@(p => p.Children)" IconSelector="@(p => p.IconSvg)"
          MayHaveChildren="@(p => p.IsLazy)" LoadChildren="LoadProjectsAsync"
          @bind-SelectedKey="selected" @bind-ExpandedKeys="expanded"
          OnActivate="OpenProject" OnRequest="RecordAction"
          ResetVersion="resetVersion" />
```

`SelectedKey` is `string?`; `ExpandedKeys` is `IReadOnlySet<string>?` (null means
empty). `SelectedKeyChanged` and `ExpandedKeysChanged` take precedence over legacy
`OnSelectionChanged`/`OnExpandedKeysChanged` when they have delegates. One user
notification calls one callback. Parameter synchronization emits no user echo.
An explicit parent rejection applies even when the declared value is unchanged.
Increment `ResetVersion` for an intentional reset before queued input is delivered;
it supersedes queued requests and resets an internally owned controller's data.
Replace `Items` rather than mutating the same collection reference silently.

Alternatively pass `Controller` and bind selected/expanded state; item/selector/load
parameters then belong to that caller-owned controller. The wrapper disposes only
controllers it creates from `Items`. Its `DataController`, `Reveal`, `RefreshAsync`,
and `Focus` members support composition without accessing renderer handlers.

Each queued discrete request retains its original key, item, and callback snapshot
across ordinary renders with unchanged subscriptions. Explicit callback replacement
or clearing, reset, data revision/replacement, or disposal invalidates it, including
while an earlier selection/expansion/activation callback awaits. The wrapper checks
that lifetime again before starting OnRequest and never retargets an old action to
a replacement callback. Application handlers already started own their continuations.
Pending selection/expansion notifications do not undo other queued fields from
the same pointer action. Application async callbacks must set bound state before
awaiting and use their own cancellation/generation check for later application
work; the wrapper never restores an old value after awaiting a callback.

## Interaction and playground registration

TreeView now participates in Tab/Shift+Tab focus traversal. Up/Down and Home/End
select; Left collapses or moves to the parent; Right expands or enters the first
child. Enter activates, or retries an error node/message. Pointer arrows expand;
double-clicking an error message retries. Loading, empty, and error/retry messages
appear as ordinary rows. `FontSize` uses logical units and rows grow with text;
selection/reveal ensure visibility, including after font/viewport changes.

Register native `Scenarios.ProjectBrowserScenario(Window).Content` through the
existing `ScenarioHost.AddScenario` seam, retaining/disposal of the scenario owner.
Register Razor `Scenarios.ProjectBrowser` with `OwnerWindow`. Entry-point registration
is deliberately left to the integration owner. Both modules provide duplicate-name
notes, lazy src, empty, fail-once packages, Reveal selected, Rename, Reorder,
Refresh src, Reset, theme/text-size controls, and selected/loading/error/cancellation
results. The fixture is deterministic and never browses the filesystem.

This is a bounded text/icon/status tree. Arbitrary row templates, virtualization,
filesystem browsing, and multi-selection are outside C05. Qualification is Linux
X11 on owned Xvfb/software rendering and software GL; no other-platform, hardware
GL, NativeAOT, IME, or high-DPI qualification is implied.
