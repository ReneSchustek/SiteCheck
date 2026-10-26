using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Application;

/// <summary>
/// Führt die Prüfungen eines Laufs der Reihe nach aus, meldet den Fortschritt und bricht auf
/// Wunsch ab. Die Reihenfolge der übergebenen Prüfungen bleibt die Reihenfolge der Befunde.
/// </summary>
public sealed class CheckRunner(IEnumerable<ICheck> checks)
{
    // Einmal aufzählen und festhalten: Die Reihenfolge ist Teil des Vertrags (Web vor Mail),
    // und die Gesamtzahl steht für die Fortschrittsmeldung von Anfang an fest.
    private readonly IReadOnlyList<ICheck> _checks = [.. checks];

    /// <summary>
    /// Führt alle Prüfungen für ein Ziel aus.
    /// </summary>
    /// <param name="target">Das Prüfziel.</param>
    /// <param name="clock">Zeitquelle für Anfang und Ende des Laufs.</param>
    /// <param name="progress">Nimmt Fortschrittsmeldungen entgegen; darf <see langword="null"/> sein.</param>
    /// <param name="cancellationToken">Bricht den Lauf zwischen den Prüfungen ab.</param>
    /// <returns>Der Bericht mit allen Befunden.</returns>
    public async Task<CheckReport> RunAsync(
        CheckTarget target,
        TimeProvider clock,
        ICheckProgress? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(clock);

        DateTimeOffset startedAt = clock.GetUtcNow();
        List<Finding> findings = [];

        for (int index = 0; index < _checks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ICheck check = _checks[index];
            progress?.Report(check.Id, index, _checks.Count);

            findings.AddRange(await RunSingleAsync(check, target, cancellationToken).ConfigureAwait(false));
        }

        progress?.Report(string.Empty, _checks.Count, _checks.Count);
        return new CheckReport(target.ToString(), startedAt, clock.GetUtcNow(), findings);
    }

    private static async Task<IReadOnlyList<Finding>> RunSingleAsync(
        ICheck check,
        CheckTarget target,
        CancellationToken cancellationToken)
    {
        try
        {
            return await check.RunAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Abbruch ist kein Prüfergebnis, sondern Nutzerwille; nach oben durchreichen.
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Eine einzelne fehlgeschlagene Prüfung darf den ganzen Lauf nicht beenden. Statt
            // den Fehler zu verschlucken, wird er als sichtbarer Befund gemeldet, mit Kennung
            // der Prüfung, damit nachvollziehbar bleibt, welche nicht durchlief.
            return
            [
                new Finding(
                    check.Id + ".error",
                    check.Category,
                    Severity.Warning,
                    "Diese Prüfung ließ sich nicht abschließen",
                    ex.Message),
            ];
        }
    }
}
