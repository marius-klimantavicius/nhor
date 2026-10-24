using System;
using System.Linq;
using Xunit;

namespace Marius.Winter.Tests;

public class ItemControllerTests
{
    public sealed record Asset(string Key, string Name, int Size);
    [Fact]
    public void ReplaceAndReorderPreserveSelectedKeyAndResolveTheNewItemAtomically()
    {
        var controller = new ItemController<Asset>(a => a.Key);
        controller.SetItems([new("a", "Duplicate", 1), new("b", "Duplicate", 2)]);
        controller.Select("b");
        var renamed = new Asset("b", "Žąsis e\u0301 👩‍💻", 3);
        controller.SetItems([renamed, new("a", "Duplicate", 1)]);
        Assert.Equal("b", controller.SelectedKey);
        Assert.Same(renamed, controller.SelectedItem);
        Assert.Throws<ArgumentException>(() => controller.SetItems([renamed, renamed]));
        Assert.Equal(new[] { "b", "a" }, controller.Items.Select(i => i.Key));
        Assert.Equal("b", controller.SelectedKey);
        controller.SetItems([new("a", "Duplicate", 1)]);
        Assert.Null(controller.SelectedKey);
        controller.Select("unmatched"); Assert.Null(controller.SelectedKey);
    }
}
