using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RepairedHoodieAutoRebuilder
{
    private const string SentinelPath = "/tmp/sizeguide_rebuild_fixed_hoodies";

    static RepairedHoodieAutoRebuilder()
    {
        if (Application.isBatchMode || !File.Exists(SentinelPath))
        {
            return;
        }

        File.Delete(SentinelPath);
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        try
        {
            Debug.Log("[RepairedHoodieAutoRebuilder] Rebuilding fixed hoodie preview prefabs.");
            RepairedHoodiePreviewBuilder.BuildAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RepairedHoodieAutoRebuilder] Rebuild complete.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RepairedHoodieAutoRebuilder] Rebuild failed: {ex}");
        }
    }
}
