using System;
using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class HoodieSizeApplier : MonoBehaviour
{
    [Serializable]
    public struct RecipeScaleEntry
    {
        public UMAWardrobeRecipe recipe;
        public float uniformScale;
    }

    [Header("Avatar")]
    public DynamicCharacterAvatar avatar;

    [Header("Wardrobe Tracking")]
    public string wardrobeSlot = "Chest";
    public string targetSlotName = "MaleHoodie";

    [Header("Fallback")]
    public bool enableDistinctRendererFallback = false;
    public bool forceFallbackWhenMeshModifierExists = false;

    [Header("Recipe Scales")]
    public RecipeScaleEntry[] recipeScales = Array.Empty<RecipeScaleEntry>();

    private readonly HashSet<string> _warnings = new HashSet<string>();

    private void Reset()
    {
        avatar = GetComponent<DynamicCharacterAvatar>();
    }

    private void OnEnable()
    {
        if (!IsUnityReady())
        {
            return;
        }

        if (avatar == null)
        {
            avatar = GetComponent<DynamicCharacterAvatar>() ?? GetComponentInParent<DynamicCharacterAvatar>();
        }

        if (avatar == null)
        {
            WarnOnce("avatar-missing", "HoodieSizeApplier could not find a DynamicCharacterAvatar.");
            return;
        }

        avatar.CharacterUpdated.RemoveListener(OnCharacterUpdated);
        avatar.CharacterUpdated.AddListener(OnCharacterUpdated);
    }

    private void OnDisable()
    {
        if (avatar != null)
        {
            avatar.CharacterUpdated.RemoveListener(OnCharacterUpdated);
        }
    }

    private void OnCharacterUpdated(UMAData umaData)
    {
        if (avatar == null || umaData == null)
        {
            return;
        }

        var activeRecipe = avatar.GetWardrobeItem(wardrobeSlot) as UMAWardrobeRecipe;
        if (activeRecipe == null)
        {
            return;
        }

        if (!TryGetDesiredScale(activeRecipe, out var desiredScale))
        {
            ResetDistinctRendererScale(umaData);
            return;
        }

        if (!enableDistinctRendererFallback)
        {
            return;
        }

        if (!forceFallbackWhenMeshModifierExists && activeRecipe.MeshModifiers != null && activeRecipe.MeshModifiers.Count > 0)
        {
            ResetDistinctRendererScale(umaData);
            return;
        }

        ApplyDistinctRendererScale(umaData, activeRecipe, desiredScale);
    }

    private bool TryGetDesiredScale(UMAWardrobeRecipe activeRecipe, out float desiredScale)
    {
        desiredScale = 1f;

        if (recipeScales == null)
        {
            return false;
        }

        for (var i = 0; i < recipeScales.Length; i++)
        {
            var entry = recipeScales[i];
            if (entry.recipe == activeRecipe)
            {
                desiredScale = entry.uniformScale;
                return true;
            }
        }

        return false;
    }

    private void ApplyDistinctRendererScale(UMAData umaData, UMAWardrobeRecipe activeRecipe, float desiredScale)
    {
        var hoodieSlot = FindTargetSlot(umaData);
        if (hoodieSlot == null)
        {
            WarnOnce($"slot-missing:{targetSlotName}", $"HoodieSizeApplier could not find slot '{targetSlotName}' on avatar '{avatar.name}'.");
            return;
        }

        if (!IsRendererExclusiveToTargetSlot(umaData, hoodieSlot))
        {
            WarnOnce(
                $"shared-renderer:{targetSlotName}",
                $"HoodieSizeApplier fallback skipped for recipe '{activeRecipe.name}' because slot '{targetSlotName}' shares a renderer with other UMA slots.");
            return;
        }

        var renderer = umaData.GetRenderer(hoodieSlot.skinnedMeshRenderer);
        if (renderer == null)
        {
            WarnOnce(
                $"renderer-missing:{targetSlotName}",
                $"HoodieSizeApplier could not resolve renderer index {hoodieSlot.skinnedMeshRenderer} for slot '{targetSlotName}'.");
            return;
        }

        renderer.transform.localScale = Vector3.one * desiredScale;
    }

    private void ResetDistinctRendererScale(UMAData umaData)
    {
        var hoodieSlot = FindTargetSlot(umaData);
        if (hoodieSlot == null)
        {
            return;
        }

        if (!IsRendererExclusiveToTargetSlot(umaData, hoodieSlot))
        {
            return;
        }

        var renderer = umaData.GetRenderer(hoodieSlot.skinnedMeshRenderer);
        if (renderer != null)
        {
            renderer.transform.localScale = Vector3.one;
        }
    }

    private SlotData FindTargetSlot(UMAData umaData)
    {
        if (umaData.umaRecipe == null || umaData.umaRecipe.slotDataList == null)
        {
            return null;
        }

        for (var i = 0; i < umaData.umaRecipe.slotDataList.Length; i++)
        {
            var slot = umaData.umaRecipe.slotDataList[i];
            if (slot != null && string.Equals(slot.slotName, targetSlotName, StringComparison.Ordinal))
            {
                return slot;
            }
        }

        return null;
    }

    private bool IsRendererExclusiveToTargetSlot(UMAData umaData, SlotData targetSlot)
    {
        if (umaData.umaRecipe == null || umaData.umaRecipe.slotDataList == null)
        {
            return false;
        }

        var targetRendererIndex = targetSlot.skinnedMeshRenderer;
        if (targetRendererIndex < 0)
        {
            return false;
        }

        for (var i = 0; i < umaData.umaRecipe.slotDataList.Length; i++)
        {
            var slot = umaData.umaRecipe.slotDataList[i];
            if (slot == null || ReferenceEquals(slot, targetSlot))
            {
                continue;
            }

            if (slot.skinnedMeshRenderer == targetRendererIndex)
            {
                return false;
            }
        }

        return true;
    }

    private void WarnOnce(string key, string message)
    {
        if (_warnings.Add(key))
        {
            Debug.LogWarning(message, this);
        }
    }

    private static bool IsUnityReady()
    {
#if UNITY_EDITOR
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return false;
        }
#endif
        return true;
    }
}
