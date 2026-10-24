using System.Linq;
using Marius.Winter;
using Marius.Winter.Playground.Scenarios;

var backend = args.Contains("--gl") ? RenderBackend.GL : RenderBackend.SW;
var window = new Window(900, 720, "Winter GUI Playground" + (backend == RenderBackend.GL ? " [GL]" : " [SW]"), Theme.Light, backend);
using var profile = new ProfileAndVolume(window);
using var gallery = new LegacyGallery(window);
using var scenarios = new ScenarioHost(window);
scenarios.AddScenario("Profile and volume", profile.Content);
scenarios.AddScenario("Existing demos", gallery.Content);
window.AddChild(scenarios.Content);
window.SetAutoLayout(scenarios.Content);
window.Run();
