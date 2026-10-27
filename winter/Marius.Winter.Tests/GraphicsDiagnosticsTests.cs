using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Marius.Winter.Tests;

public class GraphicsDiagnosticsTests
{
    [Fact]
    public void InitializationCleanupKeepsOriginalFailureAndNativeDiagnostic()
    {
        var original = new InvalidOperationException("target GL canvas: WGL surface HDC missing");
        var cleanup = new InvalidOperationException("canvas cleanup also failed");
        var caught = Record.Exception((Action)(() =>
        {
            try { throw original; }
            catch (Exception failure)
            {
                GraphicsDiagnostics.CleanupFailedInitialization(failure, "target GL canvas", "WGL target", () => throw cleanup);
                throw;
            }
        }));
        Assert.Same(original, caught);
        Assert.Equal("target GL canvas", caught!.Data["Winter.Graphics.Stage"]);
        Assert.Equal("WGL target", caught.Data["Winter.Graphics.Context"]);
        Assert.Same(cleanup, caught.Data["Winter.Graphics.Cleanup"]);
        Assert.Contains("WGL surface HDC missing", caught.Message);
    }

    [Theory]
    [InlineData("Marius.Winter.Playground", false)]
    [InlineData("Marius.Winter.Playground", true)]
    [InlineData("Marius.Winter.Blazor.Playground", false)]
    [InlineData("Marius.Winter.Blazor.Playground", true)]
    public void DiagnosticCommandReportsContextAndExitsWithoutShowingWindow(string project, bool gl)
    {
        var result = Probe(project, gl, noDisplay: false);
        Assert.Equal(0, result.Code);
        Assert.Contains("Backend: " + (gl ? "GL" : "SW"), result.Output);
        Assert.Contains("Requested: OpenGL 3.3 core", result.Output);
        Assert.Contains("OpenGL:", result.Output);
        Assert.Contains("Profile: core", result.Output);
        Assert.Contains("Vendor:", result.Output);
        Assert.Contains("Renderer:", result.Output);
        Assert.Contains("GLSL:", result.Output);
        Assert.Contains("Result: success (hidden window; initialization and first frame)", result.Output);
        Assert.DoesNotContain("StackTrace", result.Output);
        Assert.False(Glfw._glfw.initialized);
    }

    [Theory]
    [InlineData("Marius.Winter.Playground")]
    [InlineData("Marius.Winter.Blazor.Playground")]
    public void DiagnosticCommandReportsNativeStartupStageWhenDisplayIsUnavailable(string project)
    {
        if (!OperatingSystem.IsLinux()) return;
        var result = Probe(project, gl: true, noDisplay: true);
        Assert.Equal(1, result.Code);
        Assert.Contains("Stage: initialize GLFW", result.Output);
        Assert.Contains("GLFW error 0x", result.Output);
        Assert.Contains("Result: failed", result.Output);
        Assert.DoesNotContain(" at Marius", result.Output);
    }

    private static (int Code, string Output) Probe(string project, bool gl, bool noDisplay)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, project + ".dll"));
        start.ArgumentList.Add(gl ? "--gl" : "--sw");
        start.ArgumentList.Add("--graphics-diagnostics");
        if (noDisplay)
        {
            start.Environment.Remove("DISPLAY");
            start.Environment.Remove("WAYLAND_DISPLAY");
        }
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(5000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail("--graphics-diagnostics must finish without entering the interactive playground loop.");
        }
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
