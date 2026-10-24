using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;

namespace Marius.Winter.Blazor.Tests;

public sealed class ItemLifetimeProbe : ComponentBase, IDisposable
{
    [Parameter] public string Key { get; set; } = "";
    public static List<string> Removed { get; } = [];
    public void Dispose() => Removed.Add(Key);
}
