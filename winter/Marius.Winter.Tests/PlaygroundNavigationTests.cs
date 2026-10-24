using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class PlaygroundNavigationTests
{
    [Fact]
    public async Task CompiledRazorHostSelectsEveryRegisteredPageAndPreservesProfile()
    {
        using var pump = new NativePump(w =>
        {
            var mounted = w.UseBlazor().AddComponent<Blazor.Playground.App>(new Dictionary<string, object> { ["OwnerWindow"] = w });
            Assert.True(mounted.IsCompletedSuccessfully);
        });
        string[] names = ["Profile and volume", "Help and rich display", "Resizable workspace", "Operation feedback", "Existing demos"];
        string[] titles = ["Profile and volume", "Help and rich display", "Resizable workspace", "Operation feedback · Preview pack", "Winter Blazor Playground"];
        Element profile = null!;
        await pump.Call(w =>
        {
            var selector = Walk(w).OfType<ComboBox>().First();
            Assert.Equal(names, selector.Items);
            profile = Walk(w).OfType<TextBox>().First();
        });
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            await pump.Call(w => Walk(w).OfType<ComboBox>().First().SelectFromUser(index));
            await pump.Call(w =>
            {
                Assert.Contains(Walk(w).OfType<Label>(), l => l.IsEffectivelyVisible && l.Text == titles[index]);
                Assert.Same(profile, Walk(w).OfType<TextBox>().First());
            });
        }
    }

    private static IEnumerable<Element> Walk(Element e)
    {
        yield return e;
        foreach (var child in e.Children) foreach (var descendant in Walk(child)) yield return descendant;
    }
}
