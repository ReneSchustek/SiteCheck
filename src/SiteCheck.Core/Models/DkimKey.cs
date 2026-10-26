using System.Security.Cryptography;

namespace SiteCheck.Core.Models;

/// <summary>Liest aus einem DKIM-TXT-Eintrag die Länge des veröffentlichten RSA-Schlüssels.</summary>
public static class DkimKey
{
    /// <summary>
    /// Bestimmt die RSA-Schlüssellänge in Bit aus dem <c>p=</c>-Feld eines DKIM-Eintrags.
    /// </summary>
    /// <param name="dkimRecord">Der vollständige DKIM-TXT-Wert.</param>
    /// <returns>
    /// Die Schlüssellänge in Bit, oder <see langword="null"/>, wenn kein lesbarer RSA-Schlüssel
    /// enthalten ist (leeres <c>p=</c> = widerrufen, Ed25519, oder ungültige Kodierung).
    /// </returns>
    public static int? RsaKeySizeBits(string dkimRecord)
    {
        ArgumentNullException.ThrowIfNull(dkimRecord);

        string? publicKey = ExtractPublicKey(dkimRecord);
        if (string.IsNullOrEmpty(publicKey))
        {
            return null;
        }

        try
        {
            byte[] der = Convert.FromBase64String(publicKey);
            using RSA rsa = RSA.Create();
            // DKIM veröffentlicht den Schlüssel als SubjectPublicKeyInfo (DER, base64).
            rsa.ImportSubjectPublicKeyInfo(der, out _);
            return rsa.KeySize;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // Kein lesbarer RSA-Schlüssel: Der Aufrufer meldet das sachlich, ohne Fehler.
            return null;
        }
    }

    private static string? ExtractPublicKey(string dkimRecord)
    {
        // Das p=-Feld bis zum nächsten Semikolon; Leerzeichen innerhalb der base64 (durch
        // zerteilte TXT-Strings) werden entfernt.
        foreach (string part in dkimRecord.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("p=", StringComparison.OrdinalIgnoreCase))
            {
                return part[2..].Replace(" ", string.Empty, StringComparison.Ordinal);
            }
        }

        return null;
    }
}
