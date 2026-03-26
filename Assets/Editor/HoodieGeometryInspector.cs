using System;
using UnityEditor;
using UnityEngine;

public static class HoodieGeometryInspector
{
    [MenuItem("Tools/Hoodie/Inspect Geometry")]
    public static void Inspect() => Run();

    public static void Run()
    {
        string[] assetPaths =
        {
            "Assets/exports/Hoodie_S.fbx",
            "Assets/exports/Hoodie_M.fbx",
            "Assets/exports/Hoodie_L.fbx",
            "Assets/exports/Hoodie_XL.fbx",
            "Assets/Generated/RepairedHoodies/Hoodie_S_Repaired.fbx",
            "Assets/Generated/RepairedHoodies/Hoodie_M_Repaired.fbx",
            "Assets/Generated/RepairedHoodies/Hoodie_M_Repaired2.fbx",
            "Assets/Generated/RepairedHoodies/Hoodie_M_Repaired3.fbx",
            "Assets/Generated/RepairedHoodies/Hoodie_L_Repaired.fbx",
            "Assets/Generated/RepairedHoodies/Hoodie_XL_Repaired.fbx",
            "Assets/Resources/Generated/RepairedHoodiePrefabs/Hoodie_S_Preview.prefab",
            "Assets/Resources/Generated/RepairedHoodiePrefabs/Hoodie_M_Preview.prefab",
            "Assets/Resources/Generated/RepairedHoodiePrefabs/Hoodie_L_Preview.prefab",
            "Assets/Resources/Generated/RepairedHoodiePrefabs/Hoodie_XL_Preview.prefab",
        };

        foreach (var path in assetPaths)
        {
            InspectAsset(path);
        }

        EditorApplication.Exit(0);
    }

    private static void InspectAsset(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
        {
            Debug.Log($"[HoodieGeometryInspector] ASSET {path} could not load as GameObject.");
            return;
        }

        Debug.Log($"[HoodieGeometryInspector] ASSET {path} root='{go.name}'.");

        var meshFilters = go.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            var m = mf.sharedMesh;
            if (m == null)
            {
                Debug.Log($"[HoodieGeometryInspector]   MeshFilter '{mf.name}' mesh=null.", mf);
                continue;
            }
            Debug.Log($"[HoodieGeometryInspector]   MeshFilter '{mf.name}' mesh='{m.name}' verts={m.vertexCount} tris={m.triangles.Length/3} boundsCenter={m.bounds.center} boundsSize={m.bounds.size} localPos={mf.transform.localPosition} localRot={mf.transform.localEulerAngles} localScale={mf.transform.localScale}.", mf);
        }

        var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in skinned)
        {
            var m = smr.sharedMesh;
            var mats = smr.sharedMaterials != null ? smr.sharedMaterials.Length : 0;
            Debug.Log($"[HoodieGeometryInspector]   Skinned '{smr.name}' mesh='{(m != null ? m.name : "null")}' verts={(m != null ? m.vertexCount : 0)} tris={(m != null ? m.triangles.Length/3 : 0)} meshBounds={(m != null ? m.bounds.size.ToString() : "n/a")} localBounds={smr.localBounds.size} bones={(smr.bones != null ? smr.bones.Length : 0)} rootBone='{(smr.rootBone != null ? smr.rootBone.name : "null")}' mats={mats} localPos={smr.transform.localPosition} localRot={smr.transform.localEulerAngles} localScale={smr.transform.localScale}.", smr);
        }
    }
}
