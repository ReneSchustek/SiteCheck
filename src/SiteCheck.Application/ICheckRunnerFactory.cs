namespace SiteCheck.Application;

/// <summary>Erzeugt je Prüflauf einen frischen <see cref="CheckRunner"/>. Über die Schnittstelle bleibt die Oberfläche testbar.</summary>
public interface ICheckRunnerFactory
{
    /// <summary>Erzeugt einen Prüflauf mit frischem Antwort-Cache.</summary>
    /// <returns>Der einsatzbereite Runner für genau einen Lauf.</returns>
    CheckRunner Create();
}
