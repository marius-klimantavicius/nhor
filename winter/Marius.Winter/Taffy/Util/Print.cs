// Ported from src/util/print.rs. Managed TextWriter errors propagate to the caller.
using System;
using System.IO;

namespace Marius.Winter.Taffy;

public static class TreeDebug
{
    public static void PrintTree(IPrintTree tree, NodeId root)
    {
        using var writer = new StringWriter(); WriteTree(writer, tree, root); Console.WriteLine(writer.ToString());
    }
    public static void WriteTree(TextWriter writer, IPrintTree tree, NodeId root)
    {
        writer.Write("TREE\n");
        void WriteNode(NodeId node, bool hasSibling, string lines)
        {
            var layout = tree.GetFinalLayout(node);
            string Number(float value) => TaffySys.FormatFloat(value);
            string Edges(Rect<float> rect) => $"l:{Number(rect.Left)} r:{Number(rect.Right)} t:{Number(rect.Top)} b:{Number(rect.Bottom)}";
            writer.Write($"{lines}{(hasSibling ? "├── " : "└── ")} {tree.GetDebugLabel(node)} [x: {Number(layout.Location.X).PadRight(4)} y: {Number(layout.Location.Y).PadRight(4)} w: {Number(layout.Size.Width).PadRight(4)} h: {Number(layout.Size.Height).PadRight(4)} overflow: {Edges(layout.ScrollableOverflowRect)}, border: {Edges(layout.Border)}, padding: {Edges(layout.Padding)}] ({node})\n");
            string childLines = lines + (hasSibling ? "│   " : "    ");
            int index = 0, count = tree.ChildCount(node);
            foreach (var child in tree.ChildIds(node)) WriteNode(child, ++index < count, childLines);
        }
        WriteNode(root, false, "");
    }
}
