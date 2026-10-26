namespace SiteCheck.App.ViewModels;

/// <summary>Trägt die Adresse, deren Prüfung aus der Seitenliste heraus angefordert wurde.</summary>
public sealed class CheckRequestedEventArgs(string address) : EventArgs
{
    /// <summary>Die zu prüfende Adresse.</summary>
    public string Address { get; } = address;
}
