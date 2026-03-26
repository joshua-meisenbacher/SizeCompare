using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UMA;
using UMA.CharacterSystem;

public class OnboardingManager : MonoBehaviour
{
    [Header("Reference")]
    public BoneAttacher boneAttacher;

    [Header("UI")]
    public GameObject onboardingPanel;
    public GameObject previewRoot;

    [Header("Sex Selection")]
    public Button maleButton;
    public Button femaleButton;

    [Header("Unit Toggle")]
    public Button metricButton;
    public Button imperialButton;

    [Header("Inputs")]
    public TMP_InputField heightInput;
    public TMP_InputField feetInput;
    public TMP_InputField inchesInput;
    public TMP_InputField chestInput;
    public TMP_Text chestLabel;
    public Button startButton;

    [Header("Mannequins")]
    public GameObject maleUnified;
    public GameObject femaleUnified;

    [Header("Preview Presentation")]
    public GameObject maleHoodieSizes;
    public GameObject femaleHoodieSizes;
    public GameObject maleLegacyHoodiePrefab;
    public GameObject femaleLegacyHoodiePrefab;

    private bool isMale = true;
    private bool isMetric = true;

    private const float REF_HEIGHT_CM = 175f;
    private const float REF_CHEST_CM = 96f;
    private const float MIN_HEIGHT_RATIO = 0.85f;
    private const float MAX_HEIGHT_RATIO = 1.15f;
    private const float MIN_CHEST_RATIO = 0.8f;
    private const float MAX_CHEST_RATIO = 1.2f;
    private const bool DiagnosticBodyScaling = true;
    private const string GeneratedRecipeS = "MaleHoodie_S";
    private const string GeneratedRecipeM = "MaleHoodie_M";
    private const string GeneratedRecipeL = "MaleHoodie_L";
    private const string GeneratedRecipeXL = "MaleHoodie_XL";
    private const string PreviewPrefabS = "Generated/RepairedHoodiePrefabs/Hoodie_S_Preview";
    private const string PreviewPrefabM = "Generated/RepairedHoodiePrefabs/Hoodie_M_Preview";
    private const string PreviewPrefabL = "Generated/RepairedHoodiePrefabs/Hoodie_L_Preview";
    private const string PreviewPrefabXL = "Generated/RepairedHoodiePrefabs/Hoodie_XL_Preview";
    private const string FixedDisplayPrefabS = "Generated/FixedHoodieDisplays/Hoodie_S_Fixed";
    private const string FixedDisplayPrefabM = "Generated/FixedHoodieDisplays/Hoodie_M_Fixed";
    private const string FixedDisplayPrefabL = "Generated/FixedHoodieDisplays/Hoodie_L_Fixed";
    private const string FixedDisplayPrefabXL = "Generated/FixedHoodieDisplays/Hoodie_XL_Fixed";

    private void Start()
    {
        if (onboardingPanel != null)
        {
            onboardingPanel.SetActive(true);
        }

        ResolvePreviewReferences();
        EnsurePreviewPresentations();
        ConfigurePreviewHoodieSwitchers();
        SetPreviewActive(false);

        if (maleButton != null) maleButton.onClick.AddListener(() => SelectSex(true));
        if (femaleButton != null) femaleButton.onClick.AddListener(() => SelectSex(false));
        if (metricButton != null) metricButton.onClick.AddListener(() => SetUnits(true));
        if (imperialButton != null) imperialButton.onClick.AddListener(() => SetUnits(false));
        if (startButton != null) startButton.onClick.AddListener(OnStart);

        SelectSex(true);
        SetUnits(true);
        UpdateAvatarPresentation();
    }

    private void SelectSex(bool male)
    {
        isMale = male;

        if (maleButton != null) maleButton.interactable = !male;
        if (femaleButton != null) femaleButton.interactable = male;

        UpdateAvatarPresentation();
    }

    private void SetUnits(bool metric)
    {
        isMetric = metric;

        if (metricButton != null) metricButton.interactable = !metric;
        if (imperialButton != null) imperialButton.interactable = metric;

        UpdateUnitLabels();
        UpdateHeightFieldVisibility();
    }

