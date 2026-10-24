# Plan: Razor Integration for Winter GUI

## Context

MobileBlazorBindings (MBB) is a Microsoft framework that bridges Blazor's component model with
native UI controls. It was built for MAUI/Xamarin.Forms. We adapt its core engine to work with
Winter GUI, so users can write `.razor` files that produce native Winter controls.

**Goal**: Write Razor like this, get native Winter UI:

```razor
<Panel Layout="@stackLayout">
    <Label Text="Hello Blazor!" FontSize="20" />
    <Button Text="Click Me" OnClick="HandleClick" />
    <TextBox Text="@name" OnTextChanged="v => name = v" Placeholder="Enter name" />
    <Checkbox Text="Enable" IsChecked="@enabled" OnChanged="v => enabled = v" />
    <Slider Value="@volume" Min="0" Max="100" OnValueChanged="v => volume = v" />
    <ProgressBar Value="@(volume / 100f)" />
</Panel>

@code {
    StackLayout stackLayout = new() { Spacing = 10 };
    string name = "";
    bool enabled = false;
    float volume = 50;
    void HandleClick() { name = "Clicked!"; }
}
```

## Architecture

```
.razor file → Blazor RenderTree → NativeComponentRenderer
    → NativeComponentAdapter (shadow tree)
    → IElementHandler.ApplyAttribute() (sets properties on Winter controls)
    → Winter Element tree (rendered by ThorVG)
```

Three layers:

1. **Core Engine** — copied from MBB Core (15 files), namespace-adapted. Framework-agnostic
   Blazor render-tree processing.
2. **Winter Bridge** — new code: `WinterElementManager`, `WinterRenderer`,
   `WinterElementHandler` base, `WinterDispatcher`, host/extensions.
3. **Components + Handlers** — one pair per Winter control. Component has `[Parameter]`
   properties; handler maps them to native control via `ApplyAttribute`.

## New Project

`winter/Marius.Winter.Blazor/Marius.Winter.Blazor.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Marius.Winter.Blazor</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Marius.Winter\Marius.Winter.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
  </ItemGroup>
</Project>
```

Sample app: `winter/Marius.Winter.Blazor.Playground/` (Razor-based playground).

## File Plan

### Phase 1: Core Engine (copy from MBB, change namespace)

Source: `MobileBlazorBindings/src/Microsoft.MobileBlazorBindings.Core/`
Target: `winter/Marius.Winter.Blazor/Core/`

Namespace `Microsoft.MobileBlazorBindings.Core` → `Marius.Winter.Blazor.Core`

| File | Notes |
|------|-------|
| `NativeComponentRenderer.cs` | Core render-tree processor. Copy as-is (namespace only). |
| `NativeComponentAdapter.cs` | Shadow tree node. Copy as-is. |
| `NativeControlComponentBase.cs` | Base Blazor component. Copy as-is. |
| `IElementHandler.cs` | `TargetElement + ApplyAttribute`. Copy as-is. |
| `ElementHandlerRegistry.cs` | Static type→factory registry. Copy as-is. |
| `ElementHandlerFactory.cs` | Factory delegate type. Copy as-is. |
| `ElementHandlerFactoryContext.cs` | Factory context. Copy as-is. |
| `ElementManager.cs` | Abstract parent-child manager. Copy as-is. |
| `ElementManagerOfElementType.cs` | Generic typed wrapper. Copy as-is. |
| `AttributesBuilder.cs` | Wraps RenderTreeBuilder. Copy as-is. |
| `IHandleChildContentText.cs` | Text child interface. Copy as-is. |
| `INonPhysicalChild.cs` | Non-physical child interface. Copy as-is. |
| `INonChildContainerElement.cs` | Special container interface. Copy as-is. |
| `TextSpanContainer.cs` | Text accumulator. Copy as-is. |
| `ServiceCollectionAdditionalServicesExtensions.cs` | DI helpers. Copy as-is. |

### Phase 2: Winter Bridge (new code)

Target: `winter/Marius.Winter.Blazor/`

