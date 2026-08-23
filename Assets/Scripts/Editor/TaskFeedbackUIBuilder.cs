#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TaskFeedbackUIBuilder
{
    [MenuItem("Tools/Cyrulik/Create Task Feedback UI (Amber Flash)", false, 4)]
    [MenuItem("GameObject/UI/Cyrulik - Task Feedback UI", false, 12)]
    public static void CreateTaskFeedbackUI()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD Canvas");
        }

        // Główny obiekt TaskFeedbackUI
        GameObject managerGo = new GameObject("TaskFeedbackUI", typeof(TaskFeedbackUI));
        managerGo.transform.SetParent(canvas.transform, false);
        TaskFeedbackUI feedbackUI = managerGo.GetComponent<TaskFeedbackUI>();

        // 1. Amber Flash Overlay
        GameObject flashGo = new GameObject("AmberFlash_Overlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        flashGo.transform.SetParent(managerGo.transform, false);
        var flashRect = flashGo.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.sizeDelta = Vector2.zero;

        var flashCg = flashGo.GetComponent<CanvasGroup>();
        flashCg.alpha = 0f;
        flashCg.blocksRaycasts = false;

        var flashImg = flashGo.GetComponent<Image>();
        flashImg.color = new Color(1f, 0.72f, 0.22f, 0.45f);
        flashImg.raycastTarget = false;

        // 2. Banner Container
        GameObject bannerGo = new GameObject("TaskBanner_Container", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        bannerGo.transform.SetParent(managerGo.transform, false);
        var bannerRect = bannerGo.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.sizeDelta = new Vector2(580f, 76f);
        bannerRect.anchoredPosition = new Vector2(0f, 90f);

        var bannerCg = bannerGo.GetComponent<CanvasGroup>();
        bannerCg.alpha = 0f;
        bannerCg.blocksRaycasts = false;

        var bannerBg = bannerGo.GetComponent<Image>();
        bannerBg.color = new Color(0.06f, 0.06f, 0.07f, 0.94f);

        var outline = bannerGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.9f, 0.7f, 0.25f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Header Text
        GameObject headerGo = new GameObject("Header_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGo.transform.SetParent(bannerGo.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 0.5f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.offsetMin = new Vector2(16f, 0f);
        headerRect.offsetMax = new Vector2(-16f, -6f);

        var headerTmp = headerGo.GetComponent<TextMeshProUGUI>();
        headerTmp.text = "✓ TASK COMPLETED";
        headerTmp.fontSize = 15f;
        headerTmp.fontStyle = FontStyles.Bold;
        headerTmp.color = new Color(0.98f, 0.82f, 0.35f, 1f);
        headerTmp.alignment = TextAlignmentOptions.Center;

        // Task Name Text
        GameObject nameGo = new GameObject("TaskName_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(bannerGo.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.offsetMin = new Vector2(16f, 6f);
        nameRect.offsetMax = new Vector2(-16f, 0f);

        var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
        nameTmp.text = "Set the right mood";
        nameTmp.fontSize = 20f;
        nameTmp.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        nameTmp.alignment = TextAlignmentOptions.Center;

        // Serializacja pól w TaskFeedbackUI
        SerializedObject so = new SerializedObject(feedbackUI);
        so.FindProperty("flashCanvasGroup").objectReferenceValue = flashCg;
        so.FindProperty("flashImage").objectReferenceValue = flashImg;
        so.FindProperty("bannerCanvasGroup").objectReferenceValue = bannerCg;
        so.FindProperty("bannerRectTransform").objectReferenceValue = bannerRect;
        so.FindProperty("headerText").objectReferenceValue = headerTmp;
        so.FindProperty("taskNameText").objectReferenceValue = nameTmp;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(managerGo, "Create Task Feedback UI");
        Selection.activeGameObject = managerGo;

        Debug.Log("[TaskFeedbackUIBuilder] Utworzono TaskFeedbackUI (Bursztynowy Rozbłysk & Retro Baner)!");
    }
}
#endif
