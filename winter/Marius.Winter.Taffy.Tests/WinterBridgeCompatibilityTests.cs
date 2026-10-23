using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class WinterBridgeCompatibilityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BridgeStylesRetainGeometryWithNativeStaticPositionDefault(int kind)
    {
        var root = new Panel { Layout = kind switch { 0 => new FlexLayout(), 1 => new GridLayout(), _ => new StackLayout() } };
        var nested = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical }, MinWidth = 30, MinHeight = 20, LayoutData = new FlexItem { Grow = 1 } };
        nested.AddChild(new Panel { MinWidth = 15, MinHeight = 12 });
        root.AddChild(nested); root.AddChild(new Panel { MinWidth = 25, MinHeight = 18 });
        var buildStyle = typeof(Element).Assembly.GetType("Marius.Winter.TaffyLayoutEngine", true)!.GetMethod("BuildStyle", BindingFlags.Static | BindingFlags.NonPublic)!;
        static List<Element> Flatten(Element element)
        {
            var list = new List<Element> { element }; foreach (var child in element.Children) list.AddRange(Flatten(child)); return list;
        }
        var elements = Flatten(root);
        (TaffyTree<object> Tree, Dictionary<Element, NodeId> Nodes) Build(bool legacyRelative)
        {
            var tree = new TaffyTree<object>(); var nodes = new Dictionary<Element, NodeId>();
            NodeId Visit(Element element)
            {
                var children = new List<NodeId>(); foreach (var child in element.Children) children.Add(Visit(child));
                var style = (Style)buildStyle.Invoke(null, new object[] { element })!;
                Assert.Equal(Position.Static, style.PositionValue);
                Assert.Equal(new Rect<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO), style.Inset);
                if (element == root) style.SizeValue = new(Dimension.Length(200), Dimension.Length(150));
                if (legacyRelative) style.PositionValue = Position.Relative;
                var node = tree.NewWithChildren(style, children.ToArray()); nodes.Add(element, node); return node;
            }
            Visit(root); return (tree, nodes);
        }
        var legacy = Build(true); var native = Build(false); var available = new Size<AvailableSpace>(AvailableSpace.Definite(200), AvailableSpace.Definite(150));
        for (int pass = 0; pass < 2; pass++)
        {
            legacy.Tree.ComputeLayout(legacy.Nodes[root], available); native.Tree.ComputeLayout(native.Nodes[root], available);
            foreach (var element in elements) Assert.Equal(legacy.Tree.GetLayout(legacy.Nodes[element]), native.Tree.GetLayout(native.Nodes[element]));
        }
    }
}
