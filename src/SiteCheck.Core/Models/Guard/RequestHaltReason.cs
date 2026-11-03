namespace SiteCheck.Core.Models.Guard;

/// <summary>Warum gerade keine Anfrage an einen Host hinausgeht.</summary>
public enum RequestHaltReason
{
    /// <summary>Der Server hat abgewiesen oder mehrfach nicht geantwortet.</summary>
    HostRejected = 0,

    /// <summary>Das Budget je Stunde für diesen Host ist aufgebraucht.</summary>
    BudgetUsed = 1,

    /// <summary>Abkühlzeit nach einem Lasttest.</summary>
    CoolingDown = 2,
}
