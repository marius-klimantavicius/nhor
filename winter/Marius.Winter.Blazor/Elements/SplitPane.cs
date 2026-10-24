using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Two fragments with a logical-unit first-pane extent and conventional Position binding.</summary>
public class SplitPane : WinterComponentBase
{
    static SplitPane() => ElementHandlerRegistry.RegisterElementHandler<SplitPane>(renderer => new Handler(renderer));

    [Parameter] public float Position { get; set; } = 220;
    [Parameter] public float FirstMinimum { get; set; }
    [Parameter] public float SecondMinimum { get; set; }
    [Parameter] public Orientation Orientation { get; set; }
    [Parameter] public float ResetPosition { get; set; } = 220;
    [Parameter] public float KeyboardStep { get; set; } = 10;
    [Parameter] public EventCallback<float> PositionChanged { get; set; }
    [Parameter] public EventCallback<float> OnPositionChanged { get; set; }
    [Parameter] public EventCallback<float> VisiblePositionChanged { get; set; }
    [Parameter] public RenderFragment? First { get; set; }
    [Parameter] public RenderFragment? Second { get; set; }

    public float VisiblePosition => (ElementHandler as IWinterElementHandler)?.ElementControl is Marius.Winter.SplitPane split ? split.VisiblePosition : 0;
    public void FocusDivider() { if (ElementHandler is IWinterElementHandler { ElementControl: Marius.Winter.SplitPane split }) split.FocusDivider(); }
    public void Reset() { if (ElementHandler is IWinterElementHandler { ElementControl: Marius.Winter.SplitPane split }) split.Reset(); }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        // A fresh immutable snapshot also carries an explicit same-value parent
        // rejection through the element diff after an optimistic native edit.
        builder.AddAttribute("Configuration", new Configuration(Position, FirstMinimum, SecondMinimum, Orientation, ResetPosition, KeyboardStep));
        builder.AddAttribute("onpositionchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, AcceptPosition));
        if (VisiblePositionChanged.HasDelegate)
            builder.AddAttribute("onvisiblepositionchanged", EventCallback.Factory.Create<ChangeEventArgs>(this, args => VisiblePositionChanged.InvokeAsync((float)args.Value!)));
    }

    private Task AcceptPosition(ChangeEventArgs args)
    {
        Position = (float)args.Value!;
        // No continuation writes state: a parent reset or replacement during an
        // awaiting callback remains authoritative when that callback completes.
        return (PositionChanged.HasDelegate ? PositionChanged : OnPositionChanged).InvokeAsync(Position);
    }

    protected override RenderFragment GetChildContent() => builder =>
    {
        Slot(builder, 0, "first", First);
        Slot(builder, 1, "second", Second);
    };

    private static void Slot(RenderTreeBuilder builder, int sequence, string key, RenderFragment? content)
    {
        builder.OpenComponent<PaneSlot>(sequence);
        builder.SetKey(key);
        builder.AddAttribute(2, nameof(PaneSlot.IsFirst), key == "first");
        builder.AddAttribute(3, nameof(PaneSlot.ChildContent), content);
        builder.CloseComponent();
    }

    // A single fragment root fills its pane. Multiple roots use a vertical stack;
    // callers can supply their own Panel/Grid/Flex root for another layout.
    private sealed class SlotLayout : ILayout
    {
        internal static readonly SlotLayout Instance = new();
        private readonly Marius.Winter.StackLayout _stack = new() { Spacing = 4, CrossAlignment = Alignment.Stretch };
        public Vector2 Measure(Element container, float availableWidth, float availableHeight) => _stack.Measure(container, availableWidth, availableHeight);
        public void Arrange(Element container, RectF bounds)
        {
            if (container.Children.Count == 1) container.Children[0].Arrange(bounds);
            else _stack.Arrange(container, bounds);
        }
    }

    private sealed record Configuration(float Position, float FirstMinimum, float SecondMinimum, Orientation Orientation, float ResetPosition, float KeyboardStep);

