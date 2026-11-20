using Serilog.Events;

namespace Scattered.Shared.Logging.Loggers;

public class ConsoleLoggerConfiguration
{
    public bool Enabled { get; set; } = false;
    public LogEventLevel MinimumLevel { get; set; }
}