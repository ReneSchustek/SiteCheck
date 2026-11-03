namespace SiteCheck.Application;

/// <summary>Was ein Prüflauf umfasst.</summary>
/// <param name="IncludeSensitivePaths">
/// Ob auch Pfade wie <c>.git</c>, <c>.env</c> und <c>/backup/</c> abgefragt werden. Für eine Abwehr
/// sieht das wie ein Scanner aus; deshalb nur nach ausdrücklicher Bestätigung je Lauf.
/// </param>
public sealed record CheckRunOptions(bool IncludeSensitivePaths)
{
    /// <summary>Der Standardlauf ohne verdächtige Pfade.</summary>
    public static CheckRunOptions Standard { get; } = new(IncludeSensitivePaths: false);

    /// <summary>Der volle Lauf mit verdächtigen Pfaden.</summary>
    public static CheckRunOptions WithSensitivePaths { get; } = new(IncludeSensitivePaths: true);
}
