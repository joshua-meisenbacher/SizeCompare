using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/*
 * Unity editor-side garment importer prototype for a 3D fitting room pipeline.
 *
 * Updated behavior:
 * - Handles two garment import cases:
 *   1) FBX already contains a SkinnedMeshRenderer
 *   2) FBX is a plain MeshRenderer/MeshFilter exported from Blender
 *      and needs skinning copied from a reference skinned garment
 *
 * This second path is important for your current Blender workflow, because the
 * generated size variants are static meshes with the same topology as the base garment.
 * We can copy bindposes + bone weights from the reference hoodie and then rebind the
 * bones to the target avatar skeleton.
 */

public enum GarmentCategory
{
    Top,
    Bottom,
    Outerwear,
    Dress,
    Shoes,
    Accessory,
    Other
}

[CreateAssetMenu(fileName = "GarmentMetadata", menuName = "FittingRoom/Garment Metadata")]
public class GarmentMetadata : ScriptableObject
{
    public string garmentId;
    public string styleName;
    public string sizeLabel;
    public GarmentCategory category;
    public GameObject garmentPrefab;
    public string sourceFbxPath;
}

public class GarmentImportWindow : EditorWindow
{
    private GameObject garmentFbx;
    private GameObject targetAvatarRoot;
    private Transform targetSkeletonRoot;
    private GameObject referenceSkinnedGarment;

    private string styleName = "Hoodie";
    private string sizeLabel = "M";
    private GarmentCategory category = GarmentCategory.Top;

    private string prefabOutputFolder = "Assets/FittingRoom/Generated/Prefabs";
    private string metadataOutputFolder = "Assets/FittingRoom/Generated/Metadata";

    [MenuItem("Tools/Fitting Room/Garment Importer")]
    public static void ShowWindow()
    {
        var window = GetWindow<GarmentImportWindow>("Garment Importer");
        window.minSize = new Vector2(540, 400);
    }

