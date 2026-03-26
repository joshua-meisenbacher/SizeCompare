using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RepairedHoodieValidator
{
    private static readonly string[] ResourcePaths =
    {
        "Generated/RepairedHoodiePrefabs/Hoodie_S_Preview",
        "Generated/RepairedHoodiePrefabs/Hoodie_M_Preview",
        "Generated/RepairedHoodiePrefabs/Hoodie_L_Preview",
        "Generated/RepairedHoodiePrefabs/Hoodie_XL_Preview"
    };

    public static void Validate()
    {
        var failed = false;

        Debug.Log("[RepairedHoodieValidator] Starting validation.");

        foreach (var resourcePath in ResourcePaths)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[RepairedHoodieValidator] Resources.Load failed for '{resourcePath}'.");
                failed = true;
                continue;
            }

            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError($"[RepairedHoodieValidator] No SkinnedMeshRenderer found in '{resourcePath}'.");
                failed = true;
                continue;
            }

            foreach (var renderer in renderers)
            {
                var meshName = renderer.sharedMesh != null ? renderer.sharedMesh.name : "null";
                var bonesCount = renderer.bones != null ? renderer.bones.Length : 0;
                var rootBoneName = renderer.rootBone != null ? renderer.rootBone.name : "null";
                var materialCount = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0;
                Debug.Log(
                    $"[RepairedHoodieValidator] '{resourcePath}' renderer='{renderer.name}' mesh='{meshName}' bones={bonesCount} rootBone='{rootBoneName}' materials={materialCount} enabled={renderer.enabled} updateWhenOffscreen={renderer.updateWhenOffscreen} localBoundsCenter={renderer.localBounds.center} localBoundsSize={renderer.localBounds.size}.",
                    renderer);

                if (renderer.sharedMesh == null)
                {
                    Debug.LogError($"[RepairedHoodieValidator] Renderer '{renderer.name}' in '{resourcePath}' has no shared mesh.");
                    failed = true;
                }

                if (bonesCount == 0)
                {
                    Debug.LogError($"[RepairedHoodieValidator] Renderer '{renderer.name}' in '{resourcePath}' has no bones.");
                    failed = true;
                }
                else if (bonesCount < 10)
                {
                    Debug.LogError($"[RepairedHoodieValidator] Renderer '{renderer.name}' in '{resourcePath}' has too few bones ({bonesCount}).");
                    failed = true;
                }

                var boneNames = renderer.bones != null
                    ? string.Join(", ", renderer.bones.Where(b => b != null).Select(b => b.name))
                    : string.Empty;
                Debug.Log($"[RepairedHoodieValidator] '{resourcePath}' boneNames=[{boneNames}].", renderer);
            }
        }

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        Debug.Log($"[RepairedHoodieValidator] Opened scene '{scene.path}'.");

        var onboarding = UnityEngine.Object.FindFirstObjectByType<OnboardingManager>(FindObjectsInactive.Include);
        if (onboarding == null)
        {
            Debug.LogError("[RepairedHoodieValidator] Could not find OnboardingManager in SampleScene.");
            failed = true;
        }
        else
        {
            Debug.Log(
                $"[RepairedHoodieValidator] OnboardingManager previewRoot='{(onboarding.previewRoot != null ? onboarding.previewRoot.name : "null")}' " +
                $"maleUnified='{(onboarding.maleUnified != null ? onboarding.maleUnified.name : "null")}' " +
                $"femaleUnified='{(onboarding.femaleUnified != null ? onboarding.femaleUnified.name : "null")}'.",
                onboarding);
        }

        var switchers = UnityEngine.Object.FindObjectsByType<HoodieSizeSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var switcher in switchers)
        {
            Debug.Log(
                $"[RepairedHoodieValidator] Switcher '{switcher.name}' active={switcher.gameObject.activeInHierarchy} " +
                $"hasAvatar={(switcher.avatar != null)} legacy={switcher.useLegacyFallback}.",
                switcher);
        }

        if (failed)
        {
            throw new Exception("Repaired hoodie validation failed. See editor log for details.");
        }

        Debug.Log("[RepairedHoodieValidator] Validation succeeded.");
        EditorApplication.Exit(0);
    }
}
