using System.Collections.Generic;
using UnityEngine;

public struct ClippingResult
{
    public bool chest;
    public bool armpit;
    public bool upperArm;
    public bool forearm;
    public bool wrist;
    public bool neck;
    public bool hem;

    public bool HasAnyClip()
    {
        return chest || armpit || upperArm || forearm || wrist || neck || hem;
    }

    public static ClippingResult Combine(ClippingResult a, ClippingResult b)
    {
        return new ClippingResult
        {
            chest = a.chest || b.chest,
            armpit = a.armpit || b.armpit,
            upperArm = a.upperArm || b.upperArm,
            forearm = a.forearm || b.forearm,
            wrist = a.wrist || b.wrist,
            neck = a.neck || b.neck,
            hem = a.hem || b.hem
        };
    }
}

[DisallowMultipleComponent]
public class HoodieClippingDetector : MonoBehaviour
{
    [System.Serializable]
    public class RegionProbe
    {
        public string region;
        public List<Collider> bodyColliders = new List<Collider>();
        public List<Collider> garmentColliders = new List<Collider>();
    }

    private static HoodieClippingDetector _active;

    [Tooltip("Define collider pairs for each region: chest, armpit, upperArm, forearm, wrist, neck, hem.")]
    public List<RegionProbe> probes = new List<RegionProbe>();

    private void OnEnable()
    {
        _active = this;
    }

    private void OnDisable()
    {
        if (_active == this)
        {
            _active = null;
        }
    }

    public static ClippingResult Scan()
    {
        if (_active == null)
        {
            var found = FindObjectOfType<HoodieClippingDetector>();
            if (found != null)
            {
                _active = found;
            }
        }

        if (_active == null)
        {
            Debug.LogWarning("HoodieClippingDetector.Scan could not find an active detector in the scene.");
            return default;
        }

        return _active.ScanInternal();
    }

    private ClippingResult ScanInternal()
    {
        var result = default(ClippingResult);

        for (var i = 0; i < probes.Count; i++)
        {
            var probe = probes[i];
            if (probe == null || string.IsNullOrWhiteSpace(probe.region))
            {
                continue;
            }

            var regionClip = RegionClips(probe);
            var key = probe.region.Trim().ToLowerInvariant();

            switch (key)
            {
                case "chest":
                    result.chest |= regionClip;
                    break;
                case "armpit":
                    result.armpit |= regionClip;
                    break;
                case "upperarm":
                case "upper_arm":
                    result.upperArm |= regionClip;
                    break;
                case "forearm":
                    result.forearm |= regionClip;
                    break;
                case "wrist":
                    result.wrist |= regionClip;
                    break;
                case "neck":
                    result.neck |= regionClip;
                    break;
                case "hem":
                    result.hem |= regionClip;
                    break;
            }
        }

        return result;
    }

    private static bool RegionClips(RegionProbe probe)
    {
        if (probe.bodyColliders == null || probe.garmentColliders == null)
        {
            return false;
        }

        for (var i = 0; i < probe.bodyColliders.Count; i++)
        {
            var body = probe.bodyColliders[i];
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy)
            {
                continue;
            }

            var bodyBounds = body.bounds;

            for (var j = 0; j < probe.garmentColliders.Count; j++)
            {
                var garment = probe.garmentColliders[j];
                if (garment == null || !garment.enabled || !garment.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (bodyBounds.Intersects(garment.bounds))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
