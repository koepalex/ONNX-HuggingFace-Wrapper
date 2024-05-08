using System.Diagnostics;

using Microsoft.Extensions.Logging;

internal sealed class ExecutorWithMeasurement: IDisposable
{
    private static readonly Stopwatch _stopwatch = new Stopwatch();
    private readonly ILogger<ExecutorWithMeasurement> _logger;

    public ExecutorWithMeasurement(ILogger<ExecutorWithMeasurement> logger) 
    {
        _logger = logger;
        _stopwatch.Start();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _logger.LogTrace($"Execution time: {_stopwatch.ElapsedMilliseconds} ms");
    }
}

internal sealed class ExecutorWithMeasurementFactory
{
    private readonly ILogger<ExecutorWithMeasurement> _logger;

    public ExecutorWithMeasurementFactory(ILogger<ExecutorWithMeasurement> logger)
    {
        _logger = logger;
    }

    public ExecutorWithMeasurement CreateExecutor()
    {
        return new ExecutorWithMeasurement(_logger);
    }
}