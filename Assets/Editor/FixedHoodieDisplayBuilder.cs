using System;
using System.Collections.Generic;
using UMA;
using UnityEditor;
using UnityEngine;

public static class FixedHoodieDisplayBuilder
{
    private const string GarmentBonePrefix = "Garment_";
    private const string ReferencePrefabPath = "Assets/UMA/Content/Contrib/MaleHoodie/MaleHoodie_Skinned.prefab";
    private const string ReferenceMaterialPath = "Assets/Hoodie.mat";
    private const string OutputRoot = "Assets/Resources/Generated/FixedHoodieDisplays";

    private const float ReferenceWidthInches = 20f;
    private const float ReferenceLengthInches = 29f;
    private const float ReferenceChestInches = 40f;
    private const float BaseWidthCalibration = 0.99f;
    private const float BaseLengthCalibration = 1.00f;
    private const float BaseChestCalibration = 1.00f;

    private static readonly VariantDefinition[] Variants =
    {
        new VariantDefinition("S", 18f, 28f, 36f, 0.004f),
        new VariantDefinition("M", 20f, 29f, 40f, 0.006f),
        new VariantDefinition("L", 22f, 30f, 44f, 0.009f),
        new VariantDefinition("XL", 24f, 31f, 48f, 0.013f),
    };

    [MenuItem("Tools/Hoodie/Build Fixed Hoodie Displays")]
    public static void BuildAll()
    {
        EnsureFolder(OutputRoot);

        var referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePrefabPath);
        var referenceMaterial = AssetDatabase.LoadAssetAtPath<Material>(ReferenceMaterialPath);
        if (referencePrefab == null || referenceMaterial == null)
        {
            throw new Exception($"Missing reference hoodie assets at {ReferencePrefabPath} or {ReferenceMaterialPath}");
        }

