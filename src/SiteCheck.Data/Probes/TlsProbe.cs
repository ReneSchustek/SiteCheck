using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models.Probes;

namespace SiteCheck.Data.Probes;

/// <summary>
/// Führt einen TLS-Handschlag und liest Fassung und Zertifikatsdaten aus. Die Verbindung wird
/// nie wegen eines Zertifikatsfehlers abgebrochen: Gerade ein abgelaufenes oder nicht passendes
/// Zertifikat ist ein Befund, den die Prüfung sehen und melden soll.
/// </summary>
public sealed class TlsProbe : ITlsProbe
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    /// <inheritdoc/>
    public async Task<TlsHandshakeResult?> HandshakeAsync(string host, int port, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        using TcpClient tcp = new();
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectTimeout);
            await tcp.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Zeitüberschreitung beim Verbinden: kein TLS-Server auf diesem Port erreichbar.
            return null;
        }
        catch (SocketException)
        {
            return null;
        }

        return await AuthenticateAsync(tcp, host, cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage("Security", "CA5359:Do not disable certificate validation",
        Justification = "SiteCheck ist ein Diagnosewerkzeug: Gerade ein abgelaufenes, nicht passendes oder nicht vertrauenswürdiges Zertifikat ist ein Befund, der gemeldet werden soll. Die Rückgabe true lehnt nichts ab, sondern beobachtet nur; die Mängel werden in IsChainTrusted/IsNameMatch festgehalten und von TlsCheck als Befund ausgegeben. Es werden keine Daten über die Verbindung gesendet.")]
    private static async Task<TlsHandshakeResult?> AuthenticateAsync(TcpClient tcp, string host, CancellationToken cancellationToken)
    {
        X509Certificate2? serverCertificate = null;
        bool chainTrusted = false;
        bool nameMatch = false;

        using SslStream ssl = new(tcp.GetStream(), leaveInnerStreamOpen: false, (_, certificate, chain, errors) =>
        {
            if (certificate is not null)
            {
                serverCertificate = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
            }

            // Die Flags getrennt festhalten, statt die Verbindung abzulehnen: nur so kommen wir
            // überhaupt an die Fassung und die Zertifikatsdaten heran, um sie zu melden. Ein
            // reiner Ablauf (NotTimeValid) zählt nicht gegen die Kette — den Ablauf meldet die
            // Prüfung eigens und deutlicher.
            SslPolicyErrors ignoringExpiry = errors & ~SslPolicyErrors.RemoteCertificateChainErrors;
            nameMatch = (errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0;
            chainTrusted = IsChainTrustedIgnoringExpiry(chain) && ignoringExpiry == SslPolicyErrors.None;
            return true;
        });

        try
        {
            await ssl.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    EnabledSslProtocols = SslProtocols.None, // Das Betriebssystem wählt die beste Fassung.
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Abbruch mitten im Handschlag: Das Zertifikat läuft hier nicht durch BuildResult
            // (das es sonst freigibt), also hier freigeben, bevor die Ausnahme weitergeht.
            serverCertificate?.Dispose();
            throw;
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException or SocketException)
        {
            // Handschlag gescheitert (keine gemeinsame Fassung o. Ä.): als „unbekannt“ melden,
            // falls ein Zertifikat trotzdem schon kam, sonst gar nichts.
            return serverCertificate is null
                ? null
                : BuildResult(TlsVersion.Unknown, serverCertificate, chainTrusted, nameMatch);
        }

        return BuildResult(MapVersion(ssl.SslProtocol), serverCertificate, chainTrusted, nameMatch);
    }

    private static bool IsChainTrustedIgnoringExpiry(X509Chain? chain)
    {
        if (chain is null)
        {
            return false;
        }

        // Jeden Statuseintrag außer „abgelaufen“ als echten Mangel werten.
        return chain.ChainStatus.All(status =>
            status.Status is X509ChainStatusFlags.NoError or X509ChainStatusFlags.NotTimeValid);
    }

    private static TlsHandshakeResult BuildResult(TlsVersion version, X509Certificate2? certificate, bool chainTrusted, bool nameMatch)
    {
        if (certificate is null)
        {
            return new TlsHandshakeResult(version, null, null, chainTrusted, nameMatch);
        }

        using (certificate)
        {
            return new TlsHandshakeResult(
                version,
                new DateTimeOffset(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero),
                certificate.Issuer,
                chainTrusted,
                nameMatch);
        }
    }

    [SuppressMessage("Security", "CA5397:Do not use deprecated SslProtocols values",
        Justification = "Die veralteten Werte werden nicht gesetzt, sondern nur gelesen, um eine bereits ausgehandelte Fassung für den Befund zu benennen. Ausgehandelt wird mit SslProtocols.None (das Betriebssystem wählt).")]
    [SuppressMessage("Security", "CA5398:Avoid hardcoded SslProtocols values",
        Justification = "Dieselbe Begründung wie CA5397: Die Enum-Werte dienen nur dem Klassifizieren der ausgehandelten Fassung, nicht dem Festlegen.")]
    private static TlsVersion MapVersion(SslProtocols protocol) => protocol switch
    {
        SslProtocols.Tls13 => TlsVersion.Tls13,
        SslProtocols.Tls12 => TlsVersion.Tls12,
#pragma warning disable SYSLIB0039 // Veraltete Fassungen absichtlich benannt, um sie als Befund melden zu können.
        SslProtocols.Tls11 or SslProtocols.Tls => TlsVersion.Outdated,
#pragma warning restore SYSLIB0039
        _ => TlsVersion.Unknown,
    };
}
