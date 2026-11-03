using Microsoft.Extensions.Logging;

namespace SiteCheck.App.Services;

/// <summary>Die Protokollmeldungen der Oberfläche, quellgeneriert und damit ohne Laufzeitkosten.</summary>
internal static partial class AppLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Erststart-Hinweis fehlgeschlagen")]
    public static partial void FirstRunNoticeFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Laden der Seitenliste fehlgeschlagen")]
    public static partial void SiteListLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Critical, Message = "Start fehlgeschlagen")]
    public static partial void StartFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Update-Prüfung beim Start fehlgeschlagen")]
    public static partial void UpdateCheckFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Lesen des Protokolls fehlgeschlagen")]
    public static partial void LogReadFailed(this ILogger logger, Exception exception);
}
