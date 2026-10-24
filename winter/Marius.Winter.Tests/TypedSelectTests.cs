using System;
using System.Reflection;
using System.Collections.Generic;
using Xunit;

namespace Marius.Winter.Tests;

public class TypedSelectTests
{
    public record Option(string Id, string Name);

    [Fact]
    public void NativeTypedSelection_PreservesValueAcrossReorderReplacementAndClear()
    {
        // Reflection lets this permanent contract run on the pre-API source as well.
        var definition = typeof(ComboBox).Assembly.GetType("Marius.Winter.Select`2");
        Assert.NotNull(definition);
        using var element = (Element)Activator.CreateInstance(definition!.MakeGenericType(typeof(Option), typeof(string)))!;
        dynamic select = element;
        select.TextSelector = (Func<Option, string>)(o => o.Name);
        select.ValueSelector = (Func<Option, string>)(o => o.Id);
        select.Items = new[] { new Option("a", "Same"), new Option("b", "Same") };
        select.Value = "b";
        Assert.Equal(1, (int)select.SelectedIndex);
        select.Items = new[] { new Option("b", "Žąsis e\u0301"), new Option("a", "Same") };
        Assert.Equal("b", (string)select.Value); Assert.Equal(0, (int)select.SelectedIndex);
        Assert.Equal("Žąsis e\u0301", (string)select.SelectedItem);
        select.Items = new[] { new Option("a", "Same") };
        Assert.Equal(-1, (int)select.SelectedIndex); Assert.Equal("b", (string)select.Value);
        select.Items = new[] { new Option("b", "Restored") };
        Assert.Equal(0, (int)select.SelectedIndex);
        select.ClearSelection(); Assert.Equal(-1, (int)select.SelectedIndex); Assert.Null((string?)select.Value);
    }

    [Fact]
    public void Controller_UsesExplicitEqualityAndRejectsInvalidOptionsAtomically()
    {
        var controller = new SelectController<Option, string>();
        controller.Configure([new("a", "Same"), new("b", "Same")], o => o.Name, o => o.Id, "B", StringComparer.OrdinalIgnoreCase);
        Assert.Equal(1, controller.SelectedIndex);
        var changes = new List<string>(); controller.UserValueChanged += changes.Add;
        Assert.Throws<ArgumentException>(() => controller.SetItems([new("a", "First"), new("A", "Duplicate")]));
        Assert.Equal(2, controller.Items.Count); Assert.Equal("B", controller.Value); Assert.Equal(1, controller.SelectedIndex);
        Assert.Throws<ArgumentException>(() => controller.SetItems([new(null!, "No key")]));
        controller.SetItems([new("b", "Renamed"), new("a", "Same")]);
        Assert.Equal(0, controller.SelectedIndex); Assert.Empty(changes);
        controller.SelectIndex(1, fromUser: true); Assert.Equal(new[] { "a" }, changes);
        controller.SelectIndex(1, fromUser: true); Assert.Single(changes);
        controller.ClearSelection(); Assert.Null(controller.Value); Assert.Equal(-1, controller.SelectedIndex);
    }

    [Fact]
    public void NonnullableValueDefault_IsAValidKeyAndExplicitNativeClearSurvivesOptionUpdates()
    {
        var controller = new SelectController<int, int>();
        controller.Configure([0, 1], i => i.ToString(), i => i, 0);
        Assert.Equal(0, controller.SelectedIndex);
        controller.ClearSelection(); controller.SetItems([1, 0]); Assert.Equal(-1, controller.SelectedIndex);
        controller.Value = 0; Assert.Equal(1, controller.SelectedIndex);
    }
}
