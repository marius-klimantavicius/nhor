using System.ComponentModel.DataAnnotations;

namespace Marius.Winter.Blazor.InstallerExample;

public sealed class InstallerConfiguration
{
    [Required(ErrorMessage = "Enter an application name.")]
    public string Name { get; set; } = "MyApp";
    [Required(ErrorMessage = "Enter an absolute destination beginning with /.")]
    [RegularExpression(@"/.*", ErrorMessage = "Enter an absolute destination beginning with /, for example /opt/myapp. This simulation never creates or changes the destination.")]
    public string Destination { get; set; } = "/opt/myapp";
    [Required, RegularExpression("production|staging|development", ErrorMessage = "Choose a listed environment.")]
    public string Environment { get; set; } = "production";
    public bool Shortcut { get; set; } = true;
    public bool LaunchAtStartup { get; set; }
}
