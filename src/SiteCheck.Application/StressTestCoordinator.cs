using System.Diagnostics;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Application;

/// <summary>
/// Führt einen Lasttest gegen die <b>eigene</b> Seite aus: hält über die eingestellte Dauer
/// eine feste Zahl gleichzeitiger Anfragen offen und misst die Antwortzeiten.
/// <para>
/// Die Bestätigung, dass die Seite dem Nutzer gehört, ist Sache der aufrufenden Schicht und
/// wird hier erzwungen: Ohne die Eigentums-Bestätigung läuft kein Test. Die Obergrenzen für
/// Parallelität und Dauer kommen fest aus <see cref="StressTestOptions.Clamp"/> und lassen
/// sich hier nicht umgehen.
/// </para>
/// </summary>
public sealed class StressTestCoordinator(ILoadProbe loadProbe)
{
    private readonly ILoadProbe _loadProbe = loadProbe;

    /// <summary>
    /// Führt den Lasttest aus.
    /// </summary>
    /// <param name="target">Die eigene, selbst gehostete Seite.</param>
    /// <param name="options">Gewünschte Parallelität und Dauer; wird auf die festen Grenzen zurückgeschnitten.</param>
    /// <param name="ownershipConfirmed">Muss <see langword="true"/> sein: der Nutzer hat bestätigt, dass die Seite ihm gehört.</param>
    /// <param name="progress">Nimmt Zwischenstände entgegen; darf <see langword="null"/> sein.</param>
    /// <param name="cancellationToken">Bricht den Test ab; bereits laufende Anfragen werden beendet.</param>
    /// <returns>Die Zusammenfassung des Laufs.</returns>
    /// <exception cref="InvalidOperationException">Wenn die Eigentums-Bestätigung fehlt.</exception>
    public async Task<StressTestResult> RunAsync(
        CheckTarget target,
        StressTestOptions options,
        bool ownershipConfirmed,
        IProgress<StressTestProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        if (!ownershipConfirmed)
        {
            throw new InvalidOperationException(
                "Ein Lasttest läuft nur nach ausdrücklicher Bestätigung, dass die Seite dem Nutzer gehört.");
        }

        StressTestOptions effective = options.Clamp();
        List<RequestOutcome> outcomes = [];
        object outcomesLock = new();

        using CancellationTokenSource durationLimit = new(effective.Duration);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationLimit.Token);

        Stopwatch stopwatch = Stopwatch.StartNew();
        using Timer progressTimer = StartProgressTimer(progress, outcomes, outcomesLock, stopwatch, effective.Duration);

        // Je ein Arbeiter je gewünschter gleichzeitiger Anfrage. Jeder schickt in Schleife
        // eine Anfrage nach der anderen, bis die Zeit abgelaufen oder abgebrochen ist. So
        // bleiben konstant so viele Anfragen offen, wie eingestellt, ohne sie im Voraus zu zählen.
        Task[] workers = new Task[effective.Concurrency];
        for (int worker = 0; worker < workers.Length; worker++)
        {
            workers[worker] = RunWorkerAsync(target.Address, outcomes, outcomesLock, linked.Token);
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
        stopwatch.Stop();

        // Ein Abbruch durch den Nutzer wird zum Ergebnis, nicht zur Ausnahme: Das bereits
        // Gemessene ist aussagekräftig, und der Lauf endet ohnehin. Nur ein Abbruch, der nicht
        // vom Zeitlimit kommt, wird als solcher gemeldet.
        if (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        StressTestResult result = StressTestResult.From(outcomes, stopwatch.Elapsed);
        progress?.Report(new StressTestProgress(result.TotalRequests, stopwatch.Elapsed, effective.Duration));
        return result;
    }

    private async Task RunWorkerAsync(Uri address, List<RequestOutcome> outcomes, object outcomesLock, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            RequestOutcome outcome;
            try
            {
                outcome = await _loadProbe.SendAsync(address, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Zeit abgelaufen oder abgebrochen mitten in einer Anfrage: Diese zählt nicht mehr.
                break;
            }

            lock (outcomesLock)
            {
                outcomes.Add(outcome);
            }
        }
    }

    private static Timer StartProgressTimer(
        IProgress<StressTestProgress>? progress,
        List<RequestOutcome> outcomes,
        object outcomesLock,
        Stopwatch stopwatch,
        TimeSpan duration)
    {
        if (progress is null)
        {
            // Kein Beobachter: ein Timer, der nie feuert, spart die Sonderbehandlung im Aufrufer.
            return new Timer(static _ => { }, null, Timeout.Infinite, Timeout.Infinite);
        }

        // Viermal pro Sekunde genügt für einen flüssigen Balken, ohne die UI zu fluten.
        TimeSpan interval = TimeSpan.FromMilliseconds(250);
        return new Timer(
            _ =>
            {
                int completed;
                lock (outcomesLock)
                {
                    completed = outcomes.Count;
                }

                progress.Report(new StressTestProgress(completed, stopwatch.Elapsed, duration));
            },
            null,
            interval,
            interval);
    }
}
