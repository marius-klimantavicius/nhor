using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Xunit.Abstractions;
using B = Marius.Winter.Blazor.Elements;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class LayoutParameterUpdateTests(ITestOutputHelper output)
{
    private static void Flush(Window window) => typeof(Window)
        .GetMethod("FlushLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);

    public sealed class LayoutApp : ComponentBase
    {
        [Parameter] public string Kind { get; set; } = "stack";
        [Parameter] public bool Labels { get; set; }
        [Parameter] public float ChildWidth { get; set; } = 40;
        public bool ShowLayout = true;
        public Orientation Direction = Orientation.Horizontal;
        public Orientation Orientation = Orientation.Vertical;
        public Alignment Alignment = Alignment.Stretch;
        public FlexWrap Wrap;
        public JustifyContent Justification;
        public float Gap = 4;
        public float ColumnGap = 4;
        public float RowGap = 4;
        public TrackSize[]? Columns = [TrackSize.Px(40), TrackSize.Px(40)];
        public TrackSize[]? Rows = [TrackSize.Px(20), TrackSize.Px(20)];

        public void Change(Action<LayoutApp> change) { change(this); StateHasChanged(); }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<B.Panel>(0);
            builder.AddAttribute(1, "Padding", new Thickness(0));
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(children =>
            {
                if (ShowLayout)
                {
                    if (Kind == "stack")
                    {
                        children.OpenComponent<B.StackLayout>(0);
                        children.AddAttribute(1, "Spacing", Gap);
                        children.AddAttribute(2, "Orientation", Orientation);
                        children.AddAttribute(3, "CrossAlignment", Alignment);
                        children.CloseComponent();
                    }
                    else if (Kind == "flex")
                    {
                        children.OpenComponent<B.FlexLayout>(4);
                        children.AddAttribute(5, "Gap", Gap);
                        children.AddAttribute(6, "Direction", Direction);
                        children.AddAttribute(7, "Wrap", Wrap);
                        children.AddAttribute(8, "AlignItems", Alignment);
                        children.AddAttribute(9, "JustifyContent", Justification);
                        children.CloseComponent();
                    }
                    else
                    {
                        children.OpenComponent<B.GridLayout>(10);
                        children.AddAttribute(11, "Columns", Columns);
                        children.AddAttribute(12, "Rows", Rows);
                        children.AddAttribute(13, "ColumnGap", ColumnGap);
                        children.AddAttribute(14, "RowGap", RowGap);
                        children.CloseComponent();
                    }
                }
                for (int i = 0; i < (Kind == "grid" ? 4 : 2); i++)
                {
                    children.OpenRegion(20);
                    if (Labels)
                    {
                        children.OpenComponent<B.Label>(0);
                        children.AddAttribute(1, "Text", i == 0 ? "one" : "two");
                        children.CloseComponent();
                    }
                    else
                    {
                        children.OpenComponent<B.Panel>(2);
                        children.AddAttribute(3, "MinWidth", ChildWidth);
                        children.AddAttribute(4, "MaxWidth", ChildWidth);
                        children.AddAttribute(5, "MinHeight", 20f);
                        children.AddAttribute(6, "MaxHeight", 20f);
                        children.CloseComponent();
                    }
                    children.CloseRegion();
                }
            }));
            builder.CloseComponent();
        }
    }

    [Fact]
    public async Task UpdatingBlazorLayoutSpacingArrangesExistingChildren()
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            var host = window.UseBlazor();
            var app = host.AddComponent<LayoutApp>(new() { ["Labels"] = true }).GetAwaiter().GetResult();
            Flush(window);
            var panel = Assert.IsType<Panel>(Assert.Single(window.Children));
            var first = panel.Children[0]; var second = panel.Children[1];
            Assert.Equal(4, second.Bounds.Y - first.Bounds.Y - first.Bounds.H);
            Assert.False(panel.IsMeasureDirty);

            host.Renderer.Dispatcher.InvokeAsync(() => app.Change(a => a.Gap = 30)).GetAwaiter().GetResult();
            output.WriteLine($"Native spacing={Assert.IsType<StackLayout>(panel.Layout).Spacing}, owner dirty={panel.IsMeasureDirty}");
            Flush(window);
            float gap = second.Bounds.Y - first.Bounds.Y - first.Bounds.H;
            output.WriteLine($"Actual gap={gap}; expected=30");
            Assert.Equal(30, gap);
            Assert.False(panel.IsMeasureDirty);
        });
    }

    [Theory]
    [InlineData("stack", "Orientation", 1, 44f, 0f)]
    [InlineData("stack", "CrossAlignment", 0, 160f, 0f)]
    [InlineData("flex", "Gap", 1, 70f, 0f)]
    [InlineData("flex", "Direction", 1, 0f, 24f)]
    [InlineData("flex", "Wrap", 1, 0f, 24f)]
    [InlineData("flex", "AlignItems", 0, 0f, 100f)]
    [InlineData("flex", "JustifyContent", 0, 116f, 0f)]
    [InlineData("grid", "Columns", 1, 74f, 0f)]
    [InlineData("grid", "Rows", 2, 0f, 39f)]
    [InlineData("grid", "ColumnGap", 1, 70f, 0f)]
    [InlineData("grid", "RowGap", 2, 0f, 50f)]
    public async Task ChangedParameterUpdatesGeometryAfterCleanFrame(string kind, string parameter, int index, float x, float y)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            var host = window.UseBlazor();
            var app = host.AddComponent<LayoutApp>(new() { ["Kind"] = kind, ["ChildWidth"] = parameter == "Wrap" ? 120f : 40f }).GetAwaiter().GetResult();
            var panel = Assert.IsType<Panel>(Assert.Single(window.Children));
            // Leave exactly two line heights plus their gap. Taffy's default
            // align-content stretches lines when there is spare cross space.
            if (parameter == "Wrap") panel.Arrange(new RectF(0, 0, 200, 44));
            Flush(window);
            var layout = panel.Layout;
            var children = panel.Children.ToArray();
            Assert.False(panel.IsMeasureDirty);
            Assert.Equal(kind == "stack" ? 0 : parameter == "Wrap" ? 124 : 44, children[1].Bounds.X);
            Assert.Equal(kind == "stack" ? 24 : 0, children[1].Bounds.Y);

            host.Renderer.Dispatcher.InvokeAsync(() => app.Change(a =>
            {
                switch (parameter)
                {
                    case "Orientation": a.Orientation = Orientation.Horizontal; break;
                    case "CrossAlignment": case "AlignItems": a.Alignment = Alignment.End; break;
                    case "Gap": a.Gap = 30; break;
                    case "Direction": a.Direction = Orientation.Vertical; break;
                    case "Wrap": a.Wrap = FlexWrap.Wrap; break;
                    case "JustifyContent": a.Justification = JustifyContent.End; break;
                    case "Columns": a.Columns = [TrackSize.Px(70), TrackSize.Px(40)]; break;
                    case "Rows": a.Rows = [TrackSize.Px(35), TrackSize.Px(20)]; break;
                    case "ColumnGap": a.ColumnGap = 30; break;
                    case "RowGap": a.RowGap = 30; break;
                }
            })).GetAwaiter().GetResult();
            bool dirty = panel.IsMeasureDirty;
            Assert.Same(layout, panel.Layout);
            Assert.Equal(children, panel.Children);
            Flush(window);
            output.WriteLine($"{kind}.{parameter}: owner dirty={dirty}; child {index}=({children[index].Bounds.X},{children[index].Bounds.Y}); expected=({x},{y})");
            Assert.Equal(x, children[index].Bounds.X);
            Assert.Equal(y, children[index].Bounds.Y);
            Assert.True(dirty, "A layout parameter change must schedule its actual owning container.");
            Assert.False(panel.IsMeasureDirty);
        });
    }

    private sealed class CountingContainer : Element, ILayoutContainer
    {
        private ILayout? _layout;
        public int Assignments;
        public ILayout? Layout
        {
            get => _layout;
            set { _layout = value; Assignments++; InvalidateMeasure(); }
        }
    }

    private static NativeControlComponentBase MountLayout(WinterRenderer renderer, Element owner, string kind) => kind switch
    {
        "stack" => renderer.AddComponent<B.StackLayout>(owner).GetAwaiter().GetResult(),
        "flex" => renderer.AddComponent<B.FlexLayout>(owner).GetAwaiter().GetResult(),
        _ => renderer.AddComponent<B.GridLayout>(owner).GetAwaiter().GetResult(),
    };

    private static void Apply(IElementHandler handler, string name, object value) => handler.ApplyAttribute(0, name, value, null!);

    [Theory]
    [InlineData("stack")]
    [InlineData("flex")]
    [InlineData("grid")]
    public async Task AttributeBatchNotifiesActualContainerOnceAndEquivalentValuesAreNoOps(string kind)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            using var owner = new CountingContainer();
            var renderer = window.UseBlazor().Renderer;
            var component = MountLayout(renderer, owner, kind);
            var handler = component.ElementHandler!;
            var layout = owner.Layout;
            owner.Measure(200, 120);
            owner._taffyStyleDirty = false;
            int before = owner.Assignments;
            var attributes = kind switch
            {
                "stack" => new[] { ("Spacing", "30"), ("Orientation", "Horizontal"), ("CrossAlignment", "End") },
                "flex" => new[] { ("Gap", "30"), ("Direction", "Vertical"), ("Wrap", "Wrap"), ("AlignItems", "End"), ("JustifyContent", "End") },
                _ => new[] { ("Columns", "40px,40px"), ("Rows", "20px,20px"), ("ColumnGap", "30"), ("RowGap", "30") },
            };
            foreach (var (name, value) in attributes) Apply(handler, name, value);
            Assert.Equal(before, owner.Assignments);
            Assert.False(owner.IsMeasureDirty);
            Assert.False(owner._taffyStyleDirty);
            ((IHandleAttributeBatch)handler).CommitAttributes();
            Assert.Same(layout, owner.Layout);
            Assert.Equal(before + 1, owner.Assignments);
            Assert.True(owner.IsMeasureDirty);
            Assert.True(owner._taffyStyleDirty);

            owner.Measure(200, 120);
            owner._taffyStyleDirty = false;
            foreach (var (name, value) in attributes)
                Apply(handler, name, value == "30" ? "30.0" : value.Replace(",", ", "));
            ((IHandleAttributeBatch)handler).CommitAttributes();
            ((IHandleAttributeBatch)handler).CommitAttributes();
            Assert.Equal(before + 1, owner.Assignments);
            Assert.False(owner.IsMeasureDirty);
            Assert.False(owner._taffyStyleDirty);
        });
    }

    [Theory]
    [InlineData("stack")]
    [InlineData("flex")]
    [InlineData("grid")]
    public async Task LayoutRemovalClearsOwnerAndRetainsPhysicalChildren(string kind)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            var host = window.UseBlazor();
            var app = host.AddComponent<LayoutApp>(new() { ["Kind"] = kind }).GetAwaiter().GetResult();
            Flush(window);
            var panel = Assert.IsType<Panel>(Assert.Single(window.Children));
            var children = panel.Children.ToArray();
            host.Renderer.Dispatcher.InvokeAsync(() => app.Change(a => a.ShowLayout = false)).GetAwaiter().GetResult();
            Assert.Null(panel.Layout);
            Assert.Equal(children, panel.Children);
            Assert.All(children, child => Assert.Same(panel, child.Parent));
        });
    }

    private sealed class CustomLayout : ILayout
    {
        public Vector2 Measure(Element container, float w, float h) => new(w, h);
        public void Arrange(Element container, RectF bounds) { }
    }

    [Theory]
    [InlineData("stack")]
    [InlineData("flex")]
    [InlineData("grid")]
    public async Task DisplacedHandlerCannotOverwriteOrRemoveReplacementLayout(string kind)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            using var owner = new CountingContainer();
            var component = MountLayout(window.UseBlazor().Renderer, owner, kind);
            var replacement = new CustomLayout();
            owner.Layout = replacement;
            int before = owner.Assignments;
            Apply(component.ElementHandler!, kind == "stack" ? "Spacing" : kind == "flex" ? "Gap" : "ColumnGap", "30");
            ((IHandleAttributeBatch)component.ElementHandler!).CommitAttributes();
            Assert.Same(replacement, owner.Layout);
            ((INonPhysicalChild)component.ElementHandler!).Remove();
            Assert.Same(replacement, owner.Layout);
            Assert.Equal(before, owner.Assignments);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemovingGridTrackAttributeClearsTracksAndSchedulesOwner(bool columns)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            var host = window.UseBlazor();
            var app = host.AddComponent<LayoutApp>(new() { ["Kind"] = "grid" }).GetAwaiter().GetResult();
            Flush(window);
            var panel = Assert.IsType<Panel>(Assert.Single(window.Children));
            Assert.False(panel.IsMeasureDirty);
            host.Renderer.Dispatcher.InvokeAsync(() => app.Change(a => { if (columns) a.Columns = null; else a.Rows = null; })).GetAwaiter().GetResult();
            var grid = Assert.IsType<GridLayout>(panel.Layout);
            Assert.Empty(columns ? grid.Columns : grid.Rows);
            Assert.True(panel.IsMeasureDirty);
            Flush(window);
            Assert.False(panel.IsMeasureDirty);
        });
    }

    [Theory]
    [InlineData("layout")]
    [InlineData("flexHint")]
    [InlineData("gridHint")]
    public async Task NativeInPlaceMutationUsesExplicitNotification(string kind)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            var panel = new Panel { Padding = new Thickness(0), Layout = kind switch
            {
                "layout" => new StackLayout(),
                "flexHint" => new FlexLayout(),
                _ => new GridLayout { Columns = [TrackSize.Px(80), TrackSize.Px(80)] },
            } };
            var child = new Box(); panel.AddChild(child);
            if (kind == "layout" || kind == "flexHint") panel.AddChild(new Box());
            if (kind == "flexHint") child.LayoutData = new FlexItem();
            if (kind == "gridHint") child.LayoutData = new GridItem();
            window.AddChild(panel);
            Flush(window);
            Assert.False(panel.IsMeasureDirty);
            if (kind == "layout")
            {
                ((StackLayout)panel.Layout!).Spacing = 30;
                Assert.False(panel.IsMeasureDirty);
                panel.Layout = panel.Layout;
            }
            else
            {
                if (child.LayoutData is FlexItem flex) flex.Grow = 1;
                else ((GridItem)child.LayoutData!).Column = 1;
                child.LayoutData = child.LayoutData;
                Assert.False(panel.IsMeasureDirty);
                child.InvalidateMeasure();
                Assert.True(child._taffyStyleDirty);
            }
            Assert.True(panel.IsMeasureDirty);
            Flush(window);
            if (kind == "layout") Assert.Equal(30, panel.Children[1].Bounds.Y - child.Bounds.Y - child.Bounds.H);
            else if (kind == "flexHint") Assert.Equal(176, child.Bounds.W);
            else Assert.Equal(84, child.Bounds.X);
            Assert.False(panel.IsMeasureDirty);
        });
    }
}
