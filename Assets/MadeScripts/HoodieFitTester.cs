using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum FitQuality
{
    Pass,
    SoftFail,
    HardFail
}

public class HoodieFitTester : MonoBehaviour
{
    [System.Serializable]
    public class PoseStep
    {
        public string label;
        public string animatorStateName;
    }

    public List<HoodieFitProfile> profiles = new List<HoodieFitProfile>();
    public AvatarBodyMeasurementApplier bodyApplier;
    public Animator avatarAnimator;
    public float poseSettleSeconds = 0.1f;
    public List<PoseStep> poseSweep = new List<PoseStep>();

    private const int WeightCritical = 5; // chest
    private const int WeightHigh = 4;     // armpit, neck
    private const int WeightMedium = 3;   // upperArm, forearm, hem
    private const int WeightLow = 1;      // wrist

    private void Reset()
    {
        bodyApplier = GetComponent<AvatarBodyMeasurementApplier>();
        avatarAnimator = GetComponentInChildren<Animator>();
        EnsureDefaultProfiles();
        EnsureDefaultPoseSweep();
    }

    public void TestAllSizes()
    {
        StartCoroutine(TestAllSizesRoutine());
    }

    private IEnumerator TestAllSizesRoutine()
    {
        EnsureDefaultProfiles();
        EnsureDefaultPoseSweep();

        if (bodyApplier == null)
        {
            Debug.LogWarning("HoodieFitTester requires AvatarBodyMeasurementApplier to run tests.", this);
            yield break;
        }

        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            if (profile == null)
            {
                continue;
            }

            yield return TestProfile(profile);
        }
    }

    private IEnumerator TestProfile(HoodieFitProfile profile)
    {
        bodyApplier.ApplyFitProfile(profile);
        yield return null;

        ConditionFitEvaluation ideal = default;
        yield return EvaluateFit(profile, "IDEAL", result => ideal = result);

        bodyApplier.ApplyChest(profile.chestMin);
        yield return null;

        ConditionFitEvaluation min = default;
        yield return EvaluateFit(profile, "MIN", result => min = result);

        bodyApplier.ApplyChest(profile.chestMax);
        yield return null;

        ConditionFitEvaluation max = default;
        yield return EvaluateFit(profile, "MAX", result => max = result);

        var quality = ClassifyAcrossConditions(ideal, min, max);
        Debug.Log($"Size: {profile.sizeLabel} | FinalResult: {quality}", this);
    }

    private IEnumerator EvaluateFit(HoodieFitProfile profile, string condition, Action<ConditionFitEvaluation> onComplete)
    {
        var aggregate = default(ClippingResult);

        for (var i = 0; i < poseSweep.Count; i++)
        {
            var pose = poseSweep[i];
            ApplyPose(pose);
            yield return new WaitForSeconds(poseSettleSeconds);

            var poseResult = HoodieClippingDetector.Scan();
            aggregate = ClippingResult.Combine(aggregate, poseResult);

            var poseSeverity = CalculateSeverity(poseResult);
            var poseQuality = ClassifyCondition(condition, poseSeverity);
            var poseSuggestions = BuildSuggestions(poseResult);
            LogPoseResult(profile.sizeLabel, condition, pose, poseResult, poseSeverity, poseQuality, poseSuggestions);
        }

        var aggregateSeverity = CalculateSeverity(aggregate);
        var aggregateQuality = ClassifyCondition(condition, aggregateSeverity);
        var aggregateSuggestions = BuildSuggestions(aggregate);

        LogFitResult(profile.sizeLabel, condition, aggregate, aggregateSeverity, aggregateQuality, aggregateSuggestions);

        onComplete?.Invoke(new ConditionFitEvaluation
        {
            condition = condition,
            clipping = aggregate,
            severity = aggregateSeverity,
            quality = aggregateQuality,
            suggestions = aggregateSuggestions
        });
    }

    private void ApplyPose(PoseStep pose)
    {
        if (avatarAnimator == null || pose == null || string.IsNullOrWhiteSpace(pose.animatorStateName))
        {
            return;
        }

        avatarAnimator.CrossFadeInFixedTime(pose.animatorStateName, 0.08f);
    }

    private static FitQuality ClassifyAcrossConditions(ConditionFitEvaluation ideal, ConditionFitEvaluation min, ConditionFitEvaluation max)
    {
        if (ideal.quality == FitQuality.HardFail)
        {
            return FitQuality.HardFail;
        }

        if (min.quality == FitQuality.HardFail || max.quality == FitQuality.HardFail)
        {
            return FitQuality.HardFail;
        }

        if (ideal.quality == FitQuality.SoftFail || min.quality == FitQuality.SoftFail || max.quality == FitQuality.SoftFail)
        {
            return FitQuality.SoftFail;
        }

        return FitQuality.Pass;
    }

    private static FitQuality ClassifyCondition(string condition, FitSeverityResult severity)
    {
        var isIdeal = string.Equals(condition, "IDEAL", StringComparison.OrdinalIgnoreCase);

        // Thresholds are tuned for actionable garment calibration:
        // - Any critical/high clip at IDEAL => HardFail.
        // - Widespread clipping (4+ regions) or score >= 10 => HardFail in any condition.
        // - Medium/low-only clipping at IDEAL => SoftFail.
        // - Boundary-only low severity can still pass.
        if ((isIdeal && (severity.hasCriticalClip || severity.hasHighClip)) ||
            severity.clippedRegionCount >= 4 ||
            severity.score >= 10)
        {
            return FitQuality.HardFail;
        }

        if (isIdeal && severity.score > 0)
        {
            return FitQuality.SoftFail;
        }

        if (!isIdeal && severity.hasMediumClip)
        {
            return FitQuality.SoftFail;
        }

        if (!isIdeal && severity.score > WeightLow)
        {
            return FitQuality.SoftFail;
        }

        return FitQuality.Pass;
    }

    private static FitSeverityResult CalculateSeverity(ClippingResult clipping)
    {
        var result = new FitSeverityResult();

        if (clipping.chest)
        {
            result.score += WeightCritical;
            result.hasCriticalClip = true;
            result.clippedRegionCount++;
        }

        if (clipping.armpit)
        {
            result.score += WeightHigh;
            result.hasHighClip = true;
            result.clippedRegionCount++;
        }

        if (clipping.neck)
        {
            result.score += WeightHigh;
            result.hasHighClip = true;
            result.clippedRegionCount++;
        }

        if (clipping.upperArm)
        {
            result.score += WeightMedium;
            result.hasMediumClip = true;
            result.clippedRegionCount++;
        }

        if (clipping.forearm)
        {
            result.score += WeightMedium;
            result.hasMediumClip = true;
            result.clippedRegionCount++;
        }

        if (clipping.hem)
        {
            result.score += WeightMedium;
            result.hasMediumClip = true;
            result.clippedRegionCount++;
        }

        if (clipping.wrist)
        {
            result.score += WeightLow;
            result.hasLowClip = true;
            result.clippedRegionCount++;
        }

        return result;
    }

    private static List<GarmentAdjustmentSuggestion> BuildSuggestions(ClippingResult clipping)
    {
        var suggestions = new List<GarmentAdjustmentSuggestion>();

        if (clipping.chest)
        {
            suggestions.Add(new GarmentAdjustmentSuggestion
            {
                region = "chest",
                suggestedAdjustment = "Increase torso circumference / chest width",
                reason = "Chest panel intersects the body at the rib cage and bust volume."
            });
        }

        if (clipping.armpit)
        {
            suggestions.Add(new GarmentAdjustmentSuggestion
            {
                region = "armpit",
                suggestedAdjustment = "Increase armhole or underarm volume",
                reason = "Underarm intersection indicates insufficient armhole clearance during arm motion."
            });
        }

        if (clipping.upperArm)
        {
            suggestions.Add(new GarmentAdjustmentSuggestion
            {
                region = "upperArm",
                suggestedAdjustment = "Widen sleeve bicep circumference",
                reason = "Sleeve is too tight around the upper arm during pose sweep."
            });
        }

        if (clipping.forearm)
        {
            suggestions.Add(new GarmentAdjustmentSuggestion
            {
                region = "forearm",
                suggestedAdjustment = "Increase lower sleeve circumference",
                reason = "Forearm clipping suggests insufficient taper clearance in lower sleeve."
            });
        }

        if (clipping.wrist)
        {
            suggestions.Add(new GarmentAdjustmentSuggestion
            {
                region = "wrist",
                suggestedAdjustment = "Widen cuff opening slightly",
                reason = "Cuff opening is too restrictive at distal sleeve edge."
            });
        }

        if (clipping.neck)
        {
            suggestions.Add(new GarmentAdjustmentSuggestion
            {
                region = "neck",
                suggestedAdjustment = "Enlarge neck opening / hood base clearance",
                reason = "Neckline intersects with neck/clavicle region in tested poses."
            });
        }

        if (clipping.hem)
        {
            suggestions.Add(new GarmentAdjustmentSuggestion
            {
                region = "hem",
                suggestedAdjustment = "Increase body sweep or lower-hem circumference",
                reason = "Hem intersection indicates insufficient lower garment sweep."
            });
        }

        return suggestions;
    }

    private void LogPoseResult(
        string sizeLabel,
        string condition,
        PoseStep pose,
        ClippingResult clipping,
        FitSeverityResult severity,
        FitQuality quality,
        List<GarmentAdjustmentSuggestion> suggestions)
    {
        var poseName = pose != null && !string.IsNullOrWhiteSpace(pose.label) ? pose.label : "UnknownPose";

        Debug.Log(
            BuildLogBlock(sizeLabel, condition, poseName, clipping, severity, quality, suggestions),
            this);
    }

    private void LogFitResult(
        string sizeLabel,
        string condition,
        ClippingResult clipping,
        FitSeverityResult severity,
        FitQuality quality,
        List<GarmentAdjustmentSuggestion> suggestions)
    {
        Debug.Log(
            BuildLogBlock(sizeLabel, condition, "AGGREGATE", clipping, severity, quality, suggestions),
            this);
    }

    private static string BuildLogBlock(
        string sizeLabel,
        string condition,
        string poseName,
        ClippingResult clipping,
        FitSeverityResult severity,
        FitQuality quality,
        List<GarmentAdjustmentSuggestion> suggestions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Size: {sizeLabel}");
        sb.AppendLine($"Condition: {condition}");
        sb.AppendLine($"Pose: {poseName}");
        sb.AppendLine(
            $"Clipping: chest={clipping.chest}, armpit={clipping.armpit}, upperArm={clipping.upperArm}, forearm={clipping.forearm}, wrist={clipping.wrist}, neck={clipping.neck}, hem={clipping.hem}");
        sb.AppendLine($"SeverityScore: {severity.score}");
        sb.AppendLine(
            $"SeverityFlags: critical={severity.hasCriticalClip}, high={severity.hasHighClip}, medium={severity.hasMediumClip}, low={severity.hasLowClip}, regions={severity.clippedRegionCount}");
        sb.AppendLine($"Result: {quality}");

        if (suggestions == null || suggestions.Count == 0)
        {
            sb.Append("SuggestedAdjustment: None");
        }
        else
        {
            sb.Append("SuggestedAdjustment: ");
            for (var i = 0; i < suggestions.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("; ");
                }

                sb.Append($"[{suggestions[i].region}] {suggestions[i].suggestedAdjustment}");
            }
        }

        return sb.ToString();
    }

    private void EnsureDefaultProfiles()
    {
        if (profiles != null && profiles.Count > 0)
        {
            return;
        }

        profiles = new List<HoodieFitProfile>
        {
            new HoodieFitProfile { sizeLabel = "S", chestIdeal = 37f, chestMin = 36f, chestMax = 38f, heightIdeal = 67f, heightMin = 66f, heightMax = 68f },
            new HoodieFitProfile { sizeLabel = "M", chestIdeal = 39f, chestMin = 38f, chestMax = 40f, heightIdeal = 69f, heightMin = 68f, heightMax = 70f },
            new HoodieFitProfile { sizeLabel = "L", chestIdeal = 41f, chestMin = 40f, chestMax = 42f, heightIdeal = 71f, heightMin = 70f, heightMax = 72f },
            new HoodieFitProfile { sizeLabel = "XL", chestIdeal = 43f, chestMin = 42f, chestMax = 44f, heightIdeal = 73f, heightMin = 72f, heightMax = 74f },
        };
    }

    private void EnsureDefaultPoseSweep()
    {
        if (poseSweep != null && poseSweep.Count > 0)
        {
            return;
        }

        poseSweep = new List<PoseStep>
        {
            new PoseStep { label = "T-pose", animatorStateName = "TPose" },
            new PoseStep { label = "Arms down", animatorStateName = "ArmsDown" },
            new PoseStep { label = "Arms forward", animatorStateName = "ArmsForward" },
            new PoseStep { label = "Elbows bent", animatorStateName = "ElbowsBent" },
            new PoseStep { label = "Slight hunch", animatorStateName = "SlightHunch" },
        };
    }
}