    private void OnGUI()
    {
        GUILayout.Label("Garment Import Pipeline", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select a garment FBX and a target avatar in the scene. If the FBX is a plain Blender-exported mesh " +
            "with no SkinnedMeshRenderer, provide a reference skinned garment with matching topology so the tool can " +
            "copy skinning data and rebind it to the avatar skeleton.",
            MessageType.Info);

        garmentFbx = (GameObject)EditorGUILayout.ObjectField("Garment FBX", garmentFbx, typeof(GameObject), false);
        referenceSkinnedGarment = (GameObject)EditorGUILayout.ObjectField("Reference Skinned Garment", referenceSkinnedGarment, typeof(GameObject), true);
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField("Target Avatar Root", targetAvatarRoot, typeof(GameObject), true);
        targetSkeletonRoot = (Transform)EditorGUILayout.ObjectField("Target Skeleton Root", targetSkeletonRoot, typeof(Transform), true);

        EditorGUILayout.Space();
        GUILayout.Label("Garment Metadata", EditorStyles.boldLabel);
        styleName = EditorGUILayout.TextField("Style Name", styleName);
        sizeLabel = EditorGUILayout.TextField("Size Label", sizeLabel);
        category = (GarmentCategory)EditorGUILayout.EnumPopup("Category", category);

        EditorGUILayout.Space();
        GUILayout.Label("Output", EditorStyles.boldLabel);
        prefabOutputFolder = EditorGUILayout.TextField("Prefab Folder", prefabOutputFolder);
        metadataOutputFolder = EditorGUILayout.TextField("Metadata Folder", metadataOutputFolder);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!IsInputValid()))
        {
            if (GUILayout.Button("Generate Garment Prefab", GUILayout.Height(36)))
            {
                GenerateGarmentPrefab();
            }
        }

        if (!IsInputValid())
        {
            EditorGUILayout.HelpBox(
                "Assign the garment FBX, target avatar root, and target skeleton root. " +
                "If your garment FBX came from Blender as a plain mesh, also assign a reference skinned garment " +
                "with the same topology, such as the original working hoodie.",
                MessageType.Warning);
        }
    }

    private bool IsInputValid()
    {
        return garmentFbx != null && targetAvatarRoot != null && targetSkeletonRoot != null;
    }

    private void GenerateGarmentPrefab()
    {
        try
        {
            EnsureFolderExists(prefabOutputFolder);
            EnsureFolderExists(metadataOutputFolder);

            GameObject garmentInstance = (GameObject)PrefabUtility.InstantiatePrefab(garmentFbx);
            if (garmentInstance == null)
            {
                throw new Exception("Failed to instantiate garment FBX prefab.");
            }

            garmentInstance.name = BuildAssetName();

            try
            {
                var garmentRenderers = garmentInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                if (garmentRenderers == null || garmentRenderers.Length == 0)
                {
                    garmentRenderers = ConvertStaticGarmentToSkinned(garmentInstance);
                }

                if (garmentRenderers == null || garmentRenderers.Length == 0)
                {
                    throw new Exception(
                        "No SkinnedMeshRenderer found in garment FBX, and static-mesh conversion failed. " +
                        "If this FBX came from Blender, assign a Reference Skinned Garment with matching topology."
                    );
                }

                Dictionary<string, Transform> avatarBoneMap = BuildBoneMap(targetSkeletonRoot);

                foreach (var smr in garmentRenderers)
                {
                    RebindSkinnedMeshRenderer(smr, avatarBoneMap);
                }

                string prefabPath = $"{prefabOutputFolder}/{BuildAssetName()}.prefab";
                string uniquePrefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                    garmentInstance,
                    uniquePrefabPath,
                    out bool success);

                if (!success || savedPrefab == null)
                {
                    throw new Exception("Failed to save garment prefab.");
                }

                GarmentMetadata metadata = CreateMetadataAsset(savedPrefab);

                Debug.Log($"Garment prefab created: {uniquePrefabPath}");
                Debug.Log($"Garment metadata created: {AssetDatabase.GetAssetPath(metadata)}");
                EditorUtility.DisplayDialog(
                    "Garment Import Complete",
                    $"Prefab created at:\n{uniquePrefabPath}\n\nMetadata asset also created.",
                    "OK");
            }
            finally
            {
                if (garmentInstance != null)
                {
                    DestroyImmediate(garmentInstance);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Garment import failed: {ex.Message}\n{ex.StackTrace}");
            EditorUtility.DisplayDialog("Garment Import Failed", ex.Message, "OK");
        }
    }

    private SkinnedMeshRenderer[] ConvertStaticGarmentToSkinned(GameObject garmentInstance)
    {
        if (referenceSkinnedGarment == null)
        {
            throw new Exception(
                "Garment FBX has no SkinnedMeshRenderer. Assign Reference Skinned Garment so the tool can copy skinning data."
            );
        }

        var referenceSmr = referenceSkinnedGarment.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (referenceSmr == null)
        {
            throw new Exception("Reference Skinned Garment does not contain a SkinnedMeshRenderer.");
        }

        var meshFilters = garmentInstance.GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters == null || meshFilters.Length == 0)
        {
            throw new Exception("Garment FBX contains neither a SkinnedMeshRenderer nor a MeshFilter.");
        }

        var createdRenderers = new List<SkinnedMeshRenderer>();

        foreach (var meshFilter in meshFilters)
        {
            Mesh staticMesh = meshFilter.sharedMesh;
            if (staticMesh == null)
            {
                continue;
            }

            Mesh skinnedMesh = BuildSkinnedMeshFromReference(staticMesh, referenceSmr.sharedMesh);
            MeshRenderer existingMeshRenderer = meshFilter.GetComponent<MeshRenderer>();

            var newSmr = meshFilter.gameObject.AddComponent<SkinnedMeshRenderer>();
            newSmr.sharedMesh = skinnedMesh;
            newSmr.sharedMaterials = existingMeshRenderer != null ? existingMeshRenderer.sharedMaterials : referenceSmr.sharedMaterials;
            newSmr.rootBone = referenceSmr.rootBone;
            newSmr.bones = referenceSmr.bones;
            newSmr.updateWhenOffscreen = true;

            if (existingMeshRenderer != null)
            {
                DestroyImmediate(existingMeshRenderer);
            }

            DestroyImmediate(meshFilter);
            createdRenderers.Add(newSmr);
        }

        return createdRenderers.ToArray();
    }

    private static Mesh BuildSkinnedMeshFromReference(Mesh staticMesh, Mesh referenceMesh)
    {
        if (staticMesh == null)
            throw new Exception("Static mesh is null.");

        if (referenceMesh == null)
            throw new Exception("Reference skinned mesh is null.");

        Vector3[] newVerts = staticMesh.vertices;
        Vector3[] refVerts = referenceMesh.vertices;

         BoneWeight[] refWeights = referenceMesh.boneWeights;
        BoneWeight[] newWeights = new BoneWeight[newVerts.Length];

        // For each vertex in the new mesh
         for (int i = 0; i < newVerts.Length; i++)
        {
            float bestDist = float.MaxValue;
            int bestIndex = 0;

            Vector3 v = newVerts[i];

            // Find nearest vertex in reference mesh
            for (int j = 0; j < refVerts.Length; j++)
            {
                float dist = (v - refVerts[j]).sqrMagnitude;

             if (dist < bestDist)
             {
                bestDist = dist;
                bestIndex = j;
                }
            }

        newWeights[i] = refWeights[bestIndex];
        }

        Mesh result = UnityEngine.Object.Instantiate(staticMesh);
        result.name = staticMesh.name + "_Skinned";

        result.bindposes = referenceMesh.bindposes;
        result.boneWeights = newWeights;

        result.RecalculateBounds();

        return result;
    }

    private string BuildAssetName()
    {
        string cleanStyle = SanitizeName(styleName);
        string cleanSize = SanitizeName(sizeLabel);
        return $"{cleanStyle}_{cleanSize}";
    }

    private static string SanitizeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unnamed";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            raw = raw.Replace(c, '_');
        }

        return raw.Replace(" ", "_").Trim();
    }

    private static Dictionary<string, Transform> BuildBoneMap(Transform skeletonRoot)
    {
        var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        Transform[] allBones = skeletonRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform bone in allBones)
        {
            if (!map.ContainsKey(bone.name))
            {
                map[bone.name] = bone;
            }
        }

        return map;
    }

    private static void RebindSkinnedMeshRenderer(SkinnedMeshRenderer smr, Dictionary<string, Transform> avatarBoneMap)
    {
        if (smr.sharedMesh == null)
        {
            throw new Exception($"Renderer '{smr.name}' has no shared mesh.");
        }

        Transform[] sourceBones = smr.bones;
        Transform[] reboundBones = new Transform[sourceBones.Length];

        for (int i = 0; i < sourceBones.Length; i++)
        {
            Transform sourceBone = sourceBones[i];
            if (sourceBone == null)
            {
                reboundBones[i] = null;
                continue;
            }

            if (!avatarBoneMap.TryGetValue(sourceBone.name, out Transform targetBone))
            {
                if (TryGetFallbackBone(avatarBoneMap, out Transform fallbackBone))
                {
                    Debug.LogWarning($"No matching avatar bone for garment bone '{sourceBone.name}' on renderer '{smr.name}'. Using fallback bone '{fallbackBone.name}'.");
                    reboundBones[i] = fallbackBone;
                    continue;
                }

                Debug.LogWarning($"No matching avatar bone for garment bone '{sourceBone.name}' on renderer '{smr.name}'. Leaving slot unbound.");
                reboundBones[i] = null;
                continue;
            }

            reboundBones[i] = targetBone;
        }

        smr.bones = reboundBones;

        if (smr.rootBone != null)
        {
            if (avatarBoneMap.TryGetValue(smr.rootBone.name, out Transform targetRootBone))
            {
                smr.rootBone = targetRootBone;
            }
            else
            {
                Debug.LogWarning($"Could not remap root bone '{smr.rootBone.name}' for renderer '{smr.name}'. Leaving as-is.");
            }
        }
    }

    private static bool TryGetFallbackBone(Dictionary<string, Transform> avatarBoneMap, out Transform fallbackBone)
    {
        string[] preferredFallbacks = { "Hips", "Pelvis", "Spine", "Root" };

        foreach (string boneName in preferredFallbacks)
        {
            if (avatarBoneMap.TryGetValue(boneName, out fallbackBone))
            {
                return true;
            }
        }

        foreach (Transform candidate in avatarBoneMap.Values)
        {
            fallbackBone = candidate;
            return true;
        }

        fallbackBone = null;
        return false;
    }

    private GarmentMetadata CreateMetadataAsset(GameObject garmentPrefab)
    {
        string assetName = BuildAssetName() + "_Metadata.asset";
        string metadataPath = AssetDatabase.GenerateUniqueAssetPath($"{metadataOutputFolder}/{assetName}");

        GarmentMetadata metadata = ScriptableObject.CreateInstance<GarmentMetadata>();
        metadata.garmentId = Guid.NewGuid().ToString("N");
        metadata.styleName = styleName;
        metadata.sizeLabel = sizeLabel;
        metadata.category = category;
        metadata.garmentPrefab = garmentPrefab;
        metadata.sourceFbxPath = AssetDatabase.GetAssetPath(garmentFbx);

        AssetDatabase.CreateAsset(metadata, metadataPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return metadata;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        if (parts.Length < 2 || parts[0] != "Assets")
        {
            throw new Exception($"Folder must be inside Assets/: {folderPath}");
        }

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}

public class GarmentSlot : MonoBehaviour
{
    [SerializeField] private Transform garmentAnchor;
    private GameObject currentGarmentInstance;

    public void Equip(GameObject garmentPrefab)
    {
        if (garmentAnchor == null)
        {
            garmentAnchor = transform;
        }

        if (currentGarmentInstance != null)
        {
            Destroy(currentGarmentInstance);
        }

        if (garmentPrefab == null)
        {
            return;
        }

        currentGarmentInstance = Instantiate(garmentPrefab, garmentAnchor);
        currentGarmentInstance.transform.localPosition = Vector3.zero;
        currentGarmentInstance.transform.localRotation = Quaternion.identity;
        currentGarmentInstance.transform.localScale = Vector3.one;
    }
}
