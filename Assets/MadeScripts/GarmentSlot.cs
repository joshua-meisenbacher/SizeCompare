using UnityEngine;
public class GarmentSlot : MonoBehaviour
{
    private const string GarmentBonePrefix = "Garment_";

    [SerializeField] private Transform garmentAnchor;
    private GameObject currentGarmentInstance;

    public void SetAnchor(Transform anchor)
    {
        garmentAnchor = anchor;
    }

    public void Clear()
    {
        if (currentGarmentInstance != null)
        {
            Destroy(currentGarmentInstance);
            currentGarmentInstance = null;
        }
    }

    public void Equip(GameObject garmentPrefab)
    {
        if (garmentAnchor == null)
        {
            garmentAnchor = transform;
        }

        Clear();

        if (garmentPrefab == null)
        {
            return;
        }

        currentGarmentInstance = Instantiate(garmentPrefab, garmentAnchor);
        currentGarmentInstance.transform.localPosition = Vector3.zero;
        currentGarmentInstance.transform.localRotation = Quaternion.identity;
        currentGarmentInstance.transform.localScale = Vector3.one;
        PrefixRigNames(currentGarmentInstance.transform);

        var poseDriver = currentGarmentInstance.GetComponent<FixedGarmentPoseDriver>();
        if (poseDriver != null)
        {
            poseDriver.BindToTarget(garmentAnchor);
        }
    }

    private static void PrefixRigNames(Transform root)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == root)
            {
                continue;
            }

            if (child.name.StartsWith(GarmentBonePrefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            child.name = $"{GarmentBonePrefix}{child.name}";
        }
    }
}
