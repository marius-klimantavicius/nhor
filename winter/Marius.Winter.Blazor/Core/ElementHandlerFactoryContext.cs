using Microsoft.AspNetCore.Components;
using System;

namespace Marius.Winter.Blazor.Core
{
    internal class ElementHandlerFactoryContext
    {
        public ElementHandlerFactoryContext(NativeComponentRenderer renderer, IElementHandler parentHandler, IComponent component)
        {
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            ParentHandler = parentHandler;
            Component = component;
        }

        public IElementHandler ParentHandler { get; }
        public IComponent Component { get; }
        public NativeComponentRenderer Renderer { get; }
    }
}
