using System.Collections.Generic;
using UnityEngine;

public class FixedGarmentPoseDriver : MonoBehaviour
{
    private const string GarmentBonePrefix = "Garment_";

    [SerializeField] private string rootBoneName = "Global";

    private readonly List<BoneMap> mappedBones = new();
    private Transform targetRoot;
    private Transform targetRootBone;

    public void BindToTarget(Transform target)
    {
        targetRoot = target;
        targetRootBone = null;
        mappedBones.Clear();

        if (targetRoot == null)
        {
            return;
        }

        var sourceByName = BuildLookup(transform, stripGarmentPrefix: true);
        var targetByName = BuildLookup(targetRoot, stripGarmentPrefix: false);

        targetByName.TryGetValue(rootBoneName, out targetRootBone);
        if (targetRootBone != null && targetRootBone.parent != null)
        {
            transform.SetParent(targetRootBone.parent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        foreach (var pair in sourceByName)
        {
            if (!targetByName.TryGetValue(pair.Key, out var targetBone))
            {
                continue;
            }

            mappedBones.Add(new BoneMap(pair.Value, targetBone));
        }
    }

    private void LateUpdate()
    {
        if (targetRoot == null)
        {
            return;
        }

        for (var i = 0; i < mappedBones.Count; i++)
        {
            var mapping = mappedBones[i];
            if (mapping.Source == null || mapping.Target == null)
            {
                continue;
            }

            mapping.Source.localRotation = mapping.Target.localRotation;

            if (mapping.Source.name == rootBoneName)
            {
                mapping.Source.localPosition = mapping.Target.localPosition;
                mapping.Source.localRotation = mapping.Target.localRotation;
            }
        }
    }

    private static Dictionary<string, Transform> BuildLookup(Transform root, bool stripGarmentPrefix)
    {
        var lookup = new Dictionary<string, Transform>();
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            var key = child.name;
            if (stripGarmentPrefix && key.StartsWith(GarmentBonePrefix, System.StringComparison.Ordinal))
            {
                key = key.Substring(GarmentBonePrefix.Length);
            }

            if (!lookup.ContainsKey(key))
            {
                lookup.Add(key, child);
            }
        }

        return lookup;
    }

    private readonly struct BoneMap
    {
        public readonly Transform Source;
        public readonly Transform Target;

        public BoneMap(Transform source, Transform target)
        {
            Source = source;
            Target = target;
        }
    }
}
