using Marius.Winter;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.InstallerExample;

var window = new Window(600, 450, "MyApp Installer", Theme.Light, RenderBackend.SW);

var host = window.UseBlazor();
_ = host.AddComponent<App>();

window.Run();
