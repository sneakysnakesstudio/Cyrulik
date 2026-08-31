using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Interaktywny Krzyż / Jezus — pierwszy obiekt interaktywny w grze.
/// Reaguje na kliknięcie PRAWYM PRZYCISKIEM MYSZY [PPM / RMB] (lub klawisz [E]):
/// 1. Płynne, wyraźne zbliżenie kamery na krzyż (Cinematic Focus Zoom)
/// 2. Ciepła, złoto-biała anielska poświata na bokach ekranu (Divine Glow)
/// 3. Subtelny mistyczny shimmer celownika i medytacyjny spokój
/// 4. Anielski, wzniosły dźwięk akordu organowo-chóralnego
/// 5. Wyświetlenie napisu / myśli: "LORD HAVE MERCY"
/// </summary>
public class CrucifixInteractable : MonoBehaviour, IInteractable, ILookAtHandler
{
    [Header("Interakcja")]
    [Tooltip("Napis wyświetlany na celowniku gracza.")]
    [SerializeField] private string interactionName = "[PPM] Spójrz na Krzyż";

    [Header("Wyzwalanie")]
    [Tooltip("Czy akcja ma się wyzwalać po kliknięciu Prawego Przycisku Myszy [PPM / RMB]?")]
    [SerializeField] private bool triggerOnRightClick = true;

    [Tooltip("Czy akcja ma się wyzwalać również po wciśnięciu klawisza interakcji [E / LPM]?")]
    [SerializeField] private bool triggerOnInteract = true;

    [Tooltip("Czy sekwencja ma się odpalać automatycznie przy samym najechaniu wzrokiem?")]
    [SerializeField] private bool triggerOnLookAt = false;

    [Tooltip("Minimalny odstęp czasowy (w sekundach) między kolejnymi modlitwami.")]
    [SerializeField] private float cooldown = 3.0f;

    [Header("Kinowe Zbliżenie Kamery (Focus / Zoom)")]
    [SerializeField] private bool enableCameraFocus = true;
    [Range(20f, 60f)]
    [SerializeField] private float focusZoomFov = 35f;
    [SerializeField] private float focusHoldDuration = 2.2f;

    [Header("Anielska Poświata (Divine Grace)")]
    [SerializeField] private bool enableDivineGlow = true;
    [SerializeField] private Color divineGlowColor = new Color(1.0f, 0.94f, 0.72f, 0.88f); // Złoto-biały anielski blask
    [Range(0.2f, 2.0f)]
    [SerializeField] private float glowIntensity = 1.0f;

    [Header("Dźwięk (SFX)")]
    [Tooltip("Nazwa playlisty/grupy audio w AudioManager (np. 'Croos_audio_sfx').")]
    [SerializeField] private string audioGroupName = "Croos_audio_sfx";
    [Tooltip("Opcjonalny bezpośredni AudioClip. Jeśli pusty, odtwarza playlistę z AudioManager lub proceduralny akord.")]
    [SerializeField] private AudioClip customHeavenlyClip;

    [Header("Napis / Modlitwa")]
    [SerializeField] private string prayerText = "LORD HAVE MERCY";

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onCinematicTriggered;

    private float _lastTriggerTime = -999f;
    private bool _isCurrentlyTargeted = false;

    public string InteractionName => interactionName;
    public string AudioGroupName { get => audioGroupName; set => audioGroupName = value; }
    public string PrayerText { get => prayerText; set => prayerText = value; }

    public void OnLookAt()
    {
        _isCurrentlyTargeted = true;

        if (triggerOnLookAt)
        {
            TryTriggerCinematic();
        }
    }

    public void Interact()
    {
        if (triggerOnInteract)
        {
            TryTriggerCinematic();
        }
    }

    private void Update()
    {
        // Sprawdź czy gracz celuje w krzyż
        bool isFocused = _isCurrentlyTargeted ||
            (PlayerMovement.Instance != null && (object)PlayerMovement.Instance.CurrentInteractable == this);

        if (isFocused && triggerOnRightClick)
        {
            bool rightClickPressed = false;

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                rightClickPressed = true;
            }
            else if (Input.GetMouseButtonDown(1))
            {
                rightClickPressed = true;
            }

            if (rightClickPressed)
            {
                TryTriggerCinematic();
            }
        }

        // Reset flagi celowania dla kolejnej klatki
        _isCurrentlyTargeted = false;
    }

    public void TryTriggerCinematic()
    {
        if (Time.time - _lastTriggerTime < cooldown) return;
        _lastTriggerTime = Time.time;

        TriggerCinematic();
    }

    /// <summary>
    /// Główna metoda odpalająca anielską sekwencję zbliżenia na Krzyż.
    /// </summary>
    public void TriggerCinematic()
    {
        // 1. Zbliżenie kamery (Focus zoom)
        if (enableCameraFocus && CinematicEffectsManager.Instance != null)
        {
            CinematicEffectsManager.Instance.FocusCameraOn(transform, focusHoldDuration, focusZoomFov);
        }

        // 2. Anielska poświata na bokach ekranu i dźwięk z playlisty "Croos_audio_sfx"
        if (enableDivineGlow && CinematicEffectsManager.Instance != null)
        {
            CinematicEffectsManager.Instance.TriggerDivineGrace(
                duration: focusHoldDuration + 0.8f,
                intensity: glowIntensity,
                glowColor: divineGlowColor,
                customClip: customHeavenlyClip,
                audioGroupName: audioGroupName
            );
        }

        // 3. Wyświetlenie napisu / myśli LORD HAVE MERCY
        if (!string.IsNullOrEmpty(prayerText) && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowThought(prayerText);
        }

        // 4. Efekt cząsteczkowy błysku
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayBurst(transform.position);
        }

        onCinematicTriggered?.Invoke();
    }
}
