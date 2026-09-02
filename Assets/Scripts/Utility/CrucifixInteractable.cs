using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Interaktywny Krzyż / Jezus — pierwszy obiekt interaktywny w grze.
/// Reaguje na kliknięcie PRAWYM PRZYCISKIEM MYSZY [PPM / RMB] (lub klawisz [E]):
/// 1. Płynny fizyczny najazd/zoom kamery z gracza na rzecz (Krzyż) i powrót do gracza
/// 2. Ciepła, złoto-biała anielska poświata na bokach ekranu (Divine Glow)
/// 3. Opcjonalny psychodeliczny shake kropki i kołysanie kamery z pełną kontrolą siły
/// 4. Odtworzenie playlisty "Croos_audio_sfx" z AudioManager
/// 5. Wyświetlenie napisu / myśli: "LORD HAVE MERCY" z konfigurowalnym opóźnieniem w ujęciu
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

    [Tooltip("Minimalny odstęp czasowy (w sekundach) liczony PO zakończeniu sekwencji najazdu kamery.")]
    [SerializeField] private float cooldown = 3.0f;

    [Header("Kinowy Najazd Kamery (Zoom z gracza na rzecz i powrót)")]
    [Tooltip("Parametry fizycznego przelotu kamery z pozycji gracza pod krzyż i powrotu.")]
    [SerializeField] private CinematicDollySettings dollySettings = new CinematicDollySettings
    {
        usePhysicalPush = true,
        targetDistance = 0.42f,
        targetOffset = new Vector3(0f, -0.05f, 0f),
        targetFov = 35f,
        approachDuration = 0.85f,
        holdDuration = 2.2f,
        returnDuration = 0.75f,
        approachEase = Ease.OutCubic,
        returnEase = Ease.InOutQuad,
        lockPlayerMovement = true
    };

    [Header("Anielska Poświata (Divine Grace)")]
    [SerializeField] private bool enableDivineGlow = true;
    [SerializeField] private Color divineGlowColor = new Color(1.0f, 0.94f, 0.72f, 0.88f);
    [Range(0.2f, 2.0f)]
    [SerializeField] private float glowIntensity = 1.0f;

    [Header("Trzęsienie (Shake) - Kropka & Kamera")]
    [Tooltip("Czy włączyć drżenie celownika (kropki) podczas ujęcia.")]
    [SerializeField] private bool enableCrosshairShake = true;
    [Range(0f, 20f)]
    [Tooltip("Siła drżenia kropki w pikselach. 0 = brak, 4 = lekki psychodeliczny shimmer, 12 = mocny wstrząs.")]
    [SerializeField] private float crosshairShakeStrength = 4.5f;

    [Tooltip("Czy włączyć kołysanie / pływanie kamery podczas ujęcia.")]
    [SerializeField] private bool enableCameraShake = true;
    [Range(0f, 2f)]
    [Tooltip("Siła kołysania kamery. 0 = stabilna kamera, 0.35 = delikatny trans, 1.0+ = mocne bujanie.")]
    [SerializeField] private float cameraShakeIntensity = 0.35f;

    [Tooltip("Opóźnienie rozpoczęcia drżenia od momentu dotarcia kamery przed krzyż (w sekundach).")]
    [SerializeField] private float shakeDelay = 0.0f;

    [Header("Dźwięk (SFX)")]
    [Tooltip("Nazwa playlisty/grupy audio w AudioManager (np. 'Croos_audio_sfx').")]
    [SerializeField] private string audioGroupName = "Croos_audio_sfx";
    [Tooltip("Opcjonalny bezpośredni AudioClip. Jeśli pusty, odtwarza playlistę z AudioManager lub proceduralny akord.")]
    [SerializeField] private AudioClip customHeavenlyClip;

    [Header("Napis / Modlitwa (LORD HAVE MERCY)")]
    [SerializeField] private string prayerText = "LORD HAVE MERCY";

    [Tooltip("Opóźnienie pojawienia się okienka z napisem po dotarciu kamery przed krzyż (w sekundach). 0 = natychmiast po dojechaniu.")]
    [SerializeField] private float textAppearanceDelay = 0.4f;

    [Header("Zasięg i Widoczność")]
    [Tooltip("Maksymalna odległość (w metrach), z jakiej gracz może spojrzeć na krzyż.")]
    [SerializeField] private float maxInteractionDistance = 3.0f;
    [Tooltip("Czy wymagać czystej linii wzroku (brak ścian/drzwi między graczem a krzyżem)?")]
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onCinematicTriggered;

    private float _cooldownUntilTime = -999f;
    private bool _isCurrentlyTargeted = false;

    public bool IsOnCooldown => Time.time < _cooldownUntilTime;
    public string InteractionName => interactionName;
    public string AudioGroupName { get => audioGroupName; set => audioGroupName = value; }
    public string PrayerText { get => prayerText; set => prayerText = value; }
    public float TextAppearanceDelay { get => textAppearanceDelay; set => textAppearanceDelay = value; }
    public CinematicDollySettings DollySettings => dollySettings;

    public void OnLookAt()
    {
        if (IsInRangeAndVisible())
        {
            _isCurrentlyTargeted = true;

            if (triggerOnLookAt)
            {
                TryTriggerCinematic();
            }
        }
    }

    public void Interact()
    {
        if (triggerOnInteract && IsInRangeAndVisible())
        {
            TryTriggerCinematic();
        }
    }

    private void Update()
    {
        // Sprawdź czy gracz faktycznie stoi blisko, patrzy w krzyż i nie ma ściany pomiędzy
        bool isFocused = IsInRangeAndVisible() && (_isCurrentlyTargeted ||
            (PlayerMovement.Instance != null && (object)PlayerMovement.Instance.CurrentInteractable == this));

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

    /// <summary>
    /// Sprawdza czy gracz znajduje się w dozwolonym zasięgu (np. max 3m), patrzy w stronę krzyża i nie ma ścian pomiędzy.
    /// </summary>
    private bool IsInRangeAndVisible()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        // 1. Sprawdź fizyczną odległość gracza od krzyża
        float distance = Vector3.Distance(cam.transform.position, transform.position);
        if (distance > maxInteractionDistance)
        {
            return false;
        }

        // 2. Sprawdź kąt patrzenia (gracz musi patrzeć w stronę krzyża, a nie w tył)
        Vector3 dirToCross = (transform.position - cam.transform.position).normalized;
        float dot = Vector3.Dot(cam.transform.forward, dirToCross);
        if (dot < 0.65f) // min. ~50 stopni w stożku wzroku
        {
            return false;
        }

        // 3. Sprawdź czy między kamerą a krzyżem nie ma ściany (Line of Sight)
        if (requireLineOfSight)
        {
            Vector3 startPos = cam.transform.position;
            Vector3 targetPos = transform.position;
            Vector3 rayDir = targetPos - startPos;
            float rayDist = rayDir.magnitude;

            if (Physics.Raycast(startPos, rayDir.normalized, out RaycastHit hit, rayDist + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Jeśli trafiliśmy w coś, co nie jest nami ani naszym dzieckiem/rodzicem
                if (hit.transform != transform && !hit.transform.IsChildOf(transform) && hit.transform != transform.parent)
                {
                    // Ściana lub mebel zasłania widok!
                    return false;
                }
            }
        }

        return true;
    }

    public void TryTriggerCinematic()
    {
        if (IsOnCooldown) return;
        if (CinematicEffectsManager.Instance != null && CinematicEffectsManager.Instance.IsDollyActive) return;

        // Blokujemy wyzwalanie na czas trwania animacji + cooldown
        float totalSeqDuration = dollySettings.approachDuration + dollySettings.holdDuration + dollySettings.returnDuration;
        _cooldownUntilTime = Time.time + totalSeqDuration + cooldown;

        TriggerCinematic();
    }

    /// <summary>
    /// Główna metoda odpalająca anielską sekwencję zbliżenia na Krzyż:
    /// Najazd z gracza -> Pod krzyżem odpalenie poświaty, audio i napisu (z opóźnieniem) -> Płynny powrót do gracza.
    /// </summary>
    public void TriggerCinematic()
    {
        if (CinematicEffectsManager.Instance == null) return;

        CinematicEffectsManager.Instance.PlayDollyZoom(
            target: transform,
            settings: dollySettings,
            onHoldStart: () =>
            {
                // 1. Anielska poświata na bokach ekranu i dźwięk z playlisty "Croos_audio_sfx"
                if (enableDivineGlow)
                {
                    CinematicEffectsManager.Instance.TriggerEdgeFlash(
                        divineGlowColor,
                        dollySettings.holdDuration + 0.5f,
                        0.85f * glowIntensity,
                        1
                    );

                    CinematicEffectsManager.Instance.PlayHeavenlyAudio(
                        customHeavenlyClip,
                        audioGroupName,
                        0.95f * glowIntensity
                    );
                }

                // 2. Obsługa drżenia kropki i kamery (z opcjonalnym opóźnieniem shakeDelay)
                if (shakeDelay > 0.01f)
                {
                    DOVirtual.DelayedCall(shakeDelay, ApplyShakes);
                }
                else
                {
                    ApplyShakes();
                }

                // 3. Wyświetlenie napisu z konfigurowalnym opóźnieniem w ujęciu
                if (!string.IsNullOrEmpty(prayerText) && DialogueManager.Instance != null)
                {
                    if (textAppearanceDelay > 0.01f)
                    {
                        DOVirtual.DelayedCall(textAppearanceDelay, () =>
                        {
                            if (DialogueManager.Instance != null)
                            {
                                DialogueManager.Instance.ShowThought(prayerText);
                            }
                        });
                    }
                    else
                    {
                        DialogueManager.Instance.ShowThought(prayerText);
                    }
                }

                // 4. Efekt cząsteczkowy błysku
                if (ParticleManager.Instance != null)
                {
                    ParticleManager.Instance.PlayBurst(transform.position);
                }

                onCinematicTriggered?.Invoke();
            },
            onComplete: () =>
            {
                // Liczymy pełny cooldown (min. 3 sekundy) od momentu gdy kamera wróciła do gracza
                _cooldownUntilTime = Time.time + cooldown;
            }
        );
    }

    private void ApplyShakes()
    {
        if (enableCameraShake && HeadBobbing.Instance != null && cameraShakeIntensity > 0f)
        {
            HeadBobbing.Instance.TriggerConcussion(dollySettings.holdDuration, cameraShakeIntensity);
        }

        if (enableCrosshairShake && Crosshair.Instance != null && crosshairShakeStrength > 0f)
        {
            Crosshair.Instance.PlayConcussionShake(dollySettings.holdDuration * 0.5f, crosshairShakeStrength, 14);
        }
    }
}
