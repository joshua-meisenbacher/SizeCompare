using System.Text;
using UMA;
using UnityEditor;
using UnityEngine;

public static class RepairedHoodieUmaDebug
{
    private const string ReferencePrefabPath = "Assets/UMA/Content/Contrib/MaleHoodie/MaleHoodie_Skinned.prefab";
    private const string ReferenceFbxPath = "Assets/MaleHoodie_Skinned.fbx";
    private const string ReferenceSlotPath = "Assets/UMA/Content/Contrib/MaleHoodie/MaleHoodie_Slot.asset";
    private static readonly string[] SourcePaths =
    {
        "Assets/Generated/RepairedHoodies/Hoodie_S_Repaired.fbx",
        "Assets/Generated/RepairedHoodies/Hoodie_M_Repaired.fbx",
        "Assets/Generated/RepairedHoodies/Hoodie_L_Repaired.fbx",
        "Assets/Generated/RepairedHoodies/Hoodie_XL_Repaired.fbx",
    };
    private static readonly string[] SlotPaths =
    {
        "Assets/Generated/HoodieSizes/Slots/MaleHoodie_Slot_S_Slot.asset",
        "Assets/Generated/HoodieSizes/Slots/MaleHoodie_Slot_M_Slot.asset",
        "Assets/Generated/HoodieSizes/Slots/MaleHoodie_Slot_L_Slot.asset",
        "Assets/Generated/HoodieSizes/Slots/MaleHoodie_Slot_XL_Slot.asset",
    };

