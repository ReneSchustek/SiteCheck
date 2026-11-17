using Microsoft.Extensions.Logging;
using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Application;

/// <summary>Die Protokollmeldungen der Abläufe. Ein Halt muss sich später nachvollziehen lassen.</summary>
internal static partial class ApplicationLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Prüflauf gestartet: {Address}")]
    public static partial void RunStarted(this ILogger logger, string address);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Prüflauf beendet: {Address}, {Findings} Befunde, angehalten: {Halted}")]
    public static partial void RunFinished(this ILogger logger, string address, int findings, bool halted);

    /// <summary>
    /// Meldet einen Halt. Das Ende steht in der Ortszeit der Zeitquelle, wie die Zeit am Anfang jeder
    /// Protokollzeile; in UTC sähe „bis 12:08Z“ neben „12:08“ aus wie „bis jetzt“.
    /// </summary>
    public static void RequestsHalted(this ILogger logger, string host, DateTimeOffset until, RequestHaltReason reason, TimeProvider clock) =>
        logger.RequestsHaltedUntilLocal(host, TimeZoneInfo.ConvertTime(until, clock.LocalTimeZone).DateTime, reason);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Anfragen an {Host} angehalten bis {Until:yyyy-MM-dd HH:mm:ss}: {Reason}")]
    private static partial void RequestsHaltedUntilLocal(this ILogger logger, string host, DateTime until, RequestHaltReason reason);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Sperrzeit für {Host} von Hand aufgehoben")]
    public static partial void HaltReleased(this ILogger logger, string host);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Lasttest gestartet: {Address}, {Concurrency} gleichzeitig, {Seconds} s")]
    public static partial void StressStarted(this ILogger logger, string address, int concurrency, double seconds);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "Lasttest beendet: {Address}, {Requests} Anfragen, wegen Abweisung beendet: {Rejected}")]
    public static partial void StressFinished(this ILogger logger, string address, int requests, bool rejected);
}
