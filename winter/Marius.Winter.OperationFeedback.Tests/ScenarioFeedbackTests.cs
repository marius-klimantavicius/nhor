using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;
using static Marius.Winter.OperationFeedback.Tests.FeedbackTests;
using static Marius.Winter.OperationFeedback.Tests.NativeFeedbackTests;

namespace Marius.Winter.OperationFeedback.Tests;

public class ScenarioFeedbackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualScenarioModulesCancelResetSuppressOldRunsAndCooperativelyClose(bool blazor)
    {
        OnOwner(() =>
        {
            using var window = new Window(1024, 900, "C08 scenario lifetime", Theme.Light);
            object scenario;
            if (blazor)
            {
                var host = window.UseBlazor();
                scenario = host.AddComponent<Marius.Winter.Blazor.Playground.Scenarios.OperationFeedback>().GetAwaiter().GetResult();
            }
            else
            {
                var native = new Marius.Winter.Playground.Scenarios.OperationFeedbackScenario(window);
                window.AddChild(native.Content);
                scenario = native;
            }
            var data = scenario.GetType().GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scenario)!;
            Task Completion() => (Task)data.GetType().GetProperty("Completion")!.GetValue(data)!;
            OperationStatus Status() => (OperationStatus)data.GetType().GetProperty("Status")!.GetValue(data)!;
            void Click(string text) => window.Dispatcher.InvokeAsync(() => Walk(window).OfType<Button>().Single(b => b.Text == text).Clicked!()).GetAwaiter().GetResult();
            var log = Walk(window).OfType<LogView>().Single();
            var feedback = Walk(window).OfType<StatusPanel>().Single();
            Click("Install preview pack");
            Assert.True(Status().IsBusy);
            var first = Completion();
            Click("Install preview pack");
            Assert.Same(first, Completion());
            var cancellation = feedback.CancelAsync();
            PumpUntil(window, cancellation);
            Assert.Equal(OperationState.Canceled, Status().State);
            Click("Reset");
            Assert.Equal(OperationState.Empty, Status().State);
            Assert.Empty(log.Text);
            Click("Simulate failure");
            var stale = Completion();
            Click("Reset");
            Click("Install preview pack");
            PumpUntil(window, stale);
            Assert.True(Status().IsBusy);
            var latest = Completion();
            window.Dispose(); // dedicated Window closing scope must await cancellation
            Assert.True(latest.IsCompletedSuccessfully);
            Assert.True(log.IsDisposed);
            Assert.Null(Get<object?>(log, "_subscription"));
            Assert.False(window.Animator.HasActiveAnimations);
        });
    }

    internal static IEnumerable<Element> Walk(Element element)
    {
        yield return element;
        foreach (var child in element.Children) foreach (var descendant in Walk(child)) yield return descendant;
    }
}
