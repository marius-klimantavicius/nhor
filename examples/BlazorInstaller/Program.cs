using Marius.Winter;
using Marius.Winter.Blazor;

var window = new Window(520, 420, "Example Installer", Theme.Light, RenderBackend.SW);

var host = window.UseBlazor();
_ = host.AddComponent<BlazorInstaller.InstallerApp>();

window.Run();
