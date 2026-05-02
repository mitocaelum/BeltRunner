using System;
using NLog;

namespace BeltRunner.Core.Logging;

internal static class BeltRunnerLogger {
    public static Logger GetLogger<T>() {
        Type type = typeof(T);
        string loggerName = type.FullName ?? type.Name;
        return LogManager.GetLogger(loggerName);
    }

    public static void Write(Logger logger, LogLevel level, string message, Action<LogEventInfo>? enrich = null, Exception? exception = null) {
        if( logger is null ) throw new ArgumentNullException(nameof(logger));
        if( level is null ) throw new ArgumentNullException(nameof(level));
        if( message is null ) throw new ArgumentNullException(nameof(message));

        if( !logger.IsEnabled(level) ) {
            return;
        }

        try {
            LogEventInfo logEvent = new(level, logger.Name, message) {
                Exception = exception
            };

            enrich?.Invoke(logEvent);
            logger.Log(logEvent);
        } catch {
            // Logging must never break BeltRunner execution.
        }
    }

    public static void SetProperty(LogEventInfo logEvent, string name, object? value) {
        if( logEvent is null ) throw new ArgumentNullException(nameof(logEvent));
        if( name is null ) throw new ArgumentNullException(nameof(name));

        if( value is null ) {
            return;
        }

        logEvent.Properties[name] = value;
    }
}