    private void UpdateUnitLabels()
    {
        if (chestLabel != null)
        {
            chestLabel.text = isMetric ? "Chest Circumference (cm)" : "Chest Circumference (in)";
        }
    }

    private void UpdateHeightFieldVisibility()
    {
        if (heightInput != null)
        {
            heightInput.gameObject.SetActive(isMetric);
        }

        if (feetInput != null)
        {
            feetInput.gameObject.SetActive(!isMetric);
        }

        if (inchesInput != null)
        {
            inchesInput.gameObject.SetActive(!isMetric);
        }
    }

    private void OnStart()
    {
        if (!TryGetMeasurements(out float heightCm, out float chestCm))
        {
            Debug.LogWarning("Invalid height or chest input.");
            return;
        }

        Debug.Log(
            $"OnboardingManager.OnStart sex={(isMale ? "Male" : "Female")} metric={isMetric} " +
            $"heightCm={heightCm:0.##} chestCm={chestCm:0.##} previewRoot={(previewRoot != null ? previewRoot.name : "null")}",
            this);

        SetPreviewActive(true);
        ApplyMeasurements(heightCm, chestCm);
        ApplyDefaultPreviewHoodieSize();

        if (onboardingPanel != null)
        {
            onboardingPanel.SetActive(false);
        }
    }

    private bool TryGetMeasurements(out float heightCm, out float chestCm)
    {
        heightCm = 0f;
        chestCm = 0f;

        if (chestInput == null || !float.TryParse(chestInput.text, out float chestValue))
        {
            return false;
        }

        if (isMetric)
        {
            if (heightInput == null || !float.TryParse(heightInput.text, out heightCm))
            {
                return false;
            }

            chestCm = chestValue;
            return true;
        }

        if (feetInput == null || inchesInput == null ||
            !float.TryParse(feetInput.text, out float feet) ||
            !float.TryParse(inchesInput.text, out float inches))
        {
            return false;
        }

        heightCm = ((feet * 12f) + inches) * 2.54f;
        chestCm = chestValue * 2.54f;
        return true;
    }

    public void ApplyMeasurements(float heightCm, float chestCm)
    {
        UpdateAvatarPresentation();
        ConfigurePreviewHoodieSwitchers();

        var profile = BuildBodyScaleProfile(heightCm, chestCm);
        var activeAvatar = GetActiveAvatar();

        Debug.Log(
            $"OnboardingManager.ApplyMeasurements sex={(isMale ? "Male" : "Female")} " +
            $"heightCm={heightCm:0.##} chestCm={chestCm:0.##} " +
            $"dnaHeight={profile.height:0.###} upperWeight={profile.upperWeight:0.###} lowerWeight={profile.lowerWeight:0.###} " +
            $"belly={profile.belly:0.###} waist={profile.waist:0.###} legsSize={profile.legsSize:0.###} " +
            $"targetAvatar={(activeAvatar != null ? activeAvatar.name : "null")} " +
            $"avatarActive={(activeAvatar != null && activeAvatar.gameObject.activeInHierarchy)} " +
            $"previewActive={(previewRoot != null && previewRoot.activeInHierarchy)}",
            this);

        if (activeAvatar == null)
        {
            Debug.LogWarning("OnboardingManager could not find the active avatar to apply body measurements.", this);
            return;
        }

        var applier = activeAvatar.GetComponent<AvatarBodyMeasurementApplier>();
        if (applier == null)
        {
            applier = activeAvatar.gameObject.AddComponent<AvatarBodyMeasurementApplier>();
        }

        applier.avatar = activeAvatar;
        applier.SetProfile(profile);

        var switcher = activeAvatar.GetComponent<HoodieSizeSwitcher>();
        if (boneAttacher != null && switcher != null && switcher.useLegacyFallback)
        {
            if (isMale)
            {
                boneAttacher.AttachToMale();
            }
            else
            {
                boneAttacher.AttachToFemale();
            }
        }
    }

    private void UpdateAvatarPresentation()
    {
        EnsurePreviewPresentations();

        if (maleUnified != null) maleUnified.SetActive(isMale);
        if (femaleUnified != null) femaleUnified.SetActive(!isMale);
        if (maleHoodieSizes != null) maleHoodieSizes.SetActive(isMale);
        if (femaleHoodieSizes != null) femaleHoodieSizes.SetActive(!isMale);

        ConfigurePreviewHoodieSwitchers();
    }

