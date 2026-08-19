using System.Collections;
using UnityEngine;

public class IntroSequence : MonoBehaviour
{
    [Header("Gracz (Do zablokowania)")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHands playerHands;

    [Header("UI Intro")]
    [Tooltip("CanvasGroup z przypisanym czarnym tłem oraz tekstem czasu.")]
    [SerializeField] private CanvasGroup introCanvasGroup;
    
    [Tooltip("Panel zegara/czasu, który po intrze chcesz wyłączyć, żeby nie zaśmiecał ekranu podczas gry.")]
    [SerializeField] private GameObject clockUIToHide;

    [Header("Timings")]
    [Tooltip("Ile sekund gracz ma patrzeć na czarny ekran z uciekającym czasem, zanim gra się rozjaśni.")]
    [SerializeField] private float waitOnBlackScreen = 5f;
    
    [Tooltip("Jak długo trwa przejście (fade) z czarnego ekranu do widoku z oczu gracza.")]
    [SerializeField] private float fadeDuration = 2.5f;

    private void Start()
    {
        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        // 1. Zablokuj gracza na start (żeby nie mógł chodzić ani wchodzić w interakcje w tle)
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerHands != null) playerHands.enabled = false;

        // Czarny ekran jest 100% nieprzezroczysty
        if (introCanvasGroup != null)
        {
            introCanvasGroup.alpha = 1f;
            introCanvasGroup.blocksRaycasts = true;
        }

        // Zegar może być włączony (GameTimeController sam tyka w tle i aktualizuje tekst)
        if (clockUIToHide != null)
            clockUIToHide.SetActive(true);

        // 2. Patrzymy na czarny ekran z tykającym czasem
        yield return new WaitForSeconds(waitOnBlackScreen);

        // 3. Rozjaśnianie ekranu (fade do zera)
        if (introCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                introCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            introCanvasGroup.blocksRaycasts = false;
        }

        // 4. Ukrywamy zegar
        if (clockUIToHide != null)
        {
            clockUIToHide.SetActive(false);
        }

        // 5. Oddajemy kontrolę graczowi
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerHands != null) playerHands.enabled = true;
    }
}
