using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Guard;

namespace SiteCheck.Application;

/// <summary>
/// Schützt den eigenen Anschluss davor, vom Hoster gesperrt zu werden. Jede Anfrage des Programms
/// holt sich hier ihren Platz; beim ersten Anzeichen einer Abweisung geht nichts mehr hinaus.
/// <para>
/// Eine Abwehr im geteilten Hosting zählt das Tempo und sperrt die anfragende Adresse, oft mit
/// Folgen für Website und Postfächer. Deshalb wird angehalten statt gedrosselt und neu versucht:
/// Jeder weitere Versuch verlängert eine Sperre. Eine Garantie gibt das nicht, denn die Regeln des
/// Hosters sind von außen unbekannt.
/// </para>
/// </summary>
public sealed class HostRequestGuard(TimeProvider clock, IRequestGuardStore store, IDnsProbe dns, ILogger? logger = null)
{
    // 1500 ms ergeben 40 Anfragen je Minute. Übliche Grenzen im geteilten Hosting liegen bei 60 bis
    // 120; 5 Anfragen je Sekunde führten bei ruhrcoder.de zur Sperre, dieses Tempo läuft dort ohne.
    // Der Abstand gilt über alle Hosts, weil mehrere eigene Seiten oft auf demselben Server liegen.
    internal static readonly TimeSpan MinimumSpacing = TimeSpan.FromMilliseconds(1500);

    // Reicht für rund sieben volle Läufe je Stunde und bremst, wer immer wieder neu startet.
    internal const int HourlyRequestBudget = 150;

    // Eine Stunde Ruhe verlängert eine Hostersperre nicht und lässt sich nach ausdrücklicher
    // Bestätigung aufheben, falls die Seite nur kurz weg war.
    internal static readonly TimeSpan HaltCooldown = TimeSpan.FromHours(1);

    internal static readonly TimeSpan StressTestCooldown = TimeSpan.FromMinutes(15);

    // Eine einzelne fehlende Antwort ist normal, etwa bei einem abgeschalteten Port 80.
    private const int UnansweredRequestsBeforeHalt = 2;

    private static readonly TimeSpan BudgetWindow = TimeSpan.FromHours(1);

    private readonly TimeProvider _clock = clock;
    private readonly IRequestGuardStore _store = store;
    private readonly IDnsProbe _dns = dns;
    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, HostGuardEntry> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _unanswered = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _nextSlot = DateTimeOffset.MinValue;

