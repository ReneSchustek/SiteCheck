namespace SiteCheck.Core.Models;

/// <summary>Bereich, zu dem eine Prüfung gehört. Die Oberfläche und der Bericht gruppieren danach.</summary>
public enum CheckCategory
{
    /// <summary>Alles, was die Webseite selbst ausliefert: Umleitung, Verschlüsselung, Kopfzeilen, Dateien.</summary>
    Web = 0,

    /// <summary>Die Mail-Einrichtung der Domain im DNS.</summary>
    Mail = 1,
}
