using System.Text.Json;

namespace Sciuscia.Foundations;

public sealed class ErrorLog(AppPaths paths)
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public async Task WriteAsync(Exception exception, string operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var entry = new
        {
            occurredAt = DateTimeOffset.UtcNow,
            operation,
            exceptionType = exception.GetType().FullName,
            exception.Message,
            exception.StackTrace
        };

        paths.EnsureDirectories();
        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(paths.ErrorLogPath, line, cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }
}

