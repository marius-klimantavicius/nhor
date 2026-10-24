using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Marius.Winter;

/// <summary>Label, caller-owned editor, help and validation messages in a wrapping column.</summary>
public class Field : Element
{
    private Element? _editor;
    private bool _readOnly, _explicitInvalid;
    private string _label = "";
    private readonly Label _help = new() { TextWrapping = ThorVG.TextWrap.Word };
    public Field(string label = "", Element? editor = null)
    {
        _label = label;
        LabelElement = new AssociatedLabel(this, label) { TextWrapping = ThorVG.TextWrap.Word };
        MessageElement = new Label() { TextWrapping = ThorVG.TextWrap.Word };
        AddChild(LabelElement); AddChild(_help); AddChild(MessageElement);
        Editor = editor;
        Refresh();
    }
    internal override bool ManagesOwnChildLayout => true;
    public Label LabelElement { get; }
    public Label MessageElement { get; }
    public string Label { get => _label; set { _label = value; Refresh(); } }
    public string HelpText { get => _help.Text; set { _help.Text = value; Refresh(); } }
    public float Gap { get; set; } = 5;
    public Element? Editor
    {
        get => _editor;
        set
        {
            if (ReferenceEquals(value, _editor)) return;
            if (value != null && value.Parent != null) throw new InvalidOperationException("Detach the editor before assigning it to a Field.");
            if (_editor != null) RemoveChild(_editor);
            _editor = value;
            if (value != null) InsertChild(value, 1);
            Refresh();
        }
    }
    public bool ReadOnly { get => _readOnly; set { _readOnly = value; Refresh(); } }
    public bool Invalid { get => _explicitInvalid || Messages.Count > 0; set { _explicitInvalid = value; Refresh(); } }
    public IReadOnlyList<string> Messages { get; private set; } = Array.Empty<string>();
    public Func<IEnumerable<string>>? Validate { get; set; }
    public void SetMessages(IEnumerable<string>? messages)
    {
        Messages = Array.AsReadOnly((messages ?? Array.Empty<string>()).Where(m => !string.IsNullOrWhiteSpace(m)).ToArray());
        MessageElement.Text = string.Join("\n", Messages);
        Refresh();
    }
    private void Refresh()
    {
        LabelElement.Text = _label + (_readOnly ? " (read-only)" : "") +
            (!IsEffectivelyEnabled ? " (disabled)" : "") + (Invalid && Messages.Count == 0 ? " (invalid)" : "");
        _help.Visible = !string.IsNullOrEmpty(_help.Text);
        MessageElement.Visible = Messages.Count > 0;
        if (_editor is TextBox box) { box.ReadOnly = _readOnly; box.Invalid = Invalid; }
        if (_editor is TextArea area) { area.ReadOnly = _readOnly; area.Invalid = Invalid; }
        MessageElement.Color = (OwnerWindow?.Theme ?? Theme.Dark).TextColor;
        InvalidateMeasure(); MarkDirty();
    }
    public bool FocusEditor()
    {
        var editor = _editor;
        if (!IsActive || editor == null || !editor.IsActive || !editor.Focusable) return false;
        OwnerWindow!.SetFocus(editor);
        return ReferenceEquals(OwnerWindow.FocusedElement, editor);
    }
    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        float width = 0, height = 0; int count = 0;
        foreach (var child in Children.Where(c => c.Visible))
        {
            var size = child.Measure(availableWidth, float.PositiveInfinity);
            width = Math.Max(width, size.X); height += size.Y; count++;
        }
        return new Vector2(float.IsFinite(availableWidth) ? availableWidth : width, height + Math.Max(0, count - 1) * Gap);
    }
    protected override void ArrangeCore(RectF bounds)
    {
        float y = 0;
        foreach (var child in Children.Where(c => c.Visible))
        {
            var size = child.Measure(bounds.W, float.PositiveInfinity);
            child.Arrange(new RectF(0, y, bounds.W, size.Y)); y += size.Y + Gap;
        }
    }
    protected override void OnThemeChanged() => Refresh();
    protected override void OnActivityChanged(bool active) => Refresh();
    public override void Dispose()
    {
        if (_editor != null) { RemoveChild(_editor); _editor = null; }
        Validate = null;
        base.Dispose();
    }
    private sealed class AssociatedLabel(Field field, string text) : Label(text)
    {
        public override bool OnMouseDown(int button, float x, float y) { if (button != 0) return false; field.FocusEditor(); return true; }
        public override void OnClick() => field.FocusEditor();
    }
}
