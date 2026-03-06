using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class Separator : WinterComponentBase
{
    static Separator()
    {
        ElementHandlerRegistry.RegisterElementHandler<Separator>(
            renderer => new Handler(renderer));
    }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
    }

    class Handler : WinterElementHandler
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Separator()) { }
    }
}
