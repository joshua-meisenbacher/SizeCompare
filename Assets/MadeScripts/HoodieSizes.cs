using UMA.CharacterSystem;
using UnityEngine;

public class HoodieSizeSwitcher : MonoBehaviour
{
    [Header("UMA Recipe Switching")]
    public DynamicCharacterAvatar avatar;
    public UMAWardrobeRecipe smallRecipe;
    public UMAWardrobeRecipe mediumRecipe;
    public UMAWardrobeRecipe largeRecipe;
    public UMAWardrobeRecipe extraLargeRecipe;

    [Header("Legacy Hoodie Objects")]
    public GameObject hoodieS;
    public GameObject hoodieM;
    public GameObject hoodieL;
    public GameObject hoodieXL;
    public bool useLegacyFallback = false;

    [Header("Fixed Garment Presentation")]
    public GarmentSlot garmentSlot;
    public GameObject fixedHoodieSPrefab;
    public GameObject fixedHoodieMPrefab;
    public GameObject fixedHoodieLPrefab;
    public GameObject fixedHoodieXLPrefab;
    public bool useFixedGarmentPresentation = false;

    public void ShowS()
    {
        if (!CanRunForCurrentPresentation())
        {
            return;
        }
        ApplySize(smallRecipe, hoodieS, "Small");
    }

    public void ShowM()
    {
        if (!CanRunForCurrentPresentation())
        {
            return;
        }
        ApplySize(mediumRecipe, hoodieM, "Medium");
    }

    public void ShowL()
    {
        if (!CanRunForCurrentPresentation())
        {
            return;
        }
        ApplySize(largeRecipe, hoodieL, "Large");
    }

    public void ShowXL()
    {
        if (!CanRunForCurrentPresentation())
        {
            return;
        }
        ApplySize(extraLargeRecipe, hoodieXL, "Extra Large");
    }

    private bool CanRunForCurrentPresentation()
    {
        return gameObject.activeInHierarchy;
    }

    private void ApplySize(UMAWardrobeRecipe recipe, GameObject legacyActiveHoodie, string sizeLabel)
    {
        if (useFixedGarmentPresentation && garmentSlot != null)
        {
            var fixedPrefab = GetFixedPrefab(sizeLabel);
            if (fixedPrefab != null)
            {
                garmentSlot.Equip(fixedPrefab);
                Debug.Log($"Applied fixed garment hoodie presentation for {sizeLabel} on '{gameObject.name}'.");
                return;
            }
        }

        if (useLegacyFallback && legacyActiveHoodie != null)
        {
            SetOnly(legacyActiveHoodie);
            AttachLegacyPresentation();
            var presentationKind = HasRepairedRoot(legacyActiveHoodie) ? "repaired overlay" : "legacy overlay";
            Debug.Log($"Applied {presentationKind} hoodie presentation for {sizeLabel} on '{gameObject.name}'.");
            DumpLegacyState($"Show{sizeLabel}");
            return;
        }

        var switchedUma = TryApplyUmaRecipe(recipe, sizeLabel);

        if (!switchedUma && !useLegacyFallback)
        {
            Debug.LogWarning($"HoodieSizeSwitcher could not apply UMA recipe for {sizeLabel}.");
        }
    }

    private GameObject GetFixedPrefab(string sizeLabel)
    {
        return sizeLabel switch
        {
            "Small" => fixedHoodieSPrefab,
            "Medium" => fixedHoodieMPrefab,
            "Large" => fixedHoodieLPrefab,
            "Extra Large" => fixedHoodieXLPrefab,
            _ => null
        };
    }

    private bool TryApplyUmaRecipe(UMAWardrobeRecipe recipe, string sizeLabel)
    {
        if (avatar == null || recipe == null)
        {
            return false;
        }

        if (!avatar.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"HoodieSizeSwitcher skipped UMA hoodie swap for {sizeLabel} because the avatar is inactive.");
            return false;
        }

        if (!avatar.SetSlot(recipe))
        {
            Debug.LogWarning($"HoodieSizeSwitcher failed to set hoodie recipe {recipe.name} for {sizeLabel}.");
            return false;
        }

        avatar.BuildCharacter(true);
        Debug.Log($"Applied UMA hoodie recipe {recipe.name} for {sizeLabel}.");
        return true;
    }

    private void SetOnly(GameObject activeHoodie)
    {
        if (hoodieS != null) hoodieS.SetActive(hoodieS == activeHoodie);
        if (hoodieM != null) hoodieM.SetActive(hoodieM == activeHoodie);
        if (hoodieL != null) hoodieL.SetActive(hoodieL == activeHoodie);
        if (hoodieXL != null) hoodieXL.SetActive(hoodieXL == activeHoodie);

        if (activeHoodie != null)
        {
            var presentationKind = HasRepairedRoot(activeHoodie) ? "repaired overlay" : "legacy overlay";
            Debug.Log($"Showing {presentationKind} hoodie: {activeHoodie.name}");
        }
    }

    private static bool HasRepairedRoot(GameObject hoodieObject)
    {
        if (hoodieObject == null)
        {
            return false;
        }

        return hoodieObject.GetComponentInParent<PreviewGarmentRootMarker>(true) != null;
    }

    private void DumpLegacyState(string reason)
    {
        var attacher = FindFirstObjectByType<BoneAttacher>();
        if (attacher == null)
        {
            return;
        }

        var objectName = gameObject.name;
        if (objectName.IndexOf("female", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            attacher.DumpFemaleState(reason);
        }
        else
        {
            attacher.DumpMaleState(reason);
        }
    }

    private void AttachLegacyPresentation()
    {
        var attacher = FindFirstObjectByType<BoneAttacher>();
        if (attacher == null)
        {
            return;
        }

        var objectName = gameObject.name;
        if (objectName.IndexOf("female", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            attacher.AttachToFemale();
        }
        else
        {
            attacher.AttachToMale();
        }
    }
}