    // Like Tab, a slot attaches its container through a native composition API,
    // while its ordinary child elements remain direct children of that container.
    private sealed class PaneSlot : WinterComponentBase
    {
        public PaneSlot() { }
        static PaneSlot() => ElementHandlerRegistry.RegisterElementHandler<PaneSlot>(renderer => new SlotHandler(renderer));
        [Parameter] public bool IsFirst { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        protected override void RenderAttributes(AttributesBuilder builder) => builder.AddAttribute(nameof(IsFirst), IsFirst);
        protected override RenderFragment? GetChildContent() => ChildContent;

        private sealed class SlotHandler : WinterElementHandler, INonPhysicalChild, IWinterContainerElementHandler
        {
            private Marius.Winter.SplitPane? _owner;
            private bool _first;
            internal SlotHandler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.Panel { Layout = SlotLayout.Instance, Padding = new Thickness(0) }) { }
            public override void ApplyAttribute(ulong id, string name, object value, string updates)
            {
                if (name == nameof(IsFirst)) _first = AttributeHelper.GetBool(value);
                else base.ApplyAttribute(id, name, value, updates);
            }
            public void SetParent(object parent)
            {
                _owner = parent as Marius.Winter.SplitPane ?? throw new InvalidOperationException("SplitPane slots require a SplitPane parent.");
                if (_first) _owner.First = ElementControl;
                else _owner.Second = ElementControl;
            }
            public void Remove()
            {
                if (_owner != null)
                {
                    if (_first && ReferenceEquals(_owner.First, ElementControl)) _owner.First = null;
                    else if (!_first && ReferenceEquals(_owner.Second, ElementControl)) _owner.Second = null;
                    _owner = null;
                }
            }
            public void AddChild(Element child, int index) => ElementControl.InsertChild(child, Math.Min(index, ElementControl.Children.Count));
            public void RemoveChild(Element child) => ElementControl.RemoveChild(child);
            public int GetChildIndex(Element child)
            {
                for (int i = 0; i < ElementControl.Children.Count; i++) if (ReferenceEquals(ElementControl.Children[i], child)) return i;
                return -1;
            }
            public void ReorderChildren(List<Element> order) => ElementControl.ReorderChildren(order);
            public override void Dispose() { Remove(); base.Dispose(); }
        }
    }

    private sealed class Handler : WinterElementHandler
    {
        private Configuration? _pending;
        private ulong _handlerId;
        private long _generation;
        private long _queuedGeneration = -1;
        private float _latest;
        private readonly CoalescedEvent _visibleChanged;
        private Marius.Winter.SplitPane Control => (Marius.Winter.SplitPane)ElementControl;

        internal Handler(NativeComponentRenderer renderer) : base(renderer, new Marius.Winter.SplitPane())
        {
            _visibleChanged = new CoalescedEvent(this);
            Control.PositionChanged = QueuePosition;
            Control.VisiblePositionChanged = value => _visibleChanged.Fire(value);
        }

        private void QueuePosition(float value)
        {
            if (IsDisposed || _handlerId == 0) return;
            _latest = value;
            if (Control.OwnerWindow is not { } window)
            {
                Renderer.DispatchNativeEventAsync(_handlerId, null, new ChangeEventArgs { Value = value });
                return;
            }
            if (_queuedGeneration == _generation) return;
            long generation = _generation;
            ulong id = _handlerId;
            _queuedGeneration = generation;
            window.DispatcherQueue.Enqueue(() =>
            {
                if (_queuedGeneration == generation) _queuedGeneration = -1;
                if (!IsDisposed && _generation == generation && _handlerId == id)
                    Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = _latest });
            });
        }

        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            switch (name)
            {
                case "Configuration":
                    _pending = WeakObjectStore.Get<Configuration>(value);
                    _generation++;
                    break;
                case "onpositionchanged":
                    _generation++;
                    RegisterEvent(id, oldId => { if (_handlerId == oldId) { _handlerId = 0; _generation++; } });
                    _handlerId = id;
                    break;
                case "onvisiblepositionchanged":
                    RegisterEvent(id, _visibleChanged.Unregister);
                    _visibleChanged.HandlerId = id;
                    break;
                default: base.ApplyAttribute(id, name, value, updates); break;
            }
        }

        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } config) return;
            _pending = null;
            Control.Orientation = config.Orientation;
            Control.FirstMinimum = config.FirstMinimum;
            Control.SecondMinimum = config.SecondMinimum;
            Control.ResetPosition = config.ResetPosition;
            Control.KeyboardStep = config.KeyboardStep;
            Control.Position = config.Position;
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            _generation++;
            _pending = null;
            Control.PositionChanged = null;
            Control.VisiblePositionChanged = null;
            base.Dispose();
        }
    }
}
