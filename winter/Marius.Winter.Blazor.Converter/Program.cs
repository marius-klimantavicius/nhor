using Marius.Winter;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Converter;

var window = new Window(1100, 700, "ThorVG Converter", Theme.Light, RenderBackend.SW);

var host = window.UseBlazor();
_ = host.AddComponent<App>(new() { [nameof(App.OwnerWindow)] = window });

window.Run();
