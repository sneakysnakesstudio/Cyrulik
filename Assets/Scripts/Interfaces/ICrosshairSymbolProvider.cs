/// <summary>
/// Typ symbolu celownika wyświetlanego po najechaniu na interaktywny obiekt.
/// </summary>
public enum ReticleSymbolType
{
    Auto,            // Automatyczny dobór na podstawie typu i nazwy (? badanie, ! akcja, ... dialog/nasłuch)
    QuestionMark,    // ? (badanie, oglądanie, myśli, tajemnice, krzyż)
    ExclamationMark, // ! (akcje bezpośrednie, zadania, narzędzia, szafa, drzwi, zlew)
    Ellipsis,        // ... (dialog, radio, mowa, nasłuch, czekanie)
    Dot              // Zwykła kropka
}

/// <summary>
/// Opcjonalny interfejs dla obiektów interaktywnych, które chcą explicite
/// zdefiniować wyświetlany symbol celownika (?, ! lub ...).
/// </summary>
public interface ICrosshairSymbolProvider
{
    ReticleSymbolType CrosshairSymbol { get; }
}