    private void ResolvePreviewReferences()
    {
        CleanupDuplicatePreviewRoots("HoodieSizes_Male", ref maleHoodieSizes);
        CleanupDuplicatePreviewRoots("HoodieSizes _Female", ref femaleHoodieSizes);

        if (previewRoot == null)
        {
            previewRoot = FindSceneObject("AR Scene");
        }

        if (maleHoodieSizes == null)
        {
            maleHoodieSizes = FindSceneObject("HoodieSizes_Male");
        }

        if (femaleHoodieSizes == null)
        {
            femaleHoodieSizes = FindSceneObject("HoodieSizes _Female");
        }
    }

    private void EnsurePreviewPresentations()
    {
        if (GeneratedUmaRecipesAvailable())
        {
            return;
        }

        if (isMale)
        {
            RebuildPreviewRoot(ref maleHoodieSizes, "HoodieSizes_Male");
        }
        else
        {
            RebuildPreviewRoot(ref femaleHoodieSizes, "HoodieSizes _Female");
        }
    }

    private void RebuildPreviewRoot(ref GameObject currentRoot, string instanceName)
    {
        if (currentRoot != null && currentRoot.GetComponent<PreviewGarmentRootMarker>() != null)
        {
            return;
        }

        if (currentRoot != null)
        {
            Destroy(currentRoot);
            currentRoot = null;
        }

        currentRoot = InstantiatePreviewHoodieRoot(instanceName);
    }

    private void CleanupDuplicatePreviewRoots(string rootName, ref GameObject currentReference)
    {
        GameObject first = null;
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null || !obj.scene.IsValid() || obj.name != rootName)
            {
                continue;
            }

            if (first == null)
            {
                first = obj;
                continue;
            }

