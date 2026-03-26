using UnityEngine;
using System.Collections.Generic;
using System.Text;
using UMA.CharacterSystem;

public class BoneAttacher : MonoBehaviour
{
    private static readonly SkinnedMeshRenderer[] EmptyHoodies = new SkinnedMeshRenderer[0];

    [Header("Hoodie Renderers - Male")]
    public SkinnedMeshRenderer[] maleHoodies;

    [Header("Hoodie Renderers - Female")]
    public SkinnedMeshRenderer[] femaleHoodies;

    [Header("UMA Skeleton Roots")]
    public Transform maleSkeletonRoot;
    public Transform femaleSkeletonRoot;

    [Header("Avatar Parents")]
    public Transform maleAvatarParent;
    public Transform femaleAvatarParent;

    private readonly HashSet<string> _warnings = new HashSet<string>();

    public void AttachToMale()
    {
        AttachSet(ref maleHoodies, ref maleSkeletonRoot, ref maleAvatarParent, true);
    }

    public void AttachToFemale()
    {
        AttachSet(ref femaleHoodies, ref femaleSkeletonRoot, ref femaleAvatarParent, false);
    }

    public void DumpMaleState(string reason)
    {
        DumpState(maleHoodies, maleSkeletonRoot, "Male", reason);
    }

    public void DumpFemaleState(string reason)
    {
        DumpState(femaleHoodies, femaleSkeletonRoot, "Female", reason);
    }

    void AttachSet(ref SkinnedMeshRenderer[] hoodies, ref Transform skeletonRoot, ref Transform avatarParent, bool male)
    {
        ResolveTargets(ref hoodies, ref skeletonRoot, ref avatarParent, male);

        if (skeletonRoot == null)
        {
            WarnOnce($"skeleton-root:{male}", $"BoneAttacher could not resolve the {(male ? "male" : "female")} skeleton root.");
            return;
        }

        if (hoodies == null || hoodies.Length == 0)
        {
            return;
        }

        var boneMap = BuildBoneMap(skeletonRoot);

        foreach (var hoodie in hoodies)
        {
            if (hoodie != null)
                BindHoodie(hoodie, boneMap, skeletonRoot, avatarParent);
        }
    }

    void ResolveTargets(ref SkinnedMeshRenderer[] hoodies, ref Transform skeletonRoot, ref Transform avatarParent, bool male)
    {
        var avatar = FindAvatar(male);
        if (avatar != null)
        {
            if (avatarParent == null)
            {
                avatarParent = avatar.transform;
            }

            if (skeletonRoot == null && avatar.umaData != null && avatar.umaData.skeleton != null)
            {
                skeletonRoot = avatar.umaData.skeleton.GetRootTransform();
            }
        }

        if (!HasValidHoodies(hoodies))
        {
            hoodies = FindHoodieRenderers(male);
        }
    }

