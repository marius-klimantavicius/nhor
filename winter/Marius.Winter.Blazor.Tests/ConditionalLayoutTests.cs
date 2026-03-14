using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using BlazorPanel = Marius.Winter.Blazor.Elements.Panel;
using BlazorFlexLayout = Marius.Winter.Blazor.Elements.FlexLayout;
using BlazorGridLayout = Marius.Winter.Blazor.Elements.GridLayout;

namespace Marius.Winter.Blazor.Tests;

public class ConditionalLayoutTests
{
    /// <summary>
    /// Test component: a Panel whose child FlexLayout is controlled by a bool parameter.
    /// Equivalent to:
    ///   &lt;Panel&gt;
    ///     @if (UseFlexLayout) { &lt;FlexLayout /&gt; }
    ///   &lt;/Panel&gt;
    /// </summary>
    private class PanelWithConditionalFlex : ComponentBase
    {
        [Parameter] public bool UseFlexLayout { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<BlazorPanel>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
            {
                if (UseFlexLayout)
                {
                    inner.OpenComponent<BlazorFlexLayout>(0);
                    inner.AddAttribute(1, "Direction", Orientation.Horizontal);
                    inner.CloseComponent();
                }
            }));
            builder.CloseComponent();
        }

        public void SetUseFlexLayout(bool value)
        {
            UseFlexLayout = value;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Same pattern with GridLayout.
    /// </summary>
    private class PanelWithConditionalGrid : ComponentBase
    {
        [Parameter] public bool UseGridLayout { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<BlazorPanel>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
            {
                if (UseGridLayout)
                {
                    inner.OpenComponent<BlazorGridLayout>(0);
                    inner.AddAttribute(1, "ColumnGap", 8f);
                    inner.CloseComponent();
                }
            }));
            builder.CloseComponent();
        }

        public void SetUseGridLayout(bool value)
        {
            UseGridLayout = value;
            StateHasChanged();
        }
    }

    private static Marius.Winter.Panel GetInnerPanel(Marius.Winter.Panel root)
    {
        Assert.Single(root.Children);
        return Assert.IsType<Marius.Winter.Panel>(root.Children[0]);
    }

    [Fact]
    public async Task FlexLayout_RemovedWhenConditionBecomesFalse()
    {
        var renderer = new TestRenderer();
        var root = new Marius.Winter.Panel();
        var rootHandler = TestRenderer.CreateRootHandler(renderer, root);

        // Render with FlexLayout enabled
        var component = await renderer.AddComponent<PanelWithConditionalFlex>(rootHandler,
            new() { ["UseFlexLayout"] = true });
        var innerPanel = GetInnerPanel(root);
        Assert.IsType<Marius.Winter.FlexLayout>(innerPanel.Layout);

        // Toggle off — layout should revert to null
        await renderer.Dispatcher.InvokeAsync(() => component.SetUseFlexLayout(false));
        Assert.Null(innerPanel.Layout);
    }

    [Fact]
    public async Task FlexLayout_ReappliedWhenConditionBecomesTrue()
    {
        var renderer = new TestRenderer();
        var root = new Marius.Winter.Panel();
        var rootHandler = TestRenderer.CreateRootHandler(renderer, root);

        // Start without layout
        var component = await renderer.AddComponent<PanelWithConditionalFlex>(rootHandler);
        var innerPanel = GetInnerPanel(root);
        Assert.Null(innerPanel.Layout);

        // Toggle on
        await renderer.Dispatcher.InvokeAsync(() => component.SetUseFlexLayout(true));
        Assert.IsType<Marius.Winter.FlexLayout>(innerPanel.Layout);

        // Toggle off again
        await renderer.Dispatcher.InvokeAsync(() => component.SetUseFlexLayout(false));
        Assert.Null(innerPanel.Layout);
    }

    [Fact]
    public async Task GridLayout_RemovedWhenConditionBecomesFalse()
    {
        var renderer = new TestRenderer();
        var root = new Marius.Winter.Panel();
        var rootHandler = TestRenderer.CreateRootHandler(renderer, root);

        // Render with GridLayout enabled
        var component = await renderer.AddComponent<PanelWithConditionalGrid>(rootHandler,
            new() { ["UseGridLayout"] = true });
        var innerPanel = GetInnerPanel(root);
        Assert.IsType<Marius.Winter.GridLayout>(innerPanel.Layout);

        // Toggle off — layout should revert to null
        await renderer.Dispatcher.InvokeAsync(() => component.SetUseGridLayout(false));
        Assert.Null(innerPanel.Layout);
    }

    [Fact]
    public async Task GridLayout_ReappliedWhenConditionBecomesTrue()
    {
        var renderer = new TestRenderer();
        var root = new Marius.Winter.Panel();
        var rootHandler = TestRenderer.CreateRootHandler(renderer, root);

        // Start without layout
        var component = await renderer.AddComponent<PanelWithConditionalGrid>(rootHandler);
        var innerPanel = GetInnerPanel(root);
        Assert.Null(innerPanel.Layout);

        // Toggle on
        await renderer.Dispatcher.InvokeAsync(() => component.SetUseGridLayout(true));
        Assert.IsType<Marius.Winter.GridLayout>(innerPanel.Layout);

        // Toggle off again
        await renderer.Dispatcher.InvokeAsync(() => component.SetUseGridLayout(false));
        Assert.Null(innerPanel.Layout);
    }
}