        foreach (var variant in Variants)
        {
            BuildVariant(referencePrefab, referenceMaterial, variant);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FixedHoodieDisplayBuilder] Built fixed hoodie display prefabs.");
    }

    private static void BuildVariant(GameObject referencePrefab, Material referenceMaterial, VariantDefinition variant)
    {
        var instance = UnityEngine.Object.Instantiate(referencePrefab);
        Mesh generatedMesh = null;

        try
        {
            var renderer = FindPrimaryRenderer(instance);
            if (renderer == null || renderer.sharedMesh == null)
            {
                throw new Exception($"Reference hoodie renderer missing for size {variant.Size}");
            }

            generatedMesh = BuildVariantMeshFromCanonical(renderer.sharedMesh, variant);
            var meshPath = $"{OutputRoot}/Hoodie_{variant.Size}_Fixed.asset";
            SaveMeshAsset(meshPath, generatedMesh);

            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (savedMesh == null)
            {
                throw new Exception($"Could not load generated mesh asset at {meshPath}");
            }

            renderer.sharedMesh = savedMesh;
            renderer.sharedMaterials = new[] { referenceMaterial };
            renderer.updateWhenOffscreen = true;

            PrefixRigNames(instance.transform);

            var poseDriver = instance.GetComponent<FixedGarmentPoseDriver>();
            if (poseDriver == null)
            {
                poseDriver = instance.AddComponent<FixedGarmentPoseDriver>();
            }

            instance.name = $"Hoodie_{variant.Size}_Fixed";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var prefabPath = $"{OutputRoot}/Hoodie_{variant.Size}_Fixed.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Debug.Log($"[FixedHoodieDisplayBuilder] Built {prefabPath}.");
        }
        finally
        {
            if (generatedMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(generatedMesh);
            }

            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }

    private static SkinnedMeshRenderer FindPrimaryRenderer(GameObject root)
    {
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh != null && renderer.name.IndexOf("hoodie", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return renderer;
            }
        }

        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh != null)
            {
                return renderer;
            }
        }

        return null;
    }

    private static Mesh BuildVariantMeshFromCanonical(Mesh referenceMesh, VariantDefinition variant)
    {
        var result = UnityEngine.Object.Instantiate(referenceMesh);
        result.name = $"Hoodie_{variant.Size}_Fixed_Mesh";

        var widthScale = (variant.WidthInches / ReferenceWidthInches) * BaseWidthCalibration;
        var lengthScale = (variant.LengthInches / ReferenceLengthInches) * BaseLengthCalibration;
        var chestScale = (variant.ChestInches / ReferenceChestInches) * BaseChestCalibration;
        var widthDelta = widthScale - 1f;
        var lengthDelta = lengthScale - 1f;
        var chestDelta = chestScale - 1f;

        var bounds = referenceMesh.bounds;
        var center = bounds.center;
        var size = bounds.size;
        var halfWidth = Mathf.Max(size.x * 0.5f, 0.0001f);
        var halfDepth = Mathf.Max(size.z * 0.5f, 0.0001f);
        var vertices = referenceMesh.vertices;
        var normals = referenceMesh.normals;
        var adjusted = new Vector3[vertices.Length];

        for (var i = 0; i < vertices.Length; i++)
        {
            var vertex = vertices[i];
            var relative = vertex - center;
            var y01 = size.y > 0.0001f ? Mathf.InverseLerp(bounds.min.y, bounds.max.y, vertex.y) : 0.5f;
            var xAbs01 = Mathf.Clamp01(Mathf.Abs(relative.x) / halfWidth);
            var zAbs01 = Mathf.Clamp01(Mathf.Abs(relative.z) / halfDepth);

            var hoodBand = Band(y01, 0.90f, 0.14f);
            var shoulderBand = Band(y01, 0.76f, 0.14f);
            var chestBand = Band(y01, 0.58f, 0.20f);
            var waistBand = Band(y01, 0.34f, 0.18f);
            var hemBand = Band(y01, 0.10f, 0.12f);
            var underArmBand = Band(y01, 0.48f, 0.12f);

            var sleeveBand = SmoothStepRange(0.56f, 0.78f, xAbs01);
            var cuffBand = SmoothStepRange(0.84f, 0.98f, xAbs01);
            var torsoOnlyBand = Mathf.Clamp01((1f - sleeveBand * 0.9f) * (0.45f * chestBand + 0.38f * waistBand + 0.24f * hemBand + 0.18f * shoulderBand));

            var xScale = 1f;
            xScale += widthDelta * (0.74f * torsoOnlyBand + 0.10f * shoulderBand + 0.20f * sleeveBand + 0.05f * cuffBand + 0.08f * underArmBand);

            var zScale = 1f;
            zScale += chestDelta * (0.78f * chestBand + 0.34f * waistBand + 0.16f * hemBand + 0.06f * underArmBand);
            zScale += widthDelta * (0.04f * hemBand + 0.03f * zAbs01 * chestBand);

            var xOffset = Mathf.Sign(relative.x) * (
                halfWidth * widthDelta * (0.02f * shoulderBand + 0.04f * cuffBand + 0.04f * sleeveBand + 0.03f * underArmBand));
            var zOffset = Mathf.Sign(relative.z) * (
                halfDepth * chestDelta * (0.03f * chestBand + 0.02f * waistBand + 0.015f * underArmBand));

            var yOffset =
                size.y * lengthDelta * (
                    -0.42f * hemBand
                    -0.10f * waistBand
                    -0.02f * chestBand
                    +0.02f * sleeveBand * cuffBand);

            var hoodDamping = 1f - hoodBand * 0.92f;
            xScale = 1f + (xScale - 1f) * hoodDamping;
            zScale = 1f + (zScale - 1f) * hoodDamping;
            xOffset *= hoodDamping;
            zOffset *= hoodDamping;
            yOffset *= hoodDamping;

            var shellNormal = normals != null && normals.Length == vertices.Length ? normals[i] : Vector3.forward;
            var shellWeight = Mathf.Clamp01(0.28f * chestBand + 0.22f * waistBand + 0.18f * hemBand + 0.10f * sleeveBand + 0.04f * shoulderBand);
            var shellOffset = shellNormal * (variant.ShellOffset * shellWeight);

            adjusted[i] = new Vector3(
                center.x + relative.x * xScale + xOffset + shellOffset.x,
                center.y + relative.y + yOffset + shellOffset.y,
                center.z + relative.z * zScale + zOffset + shellOffset.z);
        }

        result.vertices = adjusted;
        result.RecalculateNormals();
        result.RecalculateTangents();
        result.RecalculateBounds();
        return result;
    }

    private static void SaveMeshAsset(string assetPath, Mesh mesh)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(mesh), assetPath);
            return;
        }

        EditorUtility.CopySerialized(mesh, existing);
        EditorUtility.SetDirty(existing);
    }

    private static float Band(float value01, float center01, float halfWidth01)
    {
        if (halfWidth01 <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - Mathf.Abs(value01 - center01) / halfWidth01);
    }

    private static float SmoothStepRange(float start, float end, float value)
    {
        if (value <= start)
        {
            return 0f;
        }

        if (value >= end)
        {
            return 1f;
        }

        var t = Mathf.InverseLerp(start, end, value);
        return t * t * (3f - 2f * t);
    }

    private static void PrefixRigNames(Transform root)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == root)
            {
                continue;
            }

            if (child.name.StartsWith(GarmentBonePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            child.name = $"{GarmentBonePrefix}{child.name}";
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private readonly struct VariantDefinition
    {
        public readonly string Size;
        public readonly float WidthInches;
        public readonly float LengthInches;
        public readonly float ChestInches;
        public readonly float ShellOffset;

        public VariantDefinition(string size, float widthInches, float lengthInches, float chestInches, float shellOffset)
        {
            Size = size;
            WidthInches = widthInches;
            LengthInches = lengthInches;
            ChestInches = chestInches;
            ShellOffset = shellOffset;
        }
    }
}
