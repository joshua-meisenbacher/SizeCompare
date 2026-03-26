using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RepairedHoodieUmaSceneValidator
{
    public static void Validate()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        Debug.Log($"[RepairedHoodieUmaSceneValidator] Opened scene '{scene.path}'.");

        var switchers = UnityEngine.Object.FindObjectsByType<HoodieSizeSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (switchers == null || switchers.Length == 0)
        {
            throw new Exception("[RepairedHoodieUmaSceneValidator] No HoodieSizeSwitcher components found.");
        }

        var failed = false;
        foreach (var switcher in switchers)
        {
            Debug.Log(
                $"[RepairedHoodieUmaSceneValidator] switcher='{switcher.name}' " +
                $"avatar='{(switcher.avatar != null ? switcher.avatar.name : "null")}' " +
                $"legacy={switcher.useLegacyFallback} " +
                $"recipes=[{(switcher.smallRecipe != null ? switcher.smallRecipe.name : "null")}, " +
                $"{(switcher.mediumRecipe != null ? switcher.mediumRecipe.name : "null")}, " +
                $"{(switcher.largeRecipe != null ? switcher.largeRecipe.name : "null")}, " +
                $"{(switcher.extraLargeRecipe != null ? switcher.extraLargeRecipe.name : "null")}].",
                switcher);

            if (switcher.useLegacyFallback)
            {
                failed = true;
            }

            if (switcher.smallRecipe == null || switcher.mediumRecipe == null || switcher.largeRecipe == null || switcher.extraLargeRecipe == null)
            {
                failed = true;
            }
        }

        if (failed)
        {
            throw new Exception("[RepairedHoodieUmaSceneValidator] One or more switchers are still using fallback or missing recipe refs.");
        }

        Debug.Log("[RepairedHoodieUmaSceneValidator] Validation succeeded.");
    }
}
