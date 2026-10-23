using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Marius.Winter.Blazor.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

// Native paint/font state and the process-global registry are shared by these cases.
[CollectionDefinition("Native rendering", DisableParallelization = true)]
public class NativeRenderingCollection { }

[Collection("Native rendering")]
public class BatchBRegressionTests
{
    public sealed class ContentHost : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content);
        public void Render(RenderFragment content) { Content = content; StateHasChanged(); }
    }

    // Render real element attributes in arbitrary orders, through the production
    // handler and Blazor's actual element diff/coercion/event registration path.
    public sealed class AttributeHost : NativeControlComponentBase
    {
        [Parameter] public Type Control { get; set; } = typeof(B.Slider);
        [Parameter] public KeyValuePair<string, object>[] Attributes { get; set; } = [];
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            RuntimeHelpers.RunClassConstructor(Control.TypeHandle);
            builder.OpenElement(0, Control.FullName!);
            var attributes = new AttributesBuilder(builder);
            foreach (var (name, value) in Attributes)
            {
                switch (value)
                {
                    case float number: attributes.AddAttribute(name, number); break;
                    case int number: attributes.AddAttribute(name, number); break;
                    case string text: attributes.AddAttribute(name, text); break;
                    case string[] items: attributes.AddAttribute(name, items); break;
                    case byte[] bytes: attributes.AddAttribute(name, bytes); break;
                    case EventCallback<ChangeEventArgs> callback: attributes.AddAttribute(name, callback); break;
                    default: attributes.AddAttribute(name, value); break;
                }
            }
            builder.CloseElement();
        }
        public Task Render(params KeyValuePair<string, object>[] attributes)
        {
            Attributes = attributes; StateHasChanged();
            var renderer = ((WinterElementHandler)ElementHandler!).Renderer;
            return PendingEvents(renderer);
        }
        public T Native<T>() where T : Element => (T)((IWinterElementHandler)ElementHandler!).ElementControl;
    }

    private static KeyValuePair<string, object> A(string name, object value) => new(name, value);
    private static RenderFragment Control<T>(params KeyValuePair<string, object>[] attributes) where T : IComponent => b =>
    {
        b.OpenComponent<T>(0);
        foreach (var (name, value) in attributes) b.AddAttribute(1, name, value);
        b.CloseComponent();
    };
    private static async Task<(ContentHost host, Panel root)> Mount(TestRenderer renderer, RenderFragment content)
    {
        var root = new Panel();
        var host = await renderer.AddComponent<ContentHost>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = content });
        await PendingEvents(renderer);
        return (host, root);
    }
    private static Task PendingEvents(NativeComponentRenderer renderer)
        => (Task?)typeof(NativeComponentRenderer).GetProperty("NativeEvents", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(renderer) ?? Task.CompletedTask;
    private static async Task<AttributeHost> MountAttributes(TestRenderer renderer, Type control, KeyValuePair<string, object>[] attributes)
    {
        AttributeHost result = null!;
        await Mount(renderer, b =>
        {
            b.OpenComponent<AttributeHost>(0); b.AddAttribute(1, "Control", control); b.AddAttribute(2, "Attributes", attributes);
            b.AddComponentReferenceCapture(3, c => result = (AttributeHost)c); b.CloseComponent();
        });
        return result;
    }

    public sealed class KeyedRows : ComponentBase
    {
        [Parameter] public string[] Order { get; set; } = ["A", "B", "C"];
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenRegion(0);
            foreach (var key in Order)
            {
                b.OpenComponent<Row>(0); b.SetKey(key); b.AddAttribute(1, "Name", key); b.CloseComponent();
            }
            b.CloseRegion();
        }
        public void Render(params string[] order) { Order = order; StateHasChanged(); }
    }
    public sealed class Row : ComponentBase
    {
        [Parameter] public string Name { get; set; } = "";
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.AddContent(0, Control<B.Label>(A("Text", Name + "1")));
            b.AddContent(1, Control<B.Label>(A("Text", Name + "2")));
        }
    }

    [Fact]
    public async Task WrapperKeyedReorder_PreservesAllSiblingsScenesAndIdentity()
    {
        using var renderer = new TestRenderer();
        KeyedRows rows = null!;
        var (_, root) = await Mount(renderer, b =>
        {
            b.AddContent(0, Control<B.Label>(A("Text", "before")));
            b.OpenComponent<KeyedRows>(1); b.AddComponentReferenceCapture(2, c => rows = (KeyedRows)c); b.CloseComponent();
            b.AddContent(3, Control<B.Label>(A("Text", "after")));
        });
        var originals = root.Children.ToDictionary(e => ((Label)e).Text);
        await renderer.Dispatcher.InvokeAsync(() => rows.Render("C", "A", "B"));
        Assert.Equal(new[] { "before", "C1", "C2", "A1", "A2", "B1", "B2", "after" }, root.Children.Cast<Label>().Select(l => l.Text));
        foreach (var child in root.Children) Assert.Same(originals[((Label)child).Text], child);
        await renderer.Dispatcher.InvokeAsync(() => rows.Render("B", "C"));
        Assert.Equal(new[] { "before", "B1", "B2", "C1", "C2", "after" }, root.Children.Cast<Label>().Select(l => l.Text));
        foreach (var child in root.Children) Assert.Same(originals[((Label)child).Text], child);
        Assert.All(root.Children, child => Assert.Same(root, child.Parent));
        var paints = (IEnumerable)Field(root.Scene, "paints")!;
        var childScenes = paints.Cast<object>().Where(p => root.Children.Any(c => ReferenceEquals(c.Scene, p))).ToArray();
        Assert.Equal(root.Children.Select(c => (object)c.Scene), childScenes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Event_AddReplaceClear(bool doubleClick)
    {
        using var renderer = new TestRenderer();
        var first = 0; var second = 0;
        var name = doubleClick ? "OnDoubleClick" : "OnClick";
        var (host, root) = await Mount(renderer, Control<B.Button>());
        var button = Assert.IsType<Button>(Assert.Single(root.Children));
        await renderer.Dispatcher.InvokeAsync(() => host.Render(Control<B.Button>(A(name, EventCallback.Factory.Create(this, () => first++)))));
        await renderer.Dispatcher.InvokeAsync(() => { if (doubleClick) button.DoubleClicked?.Invoke(); else button.Clicked?.Invoke(); });
        Assert.Equal(1, first);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(Control<B.Button>(A(name, EventCallback.Factory.Create(this, () => second++)))));
        await renderer.Dispatcher.InvokeAsync(() => { if (doubleClick) button.DoubleClicked?.Invoke(); else button.Clicked?.Invoke(); });
        Assert.Equal(1, second);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(Control<B.Button>(A(name, default(EventCallback)))));
        await renderer.Dispatcher.InvokeAsync(() => { if (doubleClick) button.DoubleClicked?.Invoke(); else button.Clicked?.Invoke(); });
        Assert.Equal(1, first); Assert.Equal(1, second);
    }

    public static IEnumerable<object[]> SliderOrders()
    {
        foreach (var order in Permutations(new[] { "Min", "Max", "Value", "Step" })) yield return [string.Join(",", order)];
    }
    private static IEnumerable<T[]> Permutations<T>(T[] values)
    {
        if (values.Length == 0) { yield return []; yield break; }
        for (var i = 0; i < values.Length; i++)
            foreach (var tail in Permutations(values.Where((_, j) => i != j).ToArray())) yield return new[] { values[i] }.Concat(tail).ToArray();
    }

    [Theory]
    [MemberData(nameof(SliderOrders))]
    public async Task Slider_AtomicFinalAttributesAllOrdersAndRemoval(string order)
    {
        using var renderer = new TestRenderer();
        var changes = new List<float>();
        var callback = EventCallback.Factory.Create<ChangeEventArgs>(this, e => changes.Add((float)e.Value!));
        KeyValuePair<string, object>[] Attributes(float min, float max, float value, float step) =>
            new[] { A("onvaluechanged", callback) }.Concat(order.Split(',').Select(n => A(n, n switch { "Min" => min, "Max" => max, "Value" => value, _ => step }))).ToArray();
        var host = await MountAttributes(renderer, typeof(B.Slider), Attributes(10, 100, 50, 3));
        var slider = host.Native<Slider>();
        Assert.Equal(49, slider.Value); Assert.Equal(new[] { 49f }, changes);
        changes.Clear();
        await renderer.Dispatcher.InvokeAsync(() => host.Render(Attributes(-100, -10, -43, 4)));
        Assert.Equal(-44, slider.Value); Assert.Equal(new[] { -44f }, changes);
        changes.Clear();
        await renderer.Dispatcher.InvokeAsync(() => host.Render(A("onvaluechanged", callback)));
        Assert.Equal(0, slider.Min); Assert.Equal(1, slider.Max); Assert.Equal(0, slider.Step); Assert.Equal(0, slider.Value);
        Assert.Equal(new[] { 0f }, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComboBox_AtomicItemsIndexAndDefaultRemoval(bool indexFirst)
    {
        using var renderer = new TestRenderer();
        var changes = new List<int>();
        var callback = EventCallback.Factory.Create<ChangeEventArgs>(this, e => changes.Add((int)e.Value!));
        KeyValuePair<string, object>[] Attributes(string[] items, int index) => new[] { A("onselectionchanged", callback) }.Concat(indexFirst
            ? new[] { A("SelectedIndex", index), A("Items", items) } : new[] { A("Items", items), A("SelectedIndex", index) }).ToArray();
        var host = await MountAttributes(renderer, typeof(B.ComboBox), Attributes(["A", "B", "C"], 2));
        var combo = host.Native<ComboBox>();
        Assert.Equal(2, combo.SelectedIndex); Assert.Equal(new[] { 2 }, changes);
        changes.Clear();
        await renderer.Dispatcher.InvokeAsync(() => host.Render(Attributes(["D"], -1)));
        Assert.Equal(-1, combo.SelectedIndex); Assert.Equal(new[] { -1 }, changes);
        changes.Clear();
        await renderer.Dispatcher.InvokeAsync(() => host.Render(A("onselectionchanged", callback)));
        Assert.Empty(combo.Items); Assert.Equal(-1, combo.SelectedIndex); Assert.Empty(changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextFragments_UpdateInsertRemoveMarkupAndWrappers(bool button)
    {
        using var renderer = new TestRenderer();
        RenderFragment Content(int value, bool middle, bool prefix = true, string markup = "%") => b =>
        {
            if (prefix) b.AddContent(0, "Progress: ");
            if (middle) b.AddContent(1, value);
            b.AddMarkupContent(2, markup);
        };
        RenderFragment View(RenderFragment text) => button ? Control<B.Button>(A("ChildContent", text)) : Control<B.Label>(A("ChildContent", text));
        var (host, root) = await Mount(renderer, View(Content(1, true)));
        string Text() => button ? ((Button)root.Children[0]).Text : ((Label)root.Children[0]).Text;
        Assert.Equal("Progress: 1%", Text());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(Content(2, true))));
        Assert.Equal("Progress: 2%", Text());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(Content(2, true, markup: " items"))));
        Assert.Equal("Progress: 2 items", Text());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(Content(2, false))));
        Assert.Equal("Progress: %", Text());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(Content(3, true, false))));
        Assert.Equal("3%", Text());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(b => { })));
        Assert.Equal("", Text());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(button ? Control<B.Button>(A("Text", "explicit")) : Control<B.Label>(A("Text", "explicit"))));
        Assert.Equal("explicit", Text());
    }

    [Fact]
    public void Slider_SnapsRelativeToMinAndKeepsBothEndpointsInRange()
    {
        var slider = new Slider(0, 1) { Step = .6f };
        slider.Value = 1; Assert.Equal(1, slider.Value);
        slider.SetRange(10, 11, 10); Assert.Equal(10, slider.Value);
        slider.Value = 10.5f; Assert.Equal(10.6f, slider.Value, 4);
        slider.Value = 50; Assert.Equal(11, slider.Value);
        slider.SetRange(-1, 1, -.45f); Assert.Equal(-.4f, slider.Value, 4);
        Assert.InRange(slider.Value, slider.Min, slider.Max);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void NativeTabRemoval_PreservesSelectedIdentityOrNextNeighbor(int remove, int expectedActive)
    {
        var tabs = new TabWidget();
        var contents = new[] { new Panel(), new Panel(), new Panel() };
        foreach (var content in contents) tabs.AddTab("tab", content);
        tabs.ActiveTab = 1;
        tabs.RemoveTab(remove);
        Assert.Equal(expectedActive, tabs.ActiveTab);
        Assert.Single(tabs.Children, c => c.Visible);
        Assert.Same(remove == 1 ? contents[2] : contents[1], tabs.Children[tabs.ActiveTab]);
        tabs.RemoveTab(1); tabs.RemoveTab(0); Assert.Equal(0, tabs.TabCount);
    }

    [Theory]
    [InlineData("menu")]
    [InlineData("tab")]
    [InlineData("tree")]
    public async Task NonPhysicalCollections_UpdateInsertRemoveReorderWithoutReplacingNativeItems(string collection)
    {
        using var renderer = new TestRenderer();
        RenderFragment View(string[] keys, string suffix) => b =>
        {
            b.OpenComponent<B.MenuBar>(0); b.AddAttribute(1, "ChildContent", (RenderFragment)(m =>
            {
                foreach (var key in keys)
                {
                    m.OpenComponent<B.Menu>(0); m.SetKey(key); m.AddAttribute(1, "Title", key + suffix);
                    m.AddAttribute(2, "ChildContent", Control<B.MenuItem>(A("Label", key + suffix))); m.CloseComponent();
                }
            })); b.CloseComponent();
            b.OpenComponent<B.TabWidget>(2); b.AddAttribute(3, "ChildContent", (RenderFragment)(t =>
            {
                foreach (var key in keys) { t.OpenComponent<B.Tab>(0); t.SetKey(key); t.AddAttribute(1, "Label", key + suffix); t.CloseComponent(); }
            })); b.CloseComponent();
            b.OpenComponent<B.TreeView>(4); b.AddAttribute(5, "ChildContent", (RenderFragment)(t =>
            {
                foreach (var key in keys) { t.OpenComponent<B.TreeViewNode>(0); t.SetKey(key); t.AddAttribute(1, "Text", key + suffix); t.AddAttribute(2, "Tag", key); t.CloseComponent(); }
            })); b.CloseComponent();
        };
        var (host, root) = await Mount(renderer, View(["A", "B", "C"], ""));
        var bar = (MenuBar)root.Children[0]; var tabs = (TabWidget)root.Children[1]; var tree = (TreeView)root.Children[2];
        var menus = ((IEnumerable)Field(bar, "_menus")!).Cast<object>().ToArray();
        var nodes = tree.Nodes.ToArray(); var contents = tabs.Children.ToArray();
        tabs.ActiveTab = 1;
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(["C", "D", "B"], "!")));
        var now = ((IEnumerable)Field(bar, "_menus")!).Cast<object>().ToArray();
        switch (collection)
        {
            case "menu":
                Assert.Equal(new[] { "C!", "D!", "B!" }, now.Select(n => (string)Field(n, "Title")!));
                Assert.Same(menus[2], now[0]); Assert.Same(menus[1], now[2]);
                break;
            case "tree":
                Assert.Equal(new[] { "C!", "D!", "B!" }, tree.Nodes.Select(n => n.Text));
                Assert.Same(nodes[2], tree.Nodes[0]); Assert.Same(nodes[1], tree.Nodes[2]);
                break;
            case "tab":
                Assert.Same(contents[2], tabs.Children[0]); Assert.Same(contents[1], tabs.Children[2]);
                Assert.Equal(new[] { "C!", "D!", "B!" }, ((IEnumerable)Field(tabs, "_tabs")!).Cast<object>().Select(t => (string)Field(t, "Label")!));
                Assert.Equal(2, tabs.ActiveTab); Assert.Single(tabs.Children, c => c.Visible);
                break;
        }
    }

    [Fact]
    public async Task MenuItem_UpdateAndConditionalRemoval()
    {
        using var renderer = new TestRenderer();
        RenderFragment View(bool present, string label) => Control<B.MenuBar>(A("ChildContent", Control<B.Menu>(A("Title", "File"), A("ChildContent", (RenderFragment)(b =>
        {
            if (present) b.AddContent(0, Control<B.MenuItem>(A("Label", label)));
        })))));
        var (host, root) = await Mount(renderer, View(true, "Open"));
        var menu = ((IEnumerable)Field(root.Children[0], "_menus")!).Cast<object>().Single();
        object[] Items() => ((IEnumerable)Field(menu, "Items")!).Cast<object>().ToArray();
        var item = Assert.Single(Items());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(true, "Save")));
        Assert.Same(item, Assert.Single(Items()));
        Assert.Equal("Save", item.GetType().GetProperty("Label")!.GetValue(item));
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(false, "")));
        Assert.Empty(Items());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registries_CleanOnAncestorRemovalAndRendererShutdown(bool shutdown)
    {
        var treeCount = B.TreeView.ElementToTreeInfo.Count;
        var menuCount = B.Menu.ElementToMenu.Count;
        using var renderer = new TestRenderer();
        var (host, root) = await Mount(renderer, Control<B.Panel>(A("ChildContent", (RenderFragment)(b =>
        {
            b.AddContent(0, Control<B.TreeView>(A("ChildContent", Control<B.TreeViewNode>(A("Text", "parent"), A("ChildContent", Control<B.TreeViewNode>(A("Text", "child")))))));
            b.AddContent(1, Control<B.MenuBar>(A("ChildContent", Control<B.Menu>(A("Title", "File"), A("ChildContent", Control<B.MenuItem>(A("Label", "Open")))))));
        }))));
        if (shutdown) await renderer.DisposeAsync();
        else await renderer.Dispatcher.InvokeAsync(() => host.Render(b => { }));
        Assert.Equal(treeCount, B.TreeView.ElementToTreeInfo.Count);
        Assert.Equal(menuCount, B.Menu.ElementToMenu.Count);
        if (!shutdown) Assert.Empty(root.Children);
    }

    [Fact]
    public void WeakTokens_StableByReferenceAndDeadEntriesSwept()
    {
        var value = new byte[4]; var key = WeakObjectStore.Add(value);
        var count = ((IDictionary)typeof(WeakObjectStore).GetField("_store", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Count;
        for (var i = 0; i < 1024; i++) Assert.Equal(key, WeakObjectStore.Add(value));
        Assert.Equal(count, ((IDictionary)typeof(WeakObjectStore).GetField("_store", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Count);
        Assert.Same(value, WeakObjectStore.Get<byte[]>(key));
        Assert.NotEqual(key, WeakObjectStore.Add(new byte[4]));
        var (deadKey, weak) = AddDeadToken();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.IsAlive);
        typeof(WeakObjectStore).GetMethod("Sweep", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
        Assert.Null(WeakObjectStore.Get(deadKey));
        GC.KeepAlive(value);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (string, WeakReference) AddDeadToken() { var value = new object(); return (WeakObjectStore.Add(value), new WeakReference(value)); }

    [Fact]
    public async Task Image_StableTransportAvoidsDecodingUnchangedSource()
    {
        using var renderer = new TestRenderer();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
        var layout = new FlexItem { Grow = 1 };
        var (host, root) = await Mount(renderer, Control<B.Image>(A("Source", bytes), A("LayoutData", layout)));
        var image = (Image)root.Children[0]; Assert.Same(bytes, image.Source); Assert.Same(layout, image.LayoutData);
        var picture = Field(image, "_picture"); Assert.NotNull(picture);
        var sourceUpdates = renderer.SourceAttributeUpdates;
        for (var i = 0; i < 40; i++) await renderer.Dispatcher.InvokeAsync(() => host.Render(Control<B.Image>(A("Source", bytes), A("LayoutData", layout))));
        Assert.Equal(sourceUpdates, renderer.SourceAttributeUpdates);
        Assert.Same(picture, Field(image, "_picture"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Image_AttributesRemainShadowedUntilFinalCommit(bool mimeFirst)
    {
        using var renderer = new TestRenderer();
        var oldBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
        var newBytes = oldBytes.ToArray();
        var host = await MountAttributes(renderer, typeof(B.Image), mimeFirst
            ? [A("MimeType", "png"), A("Source", oldBytes)] : [A("Source", oldBytes), A("MimeType", "png")]);
        var image = host.Native<Image>(); var handler = host.ElementHandler!;
        foreach (var attribute in mimeFirst ? new[] { A("MimeType", "image/png"), A("Source", newBytes) } : new[] { A("Source", newBytes), A("MimeType", "image/png") })
        {
            handler.ApplyAttribute(0, attribute.Key, attribute.Value is byte[] bytes ? WeakObjectStore.Add(bytes) : attribute.Value, null!);
            Assert.Same(oldBytes, image.Source); Assert.Equal("png", image.MimeType);
        }
        handler.GetType().GetMethod("CommitAttributes")!.Invoke(handler, null);
        Assert.Same(newBytes, image.Source); Assert.Equal("image/png", image.MimeType); Assert.NotNull(Field(image, "_picture"));
        await renderer.Dispatcher.InvokeAsync(() => host.Render());
        Assert.Null(image.Source); Assert.Equal("png", image.MimeType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingEvent_ClearOrReplaceDoesNotReachAnotherRegistration(bool clear)
    {
        using var renderer = new TestRenderer();
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var window = new Window(80, 80, "Winter event regression");
            try
            {
            var oldValues = new List<float>(); var newValues = new List<float>();
            var oldCallback = EventCallback.Factory.Create<ChangeEventArgs>(this, e => oldValues.Add((float)e.Value!));
            var nextCallback = EventCallback.Factory.Create<ChangeEventArgs>(this, e => newValues.Add((float)e.Value!));
            var host = await MountAttributes(renderer, typeof(B.Slider), [A("onvaluechanged", oldCallback)]);
            var slider = host.Native<Slider>(); window.AddChild(slider.Parent!);
            slider.Value = .25f;
            Assert.Single(window.DispatcherQueue);
            await host.Render(clear ? [] : [A("onvaluechanged", nextCallback)]);
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
            Assert.Empty(oldValues); Assert.Empty(newValues);
            slider.Value = .75f;
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
            Assert.Empty(oldValues); Assert.Equal(clear ? [] : new[] { .75f }, newValues);
            }
            finally { renderer.Dispose(); window.Dispose(); }
        });
    }

    public sealed class TextPiece : ComponentBase
    {
        [Parameter] public string Text { get; set; } = "";
        protected override void BuildRenderTree(RenderTreeBuilder b) { b.AddContent(0, Text); b.AddMarkupContent(1, "!"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextWrappers_KeyedReorderAndRemoval(bool button)
    {
        using var renderer = new TestRenderer();
        RenderFragment View(string[] keys) => (button ? Control<B.Button> : (Func<KeyValuePair<string, object>[], RenderFragment>)Control<B.Label>)([A("ChildContent", (RenderFragment)(b =>
        {
            b.AddContent(0, "prefix ");
            foreach (var key in keys) { b.OpenComponent<TextPiece>(1); b.SetKey(key); b.AddAttribute(2, "Text", key); b.CloseComponent(); }
            b.AddContent(3, " suffix");
        }))]);
        var (host, root) = await Mount(renderer, View(["A", "B"]));
        string Text() => button ? ((Button)root.Children[0]).Text : ((Label)root.Children[0]).Text;
        Assert.Equal("prefix A!B! suffix", Text());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(["B", "C", "A"])));
        Assert.Equal("prefix B!C!A! suffix", Text());
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(["A"])));
        Assert.Equal("prefix A! suffix", Text());
    }

    [Fact]
    public async Task RegistryTreesAndComponents_AreCollectableAfterRemoval()
    {
        var weak = await RemoveOwnedTree();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.All(weak, reference => Assert.False(reference.IsAlive));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference[]> RemoveOwnedTree()
    {
        using var renderer = new TestRenderer();
        B.TreeView component = null!;
        var (host, root) = await Mount(renderer, b =>
        {
            b.OpenComponent<B.TreeView>(0); b.AddAttribute(1, "ChildContent", Control<B.TreeViewNode>(A("Text", "node")));
            b.AddComponentReferenceCapture(2, c => component = (B.TreeView)c); b.CloseComponent();
        });
        var weak = new[] { new WeakReference(root.Children[0]), new WeakReference(component), new WeakReference(component.ElementHandler!) };
        await renderer.Dispatcher.InvokeAsync(() => host.Render(b => { }));
        component = null!;
        return weak;
    }

    [Fact]
    public async Task MenuParentDisposal_UnregistersAndReleasesNativeActions()
    {
        using var renderer = new TestRenderer();
        var count = B.Menu.ElementToMenu.Count;
        var (host, root) = await Mount(renderer, Control<B.Panel>(A("ChildContent", Control<B.MenuBar>(A("ChildContent", Control<B.Menu>(A("Title", "File"), A("ChildContent", Control<B.MenuItem>(A("Label", "Open"), A("OnClick", EventCallback.Factory.Create(this, () => { }))))))))));
        var bar = root.Children[0].Children[0];
        var native = ((IEnumerable)Field(bar, "_menus")!).Cast<object>().Single();
        var item = ((IEnumerable)Field(native, "Items")!).Cast<object>().Single();
        await renderer.Dispatcher.InvokeAsync(() => host.Render(b => { }));
        Assert.Equal(count, B.Menu.ElementToMenu.Count);
        Assert.Empty(((IEnumerable)Field(native, "Items")!).Cast<object>());
        Assert.Null(item.GetType().GetProperty("Action")!.GetValue(item));
    }

    [Fact]
    public async Task MenuCommands_KeyedOrderInsertionAndRemovalPreserveIdentity()
    {
        using var renderer = new TestRenderer();
        RenderFragment View(string[] keys) => Control<B.MenuBar>(A("ChildContent", Control<B.Menu>(A("Title", "File"), A("ChildContent", (RenderFragment)(b =>
        {
            foreach (var key in keys) { b.OpenComponent<B.MenuItem>(0); b.SetKey(key); b.AddAttribute(1, "Label", key); b.CloseComponent(); }
        })))));
        var (host, root) = await Mount(renderer, View(["A", "B", "C"]));
        var menu = ((IEnumerable)Field(root.Children[0], "_menus")!).Cast<object>().Single();
        object[] Items() => ((IEnumerable)Field(menu, "Items")!).Cast<object>().ToArray();
        var old = Items();
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(["C", "D", "B"])));
        var items = Items();
        Assert.Equal(new[] { "C", "D", "B" }, items.Select(i => (string)i.GetType().GetProperty("Label")!.GetValue(i)!));
        Assert.Same(old[2], items[0]); Assert.Same(old[1], items[2]);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View([])));
        Assert.Empty(Items());
    }

    [Fact]
    public async Task TreeNode_MetadataAndNestedOrderUpdateWithoutReplacingNodes()
    {
        using var renderer = new TestRenderer();
        const string icon = "<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1'><rect width='1' height='1' fill='red'/></svg>";
        RenderFragment View(string[] keys, bool metadata) => Control<B.TreeView>(A("ChildContent", Control<B.TreeViewNode>(A("Text", "parent"), A("ChildContent", (RenderFragment)(b =>
        {
            foreach (var key in keys)
            {
                b.OpenComponent<B.TreeViewNode>(0); b.SetKey(key); b.AddAttribute(1, "Text", key + (metadata ? "!" : ""));
                b.AddAttribute(2, "Icon", metadata ? icon : null); b.AddAttribute(3, "Tag", metadata ? key : null); b.AddAttribute(4, "MayHaveChildren", metadata);
                b.CloseComponent();
            }
        })))));
        var (host, root) = await Mount(renderer, View(["A", "B"], false));
        var tree = (TreeView)root.Children[0]; var parent = tree.Nodes.Single(); var old = parent.Children.ToArray();
        old[1].IsExpanded = true;
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(["B", "C", "A"], true)));
        Assert.Equal(new[] { "B!", "C!", "A!" }, parent.Children.Select(n => n.Text));
        Assert.Same(old[1], parent.Children[0]); Assert.True(old[1].IsExpanded); Assert.Same(old[0], parent.Children[2]);
        Assert.All(parent.Children, node => { Assert.Same(parent, node.Parent); Assert.Equal(icon, node.IconSvg); Assert.True(node.MayHaveChildren); Assert.NotNull(node.Tag); });
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(["B"], false)));
        var node = Assert.Single(parent.Children); Assert.Same(old[1], node);
        Assert.Equal("B", node.Text); Assert.Null(node.IconSvg); Assert.Null(node.Tag); Assert.False(node.MayHaveChildren);
        Assert.Null(old[0].Parent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Slider_OrdinaryWrapperInitialRangeUsesOnlyEffectiveFinalCallback(bool negative)
    {
        using var renderer = new TestRenderer();
        var changes = new List<float>();
        var value = negative ? -43f : 50f;
        var (_, root) = await Mount(renderer, Control<B.Slider>(A("Min", negative ? -100f : 10f), A("Max", negative ? -10f : 100f), A("Value", value), A("OnValueChanged", EventCallback.Factory.Create<float>(this, changes.Add))));
        Assert.Equal(value, ((Slider)root.Children[0]).Value); Assert.Equal(new[] { value }, changes);
    }

    [Fact]
    public async Task HandlerShutdown_IsIdempotentAndClearsNativeDelegates()
    {
        var renderer = new TestRenderer();
        B.Button component = null!;
        var (_, root) = await Mount(renderer, b =>
        {
            b.OpenComponent<B.Button>(0); b.AddAttribute(1, "OnClick", EventCallback.Factory.Create(this, () => { }));
            b.AddComponentReferenceCapture(2, c => component = (B.Button)c); b.CloseComponent();
        });
        var handler = component.ElementHandler!; var native = (Button)root.Children[0];
        await renderer.DisposeAsync(); await renderer.DisposeAsync();
        ((IDisposable)handler).Dispose(); ((IDisposable)handler).Dispose();
        Assert.Null(native.Clicked); Assert.Null(native.DoubleClicked); Assert.Empty(root.Children);
        Assert.Null(component.ElementHandler);
    }

    [Fact]
    public async Task EventArguments_RemainSnapshotsWhileEarlierCallbackAwaits()
    {
        using var renderer = new TestRenderer();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = new List<float>(); var after = new List<float>();
        var callback = EventCallback.Factory.Create<ChangeEventArgs>(this, async e =>
        {
            before.Add((float)e.Value!); await release.Task; after.Add((float)e.Value!);
            if (after.Count == 2) done.SetResult();
        });
        var host = await MountAttributes(renderer, typeof(B.Slider), [A("onvaluechanged", callback)]);
        var slider = host.Native<Slider>();
        await renderer.Dispatcher.InvokeAsync(() => { slider.Value = .25f; slider.Value = .75f; });
        Assert.Equal(new[] { .25f, .75f }, before);
        release.SetResult(); await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { .25f, .75f }, after.Order());
    }

    public sealed class EphemeralAttributes : NativeControlComponentBase
    {
        static EphemeralAttributes()
        {
            RuntimeHelpers.RunClassConstructor(typeof(B.Panel).TypeHandle);
            ElementHandlerRegistry.ElementHandlers.Add(typeof(EphemeralAttributes).FullName!, ElementHandlerRegistry.ElementHandlers[typeof(B.Panel).FullName!]);
        }
        protected override void RenderAttributes(AttributesBuilder attributes)
        {
            AddTemporaryValue(attributes);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AddTemporaryValue(AttributesBuilder attributes) => attributes.AddAttribute("LayoutData", new FlexItem { Grow = 2 });
    }

    [Fact]
    public async Task WeakTransport_KeepsNewReferencesAliveThroughActualBatchApplication()
    {
        using var renderer = new TestRenderer();
        var (_, root) = await Mount(renderer, Control<EphemeralAttributes>());
        var layout = Assert.IsType<FlexItem>(root.Children[0].LayoutData);
        Assert.Equal(2, layout.Grow);
    }

    [Fact]
    public async Task NativeRootComponent_IsReferencedParentedAndRemovedAtShutdown()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        var component = await renderer.AddComponent<B.Label>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Text"] = "root" });
        var label = Assert.IsType<Label>(Assert.Single(root.Children));
        Assert.Equal("root", label.Text); Assert.NotNull(component.ElementHandler);
        await renderer.DisposeAsync();
        Assert.Empty(root.Children); Assert.Null(component.ElementHandler);
    }

    private sealed class DisposablePanel : Panel, IDisposable
    {
        public int Disposals;
        void IDisposable.Dispose() => Disposals++;
    }

    [Fact]
    public void HandlerDisposal_ReleasesOwnedDisposableNativeElementExactlyOnce()
    {
        using var renderer = new TestRenderer();
        var native = new DisposablePanel(); var handler = new WinterElementHandler(renderer, native);
        ((IDisposable)handler).Dispose(); ((IDisposable)handler).Dispose();
        Assert.Equal(1, native.Disposals);
    }

    private static object? Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(value);
}
