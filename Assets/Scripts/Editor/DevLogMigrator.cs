using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Jednorazowe narzędzie migracji: podmienia Debug.Log/LogWarning/LogError na DevLog
/// we wszystkich skryptach produkcyjnych (poza folderem Editor/).
/// Uruchom: Tools → Cyrulik → Migrate Debug.Log to DevLog
/// </summary>
public class DevLogMigrator : Editor
{
    [MenuItem("Tools/Cyrulik/Dev & Narzędzia/Migrate Debug.Log to DevLog (One-Time)", false, 200)]
    public static void MigrateAll()
    {
        string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
        string[] csFiles = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

        int modifiedCount = 0;

        foreach (string filePath in csFiles)
        {
            // Pomiń skrypty edytorowe — tam Debug.Log jest OK
            if (filePath.Replace('\\', '/').Contains("/Editor/")) continue;
            // Pomiń sam DevLog.cs
            if (filePath.EndsWith("DevLog.cs")) continue;

            string original = File.ReadAllText(filePath);
            string modified = original;

            // Podmień Debug.LogWarning(msg, context) → DevLog.LogWarning(msg, context)
            modified = Regex.Replace(modified,
                @"\bDebug\.LogWarning\b",
                "DevLog.LogWarning");

            // Podmień Debug.LogError(msg, context) → DevLog.LogError(msg, context)
            modified = Regex.Replace(modified,
                @"\bDebug\.LogError\b",
                "DevLog.LogError");

            // Podmień Debug.Log( → DevLog.Log(
            modified = Regex.Replace(modified,
                @"\bDebug\.Log\b(?!\w)",
                "DevLog.Log");

            if (modified != original)
            {
                File.WriteAllText(filePath, modified);
                modifiedCount++;
                Debug.Log($"[DevLogMigrator] Zmigrowano: {Path.GetFileName(filePath)}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[DevLogMigrator] Gotowe! Zmigrowano {modifiedCount} plików. Debug.Log → DevLog.Log w skryptach produkcyjnych.");
    }
}
