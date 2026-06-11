using Microsoft.Extensions.Logging;

namespace SmartNode.Mapek.Autonomous;

// Drives a tick action on a fixed interval until cancelled (step 1.8). The tick
// action is injected, so the loop mechanics are unit-testable without the full
// MAPE-K stack. A failing tick never kills the loop — it is logged and the next
// iteration runs. Cancellation stops the loop cleanly at the next await.
public sealed class AutonomousTickRunner
{
    private readonly Func<CancellationToken, Task> _tick;
    private readonly TimeSpan _interval;
    private readonly ILogger? _logger;

    public AutonomousTickRunner(Func<CancellationToken, Task> tick, TimeSpan interval, ILogger? logger = null)
    {
        _tick = tick;
        _interval = interval;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation(
            "MAPE-K autonomous tick started (interval {sec}s, dry-run only).", _interval.TotalSeconds);

        var count = 0;
        try
        {
            using var timer = new PeriodicTimer(_interval);
            while (!cancellationToken.IsCancellationRequested)
            {
                count++;
                try
                {
                    await _tick(cancellationToken);
                    _logger?.LogInformation("MAPE-K autonomous tick #{n} ok.", count);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A failing tick must never stop the supervision loop.
                    _logger?.LogWarning("MAPE-K autonomous tick #{n} failed: {msg}", count, ex.Message);
                }

                try
                {
                    if (!await timer.WaitForNextTickAsync(cancellationToken)) break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            _logger?.LogInformation("MAPE-K autonomous tick stopped after {n} iteration(s).", count);
        }
    }
}
