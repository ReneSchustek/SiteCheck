using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Application;

/// <summary>
/// Die Sperrwache lässt keine Anfrage mehr an diesen Host hinaus. Läuft als Ausnahme durch die
/// Prüfungen, damit keine von ihnen eine fehlende Antwort als Befund über die Seite ausgibt.
/// </summary>
public sealed class RequestsHaltedException : Exception
{
    /// <summary>Erstellt die Ausnahme ohne nähere Angaben.</summary>
    public RequestsHaltedException()
    {
    }

    /// <summary>Erstellt die Ausnahme mit einem Text.</summary>
    /// <param name="message">Der Text.</param>
    public RequestsHaltedException(string message)
        : base(message)
    {
    }

    /// <summary>Erstellt die Ausnahme mit einem Text und einer Ursache.</summary>
    /// <param name="message">Der Text.</param>
    /// <param name="innerException">Die Ursache.</param>
    public RequestsHaltedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Erstellt die Ausnahme mit Grund, Host und Ende der Sperre.</summary>
    /// <param name="reason">Warum nichts gesendet wird.</param>
    /// <param name="host">Der betroffene Host.</param>
    /// <param name="until">Bis wann nichts gesendet wird.</param>
    public RequestsHaltedException(RequestHaltReason reason, string host, DateTimeOffset until)
        : base($"Keine Anfragen an {host}: {reason}")
    {
        Reason = reason;
        Host = host;
        Until = until;
    }

    /// <summary>Warum nichts gesendet wird.</summary>
    public RequestHaltReason Reason { get; }

    /// <summary>Der betroffene Host.</summary>
    public string Host { get; } = string.Empty;

    /// <summary>Bis wann nichts gesendet wird.</summary>
    public DateTimeOffset Until { get; }
}
