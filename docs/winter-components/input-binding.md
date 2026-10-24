# C01: input binding and text commits

Winter Blazor supports conventional typed bindings:

```razor
<TextBox @bind-Text="name" TextCommitted="CommitName" ResetVersion="resetVersion" />
<TextArea @bind-Text="notes" TextCommitted="CommitNotes" ResetVersion="resetVersion" />
<Checkbox @bind-IsChecked="shortcut" ResetVersion="resetVersion" />
<Slider @bind-Value="volume" Min="0" Max="100" Step="1" ResetVersion="resetVersion" />
<ComboBox Items="profiles" @bind-SelectedIndex="profile" ResetVersion="resetVersion" />
```

The paired callbacks are `TextChanged` (`string`), `IsCheckedChanged` (`bool`),
`ValueChanged` (`float`), and `SelectedIndexChanged` (`int`). A paired callback
with `HasDelegate` wins over `OnTextChanged`, `OnChanged`, `OnValueChanged`, or
`OnSelectionChanged`. The legacy callback is a fallback; one edit never calls both.
Clearing the paired callback restores the legacy fallback; clearing both stops
application notifications. Callback replacement invalidates queued input for the
previous subscription.

Parameter synchronization does not invoke either application change callback.
`Text = null` and `Placeholder = null` clear text. `Items = null` clears items;
`SelectedIndex = -1` clears selection. The existing default index of zero still
selects the first item when items are supplied. Scalar boolean
and number parameters use their ordinary non-nullable types. Item arrays preserve
empty labels and Unicode through object transport.

Slider range/step/value and ComboBox items/index are applied atomically. A range
change can clamp the displayed native value without changing the parent model:
the parent should normalize its model when changing constraints if it needs both
to remain equal. Clamping is synchronization and does not synthesize a user edit.

## Reset, rejection, and asynchronous handlers

The renderer records each delivered native field value in Blazor's retained render
tree before invoking its callback. A parent can reject an edit by retaining its
old parameter; the next parent render produces an actual native correction even
when the parent value itself did not change. An unchanged ordinary render keeps
the native caret/selection rather than replacing equal text.

For an intentional Reset, assign the desired model values, increment
`resetVersion`, and render. This also handles resetting an unchanged declared
value while a native edit is still queued for delivery. The version snapshots the
current value and cancels that input's undelivered scalar event. Ordinary
acceptance/rejection of an already delivered edit requires no reset version.

Update bound state before awaiting work. The parent is authoritative during an
await, including an intentional rejection or reset. The bridge does not restore
an old value when an earlier handler finishes; removed inputs detach their native
subscriptions. Event arguments are snapshots. Scalar edits may coalesce before
delivery; commits are discrete and preserve every action.

Application work that can finish out of order must use cancellation **and** a
generation/identity check, and marshal UI completion through its owning dispatcher
(`InvokeAsync` in Razor). The bridge cannot undo assignments made by application
code after an await. The playground's delayed save demonstrates this contract.

## Text commits and native APIs

Both text controls expose `TextCommitted` with immutable `TextCommitEventArgs`:
`Text` and `Reason` (`TextCommitReason.Enter` or `Blur`). TextBox Enter/keypad Enter
commits. TextArea Enter inserts a newline; Ctrl+Enter/keypad Enter commits. Blur
commits remaining edits. Held Enter and blur after an Enter commit do not create a
second commit. An unchanged/programmatically synchronized field does not commit.
Pending text is delivered before its commit can rerender the parent.
The commit keeps its originating callback registration across that text flush.
If the flush replaces or clears `TextCommitted`, or removes the input, the
already-entered commit is dropped. It is never delivered to a replacement
callback. An unchanged registration receives the immutable final-text snapshot
once, after `TextChanged`; awaiting callbacks keep their existing snapshot.

Native `TextChanged`, Checkbox `Changed`, Slider `ValueChanged`, and ComboBox
`SelectionChanged` retain their setter notifications. Native user-only events are
`UserTextChanged`, `UserChanged`, `UserValueChanged`, and `UserSelectionChanged`.
Slider `SetValueFromUser` and ComboBox `SelectFromUser` apply the same valid-value
rules as normal interaction. `ResetText` intentionally clears an outstanding edit,
including when the replacement text is equal; `CommitEdit` requests a commit for
an outstanding user edit. Core controls have no Blazor dependency.

Both playgrounds start with **Profile and volume**: name protection/rejection,
shortcut, profile type, slider/progress, range limit, multiline notes, explicit
commit result, Clear, Reset, and a cancelable delayed save. Existing demos remain
under the scenario selector. Native modules are `Scenarios` classes taking a
`Window`, exposing `Element Content`, and implementing `IDisposable`. Register
their content with `ScenarioHost.AddScenario` before `Run`. Razor modules live in
`Scenarios` and are registered in the small `App.razor` host. Only real scenarios
are registered; module implementations need no entrypoint edits.
