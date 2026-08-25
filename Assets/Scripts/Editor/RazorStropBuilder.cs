#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Narzędzie edytora Unity do automatycznej konfiguracji, stylizacji i budowania
/// klimatycznego interfejsu (UI) minigry ostrzenia brzytwy w stylu retro/PSX/Cyrulik.
/// </summary>
public static class RazorStropBuilder
{
    [MenuItem("Tools/Cyrulik/1. Create or Find ParticleManager in Scene", false, 1)]
    public static void CreateParticleManager()
    {
        ParticleManager existing = Object.FindAnyObjectByType<ParticleManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("[Cyrulik] ParticleManager już istnieje w scenie.");
            return;
        }

        GameObject pmGo = new GameObject("ParticleManager", typeof(ParticleManager));
        Undo.RegisterCreatedObjectUndo(pmGo, "Create ParticleManager");
        Selection.activeGameObject = pmGo;
        EditorGUIUtility.PingObject(pmGo);
        Debug.Log("[Cyrulik] Utworzono ParticleManager w scenie!");
    }

    [MenuItem("Tools/Cyrulik/2. Build PSX Razor Hanging Strop (Pas do brzytwy)", false, 2)]
    public static void BuildRazorStrop()
    {
        // 1. Sprawdzamy czy w scenie jest już RazorMinigame_Object lub zaznaczony obiekt
        GameObject targetParent = Selection.activeGameObject;
        Vector3 spawnPos = new Vector3(0f, 1.2f, 4.05f);
        Quaternion spawnRot = Quaternion.identity;

        if (targetParent != null)
        {
            spawnPos = targetParent.transform.position;
            spawnRot = targetParent.transform.rotation;
        }
        else
        {
            GameObject existingObj = GameObject.Find("RazorMinigame_Object");
            if (existingObj != null)
            {
                spawnPos = existingObj.transform.position;
                spawnRot = existingObj.transform.rotation;
            }
        }

        // 2. Główny Root Wiszącego Pasa
        GameObject stropRoot = new GameObject("RazorStrop_Hanging");
        stropRoot.transform.position = spawnPos;
        stropRoot.transform.rotation = spawnRot;
        Undo.RegisterCreatedObjectUndo(stropRoot, "Create Razor Strop Hanging");

        // 3. Montaż ścienny / Haczyk (Hook)
        GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hook.name = "Wall_Hook";
        hook.transform.SetParent(stropRoot.transform, false);
        hook.transform.localPosition = new Vector3(0f, 0.45f, 0.03f);
        hook.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hook.transform.localScale = new Vector3(0.025f, 0.03f, 0.025f);
        Object.DestroyImmediate(hook.GetComponent<Collider>());

        // 4. Punkt Obrotu Pasa (Pivot na samej górze)
        GameObject pivotGo = new GameObject("Strap_Pivot");
        pivotGo.transform.SetParent(stropRoot.transform, false);
        pivotGo.transform.localPosition = new Vector3(0f, 0.45f, 0.04f);

        // 5. Skórzany Pasek (Główna bryła)
        GameObject leatherStrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leatherStrap.name = "Leather_Strap_Mesh";
        leatherStrap.transform.SetParent(pivotGo.transform, false);
        leatherStrap.transform.localPosition = new Vector3(0f, -0.42f, 0f);
        leatherStrap.transform.localScale = new Vector3(0.09f, 0.85f, 0.012f);
        Object.DestroyImmediate(leatherStrap.GetComponent<Collider>());

        // 6. Dolny pierścień / rączka (D-Ring / Handle)
        GameObject bottomRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottomRing.name = "Bottom_Handle_Ring";
        bottomRing.transform.SetParent(pivotGo.transform, false);
        bottomRing.transform.localPosition = new Vector3(0f, -0.86f, 0f);
        bottomRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bottomRing.transform.localScale = new Vector3(0.045f, 0.008f, 0.045f);
        Object.DestroyImmediate(bottomRing.GetComponent<Collider>());

        // 7. Przypisanie Tekstur / Materiałów
        Material leatherMat = FindOrCreateLeatherMaterial();
        if (leatherMat != null)
        {
            leatherStrap.GetComponent<MeshRenderer>().sharedMaterial = leatherMat;
        }

        Material brassMat = FindOrCreateBrassMaterial();
        if (brassMat != null)
        {
            hook.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
            bottomRing.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
        }

        // 8. BoxCollider na całym obiekcie (dla łatwej interakcji)
        BoxCollider boxCol = stropRoot.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0f, 0f, 0.04f);
        boxCol.size = new Vector3(0.18f, 0.95f, 0.12f);

        // 9. Komponenty fizyki i cząsteczek
        HangingStrapSway sway = stropRoot.AddComponent<HangingStrapSway>();
        var pivotField = typeof(HangingStrapSway).GetField("pivotTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (pivotField != null) pivotField.SetValue(sway, pivotGo.transform);

        InteractiveParticleHint hint = stropRoot.AddComponent<InteractiveParticleHint>();
        var offsetField = typeof(InteractiveParticleHint).GetField("localOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (offsetField != null) offsetField.SetValue(hint, new Vector3(0f, 0f, 0.05f));

        Selection.activeGameObject = stropRoot;
        EditorGUIUtility.PingObject(stropRoot);

        Debug.Log("[Cyrulik] Gotowy wiszący pas (Razor Strop) został wygenerowany w scenie!");
    }

    [MenuItem("Tools/Cyrulik/3. Build & Enhance Razor Minigame UI (From Scratch)", false, 3)]
    public static void EnhanceRazorMinigameUI()
    {
        // 1. Szukamy GameObjectu o nazwie "RazorMinigame" lub z zaznaczenia / po typie
        GameObject targetGo = GameObject.Find("RazorMinigame");
        if (targetGo == null && Selection.activeGameObject != null && Selection.activeGameObject.name.Contains("Razor"))
        {
            targetGo = Selection.activeGameObject;
        }
        if (targetGo == null)
        {
            targetGo = GameObject.Find("RazorMinigame_Object");
        }
        if (targetGo == null)
        {
            RazorMinigame found = Object.FindAnyObjectByType<RazorMinigame>(FindObjectsInactive.Include);
            if (found != null) targetGo = found.gameObject;
        }
        if (targetGo == null)
        {
            targetGo = new GameObject("RazorMinigame");
            Undo.RegisterCreatedObjectUndo(targetGo, "Create RazorMinigame GameObject");
        }

        RazorMinigame minigame = targetGo.GetComponent<RazorMinigame>();
        if (minigame == null)
        {
            minigame = targetGo.AddComponent<RazorMinigame>();
            Debug.Log("[Cyrulik] Dodano brakujący komponent RazorMinigame do obiektu: " + targetGo.name);
        }

        Undo.RegisterFullObjectHierarchyUndo(targetGo, "Build Razor Minigame UI From Scratch");

        // 2. Szukamy lub tworzymy Canvas pod GameObjectem RazorMinigame
        Canvas canvas = targetGo.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("Minigame_Razor_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(targetGo.transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup minigameCg = canvasGo.GetComponent<CanvasGroup>();
            minigameCg.alpha = 1f;
            minigameCg.interactable = true;
            minigameCg.blocksRaycasts = true;
        }

        Transform canvasTransform = canvas.transform;
        CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>() ?? canvas.gameObject.AddComponent<CanvasGroup>();

        // 2. Tło (delikatna ciemna winieta, brak ciężkiej ramki)
        Transform bgTransform = canvasTransform.Find("Minigame_razor_Background");
        GameObject bgGo = bgTransform != null ? bgTransform.gameObject : null;
        if (bgGo == null)
        {
            bgGo = new GameObject("Minigame_razor_Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(canvasTransform, false);
        }
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        Image bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = null;
        bgImg.color = new Color(0.02f, 0.02f, 0.04f, 0.45f);
        bgImg.raycastTarget = false;
        bgGo.transform.SetAsFirstSibling();
        EditorUtility.SetDirty(bgGo);

        // 3. Główny Pas Skórzany (Pasek - vintage_strop_main.png)
        Transform pasekTransform = canvasTransform.Find("Pasek");
        GameObject pasekGo = pasekTransform != null ? pasekTransform.gameObject : null;
        if (pasekGo == null)
        {
            pasekGo = new GameObject("Pasek", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pasekGo.transform.SetParent(canvasTransform, false);
        }
        RectTransform pasekRect = pasekGo.GetComponent<RectTransform>();
        pasekRect.anchorMin = new Vector2(0.5f, 0.5f);
        pasekRect.anchorMax = new Vector2(0.5f, 0.5f);
        pasekRect.pivot = new Vector2(0.5f, 0.5f);
        pasekRect.anchoredPosition = new Vector2(60f, 0f);
        pasekRect.sizeDelta = new Vector2(1150f, 1150f);
        Image pasekImg = pasekGo.GetComponent<Image>();
        Sprite vintageStrop = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/vintage_strop_main.png");
        if (vintageStrop != null) pasekImg.sprite = vintageStrop;
        pasekImg.preserveAspect = true;
        pasekImg.raycastTarget = false;
        EditorUtility.SetDirty(pasekGo);

        // Anchory trasy ostrzenia (BottomAnchor i TopAnchor)
        Transform botAnchorT = canvasTransform.Find("BottomAnchor");
        if (botAnchorT == null)
        {
            GameObject botGo = new GameObject("BottomAnchor", typeof(RectTransform));
            botGo.transform.SetParent(canvasTransform, false);
            botAnchorT = botGo.transform;
        }
        RectTransform botRect = botAnchorT.GetComponent<RectTransform>();
        botRect.anchoredPosition = new Vector2(-230f, -440f);

        Transform topAnchorT = canvasTransform.Find("TopAnchor");
        if (topAnchorT == null)
        {
            GameObject topGo = new GameObject("TopAnchor", typeof(RectTransform));
            topGo.transform.SetParent(canvasTransform, false);
            topAnchorT = topGo.transform;
        }
        RectTransform topRect = topAnchorT.GetComponent<RectTransform>();
        topRect.anchoredPosition = new Vector2(480f, 380f);

        // Strefy trafień (Perfect i Good)
        Transform perfectZoneT = pasekGo.transform.Find("PerfectZone_Image");
        GameObject perfGo = perfectZoneT != null ? perfectZoneT.gameObject : null;
        if (perfGo == null)
        {
            perfGo = new GameObject("PerfectZone_Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            perfGo.transform.SetParent(pasekGo.transform, false);
        }
        RectTransform perfRect = perfGo.GetComponent<RectTransform>();
        perfRect.anchoredPosition = new Vector2(240f, 260f);
        perfRect.sizeDelta = new Vector2(180f, 180f);
        Image perfImg = perfGo.GetComponent<Image>();
        Sprite perfSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/strefa_perfect_biuletyn.png")
                         ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/strefa_perfect_v2.png");
        if (perfSprite != null) perfImg.sprite = perfSprite;
        perfImg.preserveAspect = true;
        perfImg.raycastTarget = false;
        EditorUtility.SetDirty(perfGo);

        Transform goodZoneT = pasekGo.transform.Find("GoodZone_Image");
        GameObject goodGo = goodZoneT != null ? goodZoneT.gameObject : null;
        if (goodGo == null)
        {
            goodGo = new GameObject("GoodZone_Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            goodGo.transform.SetParent(pasekGo.transform, false);
        }
        RectTransform goodRect = goodGo.GetComponent<RectTransform>();
        goodRect.anchoredPosition = new Vector2(80f, 100f);
        goodRect.sizeDelta = new Vector2(200f, 200f);
        Image goodImg = goodGo.GetComponent<Image>();
        Sprite goodSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/strefa_good_biuletyn.png")
                         ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/strefa_good_v2.png");
        if (goodSprite != null) goodImg.sprite = goodSprite;
        goodImg.preserveAspect = true;
        goodImg.raycastTarget = false;
        EditorUtility.SetDirty(goodGo);

        // 4. Brzytwa na pasie (RazorImage)
        Transform razorTransform = canvasTransform.Find("RazorImage");
        GameObject razorGo = razorTransform != null ? razorTransform.gameObject : null;
        if (razorGo == null)
        {
            razorGo = new GameObject("RazorImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            razorGo.transform.SetParent(canvasTransform, false);
        }
        RectTransform razorRect = razorGo.GetComponent<RectTransform>();
        razorRect.anchoredPosition = new Vector2(-230f, -440f);
        razorRect.sizeDelta = new Vector2(380f, 220f);
        Image razorImg = razorGo.GetComponent<Image>();
        Sprite razorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/brzytwa_1ostrze_drewno.png")
                          ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/brzytwa_wskaznik_v3_orzech.png");
        if (razorSprite != null) razorImg.sprite = razorSprite;
        razorImg.preserveAspect = true;
        razorImg.raycastTarget = false;
        EditorUtility.SetDirty(razorGo);

        // 5. Pasek ostrości (ProgressBar) z dynamicznym gradientem i tekstem prób
        Transform progressBarT = canvasTransform.Find("ProgressBar");
        GameObject pbGo = progressBarT != null ? progressBarT.gameObject : null;
        if (pbGo == null)
        {
            pbGo = new GameObject("ProgressBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pbGo.transform.SetParent(canvasTransform, false);
        }
        RectTransform pbRect = pbGo.GetComponent<RectTransform>();
        pbRect.anchorMin = new Vector2(0.5f, 0.5f);
        pbRect.anchorMax = new Vector2(0.5f, 0.5f);
        pbRect.pivot = new Vector2(0.5f, 0.5f);
        pbRect.anchoredPosition = new Vector2(-480f, 280f);
        pbRect.sizeDelta = new Vector2(600f, 106f);
        Image pbImg = pbGo.GetComponent<Image>();
        Sprite barFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/razorminigame_image_pasekpostepu.png");
        if (barFrameSprite != null) pbImg.sprite = barFrameSprite;
        pbImg.preserveAspect = false;
        pbImg.raycastTarget = false;

        // Slot_Backdrop
        Transform sbT = pbGo.transform.Find("Slot_Backdrop");
        GameObject sbGo = sbT != null ? sbT.gameObject : null;
        if (sbGo == null)
        {
            sbGo = new GameObject("Slot_Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sbGo.transform.SetParent(pbGo.transform, false);
        }
        RectTransform sbRect = sbGo.GetComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(0.5f, 0.5f);
        sbRect.anchorMax = new Vector2(0.5f, 0.5f);
        sbRect.pivot = new Vector2(0.5f, 0.5f);
        sbRect.anchoredPosition = Vector2.zero;
        sbRect.sizeDelta = new Vector2(390f, 38f);
        Image sbImg = sbGo.GetComponent<Image>();
        sbImg.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
        sbImg.raycastTarget = false;

        // Sharpness_Fill_Gradient
        Transform fillT = pbGo.transform.Find("Sharpness_Fill_Gradient");
        GameObject fillGo = fillT != null ? fillT.gameObject : null;
        if (fillGo == null)
        {
            fillGo = new GameObject("Sharpness_Fill_Gradient", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(pbGo.transform, false);
        }
        RectTransform fRect = fillGo.GetComponent<RectTransform>();
        fRect.anchorMin = new Vector2(0.5f, 0.5f);
        fRect.anchorMax = new Vector2(0.5f, 0.5f);
        fRect.pivot = new Vector2(0.5f, 0.5f);
        fRect.anchoredPosition = Vector2.zero;
        fRect.sizeDelta = new Vector2(390f, 38f);
        Image fillImg = fillGo.GetComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f;
        fillImg.raycastTarget = false;
        Sprite gradSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/sharpness_gradient_fill.png");
        if (gradSprite != null) fillImg.sprite = gradSprite;

        // Wskaznik (SharpnessMarker)
        Transform markerT = pbGo.transform.Find("Wskaznik");
        GameObject markerGo = markerT != null ? markerT.gameObject : null;
        if (markerGo == null)
        {
            markerGo = new GameObject("Wskaznik", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerGo.transform.SetParent(pbGo.transform, false);
        }
        RectTransform mRect = markerGo.GetComponent<RectTransform>();
        mRect.anchoredPosition = new Vector2(-195f, 68f);
        mRect.sizeDelta = new Vector2(44f, 86f);
        Image markerImg = markerGo.GetComponent<Image>();
        Sprite markerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/brzytwa_1ostrze_antyk.png");
        if (markerSprite != null) markerImg.sprite = markerSprite;
        markerImg.raycastTarget = false;
        markerGo.transform.SetAsLastSibling();

        // Attempts_Text
        Transform attemptsTextT = pbGo.transform.Find("Attempts_Text");
        GameObject attGo = attemptsTextT != null ? attemptsTextT.gameObject : null;
        if (attGo == null)
        {
            attGo = new GameObject("Attempts_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            attGo.transform.SetParent(pbGo.transform, false);
        }
        RectTransform aRect = attGo.GetComponent<RectTransform>();
        aRect.anchorMin = new Vector2(0.5f, 0f);
        aRect.anchorMax = new Vector2(0.5f, 0f);
        aRect.pivot = new Vector2(0.5f, 1f);
        aRect.anchoredPosition = new Vector2(0f, -14f);
        aRect.sizeDelta = new Vector2(600f, 38f);
        TextMeshProUGUI tmpAttempts = attGo.GetComponent<TextMeshProUGUI>();
        tmpAttempts.alignment = TextAlignmentOptions.Center;
        tmpAttempts.fontSize = 20f;
        tmpAttempts.color = new Color(0.95f, 0.82f, 0.55f, 1f);
        tmpAttempts.text = "ATTEMPT: 1 / 5   •   LEFT: 5   •   SHARPNESS: 0%";

        // 6. Poradnik na zwoju pergaminu (Guide Overlay)
        Transform guideTransform = canvasTransform.Find("Guide");
        GameObject guideGo = guideTransform != null ? guideTransform.gameObject : null;
        if (guideGo == null)
        {
            guideGo = new GameObject("Guide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            guideGo.transform.SetParent(canvasTransform, false);
        }
        RectTransform guideRect = guideGo.GetComponent<RectTransform>();
        guideRect.anchorMin = new Vector2(0.5f, 0.5f);
        guideRect.anchorMax = new Vector2(0.5f, 0.5f);
        guideRect.pivot = new Vector2(0.5f, 0.5f);
        guideRect.anchoredPosition = Vector2.zero;
        guideRect.sizeDelta = new Vector2(780f, 580f);
        Image guideImage = guideGo.GetComponent<Image>();
        Sprite manualSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Razorminigame_art/guide_manual_clean.png");
        if (manualSprite != null) guideImage.sprite = manualSprite;
        guideImage.color = Color.white;
        guideImage.preserveAspect = true;
        guideImage.raycastTarget = false;
        CanvasGroup guideCg = guideGo.GetComponent<CanvasGroup>() ?? guideGo.AddComponent<CanvasGroup>();
        guideGo.transform.SetAsLastSibling();

        // 7. FeedbackText i InstructionText
        Transform fbTransform = canvasTransform.Find("FeedbackText");
        GameObject fbGo = fbTransform != null ? fbTransform.gameObject : null;
        if (fbGo == null)
        {
            fbGo = new GameObject("FeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
            fbGo.transform.SetParent(canvasTransform, false);
        }
        RectTransform fbRect = fbGo.GetComponent<RectTransform>();
        fbRect.anchoredPosition = new Vector2(0f, 380f);
        fbRect.sizeDelta = new Vector2(700f, 90f);
        TextMeshProUGUI tmpFb = fbGo.GetComponent<TextMeshProUGUI>();
        tmpFb.alignment = TextAlignmentOptions.Center;
        tmpFb.fontSize = 44f;
        tmpFb.text = "";

        Transform instTransform = canvasTransform.Find("InstructionText");
        GameObject instGo = instTransform != null ? instTransform.gameObject : null;
        if (instGo == null)
        {
            instGo = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            instGo.transform.SetParent(canvasTransform, false);
        }
        RectTransform instRect = instGo.GetComponent<RectTransform>();
        instRect.anchorMin = new Vector2(0.5f, 0f);
        instRect.anchorMax = new Vector2(0.5f, 0f);
        instRect.pivot = new Vector2(0.5f, 0.5f);
        instRect.anchoredPosition = new Vector2(0f, 90f);
        instRect.sizeDelta = new Vector2(900f, 60f);
        TextMeshProUGUI tmpInst = instGo.GetComponent<TextMeshProUGUI>();
        tmpInst.alignment = TextAlignmentOptions.Center;
        tmpInst.fontSize = 24f;
        tmpInst.color = Color.white;
        tmpInst.text = "PRESS [SPACE] TO START";

        // 8. Podpięcie referencji i angielskich tekstów do RazorMinigame
        SerializedObject so = new SerializedObject(minigame);
        so.FindProperty("minigameCanvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("razorIndicator").objectReferenceValue = razorRect;
        so.FindProperty("sharpnessMarker").objectReferenceValue = mRect;
        so.FindProperty("sharpnessFillImage").objectReferenceValue = fillImg;
        so.FindProperty("attemptsText").objectReferenceValue = tmpAttempts;
        so.FindProperty("feedbackText").objectReferenceValue = tmpFb;
        so.FindProperty("instructionText").objectReferenceValue = tmpInst;
        so.FindProperty("guideOverlayUI").objectReferenceValue = guideGo;
        so.FindProperty("guideOverlayCanvasGroup").objectReferenceValue = guideCg;
        so.FindProperty("showTutorialOnStart").boolValue = true;
        so.FindProperty("bottomAnchor").objectReferenceValue = botRect;
        so.FindProperty("topAnchor").objectReferenceValue = topRect;
        so.FindProperty("zoneGood").objectReferenceValue = goodRect;
        so.FindProperty("zonePerfect").objectReferenceValue = perfRect;

        // Automatyczne podpięcie gracza i kamery
        SerializedProperty moveProp = so.FindProperty("playerMovement");
        if (moveProp != null && moveProp.objectReferenceValue == null)
        {
            PlayerMovement pm = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (pm != null) moveProp.objectReferenceValue = pm;
        }

        SerializedProperty handsProp = so.FindProperty("playerHands");
        if (handsProp != null && handsProp.objectReferenceValue == null)
        {
            PlayerHands ph = Object.FindAnyObjectByType<PlayerHands>(FindObjectsInactive.Include);
            if (ph != null) handsProp.objectReferenceValue = ph;
        }

        SerializedProperty cineProp = so.FindProperty("cinemachineBrain");
        if (cineProp != null && cineProp.objectReferenceValue == null)
        {
            var brain = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineBrain>(FindObjectsInactive.Include);
            if (brain != null) cineProp.objectReferenceValue = brain;
        }

        // Ustawienie wszystkich promptów na język angielski
        SetSerializedString(so, "promptStartMinigame", "PRESS [SPACE] TO START");
        SetSerializedString(so, "promptHoldToSharpen", "PRESS [SPACE] TO STROKE  |  CLICK [LMB] IN ZONE");
        SetSerializedString(so, "promptStrokeInFlight", "CLICK [LMB] IN ZONE (GOOD / PERFECT)!");
        SetSerializedString(so, "promptReturning", "FLIPPING BLADE...");
        SetSerializedString(so, "textPerfect", "PERFECT!");
        SetSerializedString(so, "textGood", "GOOD!");
        SetSerializedString(so, "textTooEarly", "TOO EARLY!");
        SetSerializedString(so, "textTooLate", "TOO LATE!");
        SetSerializedString(so, "textBladeSharp", "RAZOR SHARPENED!");
        SetSerializedString(so, "textBladeDull", "RAZOR IS TOO DULL!");

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(minigame);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(minigame.gameObject.scene);

        Debug.Log("[Cyrulik] Pełne UI Minigry Ostrzenia Brzytwy zostało pomyślnie zbudowane od podstaw w stylu ryciny z przewodnika!");
    }

    private static void SetSerializedString(SerializedObject so, string propName, string value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null)
        {
            prop.stringValue = value;
        }
    }

    private static Material FindOrCreateLeatherMaterial()
    {
        string[] guids = AssetDatabase.FindAssets("strop_szkic_ciemna_skora t:Texture2D");
        if (guids.Length == 0) guids = AssetDatabase.FindAssets("strop_pasek_plaski t:Texture2D");
        if (guids.Length == 0) guids = AssetDatabase.FindAssets("strop_pelny_oryginal t:Texture2D");

        Texture2D leatherTex = null;
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            leatherTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        string matPath = "Assets/Art/Razorminigame_art/Materials/M_HangingLeatherStrop.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            Shader psxOrUrp = Shader.Find("PSX/RetroSurface") 
                           ?? Shader.Find("Universal Render Pipeline/Lit") 
                           ?? Shader.Find("Standard");

            mat = new Material(psxOrUrp);
            if (leatherTex != null)
            {
                mat.mainTexture = leatherTex;
            }
            else
            {
                mat.color = new Color(0.35f, 0.20f, 0.12f, 1f);
            }

            if (!AssetDatabase.IsValidFolder("Assets/Art/Razorminigame_art/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Art/Razorminigame_art", "Materials");
            }

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }

        return mat;
    }

    private static Material FindOrCreateBrassMaterial()
    {
        string matPath = "Assets/Art/Razorminigame_art/Materials/M_StropBrassHardware.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.color = new Color(0.65f, 0.52f, 0.28f, 1f);

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.75f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }

        return mat;
    }
}
#endif

