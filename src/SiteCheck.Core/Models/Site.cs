namespace SiteCheck.Core.Models;

/// <summary>
/// Eine eigene Seite, die man regelmäßig prüft. Die Adresse ist zugleich die Kennung:
/// Dieselbe Seite zweimal in der Liste ergäbe zwei Verläufe, die einander widersprechen.
/// </summary>
/// <param name="Name">Frei gewählter Anzeigename.</param>
/// <param name="Address">Vereinheitlichte Adresse, wie <see cref="CheckTarget.ToString"/> sie liefert.</param>
public sealed record Site(string Name, string Address);
