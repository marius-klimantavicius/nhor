using System;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class MenuSeparator : WinterComponentBase
{
    static MenuSeparator() => ElementHandlerRegistry.RegisterElementHandler<MenuSeparator>(r => new MenuItem.Handler(r, MenuItem.EntryKind.Separator));
}