    DynamicCharacterAvatar FindAvatar(bool male)
    {
        var avatars = Resources.FindObjectsOfTypeAll<DynamicCharacterAvatar>();
        for (int i = 0; i < avatars.Length; i++)
        {
            var avatar = avatars[i];
            if (avatar == null || !avatar.gameObject.scene.IsValid())
            {
                continue;
            }

            var raceName = avatar.activeRace != null ? avatar.activeRace.name : string.Empty;
            var objectName = avatar.name;
            if (male)
            {
                var raceIsMale = raceName.IndexOf("male", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                                 raceName.IndexOf("female", System.StringComparison.OrdinalIgnoreCase) < 0;
                var objectIsMale = objectName.IndexOf("male", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                                   objectName.IndexOf("female", System.StringComparison.OrdinalIgnoreCase) < 0;
                if (raceIsMale || objectIsMale)
                {
                    return avatar;
                }
            }
            else
            {
                if (raceName.IndexOf("female", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objectName.IndexOf("female", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return avatar;
                }
            }
        }

        return null;
    }

    SkinnedMeshRenderer[] FindHoodieRenderers(bool male)
    {
        var rootName = male ? "HoodieSizes_Male" : "HoodieSizes _Female";
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null || !obj.scene.IsValid() || obj.name != rootName)
            {
                continue;
            }

            var renderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var hoodieRenderers = new List<SkinnedMeshRenderer>();
            for (int j = 0; j < renderers.Length; j++)
            {
                var renderer = renderers[j];
                if (renderer != null && renderer.name.IndexOf("hoodie", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hoodieRenderers.Add(renderer);
                }
            }

            return hoodieRenderers.ToArray();
        }

        return EmptyHoodies;
    }

    Dictionary<string, Transform> BuildBoneMap(Transform root)
    {
        var map = new Dictionary<string, Transform>();
        foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
        {
            if (!map.ContainsKey(bone.name))
                map.Add(bone.name, bone);
        }
        return map;
    }

    void BindHoodie(
        SkinnedMeshRenderer hoodie,
        Dictionary<string, Transform> boneMap,
        Transform skeletonRoot,
        Transform avatarParent)
    {
        if (hoodie == null || hoodie.sharedMesh == null)
        {
            Debug.LogWarning("Hoodie or shared mesh missing.");
            return;
        }

        Transform[] reboundBones = new Transform[hoodie.bones.Length];

        for (int i = 0; i < hoodie.bones.Length; i++)
        {
            Transform sourceBone = hoodie.bones[i];
            if (sourceBone == null)
            {
                reboundBones[i] = null;
                continue;
            }

            if (boneMap.TryGetValue(sourceBone.name, out Transform mappedBone))
            {
                reboundBones[i] = mappedBone;
            }
            else
            {
                Debug.LogWarning($"Missing bone on avatar skeleton: {sourceBone.name}");
                reboundBones[i] = null;
            }
        }

        hoodie.bones = reboundBones;

        if (boneMap.TryGetValue("Hips", out Transform hips))
        {
            hoodie.rootBone = hips;
        }
        else if (hoodie.rootBone != null && boneMap.TryGetValue(hoodie.rootBone.name, out Transform mappedRoot))
        {
            hoodie.rootBone = mappedRoot;
        }

        if (avatarParent != null)
        {
            var attachmentRoot = GetAttachmentRoot(hoodie.transform);
            if (attachmentRoot != null)
            {
                attachmentRoot.SetParent(avatarParent, false);
                attachmentRoot.localPosition = Vector3.zero;
                attachmentRoot.localRotation = Quaternion.identity;
                attachmentRoot.localScale = Vector3.one;
            }
        }

        hoodie.enabled = true;
        hoodie.updateWhenOffscreen = true;
        hoodie.localBounds = ExpandBounds(hoodie.localBounds, hoodie.sharedMesh != null ? hoodie.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one), 1.75f);
    }

    Transform GetAttachmentRoot(Transform rendererTransform)
    {
        if (rendererTransform == null)
        {
            return null;
        }

        Transform hoodieRoot = null;
        var current = rendererTransform;
        while (current != null)
        {
            if (current.name.IndexOf("HoodieSizes", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hoodieRoot = current;
            }

            current = current.parent;
        }

        return hoodieRoot != null ? hoodieRoot : rendererTransform;
    }

    void WarnOnce(string key, string message)
    {
        if (_warnings.Add(key))
        {
            Debug.LogWarning(message, this);
        }
    }

    bool HasValidHoodies(SkinnedMeshRenderer[] hoodies)
    {
        if (hoodies == null || hoodies.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < hoodies.Length; i++)
        {
            if (hoodies[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    void DumpState(SkinnedMeshRenderer[] hoodies, Transform skeletonRoot, string label, string reason)
    {
        Debug.Log($"[BoneAttacher] DumpState label={label} reason={reason}", this);
        Debug.Log($"[BoneAttacher] skeletonRoot={(skeletonRoot != null ? skeletonRoot.name : "null")} worldPos={(skeletonRoot != null ? skeletonRoot.position.ToString("F4") : "n/a")}", this);

        var roots = new HashSet<Transform>();
        if (hoodies != null)
        {
            for (int i = 0; i < hoodies.Length; i++)
            {
                var hoodie = hoodies[i];
                if (hoodie == null)
                {
                    Debug.Log($"[BoneAttacher] hoodie[{i}]=null", this);
                    continue;
                }

                var attachmentRoot = GetAttachmentRoot(hoodie.transform);
                if (attachmentRoot != null)
                {
                    roots.Add(attachmentRoot);
                }

                Debug.Log($"[BoneAttacher] {DescribeTransform($"hoodie[{i}] renderer", hoodie.transform)}", this);
                Debug.Log($"[BoneAttacher] hoodie[{i}] rootBone={(hoodie.rootBone != null ? hoodie.rootBone.name : "null")} rootBoneWorld={(hoodie.rootBone != null ? hoodie.rootBone.position.ToString("F4") : "n/a")}", this);
                Debug.Log($"[BoneAttacher] hoodie[{i}] activeSelf={hoodie.gameObject.activeSelf} activeInHierarchy={hoodie.gameObject.activeInHierarchy}", this);
            }
        }

        foreach (var root in roots)
        {
            Debug.Log(DescribeHierarchy(root, 0, 4), this);
        }

        var sceneRoots = Resources.FindObjectsOfTypeAll<GameObject>();
        int maleRootCount = 0;
        int femaleRootCount = 0;
        for (int i = 0; i < sceneRoots.Length; i++)
        {
            var obj = sceneRoots[i];
            if (obj == null || !obj.scene.IsValid())
            {
                continue;
            }

            if (obj.name == "HoodieSizes_Male") maleRootCount++;
            if (obj.name == "HoodieSizes _Female") femaleRootCount++;
        }

        Debug.Log($"[BoneAttacher] sceneRootCounts male={maleRootCount} female={femaleRootCount}", this);
    }

    string DescribeHierarchy(Transform root, int depth, int maxDepth)
    {
        if (root == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}{DescribeTransform("node", root)} activeSelf={root.gameObject.activeSelf} activeInHierarchy={root.gameObject.activeInHierarchy}");
        if (depth >= maxDepth)
        {
            return sb.ToString();
        }

        for (int i = 0; i < root.childCount; i++)
        {
            sb.Append(DescribeHierarchy(root.GetChild(i), depth + 1, maxDepth));
        }

        return sb.ToString();
    }

    string DescribeTransform(string prefix, Transform transform)
    {
        var parentName = transform.parent != null ? transform.parent.name : "null";
        return $"{prefix} name={transform.name} parent={parentName} worldPos={transform.position.ToString("F4")} localPos={transform.localPosition.ToString("F4")} localRot={transform.localRotation.eulerAngles.ToString("F2")} localScale={transform.localScale.ToString("F4")}";
    }

    Bounds ExpandBounds(Bounds currentBounds, Bounds meshBounds, float multiplier)
    {
        var bounds = currentBounds;
        var size = bounds.size;
        if (size == Vector3.zero)
        {
            size = meshBounds.size;
        }

        if (size == Vector3.zero)
        {
            size = Vector3.one;
        }

        bounds.center = meshBounds.center;
        bounds.size = size * multiplier;
        return bounds;
    }
}
