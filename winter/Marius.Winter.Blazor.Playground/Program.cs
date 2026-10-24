using System.Collections.Generic;
using System.Linq;
using Marius.Winter;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Playground;

var backend = args.Contains("--gl") ? RenderBackend.GL : RenderBackend.SW;
var window = new Window(900, 720, "Winter Blazor Playground", Theme.Light, backend);

var host = window.UseBlazor();
_ = host.AddComponent<App>(new Dictionary<string, object> { ["OwnerWindow"] = window });

window.Run();
