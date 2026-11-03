namespace SiteCheck.App.Services;

/// <summary>
/// Liefert die Oberflächentexte zur Anzeigesprache. ViewModels und Dienste kennen nur Schlüssel,
/// damit sich Texte übersetzen lassen und Tests auf die Aussage prüfen statt auf einen Satzteil.
/// </summary>
public interface IStringResources
{
    /// <summary>Der Text zum Schlüssel.</summary>
    /// <param name="key">Der Schlüssel aus <c>Resources.resw</c>.</param>
    /// <returns>Der Text; bei unbekanntem Schlüssel <c>[?Schlüssel?]</c>, damit die Lücke sichtbar bleibt.</returns>
    string GetString(string key);

    /// <summary>Der Text zum Schlüssel mit eingesetzten Werten, formatiert nach der aktuellen Kultur.</summary>
    /// <param name="key">Der Schlüssel aus <c>Resources.resw</c>.</param>
    /// <param name="arguments">Die Werte für die Platzhalter.</param>
    /// <returns>Der fertige Text.</returns>
    string GetFormatted(string key, params object[] arguments);
}
