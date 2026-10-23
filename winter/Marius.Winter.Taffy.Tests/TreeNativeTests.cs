using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

public class TreeNativeTests
{
    [Fact]
    public void LifecycleMatchesNativeKeysContextsParentsChildrenAndDirtyState()
    {
        using var raw = typeof(TreeNativeTests).Assembly.GetManifestResourceStream("Marius.Winter.Taffy.Tests.Native.tree-corpus.json.gz")!;
        using var gzip = new GZipStream(raw, CompressionMode.Decompress); using var corpus = JsonDocument.Parse(gzip);
        var tree = new TaffyTree<int>(); var ids = new List<NodeId>(); int step = 0;
        foreach (var operation in corpus.RootElement.EnumerateArray())
        {
            var kind = operation.GetProperty("kind").GetString(); int index = operation.TryGetProperty("node", out var node) ? node.GetInt32() : 0;
            switch (kind)
            {
                case "new":
                    var style = new Style { SizeValue = new(Dimension.Length(20), Dimension.Length(10)) }; var context = operation.GetProperty("context");
                    ids.Add(context.ValueKind == JsonValueKind.Null ? tree.NewLeaf(style) : tree.NewLeafWithContext(style, context.GetInt32())); break;
                case "remove": tree.Remove(ids[index]); break;
                case "clear": tree.Clear(); break;
                case "context":
                    var updated = operation.GetProperty("context"); if (updated.ValueKind == JsonValueKind.Null) tree.ClearNodeContext(ids[index]); else tree.SetNodeContext(ids[index], updated.GetInt32()); break;
                case "dirty": tree.MarkDirty(ids[index]); break;
                case "compute": tree.ComputeLayout(ids[index], new(AvailableSpace.MaxContent, AvailableSpace.MaxContent)); break;
                case "children": tree.SetChildren(ids[index], operation.GetProperty("children").EnumerateArray().Select(v => ids[v.GetInt32()]).ToArray()); break;
            }
            var expected = operation.GetProperty("expected"); var expectedNodes = expected.GetProperty("nodes").EnumerateArray().ToArray();
            Assert.Equal(expectedNodes.Length, tree.TotalNodeCount());
            foreach (var item in expectedNodes)
            {
                var id = ids[item.GetProperty("logical").GetInt32()];
                Assert.True(item.GetProperty("id").GetUInt64() == id.Value, $"Step {step} {kind}: expected key {item.GetProperty("id")}, actual {id}");
                Assert.Equal(item.GetProperty("parent").ValueKind == JsonValueKind.Null ? (ulong?)null : item.GetProperty("parent").GetUInt64(), tree.Parent(id)?.Value);
                Assert.Equal(item.GetProperty("children").EnumerateArray().Select(v => v.GetUInt64()), tree.Children(id).Select(n => n.Value));
                Assert.Equal(item.GetProperty("dirty").GetBoolean(), tree.Dirty(id));
            }
            int contextIndex = 0;
            foreach (var value in expected.GetProperty("contexts").EnumerateArray())
            {
                bool present = tree.TryGetNodeContext(ids[contextIndex++], out var actual);
                Assert.Equal(value.ValueKind != JsonValueKind.Null, present); if (present) Assert.Equal(value.GetInt32(), actual);
            }
            if (expected.GetProperty("disjoint").ValueKind != JsonValueKind.Null)
            {
                var a = ids[expectedNodes[0].GetProperty("logical").GetInt32()]; var b = ids[expectedNodes[1].GetProperty("logical").GetInt32()];
                Assert.Equal(expected.GetProperty("disjoint")[0].GetBoolean(), tree.GetDisjointNodeContextMut([a, b]) != null);
                Assert.Equal(expected.GetProperty("disjoint")[1].GetBoolean(), tree.GetDisjointNodeContextMut([a, a]) != null);
            }
            step++;
        }
        Assert.Equal(800, step);
    }
}
