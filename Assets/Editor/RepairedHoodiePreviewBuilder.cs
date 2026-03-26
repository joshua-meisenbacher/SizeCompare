using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RepairedHoodiePreviewBuilder
{
    private const string ReferencePrefabPath = "Assets/MaleHoodie_Skinned.fbx";
    private const string BaselinePreviewPrefabPath = "Assets/Resources/Generated/RepairedHoodiePrefabs/Hoodie_M_Preview.prefab";
    private const string OutputRoot = "Assets/Resources/Generated/RepairedHoodiePrefabs";

    private static readonly string[] Sizes = { "S", "M", "L", "XL" };

    public static void BuildAll()
    {
        Directory.CreateDirectory(OutputRoot);

        foreach (var size in Sizes)
        {
            BuildOne(size);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RepairedHoodiePreviewBuilder] Built all preview prefabs.");
    }

    private static void BuildOne(string size)
    {
        var referenceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePrefabPath);
        if (referenceModel == null)
        {
            throw new Exception($"Missing reference model for size {size}.");
        }

        var baselineRoot = LoadBaselineRoot(size);
        var referenceRoot = PrefabUtility.InstantiatePrefab(referenceModel) as GameObject;

        try
        {
            var baselineRenderer = baselineRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var referenceRenderer = referenceRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);

            if (baselineRenderer == null || baselineRenderer.sharedMesh == null || referenceRenderer == null || referenceRenderer.sharedMesh == null)
            {
                throw new Exception($"Invalid baseline/reference hierarchy for size {size}.");
            }

            var generatedMesh = BuildFixedGarmentMesh(baselineRenderer.sharedMesh, size);
            var meshAssetPath = $"{OutputRoot}/Hoodie_{size}_Preview.asset";
            ReplaceAsset(meshAssetPath);
            AssetDatabase.CreateAsset(generatedMesh, meshAssetPath);

            referenceRenderer.sharedMesh = generatedMesh;
            referenceRenderer.name = $"hoodie_blue_{size}_preview";
            referenceRenderer.rootBone = FindChildByName(referenceRoot.transform, "Hips") ??
                                        FindChildByName(referenceRoot.transform, "LowerBack");
            referenceRenderer.updateWhenOffscreen = true;
            referenceRenderer.localBounds = ExpandBounds(referenceRenderer.localBounds, 1.5f);

            var prefabPath = $"{OutputRoot}/Hoodie_{size}_Preview.prefab";
            ReplaceAsset(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(referenceRoot, prefabPath);
            Debug.Log($"[RepairedHoodiePreviewBuilder] Built {prefabPath}.");
        }
        finally
        {
            if (baselineRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(baselineRoot);
            }

            if (referenceRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(referenceRoot);
            }
        }
    }

    private static GameObject LoadBaselineRoot(string size)
    {
        if (size == "M")
        {
            var mediumBaseline = AssetDatabase.LoadAssetAtPath<GameObject>(BaselinePreviewPrefabPath);
            if (mediumBaseline != null)
            {
                return PrefabUtility.InstantiatePrefab(mediumBaseline) as GameObject;
            }
        }

        var mediumSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/RepairedHoodies/Hoodie_M_Repaired3.fbx") ??
                           AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/RepairedHoodies/Hoodie_M_Repaired.fbx") ??
                           AssetDatabase.LoadAssetAtPath<GameObject>("Assets/exports/Hoodie_M.fbx");

        if (mediumSource == null)
        {
            throw new Exception("Missing medium baseline source.");
        }

        return PrefabUtility.InstantiatePrefab(mediumSource) as GameObject;
    }

    private static Mesh BuildFixedGarmentMesh(Mesh baselineMesh, string size)
    {
        if (baselineMesh == null)
        {
            throw new Exception($"Missing baseline mesh data for size {size}.");
        }

        var mesh = UnityEngine.Object.Instantiate(baselineMesh);
        mesh.name = $"hoodie_blue_{size}_preview";
        mesh.vertices = BuildSizedVertices(baselineMesh.vertices, baselineMesh.bounds, size);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Vector3[] BuildSizedVertices(Vector3[] vertices, Bounds bounds, string size)
    {
        var result = new Vector3[vertices.Length];
        var min = bounds.min;
        var max = bounds.max;
        var center = bounds.center;
        var meshSize = bounds.size;

        var profile = GetProfile(size);
        for (var i = 0; i < vertices.Length; i++)
        {
            result[i] = ApplyProfile(vertices[i], min, max, center, meshSize, profile);
        }

        return result;
    }

    private static Vector3 ApplyProfile(Vector3 vertex, Vector3 min, Vector3 max, Vector3 center, Vector3 meshSize, GarmentProfile profile)
    {
        var width = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)));
        var depth = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z)));
        var height = Mathf.Max(0.0001f, max.y - min.y);

        var yNorm = Mathf.InverseLerp(min.y, max.y, vertex.y);
        var torsoBand = Gaussian(yNorm, 0.50f, 0.22f);
        var chestBand = Gaussian(yNorm, 0.63f, 0.15f);
        var hemBand = Gaussian(yNorm, 0.15f, 0.10f);
        var shoulderBand = Gaussian(yNorm, 0.78f, 0.08f);
        var cuffBand = Gaussian(yNorm, 0.46f, 0.07f);
        var sleeveBand = Gaussian(yNorm, 0.55f, 0.14f);
        var outerArm = Mathf.SmoothStep(0.36f, 0.72f, Mathf.Abs(vertex.x) / width);
        var torsoCore = 1f - Mathf.SmoothStep(0.45f, 0.80f, Mathf.Abs(vertex.x) / width);

        var xDir = Mathf.Abs(vertex.x) < 0.0001f ? 0f : Mathf.Sign(vertex.x);
        var zDir = Mathf.Abs(vertex.z - center.z) < 0.0001f ? 0f : Mathf.Sign(vertex.z - center.z);

        var xOffset = 0f;
        xOffset += profile.TorsoWidth * torsoBand * torsoCore;
        xOffset += profile.ChestWidth * chestBand * Mathf.Lerp(0.4f, 1f, torsoCore);
        xOffset += profile.ShoulderWidth * shoulderBand * Mathf.Lerp(0.5f, 1f, outerArm);
        xOffset += profile.HemWidth * hemBand * Mathf.Lerp(0.25f, 1f, torsoCore);
        xOffset += profile.SleeveWidth * sleeveBand * outerArm;
        xOffset += profile.CuffWidth * cuffBand * outerArm;

        var zOffset = 0f;
        zOffset += profile.DepthEase * torsoBand * 0.8f;
        zOffset += profile.DepthEase * chestBand * 0.7f;
        zOffset += profile.CuffWidth * cuffBand * outerArm * 0.6f;

        var yOffset = 0f;
        yOffset += profile.BodyLength * Mathf.InverseLerp(0.20f, 0.90f, yNorm);
        yOffset += profile.SleeveLength * outerArm * Mathf.InverseLerp(0.35f, 0.80f, yNorm);

        vertex.x += xDir * width * xOffset;
        vertex.z += zDir * depth * zOffset;
        vertex.y += height * yOffset;
        return vertex;
    }

    private static GarmentProfile GetProfile(string size)
    {
        switch (size)
        {
            case "S":
                return new GarmentProfile(-0.070f, -0.085f, -0.060f, -0.050f, -0.035f, -0.020f, -0.040f, -0.030f, -0.025f);
            case "M":
                return new GarmentProfile(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            case "L":
                return new GarmentProfile(0.085f, 0.095f, 0.075f, 0.060f, 0.040f, 0.025f, 0.050f, 0.035f, 0.030f);
            case "XL":
                return new GarmentProfile(0.155f, 0.170f, 0.130f, 0.105f, 0.070f, 0.045f, 0.085f, 0.060f, 0.050f);
            default:
                return new GarmentProfile(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        }
    }

    private static float Gaussian(float value, float center, float width)
    {
        var delta = (value - center) / Mathf.Max(0.0001f, width);
        return Mathf.Exp(-delta * delta);
    }

    private static void ReplaceAsset(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == childName)
            {
                return transform;
            }
        }

        return null;
    }

    private static Bounds ExpandBounds(Bounds bounds, float multiplier)
    {
        var size = bounds.size;
        if (size == Vector3.zero)
        {
            size = Vector3.one;
        }

        bounds.size = size * multiplier;
        return bounds;
    }

    private readonly struct GarmentProfile
    {
        public readonly float TorsoWidth;
        public readonly float ChestWidth;
        public readonly float HemWidth;
        public readonly float ShoulderWidth;
        public readonly float SleeveWidth;
        public readonly float CuffWidth;
        public readonly float DepthEase;
        public readonly float BodyLength;
        public readonly float SleeveLength;

        public GarmentProfile(
            float torsoWidth,
            float chestWidth,
            float hemWidth,
            float shoulderWidth,
            float sleeveWidth,
            float cuffWidth,
            float depthEase,
            float bodyLength,
            float sleeveLength)
        {
            TorsoWidth = torsoWidth;
            ChestWidth = chestWidth;
            HemWidth = hemWidth;
            ShoulderWidth = shoulderWidth;
            SleeveWidth = sleeveWidth;
            CuffWidth = cuffWidth;
            DepthEase = depthEase;
            BodyLength = bodyLength;
            SleeveLength = sleeveLength;
        }
    }
}
