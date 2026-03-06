using Microsoft.AspNetCore.Components;
using System;

namespace Marius.Winter.Blazor.Core
{
    internal class ElementHandlerFactory
    {
        private readonly Func<NativeComponentRenderer, IElementHandler, IComponent, IElementHandler> _callback;

        public ElementHandlerFactory(Func<NativeComponentRenderer, IElementHandler, IComponent, IElementHandler> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public IElementHandler CreateElementHandler(ElementHandlerFactoryContext context)
        {
            return _callback(context.Renderer, context.ParentHandler, context.Component);
        }
    }
}