| File | Purpose |
|------|---------|
| `IWinterElementHandler.cs` | `IElementHandler` + `Element ElementControl`, `IsParented()`, `SetParent()` |
| `IWinterContainerElementHandler.cs` | `AddChild(Element, int)`, `RemoveChild(Element)`, `GetChildIndex(Element)` |
| `WinterElementManager.cs` | Maps Core's `AddChildElement`/`RemoveChildElement` → `Element.AddChild/InsertChild/RemoveChild`. Handles `INonPhysicalChild`. |
| `WinterRenderer.cs` | Extends `NativeComponentRenderer`. Creates `WinterElementManager`. `AddComponent<T>(Element parent)` entry point. |
| `WinterDispatcher.cs` | Blazor `Dispatcher` backed by the Window dispatcher and its UI synchronization context. |
| `WinterBlazorHost.cs` | Owns renderer and services; exposes `AddComponent<T>()` and `DisposeAsync()`, and tears down automatically on Window close. |
| `WindowExtensions.cs` | `UseBlazor(this Window, Action<IServiceCollection>?)` extension. Creates renderer, root panel, returns host. |
| `WinterElementHandler.cs` | Base handler: `EventManager`, `ConfigureEvent()`, base `ApplyAttribute` for `Visible`, `Enabled`, `Opacity`, `Tooltip`. |
| `EventManager.cs` | Copied from MBB `Elements/Handlers/EventManager.cs`, namespace-adapted. |
| `EventRegistration.cs` | Copied from MBB `Elements/Handlers/EventRegistration.cs`, namespace-adapted. |
| `AttributeHelper.cs` | Type conversions: `Color4` ↔ string, `float`/`bool`/`int` parsing, `Thickness`, enums. Objects (like `ILayout`) passed directly via `AttributesBuilder.AddAttribute(name, object)`. |

### Phase 3: Element + Handler Pairs

Target: `winter/Marius.Winter.Blazor/Elements/` and `winter/Marius.Winter.Blazor/Handlers/`

Each component registers its handler in a static constructor via `ElementHandlerRegistry`.

#### Simple Controls

| Component | Handler | Parameters | Events |
|-----------|---------|------------|--------|
| `WLabel` | `LabelHandler` | `Text`, `FontSize`, `Color` | — |
| `WButton` | `ButtonHandler` + `IHandleChildContentText` | `Text` | `OnClick` |
| `WSeparator` | `SeparatorHandler` | — | — |
| `WProgressBar` | `ProgressBarHandler` | `Value` | — |
| `WCheckbox` | `CheckboxHandler` | `Text`, `IsChecked` | `OnChanged(bool)` |
| `WSlider` | `SliderHandler` | `Value`, `Min`, `Max`, `Step`, `ShowValue` | `OnValueChanged(float)` |
| `WTextBox` | `TextBoxHandler` | `Text`, `Placeholder` | `OnTextChanged(string)` |
| `WComboBox` | `ComboBoxHandler` | `Items(string[])`, `SelectedIndex` | `OnSelectionChanged(int)` |
| `WSvgImage` | `SvgImageHandler` | `Svg` | — |
| `WRichLabel` | `RichLabelHandler` | `Text`, `Svg` | — |

#### Container Controls

| Component | Handler | Parameters | Events |
|-----------|---------|------------|--------|
| `WPanel` | `PanelHandler` : `IWinterContainerElementHandler` | `Layout(ILayout)`, `Background(Color4)` | — |
| `WScrollPanel` | `ScrollPanelHandler` : `IWinterContainerElementHandler` | `Layout(ILayout)`, `Background(Color4)` | — |
| `WDialogWindow` | `DialogWindowHandler` : `IWinterContainerElementHandler` | `Title` | — |

#### Complex Controls (Non-Physical Children)

| Component | Handler | Pattern |
|-----------|---------|---------|
| `WTabWidget` | `TabWidgetHandler` : container | Wraps `TabWidget` |
| `WTab` | `TabHandler` : `INonPhysicalChild` + container | `SetParent` → `tabWidget.AddTab(label, contentPanel)`. Children go into inner panel. |
| `WTreeView` | `TreeViewHandler` | Wraps `TreeView` |
| `WTreeNode` | `TreeNodeHandler` : `INonPhysicalChild` | `SetParent` → `treeView.AddNode(text, parentNode)` |
| `WMenuBar` | `MenuBarHandler` : container | Wraps `MenuBar` |
| `WMenu` | `MenuHandler` : `INonPhysicalChild` | `SetParent` → `menuBar.AddMenu(title)` |
| `WMenuItem` | `MenuItemHandler` : `INonPhysicalChild` | → `menu.AddItem(label, action)`, event: `OnClick` |

### Phase 4: Changes to Winter GUI (Marius.Winter)

| File | Change |
|------|--------|
| `Core/Element.cs` | Add `InsertChild(Element child, int index)` — needed for Blazor sibling ordering. Same as `AddChild` but uses `_children.Insert(index, child)`. |
| `Core/Window.cs` | Owns the bounded dispatcher pump, native wait/wake, dirty layout flush and shutdown. |

