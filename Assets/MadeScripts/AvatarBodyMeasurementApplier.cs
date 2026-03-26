using System.Collections;
using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AvatarBodyMeasurementApplier : MonoBehaviour
{
    [System.Serializable]
    public struct BodyDnaProfile
    {
        public float height;
        public float upperWeight;
        public float lowerWeight;
        public float belly;
        public float waist;
        public float legsSize;
        public float armWidth;
        public float forearmWidth;
        public float armLength;
        public float forearmLength;

        public static BodyDnaProfile Default => new BodyDnaProfile
        {
            height = 0.5f,
            upperWeight = 0.5f,
            lowerWeight = 0.5f,
            belly = 0.5f,
            waist = 0.5f,
            legsSize = 0.5f,
            armWidth = 0.5f,
            forearmWidth = 0.5f,
            armLength = 0.5f,
            forearmLength = 0.5f
        };
    }

    private static readonly string[] RelevantDnaChannels =
    {
        "height",
        "upperWeight",
        "lowerWeight",
        "belly",
        "waist",
        "legsSize",
        "armWidth",
        "forearmWidth",
        "armLength",
        "forearmLength",
    };

    public DynamicCharacterAvatar avatar;
    public bool hasProfile;
    public BodyDnaProfile profile = BodyDnaProfile.Default;

    private Coroutine _deferredApply;
    private bool _loggedAvailableDna;

    private void Reset()
    {
        avatar = GetComponent<DynamicCharacterAvatar>();
    }

    private void OnEnable()
    {
        if (avatar == null)
        {
            avatar = GetComponent<DynamicCharacterAvatar>();
        }

        if (avatar == null)
        {
            return;
        }

        avatar.CharacterUpdated.RemoveListener(OnCharacterUpdated);
        avatar.CharacterUpdated.AddListener(OnCharacterUpdated);

        Debug.Log(
            $"AvatarBodyMeasurementApplier.OnEnable avatar='{avatar.name}' active={avatar.gameObject.activeInHierarchy} hasProfile={hasProfile}.",
            avatar);

        TryApplyOrDefer("OnEnable", rebuild: false);
    }

    private void OnDisable()
    {
        if (_deferredApply != null)
        {
            StopCoroutine(_deferredApply);
            _deferredApply = null;
        }

        if (avatar != null)
        {
            avatar.CharacterUpdated.RemoveListener(OnCharacterUpdated);
        }
    }

    public void SetProfile(BodyDnaProfile newProfile)
    {
        profile = newProfile;
        hasProfile = true;

        Debug.Log(
            $"AvatarBodyMeasurementApplier.SetProfile avatar='{(avatar != null ? avatar.name : "null")}' " +
            $"height={profile.height:0.###} upperWeight={profile.upperWeight:0.###} lowerWeight={profile.lowerWeight:0.###} " +
            $"belly={profile.belly:0.###} waist={profile.waist:0.###} legsSize={profile.legsSize:0.###} " +
            $"armWidth={profile.armWidth:0.###} forearmWidth={profile.forearmWidth:0.###} " +
            $"armLength={profile.armLength:0.###} forearmLength={profile.forearmLength:0.###}.",
            this);

        TryApplyOrDefer("SetProfile", rebuild: true);
    }

    private void OnCharacterUpdated(UMAData umaData)
    {
        if (umaData == null || !hasProfile || avatar == null)
        {
            return;
        }

        Debug.Log(
            $"AvatarBodyMeasurementApplier.OnCharacterUpdated avatar='{avatar.name}' rebuildReapply=true.",
            avatar);

        ApplyProfile(rebuild: false, reason: "CharacterUpdated");
    }

    private void TryApplyOrDefer(string reason, bool rebuild)
    {
        if (avatar == null || !hasProfile)
        {
            return;
        }

        if (avatar.umaData != null)
        {
            ApplyProfile(rebuild, reason);
            return;
        }

        Debug.Log(
            $"AvatarBodyMeasurementApplier.TryApplyOrDefer reason={reason} immediate=false avatar='{avatar.name}' active={avatar.gameObject.activeInHierarchy}.",
            avatar);

        if (_deferredApply != null)
        {
            StopCoroutine(_deferredApply);
        }

        _deferredApply = StartCoroutine(DeferredApplyProfile(reason, rebuild));
    }

    private IEnumerator DeferredApplyProfile(string reason, bool rebuild)
    {
        for (var i = 0; i < 15; i++)
        {
            yield return null;

            if (avatar == null)
            {
                yield break;
            }

            if (avatar.umaData != null)
            {
                Debug.Log(
                    $"AvatarBodyMeasurementApplier.DeferredApplyProfile reason={reason} frameDelay={i + 1} avatar='{avatar.name}'.",
                    avatar);
                ApplyProfile(rebuild, reason + "_Deferred");
                _deferredApply = null;
                yield break;
            }
        }

        Debug.LogWarning(
            $"AvatarBodyMeasurementApplier.DeferredApplyProfile timed out waiting for UMA data on '{(avatar != null ? avatar.name : "null")}'.",
            this);
        _deferredApply = null;
    }

    private void ApplyProfile(bool rebuild, string reason)
    {
        if (!hasProfile || avatar == null)
        {
            return;
        }

        if (avatar.predefinedDNA == null)
        {
            avatar.predefinedDNA = new UMAPredefinedDNA();
        }

        avatar.keepPredefinedDNA = true;

        var dna = avatar.GetDNA();
        if (dna == null || dna.Count == 0)
        {
            Debug.LogWarning($"AvatarBodyMeasurementApplier.ApplyProfile found no DNA on '{avatar.name}'.", avatar);
            return;
        }

        LogAvailableDnaOnce(dna);
        ApplyDnaValue(dna, "height", profile.height);
        ApplyDnaValue(dna, "upperWeight", profile.upperWeight);
        ApplyDnaValue(dna, "lowerWeight", profile.lowerWeight);
        ApplyDnaValue(dna, "belly", profile.belly);
        ApplyDnaValue(dna, "waist", profile.waist);
        ApplyDnaValue(dna, "legsSize", profile.legsSize);
        ApplyDnaValue(dna, "armWidth", profile.armWidth);
        ApplyDnaValue(dna, "forearmWidth", profile.forearmWidth);
        ApplyDnaValue(dna, "armLength", profile.armLength);
        ApplyDnaValue(dna, "forearmLength", profile.forearmLength);

        Debug.Log(
            $"AvatarBodyMeasurementApplier.ApplyProfile avatar='{avatar.name}' reason={reason} rebuild={rebuild} " +
            $"height={profile.height:0.###} upperWeight={profile.upperWeight:0.###} lowerWeight={profile.lowerWeight:0.###} " +
            $"belly={profile.belly:0.###} waist={profile.waist:0.###} legsSize={profile.legsSize:0.###}.",
            avatar);

        if (rebuild && avatar.gameObject.activeInHierarchy)
        {
            avatar.BuildCharacter(true);
        }
    }

    private void ApplyDnaValue(Dictionary<string, DnaSetter> dna, string name, float value)
    {
        avatar.predefinedDNA.AddDNA(name, value);

        if (dna.TryGetValue(name, out var setter))
        {
            setter.Set(value);
        }
        else
        {
            Debug.LogWarning($"AvatarBodyMeasurementApplier could not find DNA channel '{name}' on '{avatar.name}'.", avatar);
        }
    }

    private void LogAvailableDnaOnce(Dictionary<string, DnaSetter> dna)
    {
        if (_loggedAvailableDna)
        {
            return;
        }

        _loggedAvailableDna = true;
        var available = new List<string>();
        for (var i = 0; i < RelevantDnaChannels.Length; i++)
        {
            var channel = RelevantDnaChannels[i];
            if (dna.ContainsKey(channel))
            {
                available.Add(channel);
            }
        }

        Debug.Log(
    $"AvatarBodyMeasurementApplier available DNA on '{avatar.name}': {string.Join(", ", available)}",
    avatar);
    }
}
