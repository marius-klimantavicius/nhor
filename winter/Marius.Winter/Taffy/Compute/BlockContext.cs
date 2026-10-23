// Ported from BlockContext in src/compute/block.rs at 802e497b.
using System;
using System.Collections.Generic;

namespace Marius.Winter.Taffy;

public readonly record struct FloatMark(int Left, int Right);
public partial class BlockContext
{
    internal bool AdjoiningLeft, AdjoiningRight;
    private (bool Left, bool Right)? _topAdjoining;
    public void CommitStrut()
    {
        _topAdjoining ??= (AdjoiningLeft, AdjoiningRight);
        AdjoiningLeft = AdjoiningRight = false;
    }
    public (bool Left, bool Right) TopAdjoiningFloats() => _topAdjoining ?? (AdjoiningLeft, AdjoiningRight);
    public void MergeAdjoiningFloats((bool Left, bool Right) flags) { AdjoiningLeft |= flags.Left; AdjoiningRight |= flags.Right; }
    public bool HasAdjoiningFloat(Clear clear) => clear switch
    {
        Clear.Left => AdjoiningLeft, Clear.Right => AdjoiningRight, Clear.Both => AdjoiningLeft || AdjoiningRight, _ => false,
    };
    public Point<float> PlaceFloatedBox(ulong id, Size<float> size, float minY, FloatDirection direction, Clear clear, bool adjoining)
    {
        if (adjoining) { if (direction == FloatDirection.Left) AdjoiningLeft = true; else AdjoiningRight = true; }
        var pos = Bfc.FloatCtx.PlaceFloatedBox(id, size, minY + YOffset, ContentBoxInsets, direction, clear);
        pos.Y -= YOffset; pos.X -= Insets[0];
        FloatContentContribution = TaffySys.F32Max(FloatContentContribution, pos.Y + size.Height);
        return pos;
    }
    public FloatMark FloatMark() => new(Bfc.FloatCtx.LeftFloats.Count, Bfc.FloatCtx.RightFloats.Count);
    public IEnumerable<(ulong Id, Point<float> Position)> FloatsPlacedSince(FloatMark mark)
    {
        for (int side = 0; side < 2; side++)
        {
            var floats = side == 0 ? Bfc.FloatCtx.LeftFloats : Bfc.FloatCtx.RightFloats;
            for (int i = side == 0 ? mark.Left : mark.Right; i < floats.Count; i++)
            {
                var placed = floats[i];
                var pos = Bfc.FloatCtx.PlacedPosition(placed, (FloatDirection)side);
                yield return (placed.Id, new(pos.X - Insets[0], pos.Y - YOffset));
            }
        }
    }
    public BfcSlot FindBfcSlot(float minY, float[] margins, Direction direction, Clear clear, int? after)
    {
        var slot = Bfc.FloatCtx.FindBfcSlot(minY + YOffset, ContentBoxInsets, margins, direction, clear, after);
        slot.Y -= YOffset; slot.X -= Insets[0]; return slot;
    }
}