    /// <summary>Wartet auf den nächsten freien Platz und vermerkt die Anfrage.</summary>
    /// <param name="host">Der Rechnername, an den gesendet werden soll.</param>
    /// <param name="cancellationToken">Bricht auch die Wartezeit ab.</param>
    /// <exception cref="RequestsHaltedException">Wenn der Host gesperrt oder sein Budget aufgebraucht ist.</exception>
    public async Task EnterAsync(string host, CancellationToken cancellationToken)
    {
        string key = KeyFor(host);
        await MergeStoredStateAsync(cancellationToken).ConfigureAwait(false);

        TimeSpan wait;
        lock (_gate)
        {
            DateTimeOffset now = _clock.GetUtcNow();
            HostGuardEntry entry = CurrentEntry(key, now);
            ThrowIfBlocked(key, entry);

            if (entry.RecentRequests.Count >= HourlyRequestBudget)
            {
                _logger.RequestsHalted(key, entry.RecentRequests[0] + BudgetWindow, RequestHaltReason.BudgetUsed, _clock);
                throw new RequestsHaltedException(RequestHaltReason.BudgetUsed, key, entry.RecentRequests[0] + BudgetWindow);
            }

            // Der Platz wird im Schloss vergeben, gewartet wird draußen.
            DateTimeOffset slot = now > _nextSlot ? now : _nextSlot;
            _nextSlot = slot + MinimumSpacing;
            _hosts[key] = entry with { RecentRequests = [.. entry.RecentRequests, slot] };
            wait = slot - now;
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, _clock, cancellationToken).ConfigureAwait(false);

            // Während der Wartezeit kann eine andere Anfrage den Halt ausgelöst haben.
            lock (_gate)
            {
                ThrowIfBlocked(key, CurrentEntry(key, _clock.GetUtcNow()));
            }
        }
    }

    /// <summary>Wertet eine Antwort aus und hält an, wenn sie eine Abweisung ist.</summary>
    /// <param name="address">Die abgefragte Adresse.</param>
    /// <param name="statusCode">Der Statuscode der Antwort.</param>
    /// <param name="cancellationToken">Bricht das Speichern ab.</param>
    /// <exception cref="RequestsHaltedException">Wenn die Antwort eine Abweisung ist.</exception>
    public async Task ObserveResponseAsync(Uri address, int statusCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        string key = KeyFor(address.Host);
        lock (_gate)
        {
            _ = _unanswered.Remove(key);
        }

        if (IsRejection(address, statusCode))
        {
            await HaltAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Vermerkt eine ausgebliebene Antwort und hält beim wiederholten Mal an.</summary>
    /// <param name="host">Der Rechnername.</param>
    /// <param name="cancellationToken">Bricht Abfrage und Speichern ab.</param>
    /// <exception cref="RequestsHaltedException">Wenn der Host wiederholt nicht geantwortet hat.</exception>
    public async Task ObserveNoAnswerAsync(string host, CancellationToken cancellationToken)
    {
        // Ein Name ohne Adresse ist ein Tippfehler oder eine fehlende Subdomain. Da hat niemand
        // abgewiesen, und eine Stunde Sperre für einen Tippfehler wäre unsinnig.
        IReadOnlyList<string> addresses = await _dns.ResolveIpv4Async(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return;
        }

        string key = KeyFor(host);
        bool halt;
        lock (_gate)
        {
            int count = _unanswered.GetValueOrDefault(key) + 1;
            _unanswered[key] = count;
            halt = count >= UnansweredRequestsBeforeHalt;
        }

        if (halt)
        {
            await HaltAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Wirft, wenn der Host gerade gesperrt ist, ohne eine Anfrage zu vermerken.</summary>
    /// <param name="host">Der Rechnername.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    /// <exception cref="RequestsHaltedException">Wenn der Host gesperrt ist.</exception>
    public async Task ThrowIfBlockedAsync(string host, CancellationToken cancellationToken)
    {
        string key = KeyFor(host);
        await MergeStoredStateAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            ThrowIfBlocked(key, CurrentEntry(key, _clock.GetUtcNow()));
        }
    }

    /// <summary>Sperrt den Host nach einem Lasttest für die Abkühlzeit, auch für normale Prüfläufe.</summary>
    /// <param name="host">Der Rechnername.</param>
    /// <param name="cancellationToken">Bricht das Speichern ab.</param>
    public async Task StartCooldownAsync(string host, CancellationToken cancellationToken)
    {
        string key = KeyFor(host);
        lock (_gate)
        {
            DateTimeOffset now = _clock.GetUtcNow();
            HostGuardEntry entry = CurrentEntry(key, now);

            // Eine längere Sperre aus einer Abweisung bleibt bestehen.
            if (entry.BlockedUntil is null)
            {
                _hosts[key] = entry with { BlockedUntil = now + StressTestCooldown, BlockReason = RequestHaltReason.CoolingDown };
            }
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Liefert den Stand eines Hosts für die Oberfläche.</summary>
    /// <param name="host">Der Rechnername.</param>
    /// <param name="cancellationToken">Bricht den Zugriff ab.</param>
    /// <returns>Der aktuelle Stand.</returns>
    public async Task<HostGuardStatus> GetStatusAsync(string host, CancellationToken cancellationToken)
    {
        string key = KeyFor(host);
        await MergeStoredStateAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            HostGuardEntry entry = CurrentEntry(key, _clock.GetUtcNow());
            return new HostGuardStatus(entry.BlockedUntil, entry.BlockReason, entry.NoticeAcknowledged);
        }
    }

    /// <summary>Vermerkt, dass der Hinweis zur Aussperr-Gefahr für diesen Host bestätigt wurde.</summary>
    /// <param name="host">Der Rechnername.</param>
    /// <param name="cancellationToken">Bricht das Speichern ab.</param>
    public async Task AcknowledgeNoticeAsync(string host, CancellationToken cancellationToken)
    {
        string key = KeyFor(host);
        lock (_gate)
        {
            _hosts[key] = CurrentEntry(key, _clock.GetUtcNow()) with { NoticeAcknowledged = true };
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Hebt die Sperre nach ausdrücklicher Bestätigung auf. Das Stundenbudget bleibt.</summary>
    /// <param name="host">Der Rechnername.</param>
    /// <param name="cancellationToken">Bricht das Speichern ab.</param>
    public async Task ReleaseAsync(string host, CancellationToken cancellationToken)
    {
        string key = KeyFor(host);
        await MergeStoredStateAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _hosts[key] = CurrentEntry(key, _clock.GetUtcNow()) with { BlockedUntil = null, BlockReason = null };
            _ = _unanswered.Remove(key);
        }

        _logger.HaltReleased(key);

        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    // 403 ist auf heiklen Pfaden die erwünschte Antwort und bei Ordnern mit "Options -Indexes" normal.
    // Auf der Wurzel und unter /.well-known/ weist der Server dagegen den Anschluss selbst ab.
    private static bool IsRejection(Uri address, int statusCode) => statusCode switch
    {
        429 or 503 => true,
        403 => address.AbsolutePath == "/" || address.AbsolutePath.StartsWith("/.well-known/", StringComparison.Ordinal),
        _ => false,
    };

    // Die Abwehr sieht die Adresse des Anschlusses, nicht den Namen: www und mta-sts zählen zur Domain.
    private static string KeyFor(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        return IPAddress.TryParse(host, out _) ? host : DomainName.GetRegistrableDomain(host);
    }

    private static void ThrowIfBlocked(string key, HostGuardEntry entry)
    {
        if (entry.BlockedUntil is DateTimeOffset until)
        {
            throw new RequestsHaltedException(entry.BlockReason ?? RequestHaltReason.HostRejected, key, until);
        }
    }

    private async Task HaltAsync(string key, CancellationToken cancellationToken)
    {
        DateTimeOffset until;
        lock (_gate)
        {
            DateTimeOffset now = _clock.GetUtcNow();
            until = now + HaltCooldown;
            _hosts[key] = CurrentEntry(key, now) with { BlockedUntil = until, BlockReason = RequestHaltReason.HostRejected };
            _ = _unanswered.Remove(key);
        }

        _logger.RequestsHalted(key, until, RequestHaltReason.HostRejected, _clock);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        throw new RequestsHaltedException(RequestHaltReason.HostRejected, key, until);
    }

    // Abgelaufene Sperren und Anfragen außerhalb des Fensters fallen beim Lesen weg.
    private HostGuardEntry CurrentEntry(string key, DateTimeOffset now)
    {
        HostGuardEntry entry = _hosts.GetValueOrDefault(key) ?? HostGuardEntry.Empty;
        bool blockExpired = entry.BlockedUntil is DateTimeOffset until && until <= now;
        return entry with
        {
            RecentRequests = [.. entry.RecentRequests.Where(request => now - request < BudgetWindow)],
            BlockedUntil = blockExpired ? null : entry.BlockedUntil,
            BlockReason = blockExpired ? null : entry.BlockReason,
        };
    }

    // Vor jeder Anfrage neu lesen, weil Konsole und Oberfläche nebeneinander laufen können. Eine
    // Sperre aus der Datei gilt auch hier; aufgehoben wird sie nur, indem sie dort gelöscht wird.
    private async Task MergeStoredStateAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, HostGuardEntry> stored;
        try
        {
            stored = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ohne lesbare Datei zählt der Stand im Speicher weiter; die Bremse bleibt also aktiv.
            return;
        }

        lock (_gate)
        {
            foreach ((string key, HostGuardEntry fromStore) in stored)
            {
                HostGuardEntry known = _hosts.GetValueOrDefault(key) ?? HostGuardEntry.Empty;
                DateTimeOffset[] requests = [.. known.RecentRequests.Union(fromStore.RecentRequests).Order()];
                bool storeBlocksLonger = known.BlockedUntil is null || fromStore.BlockedUntil > known.BlockedUntil;
                _hosts[key] = new HostGuardEntry(
                    requests,
                    storeBlocksLonger ? fromStore.BlockedUntil : known.BlockedUntil,
                    storeBlocksLonger ? fromStore.BlockReason : known.BlockReason,
                    known.NoticeAcknowledged || fromStore.NoticeAcknowledged);

                if (requests.Length > 0 && requests[^1] + MinimumSpacing > _nextSlot)
                {
                    _nextSlot = requests[^1] + MinimumSpacing;
                }
            }
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, HostGuardEntry> snapshot;
        lock (_gate)
        {
            snapshot = new Dictionary<string, HostGuardEntry>(_hosts, StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await _store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Datei gesperrt oder Platte voll: Die Prüfung läuft weiter, gezählt wird im Speicher.
        }
    }
}
