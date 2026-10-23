using System;
using System.IO;
using System.Text.Json;
internal static unsafe partial class Program
{
    private static void ValidateConstants()
    {
        using var values = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("GLFW_CONSTANTS_JSON")!));
        int count = 0;
        foreach (var property in values.RootElement.EnumerateObject())
        {
            count++;
            var field = typeof(Glfw.GLFW).GetField(property.Name);
            Check(field != null, "missing constant: " + property.Name);
            Check(Convert.ToInt64(field!.GetRawConstantValue()) == property.Value.GetInt64(), "constant differs: " + property.Name);
        }
        Record("public-numeric-constants", count);
    }
}
