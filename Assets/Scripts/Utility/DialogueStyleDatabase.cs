using System;
using UnityEngine;

/// <summary>
/// Baza zasobów graficznych (sprite'y 9-slice i palety) dla 5 stylów ramek dialogów i myśli.
/// </summary>
[CreateAssetMenu(fileName = "DialogueStyleDatabase", menuName = "Cyrulik/Dialogue Style Database")]
public class DialogueStyleDatabase : ScriptableObject
{
    [System.Serializable]
    public class StyleEntry
    {
        public DialogueFrameStyle style;
        public string displayName;
        public Sprite frameSprite;
        public Sprite bgSprite;
        public DialogueStyleTheme theme;
    }

    public StyleEntry[] styles = new StyleEntry[5];

    private static DialogueStyleDatabase _instance;
    public static DialogueStyleDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<DialogueStyleDatabase>("DialogueStyleDatabase");
#if UNITY_EDITOR
                if (_instance == null)
                {
                    _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueStyleDatabase>("Assets/Resources/DialogueStyleDatabase.asset");
                }
#endif
            }
            return _instance;
        }
    }

    public StyleEntry GetEntry(DialogueFrameStyle style)
    {
        if (styles != null)
        {
            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] != null && styles[i].style == style)
                    return styles[i];
            }
        }
        return null;
    }
}
