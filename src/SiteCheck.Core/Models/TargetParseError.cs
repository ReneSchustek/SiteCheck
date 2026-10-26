namespace SiteCheck.Core.Models;

/// <summary>Warum eine Eingabe nicht als Prüfziel taugt. Die Oberfläche macht daraus einen freundlichen Satz.</summary>
public enum TargetParseError
{
    /// <summary>Die Eingabe ist gültig.</summary>
    None = 0,

    /// <summary>Es wurde nichts eingegeben.</summary>
    Empty = 1,

    /// <summary>Die Eingabe ist keine Web-Adresse mit Rechnernamen.</summary>
    InvalidAddress = 2,

    /// <summary>Ein anderes Schema als http oder https, etwa <c>ftp://</c>.</summary>
    UnsupportedScheme = 3,

    /// <summary>Die Adresse enthält Zugangsdaten (<c>name:passwort@</c>); die gehören in keine Prüfung und in keinen Bericht.</summary>
    ContainsCredentials = 4,
}
