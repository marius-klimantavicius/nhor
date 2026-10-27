using System;
using System.Collections.Generic;
using System.Linq;
using Marius.Winter;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Playground;

var backend = args.Contains("--gl") ? RenderBackend.GL : RenderBackend.SW;
if (args.Contains("--graphics-diagnostics")) return GraphicsDiagnostics.Run(Console.Out, backend);
var window = new Window(900, 720, "Winter Blazor Playground", Theme.Light, backend);

var host = window.UseBlazor();
_ = host.AddComponent<App>(new Dictionary<string, object> { ["OwnerWindow"] = window });

window.Run();
return 0;