            Debug.LogWarning($"OnboardingManager destroying duplicate preview root '{rootName}' on '{obj.transform.parent?.name ?? "null"}'.", this);
            Destroy(obj);
        }

        if (currentReference == null)
        {
            currentReference = first;
        }
    }

    private GameObject InstantiatePreviewHoodieRoot(string instanceName)
    {
        var parent = previewRoot != null ? previewRoot.transform : null;
        var instance = new GameObject(instanceName);
        if (parent != null)
        {
            instance.transform.SetParent(parent, false);
        }
        instance.name = instanceName;
        var instanceTransform = instance.transform;
        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = Vector3.one;
        instance.SetActive(false);
        instance.AddComponent<PreviewGarmentRootMarker>();

        var hoodieS = InstantiateRepairedHoodie(PreviewPrefabS, instance.transform, "Hoodie_S");
        var hoodieM = InstantiateRepairedHoodie(PreviewPrefabM, instance.transform, "Hoodie_M");
        var hoodieL = InstantiateRepairedHoodie(PreviewPrefabL, instance.transform, "Hoodie_L");
        var hoodieXL = InstantiateRepairedHoodie(PreviewPrefabXL, instance.transform, "Hoodie_XL");

        if (hoodieS == null || hoodieM == null || hoodieL == null || hoodieXL == null)
        {
            Debug.LogWarning($"OnboardingManager could not build repaired hoodie preview root '{instanceName}'. Falling back to legacy prefab if available.", this);
            Destroy(instance);

            var fallbackPrefab = instanceName.IndexOf("Female", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? femaleLegacyHoodiePrefab
                : maleLegacyHoodiePrefab;

            if (fallbackPrefab == null)
            {
                return null;
            }

            var fallback = Instantiate(fallbackPrefab, parent);
            fallback.name = instanceName;
            fallback.transform.localPosition = Vector3.zero;
            fallback.transform.localRotation = Quaternion.identity;
            fallback.transform.localScale = Vector3.one;
            fallback.SetActive(false);
            return fallback;
        }

        var switcher = instance.AddComponent<HoodieSizeSwitcher>();
        switcher.hoodieS = hoodieS;
        switcher.hoodieM = hoodieM;
        switcher.hoodieL = hoodieL;
        switcher.hoodieXL = hoodieXL;
        switcher.useLegacyFallback = true;

        Debug.Log($"OnboardingManager instantiated repaired preview hoodie root '{instanceName}'.", this);
        return instance;
    }

    private GameObject InstantiateRepairedHoodie(string resourcePath, Transform parent, string instanceName)
    {
        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"OnboardingManager could not load repaired hoodie resource '{resourcePath}'.", this);
            return null;
        }

        var instance = Instantiate(prefab, parent);
        instance.name = instanceName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        SanitizeRepairedHoodieInstance(instance);
        instance.SetActive(false);
        return instance;
    }

    private void SanitizeRepairedHoodieInstance(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (renderer == null)
        {
            return;
        }

        var rendererTransform = renderer.transform;
        if (rendererTransform.parent != instance.transform)
        {
            rendererTransform.SetParent(instance.transform, false);
        }

        rendererTransform.localPosition = Vector3.zero;
        rendererTransform.localRotation = Quaternion.identity;
        rendererTransform.localScale = Vector3.one;

        foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
        {
            if (transform == null || transform == instance.transform || transform == rendererTransform)
            {
                continue;
            }

            if (transform.IsChildOf(rendererTransform))
            {
                continue;
            }

            transform.gameObject.SetActive(false);
        }
    }

    private void ConfigurePreviewHoodieSwitchers()
    {
        if (isMale)
        {
            ConfigurePreviewHoodieSwitcher(maleUnified, maleHoodieSizes, "Male");
            DisablePreviewHoodieSwitcher(femaleUnified);
        }
        else
        {
            ConfigurePreviewHoodieSwitcher(femaleUnified, femaleHoodieSizes, "Female");
            DisablePreviewHoodieSwitcher(maleUnified);
        }
    }

    private void ConfigurePreviewHoodieSwitcher(GameObject avatarObject, GameObject previewRootObject, string label)
    {
        if (avatarObject == null)
        {
            return;
        }

        var avatar = avatarObject.GetComponent<DynamicCharacterAvatar>();
        var avatarSwitcher = avatarObject.GetComponent<HoodieSizeSwitcher>();

        if (avatar == null || avatarSwitcher == null)
        {
            return;
        }

        avatarSwitcher.avatar = avatar;

        if (TryConfigureFixedDisplaySwitcher(avatarObject, avatar, avatarSwitcher, label))
        {
            if (previewRootObject != null)
            {
                previewRootObject.SetActive(false);
            }
            return;
        }

        if (TryConfigureUmaRecipeSwitcher(avatar, avatarSwitcher, label))
        {
            if (previewRootObject != null)
            {
                previewRootObject.SetActive(false);
            }
            return;
        }

        if (previewRootObject == null)
        {
            return;
        }

        var previewSwitcher = previewRootObject.GetComponent<HoodieSizeSwitcher>();
        if (previewSwitcher == null)
        {
            return;
        }

        avatarSwitcher.hoodieS = previewSwitcher.hoodieS;
        avatarSwitcher.hoodieM = previewSwitcher.hoodieM;
        avatarSwitcher.hoodieL = previewSwitcher.hoodieL;
        avatarSwitcher.hoodieXL = previewSwitcher.hoodieXL;
        avatarSwitcher.useLegacyFallback = true;
        avatarSwitcher.smallRecipe = null;
        avatarSwitcher.mediumRecipe = null;
        avatarSwitcher.largeRecipe = null;
        avatarSwitcher.extraLargeRecipe = null;

        if (boneAttacher != null)
        {
            var renderers = previewRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var hoodieRenderers = new List<SkinnedMeshRenderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer != null && renderer.name.IndexOf("hoodie", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hoodieRenderers.Add(renderer);
                }
            }

            if (label == "Male")
            {
                boneAttacher.maleHoodies = hoodieRenderers.ToArray();
            }
            else
            {
                boneAttacher.femaleHoodies = hoodieRenderers.ToArray();
            }
        }

        avatar.ClearSlot("Chest");
        if (avatar.gameObject.activeInHierarchy)
        {
            avatar.BuildCharacter(true);
        }

        Debug.Log(
            $"OnboardingManager configured {label} preview switcher for repaired hoodie overlays on '{avatar.name}' " +
            $"hoodies=[{DescribeHoodieBinding(avatarSwitcher.hoodieS)}, {DescribeHoodieBinding(avatarSwitcher.hoodieM)}, {DescribeHoodieBinding(avatarSwitcher.hoodieL)}, {DescribeHoodieBinding(avatarSwitcher.hoodieXL)}].",
            avatar);
    }

    private bool TryConfigureFixedDisplaySwitcher(GameObject avatarObject, DynamicCharacterAvatar avatar, HoodieSizeSwitcher avatarSwitcher, string label)
    {
        var small = Resources.Load<GameObject>(FixedDisplayPrefabS);
        var medium = Resources.Load<GameObject>(FixedDisplayPrefabM);
        var large = Resources.Load<GameObject>(FixedDisplayPrefabL);
        var extraLarge = Resources.Load<GameObject>(FixedDisplayPrefabXL);

        if (small == null || medium == null || large == null || extraLarge == null)
        {
            return false;
        }

        var garmentSlot = avatarObject.GetComponent<GarmentSlot>();
        if (garmentSlot == null)
        {
            garmentSlot = avatarObject.AddComponent<GarmentSlot>();
        }

        garmentSlot.SetAnchor(GetOrCreateFixedGarmentAnchor(label, avatarObject.transform));
        garmentSlot.Clear();

        avatarSwitcher.smallRecipe = null;
        avatarSwitcher.mediumRecipe = null;
        avatarSwitcher.largeRecipe = null;
        avatarSwitcher.extraLargeRecipe = null;
        avatarSwitcher.hoodieS = null;
        avatarSwitcher.hoodieM = null;
        avatarSwitcher.hoodieL = null;
        avatarSwitcher.hoodieXL = null;
        avatarSwitcher.useLegacyFallback = false;
        avatarSwitcher.garmentSlot = garmentSlot;
        avatarSwitcher.fixedHoodieSPrefab = small;
        avatarSwitcher.fixedHoodieMPrefab = medium;
        avatarSwitcher.fixedHoodieLPrefab = large;
        avatarSwitcher.fixedHoodieXLPrefab = extraLarge;
        avatarSwitcher.useFixedGarmentPresentation = true;

        avatar.ClearSlot("Chest");
        if (avatar.gameObject.activeInHierarchy)
        {
            avatar.BuildCharacter(true);
        }

        Debug.Log(
            $"OnboardingManager configured {label} preview switcher for fixed garment hoodies on '{avatar.name}' " +
            $"prefabs=[{small.name}, {medium.name}, {large.name}, {extraLarge.name}].",
            avatar);
        return true;
    }

    private Transform GetOrCreateFixedGarmentAnchor(string label, Transform avatarTransform)
    {
        var anchorName = $"FixedGarmentAnchor_{label}";
        var parent = previewRoot != null ? previewRoot.transform : avatarTransform.parent;
        if (parent == null)
        {
            parent = avatarTransform;
        }

        var existing = parent.Find(anchorName);
        if (existing != null)
        {
            existing.SetPositionAndRotation(avatarTransform.position, avatarTransform.rotation);
            existing.localScale = Vector3.one;
            return existing;
        }

        var anchor = new GameObject(anchorName).transform;
        anchor.SetParent(parent, false);
        anchor.SetPositionAndRotation(avatarTransform.position, avatarTransform.rotation);
        anchor.localScale = Vector3.one;
        return anchor;
    }

    private bool TryConfigureUmaRecipeSwitcher(DynamicCharacterAvatar avatar, HoodieSizeSwitcher avatarSwitcher, string label)
    {
        if (!TryLoadGeneratedHoodieRecipes(out var small, out var medium, out var large, out var extraLarge))
        {
            return false;
        }

        avatarSwitcher.smallRecipe = small;
        avatarSwitcher.mediumRecipe = medium;
        avatarSwitcher.largeRecipe = large;
        avatarSwitcher.extraLargeRecipe = extraLarge;
        avatarSwitcher.hoodieS = null;
        avatarSwitcher.hoodieM = null;
        avatarSwitcher.hoodieL = null;
        avatarSwitcher.hoodieXL = null;
        avatarSwitcher.useLegacyFallback = false;
        avatarSwitcher.garmentSlot = null;
        avatarSwitcher.fixedHoodieSPrefab = null;
        avatarSwitcher.fixedHoodieMPrefab = null;
        avatarSwitcher.fixedHoodieLPrefab = null;
        avatarSwitcher.fixedHoodieXLPrefab = null;
        avatarSwitcher.useFixedGarmentPresentation = false;

        avatar.ClearSlot("Chest");
        if (avatar.gameObject.activeInHierarchy)
        {
            avatar.BuildCharacter(true);
        }

        Debug.Log(
            $"OnboardingManager configured {label} preview switcher for UMA hoodie recipes on '{avatar.name}' " +
            $"recipes=[{small.name}, {medium.name}, {large.name}, {extraLarge.name}].",
            avatar);
        return true;
    }

    private void DisablePreviewHoodieSwitcher(GameObject avatarObject)
    {
        if (avatarObject == null)
        {
            return;
        }

        var switcher = avatarObject.GetComponent<HoodieSizeSwitcher>();
        if (switcher == null)
        {
            return;
        }

        if (switcher.garmentSlot != null)
        {
            switcher.garmentSlot.Clear();
        }

        switcher.garmentSlot = null;
        switcher.fixedHoodieSPrefab = null;
        switcher.fixedHoodieMPrefab = null;
        switcher.fixedHoodieLPrefab = null;
        switcher.fixedHoodieXLPrefab = null;
        switcher.useFixedGarmentPresentation = false;
        switcher.useLegacyFallback = false;
    }

    private void ApplyDefaultPreviewHoodieSize()
    {
        var activeAvatar = GetActiveAvatar();
        if (activeAvatar == null)
        {
            return;
        }

        var switcher = activeAvatar.GetComponent<HoodieSizeSwitcher>();
        if (switcher == null)
        {
            return;
        }

        if (switcher.useFixedGarmentPresentation && switcher.fixedHoodieMPrefab == null)
        {
            return;
        }

        if (switcher.useLegacyFallback && switcher.hoodieM == null)
        {
            return;
        }

        if (!switcher.useFixedGarmentPresentation && !switcher.useLegacyFallback && switcher.mediumRecipe == null)
        {
            return;
        }

        switcher.ShowM();

        if (boneAttacher != null && switcher.useLegacyFallback)
        {
            if (isMale)
            {
                boneAttacher.AttachToMale();
                boneAttacher.DumpMaleState("OnboardingApplyMeasurements");
            }
            else
            {
                boneAttacher.AttachToFemale();
                boneAttacher.DumpFemaleState("OnboardingApplyMeasurements");
            }
        }
    }

    private void SetPreviewActive(bool isActive)
    {
        if (previewRoot != null)
        {
            previewRoot.SetActive(isActive);
            Debug.Log($"OnboardingManager.SetPreviewActive root='{previewRoot.name}' active={isActive}.", this);
        }
    }

    private AvatarBodyMeasurementApplier.BodyDnaProfile BuildBodyScaleProfile(float heightCm, float chestCm)
    {
        var clampedHeightRatio = Mathf.Clamp(heightCm / REF_HEIGHT_CM, MIN_HEIGHT_RATIO, MAX_HEIGHT_RATIO);
        var clampedChestRatio = Mathf.Clamp(chestCm / REF_CHEST_CM, MIN_CHEST_RATIO, MAX_CHEST_RATIO);

        var heightDelta = clampedHeightRatio - 1f;
        var chestDelta = clampedChestRatio - 1f;

        if (DiagnosticBodyScaling)
        {
            return new AvatarBodyMeasurementApplier.BodyDnaProfile
            {
                // Keep shared length DNA near neutral so the hoodie does not inherit the person's
                // height too strongly. Let bulk DNA carry most visible body variation instead.
                height = AmplifyDna(0.5f + (heightDelta * 0.10f), 0.85f, 0.44f, 0.56f),
                upperWeight = AmplifyDna(0.5f + (chestDelta * 0.52f), 1.30f, 0.22f, 0.82f),
                lowerWeight = AmplifyDna(0.5f + (chestDelta * 0.34f), 1.15f, 0.26f, 0.74f),
                belly = AmplifyDna(0.5f + (chestDelta * 0.46f), 1.30f, 0.20f, 0.84f),
                waist = AmplifyDna(0.5f + (chestDelta * 0.42f), 1.25f, 0.22f, 0.82f),
                legsSize = AmplifyDna(0.5f + (chestDelta * 0.22f), 1.05f, 0.32f, 0.68f),
                // Keep arm DNA neutral so the skinned hoodie sleeves do not track body changes.
                armWidth = 0.5f,
                forearmWidth = 0.5f,
                armLength = 0.5f,
                forearmLength = 0.5f
            };
        }

        return new AvatarBodyMeasurementApplier.BodyDnaProfile
        {
            height = Mathf.Clamp(0.5f + (heightDelta * 0.18f), 0.40f, 0.60f),
            upperWeight = Mathf.Clamp(0.5f + (chestDelta * 0.24f), 0.34f, 0.66f),
            lowerWeight = Mathf.Clamp(0.5f + (chestDelta * 0.18f), 0.36f, 0.64f),
            belly = Mathf.Clamp(0.5f + (chestDelta * 0.22f), 0.32f, 0.68f),
            waist = Mathf.Clamp(0.5f + (chestDelta * 0.20f), 0.34f, 0.66f),
            legsSize = Mathf.Clamp(0.5f + (chestDelta * 0.10f), 0.40f, 0.60f),
            armWidth = 0.5f,
            forearmWidth = 0.5f,
            armLength = 0.5f,
            forearmLength = 0.5f
        };
    }

    private static float AmplifyDna(float rawValue, float factor, float min, float max)
    {
        var amplified = 0.5f + ((rawValue - 0.5f) * factor);
        return Mathf.Clamp(amplified, min, max);
    }

    private DynamicCharacterAvatar GetActiveAvatar()
    {
        var avatarObject = isMale ? maleUnified : femaleUnified;
        return avatarObject != null ? avatarObject.GetComponent<DynamicCharacterAvatar>() : null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null || !obj.scene.IsValid())
            {
                continue;
            }

            if (obj.name == objectName)
            {
                return obj;
            }
        }

        return null;
    }

    private static string DescribeHoodieBinding(GameObject hoodieObject)
    {
        if (hoodieObject == null)
        {
            return "null";
        }

        var marker = hoodieObject.GetComponentInParent<PreviewGarmentRootMarker>(true);
        var source = marker != null ? "repaired" : "legacy";
        return $"{hoodieObject.name}:{source}";
    }

    private static bool GeneratedUmaRecipesAvailable()
    {
        return TryLoadGeneratedHoodieRecipes(out _, out _, out _, out _);
    }

    private static bool TryLoadGeneratedHoodieRecipes(
        out UMAWardrobeRecipe small,
        out UMAWardrobeRecipe medium,
        out UMAWardrobeRecipe large,
        out UMAWardrobeRecipe extraLarge)
    {
        small = FindWardrobeRecipeByName(GeneratedRecipeS);
        medium = FindWardrobeRecipeByName(GeneratedRecipeM);
        large = FindWardrobeRecipeByName(GeneratedRecipeL);
        extraLarge = FindWardrobeRecipeByName(GeneratedRecipeXL);
        return small != null && medium != null && large != null && extraLarge != null;
    }

    private static UMAWardrobeRecipe FindWardrobeRecipeByName(string recipeName)
    {
        try
        {
            var indexer = UMAAssetIndexer.Instance;
            if (indexer != null)
            {
                var indexedRecipe = indexer.GetAsset<UMAWardrobeRecipe>(recipeName);
                if (indexedRecipe != null)
                {
                    return indexedRecipe;
                }
            }
        }
        catch
        {
            // Ignore indexer timing/setup issues and fall back to a scene-wide asset scan.
        }

        var recipes = Resources.FindObjectsOfTypeAll<UMAWardrobeRecipe>();
        for (var i = 0; i < recipes.Length; i++)
        {
            var recipe = recipes[i];
            if (recipe == null)
            {
                continue;
            }

            if (recipe.name == recipeName || recipe.DisplayValue == recipeName)
            {
                return recipe;
            }
        }

        return null;
    }
}
