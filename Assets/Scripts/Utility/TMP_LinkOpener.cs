using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Umożliwia klikanie w linki wewnątrz TextMeshPro (znaczniki &lt;link="URL"&gt;tekst&lt;/link&gt;).
/// Po kliknięciu w link otwiera domyślną przeglądarkę z podanym adresem URL.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TMP_LinkOpener : MonoBehaviour, IPointerClickHandler
{
    private TextMeshProUGUI _textMeshPro;
    private Canvas _parentCanvas;
    private Camera _uiCamera;

    private void Awake()
    {
        _textMeshPro = GetComponent<TextMeshProUGUI>();
        _parentCanvas = GetComponentInParent<Canvas>();

        if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            _uiCamera = _parentCanvas.worldCamera;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_textMeshPro == null) return;

        Camera cam = null;
        if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = _parentCanvas.worldCamera != null ? _parentCanvas.worldCamera : Camera.main;
        }

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_textMeshPro, eventData.position, cam);

        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = _textMeshPro.textInfo.linkInfo[linkIndex];
            string url = linkInfo.GetLinkID();

            if (!string.IsNullOrEmpty(url))
            {
                Debug.Log($"[TMP_LinkOpener] Otwieram link: {url}");
                Application.OpenURL(url);
            }
        }
    }
}
