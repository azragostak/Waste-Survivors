using TMPro;
using UnityEditor;
using UnityEngine;

// TextMeshPro'nun temel kaynakları (varsayılan font, TMP Settings) projede yoksa bir kez otomatik import eder.
// Bunlar olmadan HUD'daki yazılar görünmez. Import edilen "Assets/TextMesh Pro" klasörü repoya eklenmeli.
[InitializeOnLoad]
public static class TmpEssentialsImporter
{
    private const string RequestedKey = "TmpEssentialsImporter.Requested";

    static TmpEssentialsImporter()
    {
        EditorApplication.delayCall += ImportIfMissing;
    }

    private static void ImportIfMissing()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return;

        // Aynı editör oturumunda tekrar tekrar denemesin
        if (SessionState.GetBool(RequestedKey, false)) return;
        SessionState.SetBool(RequestedKey, true);

        Debug.Log("TextMeshPro temel kaynakları import ediliyor (Assets/TextMesh Pro)...");
        TMP_PackageResourceImporter.ImportResources(true, false, false);
    }
}
