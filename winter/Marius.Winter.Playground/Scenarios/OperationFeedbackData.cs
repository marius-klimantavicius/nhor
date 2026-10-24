using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Shared application simulation for the two playgrounds; no work scheduler or real converter.</summary>
internal sealed class OperationFeedbackData : IAsyncDisposable
{
    private readonly Func<Action, Task> _dispatch;
    private readonly TimeSpan _delay;
    private readonly object _lifetime = new();
    private readonly List<Task> _runs = new();
    private CancellationTokenSource? _cancellation;
    private Task _run = Task.CompletedTask;
    private long _generation;
    private volatile bool _disposed;

    public OperationFeedbackData(Func<Action, Task> dispatch, TimeSpan? stepDelay = null)
    { _dispatch = dispatch; _delay = stepDelay ?? TimeSpan.FromMilliseconds(180); }

    public LogBuffer Logs { get; } = new(160, 20_000);
    public OperationStatus Status { get; private set; } = new("No preview pack installed. Add a review note, then start the simulation.");
    public string Result { get; private set; } = "No files written. This is a cooperative simulation.";
    public event Action? Changed;
    public Task Completion => _run;

    public Task StartAsync(bool fail = false)
    {
        if (_disposed || Status.IsBusy) return Task.CompletedTask;
        var source = new CancellationTokenSource();
        lock (_lifetime) _cancellation = source;
        long generation = ++_generation;
        Status = new("Preparing preview pack…", OperationState.Working);
        Result = "Preparing · no output yet.";
        Logs.AppendLine("Preparing preview pack: cover.png, motion.gif, pulse.json · notes é 👩‍💻");
        Changed?.Invoke();
        _runs.RemoveAll(task => task.IsCompleted);
        _run = RunAsync(generation, source, fail);
        _runs.Add(_run);
        return _run;
    }

    public Task RetryAsync() => StartAsync();

    public async Task CancelAsync()
    {
        var task = _run;
        lock (_lifetime) _cancellation?.Cancel();
        await task.ConfigureAwait(false);
    }

    public void Reset()
    {
        if (_disposed) return;
        _generation++;
        lock (_lifetime) { _cancellation?.Cancel(); _cancellation = null; }
        Status = new("No preview pack installed. Add a review note, then start the simulation.");
        Result = "No files written. This is a cooperative simulation.";
        Logs.Clear();
        Changed?.Invoke();
    }

    private async Task RunAsync(long generation, CancellationTokenSource source, bool fail)
    {
        var token = source.Token;
        try
        {
            for (int i = 1; i <= 5; i++)
            {
                await Task.Delay(_delay, token).ConfigureAwait(false);
                await Update(() => Logs.AppendLine($"Prepare {i}/5 · validating sample media"));
            }
            for (int i = 1; i <= 40; i++)
            {
                await Task.Delay(_delay, token).ConfigureAwait(false);
                int completed = i;
                await Update(() =>
                {
                    Status = new($"Installing preview {completed}/40…", OperationState.Working, Progress: completed / 40f);
                    Result = $"Installing · {completed}/40 simulated previews prepared; no files written.";
                    Logs.AppendLine($"{completed:00}/40 · generated preview · 日本語 · Café");
                    Logs.AppendLine($"  checked raster / GIF / Lottie · step {completed:00}");
                });
                if (fail && i == 18) throw new InvalidOperationException("Sample destination is read-only. Retry uses a writable simulation destination.");
            }
            await Update(() =>
            {
                Status = new("Preview pack installed.", OperationState.Completed, StatusSeverity.Success, 1);
                Result = "Completed: 40 simulated previews; raster, GIF and Lottie samples are ready.";
                Logs.AppendLine("Completed · 40 previews · no real files written.");
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!_disposed && generation == _generation)
                await _dispatch(() =>
                {
                    if (_disposed || generation != _generation) return;
                    Status = new("Installation canceled.", OperationState.Canceled, StatusSeverity.Warning);
                    Result = "Canceled cooperatively; no output was committed.";
                    Logs.AppendLine("Canceled at a cooperative checkpoint.");
                    Changed?.Invoke();
                }).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await Update(() =>
            {
                Status = new(error.Message, OperationState.Failed, StatusSeverity.Error);
                Result = "Failed: sample destination read-only. Retry is available.";
                Logs.AppendLine("Failed · " + error.Message);
            });
        }
        finally
        {
            // UI state is updated only by generation-checked dispatches. A newer
            // run can own _cancellation while this source finishes disposing.
            lock (_lifetime)
            {
                if (ReferenceEquals(_cancellation, source)) _cancellation = null;
                source.Dispose();
            }
        }

        Task Update(Action action) => _dispatch(() =>
        {
            if (_disposed || generation != _generation || token.IsCancellationRequested) return;
            action();
            Changed?.Invoke();
        }).WaitAsync(token);
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _generation++;
            Changed = null;
            // Completed runs have already disposed their captured source.
            lock (_lifetime) { _cancellation?.Cancel(); _cancellation = null; }
        }
        return new ValueTask(SettleAsync());
    }

    private async Task SettleAsync()
    {
        try { await Task.WhenAll(_runs).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_disposed) { }
    }
}
