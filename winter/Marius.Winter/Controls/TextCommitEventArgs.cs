using System;

namespace Marius.Winter;

public enum TextCommitReason { Enter, Blur }

/// <summary>A snapshot of a user-edited value committed by Enter (Ctrl+Enter in TextArea) or focus loss.</summary>
public sealed class TextCommitEventArgs(string text, TextCommitReason reason) : EventArgs
{
    public string Text { get; } = text;
    public TextCommitReason Reason { get; } = reason;
}
