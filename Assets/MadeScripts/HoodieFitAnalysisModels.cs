using System.Collections.Generic;

public struct FitSeverityResult
{
    public int score;
    public bool hasCriticalClip;
    public bool hasHighClip;
    public bool hasMediumClip;
    public bool hasLowClip;
    public int clippedRegionCount;
}

public struct GarmentAdjustmentSuggestion
{
    public string region;
    public string suggestedAdjustment;
    public string reason;
}

public struct ConditionFitEvaluation
{
    public string condition;
    public ClippingResult clipping;
    public FitSeverityResult severity;
    public FitQuality quality;
    public List<GarmentAdjustmentSuggestion> suggestions;
}