    [MenuItem("Tools/Hoodie/Debug UMA Reference Hoodie")]
    public static void DumpReference()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[RepairedHoodieUmaDebug] Reference prefab missing.");
            return;
        }

        var instance = Object.Instantiate(prefab);
        try
        {
            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null)
            {
                Debug.LogError("[RepairedHoodieUmaDebug] No SkinnedMeshRenderer found.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[RepairedHoodieUmaDebug] reference hoodie");
            sb.AppendLine($"renderer={renderer.name}");
            sb.AppendLine($"sharedMesh={(renderer.sharedMesh != null ? renderer.sharedMesh.name : "null")}");
            sb.AppendLine($"meshBindposes={(renderer.sharedMesh != null ? renderer.sharedMesh.bindposes.Length : -1)}");
            sb.AppendLine($"rendererBones={(renderer.bones != null ? renderer.bones.Length : -1)}");
            sb.AppendLine($"rootBone={(renderer.rootBone != null ? renderer.rootBone.name : "null")}");

            if (renderer.bones != null)
            {
                for (var i = 0; i < renderer.bones.Length; i++)
                {
                    var bone = renderer.bones[i];
                    sb.AppendLine($"bone[{i}]={(bone != null ? bone.name : "null")}");
                }
            }

            Debug.Log(sb.ToString());
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [MenuItem("Tools/Hoodie/Debug FBX Reference Hoodie")]
    public static void DumpReferenceFbx()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceFbxPath);
        if (prefab == null)
        {
            Debug.LogError("[RepairedHoodieUmaDebug] Reference FBX missing.");
            return;
        }

        var instance = Object.Instantiate(prefab);
        try
        {
            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null)
            {
                Debug.LogError("[RepairedHoodieUmaDebug] No SkinnedMeshRenderer found in FBX.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[RepairedHoodieUmaDebug] reference FBX hoodie");
            sb.AppendLine($"renderer={renderer.name}");
            sb.AppendLine($"sharedMesh={(renderer.sharedMesh != null ? renderer.sharedMesh.name : "null")}");
            sb.AppendLine($"meshBindposes={(renderer.sharedMesh != null ? renderer.sharedMesh.bindposes.Length : -1)}");
            sb.AppendLine($"rendererBones={(renderer.bones != null ? renderer.bones.Length : -1)}");
            sb.AppendLine($"rootBone={(renderer.rootBone != null ? renderer.rootBone.name : "null")}");
            Debug.Log(sb.ToString());
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [MenuItem("Tools/Hoodie/Debug Repaired Hoodie Bounds")]
    public static void DumpSourceBounds()
    {
        foreach (var path in SourcePaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[RepairedHoodieUmaDebug] Missing source prefab at {path}");
                continue;
            }

            var instance = Object.Instantiate(prefab);
            try
            {
                var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var mf = instance.GetComponentInChildren<MeshFilter>(true);
                Mesh mesh = null;
                string kind = null;
                if (smr != null && smr.sharedMesh != null)
                {
                    mesh = smr.sharedMesh;
                    kind = "SkinnedMeshRenderer";
                }
                else if (mf != null && mf.sharedMesh != null)
                {
                    mesh = mf.sharedMesh;
                    kind = "MeshFilter";
                }

                if (mesh == null)
                {
                    Debug.LogWarning($"[RepairedHoodieUmaDebug] No mesh found in {path}");
                    continue;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"[RepairedHoodieUmaDebug] source='{path}' kind={kind} mesh='{mesh.name}'");
                sb.AppendLine($"boundsCenter={mesh.bounds.center}");
                sb.AppendLine($"boundsSize={mesh.bounds.size}");
                sb.AppendLine($"verts={mesh.vertexCount}");
                if (smr != null)
                {
                    sb.AppendLine($"rendererBones={(smr.bones != null ? smr.bones.Length : -1)}");
                    sb.AppendLine($"rootBone={(smr.rootBone != null ? smr.rootBone.name : "null")}");
                    sb.AppendLine($"meshBindposes={(smr.sharedMesh != null && smr.sharedMesh.bindposes != null ? smr.sharedMesh.bindposes.Length : -1)}");
                    sb.AppendLine($"meshBoneWeights={(smr.sharedMesh != null && smr.sharedMesh.boneWeights != null ? smr.sharedMesh.boneWeights.Length : -1)}");
                }

                Debug.Log(sb.ToString());
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    [MenuItem("Tools/Hoodie/Debug Generated UMA Slots")]
    public static void DumpGeneratedSlots()
    {
        foreach (var path in SlotPaths)
        {
            var slot = AssetDatabase.LoadAssetAtPath<SlotDataAsset>(path);
            if (slot == null)
            {
                Debug.LogWarning($"[RepairedHoodieUmaDebug] Missing slot at {path}");
                continue;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[RepairedHoodieUmaDebug] slot='{path}'");
            sb.AppendLine($"slotName={slot.slotName}");
            sb.AppendLine($"rootBone={slot.meshData?.RootBoneName ?? "null"}");
            sb.AppendLine($"meshVerts={(slot.meshData?.vertices != null ? slot.meshData.vertices.Length : -1)}");
            sb.AppendLine($"bindPoses={(slot.meshData?.bindPoses != null ? slot.meshData.bindPoses.Length : -1)}");
            sb.AppendLine($"boneWeights={(slot.meshData?.boneWeights != null ? slot.meshData.boneWeights.Length : -1)}");
            sb.AppendLine($"bones={(slot.meshData?.boneNameHashes != null ? slot.meshData.boneNameHashes.Length : -1)}");

            if (slot.meshData?.vertices != null && slot.meshData.vertices.Length > 0)
            {
                var min = slot.meshData.vertices[0];
                var max = slot.meshData.vertices[0];
                for (var i = 1; i < slot.meshData.vertices.Length; i++)
                {
                    var vertex = slot.meshData.vertices[i];
                    min = Vector3.Min(min, vertex);
                    max = Vector3.Max(max, vertex);
                }

                var center = (min + max) * 0.5f;
                var size = max - min;
                sb.AppendLine($"vertexBoundsCenter={center}");
                sb.AppendLine($"vertexBoundsSize={size}");
            }

            Debug.Log(sb.ToString(), slot);
        }
    }

    [MenuItem("Tools/Hoodie/Compare Canonical Slot To Generated Slots")]
    public static void CompareReferenceSlotToGeneratedSlots()
    {
        var referenceSlot = AssetDatabase.LoadAssetAtPath<SlotDataAsset>(ReferenceSlotPath);
        if (referenceSlot == null)
        {
            Debug.LogError("[RepairedHoodieUmaDebug] Canonical reference slot missing.");
            return;
        }

        var referenceSummary = BuildSlotSummary(referenceSlot, ReferenceSlotPath);
        Debug.Log(FormatSlotSummary(referenceSummary), referenceSlot);

        foreach (var path in SlotPaths)
        {
            var slot = AssetDatabase.LoadAssetAtPath<SlotDataAsset>(path);
            if (slot == null)
            {
                Debug.LogWarning($"[RepairedHoodieUmaDebug] Missing generated slot at {path}");
                continue;
            }

            var generatedSummary = BuildSlotSummary(slot, path);
            var sb = new StringBuilder();
            sb.AppendLine($"[RepairedHoodieUmaDebug] compare canonical -> '{path}'");
            sb.AppendLine($"slotName canonical='{referenceSummary.SlotName}' generated='{generatedSummary.SlotName}'");
            sb.AppendLine($"rootBone canonical='{referenceSummary.RootBone}' generated='{generatedSummary.RootBone}'");
            sb.AppendLine($"meshVerts canonical={referenceSummary.VertexCount} generated={generatedSummary.VertexCount}");
            sb.AppendLine($"bindPoses canonical={referenceSummary.BindPoseCount} generated={generatedSummary.BindPoseCount}");
            sb.AppendLine($"boneWeights canonical={referenceSummary.BoneWeightCount} generated={generatedSummary.BoneWeightCount}");
            sb.AppendLine($"bones canonical={referenceSummary.BoneCount} generated={generatedSummary.BoneCount}");
            sb.AppendLine($"vertexBoundsCenter canonical={referenceSummary.BoundsCenter} generated={generatedSummary.BoundsCenter}");
            sb.AppendLine($"vertexBoundsSize canonical={referenceSummary.BoundsSize} generated={generatedSummary.BoundsSize}");
            Debug.Log(sb.ToString(), slot);
        }
    }

    private static SlotSummary BuildSlotSummary(SlotDataAsset slot, string path)
    {
        var summary = new SlotSummary
        {
            Path = path,
            SlotName = slot.slotName,
            RootBone = slot.meshData?.RootBoneName ?? "null",
            VertexCount = slot.meshData?.vertices != null ? slot.meshData.vertices.Length : -1,
            BindPoseCount = slot.meshData?.bindPoses != null ? slot.meshData.bindPoses.Length : -1,
            BoneWeightCount = slot.meshData?.boneWeights != null ? slot.meshData.boneWeights.Length : -1,
            BoneCount = slot.meshData?.boneNameHashes != null ? slot.meshData.boneNameHashes.Length : -1,
        };

        if (slot.meshData?.vertices != null && slot.meshData.vertices.Length > 0)
        {
            var min = slot.meshData.vertices[0];
            var max = slot.meshData.vertices[0];
            for (var i = 1; i < slot.meshData.vertices.Length; i++)
            {
                var vertex = slot.meshData.vertices[i];
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            summary.BoundsCenter = (min + max) * 0.5f;
            summary.BoundsSize = max - min;
        }

        return summary;
    }

    private static string FormatSlotSummary(SlotSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[RepairedHoodieUmaDebug] canonical slot='{summary.Path}'");
        sb.AppendLine($"slotName={summary.SlotName}");
        sb.AppendLine($"rootBone={summary.RootBone}");
        sb.AppendLine($"meshVerts={summary.VertexCount}");
        sb.AppendLine($"bindPoses={summary.BindPoseCount}");
        sb.AppendLine($"boneWeights={summary.BoneWeightCount}");
        sb.AppendLine($"bones={summary.BoneCount}");
        sb.AppendLine($"vertexBoundsCenter={summary.BoundsCenter}");
        sb.AppendLine($"vertexBoundsSize={summary.BoundsSize}");
        return sb.ToString();
    }

    private struct SlotSummary
    {
        public string Path;
        public string SlotName;
        public string RootBone;
        public int VertexCount;
        public int BindPoseCount;
        public int BoneWeightCount;
        public int BoneCount;
        public Vector3 BoundsCenter;
        public Vector3 BoundsSize;
    }
}