### Phase 5: Sample Playground

`winter/Marius.Winter.Blazor.Playground/`

```
Marius.Winter.Blazor.Playground.csproj  (targets net8.0, refs Marius.Winter.Blazor)
Program.cs                               (creates Window, calls UseBlazor, AddComponent<App>)
App.razor                                (root component with panels, buttons, etc.)
```

## Key Design Decisions

### Dispatcher (GLFW Thread Safety)

Window construction, Run and disposal use one owning UI thread. The real
`WinterDispatcher` delegates to `Window.Dispatcher`, which installs a scoped UI
synchronization context and wakes GLFW for queued continuations. Await returned
tasks to observe errors; ordinary async lifecycle and event continuations stay
on that thread. The default Blazor dispatcher is not a native Window substitute.
On close, application tasks are canceled and late UI continuations dropped;
async component/service disposal is pumped before native teardown. See
[runtime contracts](GUI.md#runtime-contracts) for cancellation and ownership rules.

### Layout Triggering

Window owns one pre-render dirty layout flush for native and Blazor trees.
Blazor roots attached directly to Window opt into filling its logical client
area; explicit Bounds/Arrange returns a root to manual sizing. Element mutations
invalidate measurement and schedule the next flush. Renderer batches and
UseBlazor resize hooks do not perform competing whole-window layouts. Custom
Panel layouts use their `ILayout` implementation when no built-in bridge exists.

Changes to `<StackLayout>`, `<FlexLayout>` and `<GridLayout>` parameters notify
their owning container once at the end of the attribute batch, so a clean tree
is arranged on the next flush. Unchanged values do not invalidate measurement.
For native layout objects or child hints passed as parameters, in-place edits
need explicit notification as described in [runtime contracts](GUI.md#runtime-contracts).
Rerendering with the same object reference does not provide that notification.

### Event Mapping

Winter uses `Action`/`Action<T>` delegates. Each handler assigns the delegate in its
constructor to dispatch through `renderer.DispatchEventAsync(eventHandlerId, ...)`.
Since handlers own their control instances, clobbering the delegate is safe.

For typed events (e.g., `Slider.ValueChanged`), the handler wraps the value in
`ChangeEventArgs` and the component unwraps it.

### Object Parameters (ILayout, Color4, etc.)

Complex objects like `ILayout` are passed directly as object attributes via
`AttributesBuilder.AddAttribute(name, (object)value)`. The handler casts them in
`ApplyAttribute`. No string serialization needed for non-primitive types.

### Component Naming

Components use `W` prefix (e.g., `WButton`, `WPanel`) to avoid collision with
Winter's native `Button`, `Panel` classes in the same solution. In Razor files:
```razor
<WButton Text="Click Me" OnClick="HandleClick" />
```

### ThorVG Scene Z-Order

`Scene.Add()` always appends. For `InsertChild(child, index)`, we remove scenes of
children after `index`, add the new child's scene, then re-add the removed scenes.
This maintains visual z-order matching logical child order.

## Implementation Order

1. Create `Marius.Winter.Blazor.csproj` with NuGet refs
2. Copy 15 Core files, adapt namespaces
3. Copy `EventManager.cs` + `EventRegistration.cs`, adapt namespaces
4. Add `InsertChild` to `Element.cs`
5. Add dispatcher queue to `Window.cs`
6. Write bridge: `IWinterElementHandler`, `IWinterContainerElementHandler`,
   `WinterElementManager`, `WinterElementHandler`, `WinterDispatcher`,
   `WinterRenderer`, `WinterBlazorHost`, `WindowExtensions`, `AttributeHelper`
7. Write simple controls: `WLabel`/`LabelHandler`, `WButton`/`ButtonHandler`
8. Write container: `WPanel`/`PanelHandler`
9. Create sample playground, verify build + basic rendering
10. Write remaining simple controls
11. Write remaining containers (`WScrollPanel`, `WDialogWindow`)
12. Write complex controls (`WTabWidget`+`WTab`, `WMenuBar`+`WMenu`+`WMenuItem`,
    `WTreeView`+`WTreeNode`)
13. Test all controls in playground

## Verification

1. `dotnet build winter/Marius.Winter.Blazor/` — zero errors
2. `dotnet build winter/Marius.Winter.Blazor.Playground/` — zero errors
3. Run playground: window appears with Razor-defined controls
4. Click button → event fires, label updates
5. Type in textbox → `@bind`-style updates
6. Drag slider → progress bar reflects value
7. Switch tabs in TabWidget
8. Open combobox, select item
9. Verify layout (stack/flex/grid) works as expected
