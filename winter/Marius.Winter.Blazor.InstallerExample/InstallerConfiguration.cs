using System.ComponentModel.DataAnnotations;

namespace Marius.Winter.Blazor.InstallerExample;

public sealed class InstallerConfiguration
{
    [CustomValidation(typeof(InstallerConfiguration), nameof(ValidateDestination))]
    public PickerPath? DestinationPath
    {
        get => string.IsNullOrEmpty(Destination) ? null : new PickerPath(Destination);
        set => Destination = value?.Value ?? "";
    }
    public static ValidationResult? ValidateDestination(PickerPath? value, ValidationContext context)
        => value is { Value: var path } && path.StartsWith('/') ? ValidationResult.Success
            : new ValidationResult("Enter an absolute destination beginning with /.", [nameof(DestinationPath)]);
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
