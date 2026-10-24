using System;
using System.Collections.Generic;

namespace Marius.Winter;

/// <summary>Bounded document snapshots; every unequal edit is one step. No Window or frontend dependency.</summary>
public sealed class RichDocumentHistory
{
    private readonly List<RichDocument> _undo = new(), _redo = new();
    public int Capacity { get; }
    public RichDocument Current { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public RichDocumentHistory(RichDocument? document = null, int capacity = 64)
    {
        if (capacity < 1 || capacity > 256) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity; Current = document ?? RichDocument.Empty;
    }
    public bool Apply(RichDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Current.Equals(document)) return false;
        Push(_undo, Current); _redo.Clear(); Current = document;
        return true;
    }
    /// <summary>External replacement is a new baseline, including an explicit equal-value reset.</summary>
    public void Reset(RichDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Current = document; _undo.Clear(); _redo.Clear();
    }
    public bool Undo() => Move(_undo, _redo);
    public bool Redo() => Move(_redo, _undo);
    private bool Move(List<RichDocument> source, List<RichDocument> target)
    {
        if (source.Count == 0) return false;
        Push(target, Current); Current = source[^1]; source.RemoveAt(source.Count - 1);
        return true;
    }
    private void Push(List<RichDocument> stack, RichDocument value)
    {
        if (stack.Count == Capacity) stack.RemoveAt(0);
        stack.Add(value);
    }
}
