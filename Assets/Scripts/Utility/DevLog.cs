/// <summary>
/// Zero-cost logging helper — wszystkie wywołania są kompletnie usuwane przez kompilator
/// w buildach produkcyjnych (DEVELOPMENT_BUILD i UNITY_EDITOR).
/// Zastępuje bezpośrednie Debug.Log, które w buildach release generują alokacje GC
/// przez StackTraceUtility.ExtractStringFromExceptionInternal (~83 KB/klatkę).
/// 
/// Użycie: DevLog.Log("wiadomość") zamiast Debug.Log("wiadomość")
/// </summary>
public static class DevLog
{
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message, UnityEngine.Object context)
    {
        UnityEngine.Debug.Log(message, context);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string message, UnityEngine.Object context)
    {
        UnityEngine.Debug.LogWarning(message, context);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(string message)
    {
        UnityEngine.Debug.LogError(message);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(string message, UnityEngine.Object context)
    {
        UnityEngine.Debug.LogError(message, context);
    }
}
