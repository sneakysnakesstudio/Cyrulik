/// <summary>
/// Typ symbolu celownika wyświetlanego po najechaniu na interaktywny obiekt.
/// Rozszerzony o pełen zestaw ikon kontekstowych odpowiadający sprite'om w Art/UI_HoldIcons/.
/// </summary>
public enum ReticleSymbolType
{
    // ── TRYB AUTOMATYCZNY ────────────────────────────────────────────────────
    Auto,               // Crosshair.cs sam wykryje odpowiedni symbol na podstawie typu/nazwy

    // ── STANDARDOWE ──────────────────────────────────────────────────────────
    Dot,                // •  Zwykła kropka (brak interakcji / neutral)
    QuestionMark,       // ?  Badanie, inspect, myśli, tajemnice
    ExclamationMark,    // !  Akcje bezpośrednie, zadania, manipulacja otoczeniem

    // ── KONTEKSTOWE IKONY ─────────────────────────────────────────────────────
    Ellipsis,           // ... Dialog, radio, rozmowa, nasłuch, czekanie
    Hand,               // ✋  Ogólna interakcja (włączniki, klamki, przedmioty do użycia)
    PickupHand,         // 🤲  Podnoszenie przedmiotu z podłogi / blatu
    Eye,                // 👁  Tylko oglądanie z bliska (inspect bez podnoszenia)
    Razor,              // 🪒  Golenie/ostrzenie (minigra brzytwy, strop)
    SpeechBubble,       // 💬  Rozmowa z NPC (Jurek i inni klienci)
    Lock,               // 🔒  Zablokowane / wymaga klucza lub warunku
    Key,                // 🗝   Użyj klucza
    Magnifier,          // 🔍  Szczegółowa inspekcja, zbliżenie, lupa
}

/// <summary>
/// Opcjonalny interfejs dla obiektów interaktywnych, które chcą explicite
/// zdefiniować wyświetlany symbol celownika zamiast auto-detekcji w Crosshair.
/// </summary>
public interface ICrosshairSymbolProvider
{
    ReticleSymbolType CrosshairSymbol { get; }
}
