using Marius.Winter;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Playground;

var window = new Window(900, 650, "Winter Blazor Playground", Theme.Light, RenderBackend.SW);

var host = window.UseBlazor();
_ = host.AddComponent<App>();

window.Run();
