using System;
using System.Collections.Generic;
using System.IO;
using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class HoodieSizeGeneratorWindow : EditorWindow
{
    private const string GeneratedRootFolderName = "Generated";
    private const string HoodieSizesFolderName = "HoodieSizes";
    private const string RecipesFolderName = "Recipes";
    private const string MeshModifiersFolderName = "MeshModifiers";

    private static readonly SizeDefinition[] SizeDefinitions =
    {
        new SizeDefinition("S", new GarmentProfile(-0.060f, -0.045f, -0.045f, -0.020f, -0.015f, -0.035f, -0.030f)),
        new SizeDefinition("M", GarmentProfile.Neutral),
        new SizeDefinition("L", new GarmentProfile(0.085f, 0.060f, 0.065f, 0.030f, 0.020f, 0.085f, 0.070f)),
        new SizeDefinition("XL", new GarmentProfile(0.150f, 0.105f, 0.115f, 0.055f, 0.035f, 0.160f, 0.135f)),
    };

    [Serializable]
    private readonly struct GarmentProfile
    {
        public readonly float torsoEase;
        public readonly float shoulderEase;
        public readonly float hemEase;
        public readonly float torsoLength;
        public readonly float sleeveLength;
        public readonly float sleeveWidth;
        public readonly float cuffOpening;

        public static GarmentProfile Neutral => new GarmentProfile(0f, 0f, 0f, 0f, 0f, 0f, 0f);

        public GarmentProfile(
            float torsoEase,
            float shoulderEase,
            float hemEase,
            float torsoLength,
            float sleeveLength,
            float sleeveWidth,
            float cuffOpening)
        {
            this.torsoEase = torsoEase;
            this.shoulderEase = shoulderEase;
            this.hemEase = hemEase;
            this.torsoLength = torsoLength;
            this.sleeveLength = sleeveLength;
            this.sleeveWidth = sleeveWidth;
            this.cuffOpening = cuffOpening;
        }
    }

    [Serializable]
    private readonly struct SizeDefinition
    {
        public readonly string suffix;
        public readonly GarmentProfile profile;

        public SizeDefinition(string suffix, GarmentProfile profile)
        {
            this.suffix = suffix;
            this.profile = profile;
        }
    }

    private UMAWardrobeRecipe baseRecipe;
    private string outputFolder = "Assets/Generated/HoodieSizes";
    private bool autoConfigureSceneSwitchers = true;
    private bool autoConfigureSceneAvatars = true;
    private bool enableDistinctRendererFallback;

    [MenuItem("Tools/Hoodie/Generate UMA Hoodie Sizes")]
    public static void ShowWindow()
    {
        var window = GetWindow<HoodieSizeGeneratorWindow>("Hoodie Size Generator");
        window.minSize = new Vector2(520f, 280f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("UMA Hoodie Size Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generates hoodie-only UMA wardrobe variants and slot-local MeshModifier assets, then optionally auto-configures the active scene avatar and switcher objects.",
            MessageType.Info);

        using (new EditorGUI.ChangeCheckScope())
        {
            baseRecipe = (UMAWardrobeRecipe)EditorGUILayout.ObjectField("Base Recipe", baseRecipe, typeof(UMAWardrobeRecipe), false);
            if (baseRecipe != null && string.IsNullOrWhiteSpace(outputFolder))
            {
                outputFolder = GetDefaultOutputFolder(baseRecipe);
            }
        }

        using (new EditorGUI.DisabledScope(baseRecipe == null))
        {
            if (string.IsNullOrWhiteSpace(outputFolder) && baseRecipe != null)
            {
                outputFolder = GetDefaultOutputFolder(baseRecipe);
            }
        }

        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        autoConfigureSceneSwitchers = EditorGUILayout.Toggle("Configure Scene Switchers", autoConfigureSceneSwitchers);
        autoConfigureSceneAvatars = EditorGUILayout.Toggle("Configure Scene Avatars", autoConfigureSceneAvatars);
        enableDistinctRendererFallback = EditorGUILayout.Toggle("Enable Renderer Fallback", enableDistinctRendererFallback);

        EditorGUILayout.Space();
        DrawVariantPreview();
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(baseRecipe == null))
        {
            if (GUILayout.Button("Generate Hoodie Variants", GUILayout.Height(36f)))
            {
                Generate();
            }
        }
    }

    private void DrawVariantPreview()
    {
        EditorGUILayout.LabelField("Variant Scales", EditorStyles.boldLabel);
        for (var i = 0; i < SizeDefinitions.Length; i++)
        {
            var definition = SizeDefinitions[i];
            EditorGUILayout.LabelField(
                definition.suffix,
                $"torso {definition.profile.torsoEase:+0.000;-0.000;0.000}, " +
                $"shoulder {definition.profile.shoulderEase:+0.000;-0.000;0.000}, " +
                $"hem {definition.profile.hemEase:+0.000;-0.000;0.000}, " +
                $"length {definition.profile.torsoLength:+0.000;-0.000;0.000}, " +
                $"sleeve {definition.profile.sleeveWidth:+0.000;-0.000;0.000}");
        }
    }

    private void Generate()
    {
        if (baseRecipe == null)
        {
            Debug.LogWarning("HoodieSizeGeneratorWindow requires a base recipe.");
            return;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            outputFolder = GetDefaultOutputFolder(baseRecipe);
        }

        outputFolder = NormalizeAssetPath(outputFolder);

        var recipesFolder = GetRecipesFolder(outputFolder);
        var meshModifiersFolder = GetMeshModifiersFolder(outputFolder);

        EnsureFolderExists(recipesFolder);
        EnsureFolderExists(meshModifiersFolder);
        MoveExistingGeneratedAssets(recipeRootName: GetRecipeRootName(baseRecipe.name), recipesFolder, meshModifiersFolder);

        var recipeRootName = GetRecipeRootName(baseRecipe.name);
        var slotName = ExtractPrimarySlotName(baseRecipe);
        var slotAsset = FindSlotDataAsset(slotName);
        var meshModifierSupported = slotAsset != null && slotAsset.meshData != null && slotAsset.meshData.vertices != null && slotAsset.meshData.vertices.Length > 0;

        if (!meshModifierSupported)
        {
            Debug.LogWarning(
                $"HoodieSizeGeneratorWindow could not resolve slot mesh data for '{slotName}'. Recipes will still be generated, and runtime fallback can be enabled if the hoodie renders as a distinct renderer.");
        }

        var generatedVariants = new List<GeneratedVariant>(SizeDefinitions.Length);

        try
        {
            AssetDatabase.StartAssetEditing();

            for (var i = 0; i < SizeDefinitions.Length; i++)
            {
                var definition = SizeDefinitions[i];
                var variantName = $"{recipeRootName}_{definition.suffix}";
                var recipePath = NormalizeAssetPath(Path.Combine(recipesFolder, $"{variantName}.asset"));
                var meshModifierPath = NormalizeAssetPath(Path.Combine(meshModifiersFolder, $"{variantName}_MeshModifier.asset"));

                MeshModifier meshModifier = null;
                if (meshModifierSupported)
                {
                    meshModifier = CreateOrUpdateMeshModifier(meshModifierPath, variantName, slotAsset, slotName, definition.profile);
                }

                var recipe = CreateOrUpdateRecipeVariant(recipePath, variantName, baseRecipe, meshModifier, definition.profile);

                generatedVariants.Add(new GeneratedVariant
                {
                    recipe = recipe,
                    meshModifier = meshModifier,
                    scale = definition.profile.torsoEase,
                    recipePath = recipePath,
                    meshModifierPath = meshModifierPath
                });
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (autoConfigureSceneSwitchers)
        {
            ConfigureSceneSwitchers(generatedVariants);
        }

        if (autoConfigureSceneAvatars)
        {
            ConfigureSceneAvatars(generatedVariants, baseRecipe.wardrobeSlot, slotName, enableDistinctRendererFallback || !meshModifierSupported);
        }

        foreach (var variant in generatedVariants)
        {
            Debug.Log($"Created or updated hoodie recipe: {variant.recipePath}");
            if (variant.meshModifier != null)
            {
                Debug.Log($"Created or updated hoodie mesh modifier: {variant.meshModifierPath}");
            }
        }

        Debug.Log($"Hoodie size generation complete for base recipe '{baseRecipe.name}'.");
    }

    private static MeshModifier CreateOrUpdateMeshModifier(string assetPath, string variantName, SlotDataAsset slotAsset, string slotName, GarmentProfile profile)
    {
        var meshModifier = AssetDatabase.LoadAssetAtPath<MeshModifier>(assetPath);
        if (meshModifier == null)
        {
            meshModifier = ScriptableObject.CreateInstance<MeshModifier>();
            AssetDatabase.CreateAsset(meshModifier, assetPath);
        }

        var runtimeModifier = BuildGarmentFitModifier(slotAsset, slotName, profile, variantName);
        var editorModifier = BuildGarmentFitModifier(slotAsset, slotName, profile, variantName);

        meshModifier.name = $"{variantName}_MeshModifier";
        meshModifier.Modifiers = new List<MeshModifier.Modifier> { runtimeModifier };
#if UNITY_EDITOR
        meshModifier.EditorModifiers = new List<MeshModifier.Modifier> { editorModifier };
        meshModifier.AdHocAdjustmentJSON = new List<string>();
#endif

        foreach (var modifier in meshModifier.Modifiers)
        {
            modifier.BeforeSaving();
        }

        foreach (var modifier in meshModifier.EditorModifiers)
        {
            modifier.BeforeSaving();
        }

        EditorUtility.SetDirty(meshModifier);
        return meshModifier;
    }

    private static MeshModifier.Modifier BuildGarmentFitModifier(
        SlotDataAsset slotAsset,
        string slotName,
        GarmentProfile profile,
        string variantName)
    {
        var vertices = slotAsset.meshData.vertices;
        var bounds = new Bounds(vertices[0], Vector3.zero);
        for (var i = 1; i < vertices.Length; i++)
        {
            bounds.Encapsulate(vertices[i]);
        }

        var center = bounds.center;
        var extents = bounds.extents;
        var widthExtent = Mathf.Max(extents.x, 0.0001f);
        var depthExtent = Mathf.Max(extents.z, 0.0001f);
        var height = Mathf.Max(bounds.size.y, 0.0001f);
        var collection = new VertexDeltaAdjustmentCollection();

        for (var i = 0; i < vertices.Length; i++)
        {
            var relative = vertices[i] - center;
            var delta = CalculateGarmentDelta(relative, widthExtent, depthExtent, height, profile);
            var adjustment = new VertexDeltaAdjustment
            {
                vertexIndex = i,
                weight = 1f,
                delta = delta
            };
#if UNITY_EDITOR
            adjustment.slotName = slotName;
#endif
            collection.Add(adjustment);
        }

        var modifier = new MeshModifier.Modifier
        {
            SlotName = slotName,
            DNAName = string.Empty,
            Scale = 1f,
            adjustments = collection,
            CollectionType = typeof(VertexDeltaAdjustmentCollection).AssemblyQualifiedName,
            AdjustmentType = typeof(VertexDeltaAdjustment).AssemblyQualifiedName,
            JsonAdjustments = new List<string>()
        };

#if UNITY_EDITOR
        modifier.ModifierName = $"Fixed Garment {variantName}";
        modifier.manuallyModified = false;
        modifier.isTemporary = false;
        modifier.keepAsIs = true;
        modifier.TemplateAdjustment = new VertexDeltaAdjustment
        {
            vertexIndex = 0,
            weight = 1f,
            delta = Vector3.zero,
            slotName = slotName
        };
#endif

        return modifier;
    }

    private static Vector3 CalculateGarmentDelta(
        Vector3 relativeVertex,
        float widthExtent,
        float depthExtent,
        float height,
        GarmentProfile profile)
    {
        if (Mathf.Approximately(profile.torsoEase, 0f)
            && Mathf.Approximately(profile.shoulderEase, 0f)
            && Mathf.Approximately(profile.hemEase, 0f)
            && Mathf.Approximately(profile.torsoLength, 0f)
            && Mathf.Approximately(profile.sleeveLength, 0f)
            && Mathf.Approximately(profile.sleeveWidth, 0f)
            && Mathf.Approximately(profile.cuffOpening, 0f))
        {
            return Vector3.zero;
        }

        var normalizedY = Mathf.Clamp01((relativeVertex.y + (height * 0.5f)) / height);
        var normalizedX = Mathf.Clamp01(Mathf.Abs(relativeVertex.x) / widthExtent);
        var normalizedZ = Mathf.Clamp01(Mathf.Abs(relativeVertex.z) / depthExtent);
        var sideSign = Mathf.Sign(relativeVertex.x);
        if (Mathf.Approximately(sideSign, 0f))
        {
            sideSign = 1f;
        }

        var depthSign = Mathf.Sign(relativeVertex.z);
        if (Mathf.Approximately(depthSign, 0f))
        {
            depthSign = 1f;
        }

        var torsoBand = Band(normalizedY, 0.28f, 0.78f);
        var chestBand = Band(normalizedY, 0.45f, 0.76f);
        var shoulderBand = Band(normalizedY, 0.72f, 0.95f);
        var hemBand = 1f - SmoothRange(normalizedY, 0.28f, 0.48f);
        var sleeveAnchorBand = shoulderBand * SmoothRange(normalizedX, 0.58f, 0.92f);
        var sleeveTubeBand = Band(normalizedY, 0.34f, 0.82f) * SmoothRange(normalizedX, 0.62f, 0.98f);
        var forearmBand = sleeveTubeBand * (1f - SmoothRange(normalizedY, 0.66f, 0.84f));
        var cuffBand = sleeveTubeBand * Band(normalizedY, 0.34f, 0.50f);

        var torsoEase = torsoBand * profile.torsoEase;
        var chestDepthEase = chestBand * (profile.torsoEase * 0.72f);
        var shoulderEase = shoulderBand * profile.shoulderEase;
        var hemEase = hemBand * profile.hemEase;
        var sleeveEase = forearmBand * profile.sleeveWidth;
        var cuffEase = cuffBand * profile.cuffOpening;

        var deltaX = sideSign * widthExtent * normalizedX * (torsoEase + shoulderEase + hemEase + sleeveEase + cuffEase);
        var deltaZ = depthSign * depthExtent * Mathf.Lerp(0.45f, 1f, normalizedZ) * (chestDepthEase + hemEase * 0.55f + sleeveEase * 0.95f + cuffEase * 1.20f);

        var hemLength = hemBand * profile.torsoLength;
        var shoulderLift = shoulderBand * profile.torsoLength * 0.18f;
        var sleeveDrop = sleeveAnchorBand * profile.sleeveLength;
        var deltaY = (-hemLength + shoulderLift - sleeveDrop) * height;

        if (sleeveAnchorBand > 0f)
        {
            deltaX += sideSign * widthExtent * profile.sleeveLength * sleeveAnchorBand * 0.32f;
        }

        return new Vector3(deltaX, deltaY, deltaZ);
    }

    private static float Band(float value, float start, float end)
    {
        if (end <= start)
        {
            return 0f;
        }

        var rampIn = SmoothRange(value, start, Mathf.Lerp(start, end, 0.35f));
        var rampOut = 1f - SmoothRange(value, Mathf.Lerp(start, end, 0.65f), end);
        return Mathf.Clamp01(rampIn * rampOut);
    }

    private static float SmoothRange(float value, float start, float end)
    {
        if (end <= start)
        {
            return value >= end ? 1f : 0f;
        }

        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((value - start) / (end - start)));
    }

    private static UMAWardrobeRecipe CreateOrUpdateRecipeVariant(
        string assetPath,
        string variantName,
        UMAWardrobeRecipe baseRecipe,
        MeshModifier meshModifier,
        GarmentProfile profile)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<UMAWardrobeRecipe>(assetPath);
        if (recipe == null)
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(baseRecipe), assetPath))
            {
                throw new InvalidOperationException($"Could not duplicate recipe to '{assetPath}'.");
            }

            AssetDatabase.ImportAsset(assetPath);
            recipe = AssetDatabase.LoadAssetAtPath<UMAWardrobeRecipe>(assetPath);
        }

        EditorUtility.CopySerialized(baseRecipe, recipe);
        recipe.name = variantName;
        recipe.DisplayValue = variantName;
        recipe.MeshModifiers = meshModifier != null ? new List<MeshModifier> { meshModifier } : new List<MeshModifier>();
        recipe.UserField =
            $"Generated by HoodieSizeGeneratorWindow. Fixed garment profile: torso={profile.torsoEase:+0.000;-0.000;0.000}, " +
            $"shoulder={profile.shoulderEase:+0.000;-0.000;0.000}, hem={profile.hemEase:+0.000;-0.000;0.000}, " +
            $"length={profile.torsoLength:+0.000;-0.000;0.000}, sleeveLen={profile.sleeveLength:+0.000;-0.000;0.000}, " +
            $"sleeveWidth={profile.sleeveWidth:+0.000;-0.000;0.000}, cuff={profile.cuffOpening:+0.000;-0.000;0.000}.";

        EditorUtility.SetDirty(recipe);
        AssetDatabase.SetLabels(recipe, new[] { "UMA_Text Recipe", "UMA_Wardrobe Recipe" });
        return recipe;
    }

    private static void ConfigureSceneSwitchers(IReadOnlyList<GeneratedVariant> generatedVariants)
    {
        var switchers = FindSceneObjectsOfType<HoodieSizeSwitcher>();
        if (switchers.Count == 0)
        {
            Debug.LogWarning("HoodieSizeGeneratorWindow found no scene HoodieSizeSwitcher instances to configure.");
            return;
        }

        foreach (var switcher in switchers)
        {
            Undo.RecordObject(switcher, "Configure Hoodie Size Switcher");
            switcher.smallRecipe = FindVariant(generatedVariants, "S")?.recipe;
            switcher.mediumRecipe = FindVariant(generatedVariants, "M")?.recipe;
            switcher.largeRecipe = FindVariant(generatedVariants, "L")?.recipe;
            switcher.extraLargeRecipe = FindVariant(generatedVariants, "XL")?.recipe;

            if (switcher.avatar == null)
            {
                switcher.avatar = FindBestAvatarForSwitcher(switcher);
            }

            EditorUtility.SetDirty(switcher);
            MarkSceneDirty(switcher.gameObject);
            Debug.Log($"Configured HoodieSizeSwitcher on '{switcher.name}'.", switcher);
        }
    }

    private static void ConfigureSceneAvatars(
        IReadOnlyList<GeneratedVariant> generatedVariants,
        string wardrobeSlot,
        string targetSlotName,
        bool enableFallback)
    {
        var avatars = FindSceneObjectsOfType<DynamicCharacterAvatar>();
        if (avatars.Count == 0)
        {
            Debug.LogWarning("HoodieSizeGeneratorWindow found no scene DynamicCharacterAvatar instances to configure.");
            return;
        }

        foreach (var avatar in avatars)
        {
            var applier = avatar.GetComponent<HoodieSizeApplier>();
            if (applier == null)
            {
                applier = Undo.AddComponent<HoodieSizeApplier>(avatar.gameObject);
            }
            else
            {
                Undo.RecordObject(applier, "Configure Hoodie Size Applier");
            }

            applier.avatar = avatar;
            applier.wardrobeSlot = string.IsNullOrWhiteSpace(wardrobeSlot) ? "Chest" : wardrobeSlot;
            applier.targetSlotName = string.IsNullOrWhiteSpace(targetSlotName) ? "MaleHoodie" : targetSlotName;
            applier.enableDistinctRendererFallback = enableFallback;
            applier.forceFallbackWhenMeshModifierExists = false;
            applier.recipeScales = new[]
            {
                BuildScaleEntry(FindVariant(generatedVariants, "S"), 0.95f),
                BuildScaleEntry(FindVariant(generatedVariants, "M"), 1.00f),
                BuildScaleEntry(FindVariant(generatedVariants, "L"), 1.05f),
                BuildScaleEntry(FindVariant(generatedVariants, "XL"), 1.10f),
            };

            EditorUtility.SetDirty(applier);
            MarkSceneDirty(avatar.gameObject);
            Debug.Log($"Configured HoodieSizeApplier on avatar '{avatar.name}'.", avatar);
        }
    }

    private static HoodieSizeApplier.RecipeScaleEntry BuildScaleEntry(GeneratedVariant variant, float scale)
    {
        return new HoodieSizeApplier.RecipeScaleEntry
        {
            recipe = variant?.recipe,
            uniformScale = scale
        };
    }

    private static DynamicCharacterAvatar FindBestAvatarForSwitcher(HoodieSizeSwitcher switcher)
    {
        var avatars = FindSceneObjectsOfType<DynamicCharacterAvatar>();
        if (avatars.Count == 0)
        {
            return null;
        }

        if (avatars.Count == 1)
        {
            return avatars[0];
        }

        var switcherName = switcher.name.ToLowerInvariant();
        for (var i = 0; i < avatars.Count; i++)
        {
            var avatar = avatars[i];
            var raceName = avatar.activeRace.name ?? string.Empty;
            if (switcherName.Contains("female") && raceName.IndexOf("female", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return avatar;
            }

            if (switcherName.Contains("male") && raceName.IndexOf("male", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return avatar;
            }
        }

        return avatars[0];
    }

    private static List<T> FindSceneObjectsOfType<T>() where T : UnityEngine.Object
    {
        var results = new List<T>();
        var allObjects = Resources.FindObjectsOfTypeAll<T>();
        for (var i = 0; i < allObjects.Length; i++)
        {
            var obj = allObjects[i];
            if (obj == null || EditorUtility.IsPersistent(obj))
            {
                continue;
            }

            if (obj is Component component && component.gameObject.scene.IsValid())
            {
                results.Add(obj);
            }
        }

        return results;
    }

    private static GeneratedVariant FindVariant(IReadOnlyList<GeneratedVariant> generatedVariants, string suffix)
    {
        for (var i = 0; i < generatedVariants.Count; i++)
        {
            var variant = generatedVariants[i];
            if (variant != null && variant.recipe != null && variant.recipe.name.EndsWith($"_{suffix}", StringComparison.Ordinal))
            {
                return variant;
            }
        }

        return null;
    }

    private static void MarkSceneDirty(GameObject gameObject)
    {
        if (gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    private static string GetDefaultOutputFolder(UMAWardrobeRecipe recipe)
    {
        var recipePath = AssetDatabase.GetAssetPath(recipe);
        var folder = Path.GetDirectoryName(recipePath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return GetGeneratedRootFolder("Assets");
        }

        var normalizedFolder = NormalizeAssetPath(folder);
        var existingGeneratedRoot = TryGetExistingGeneratedRoot(normalizedFolder);
        return string.IsNullOrWhiteSpace(existingGeneratedRoot)
            ? GetGeneratedRootFolder("Assets")
            : existingGeneratedRoot;
    }

    private static string GetRecipeRootName(string recipeName)
    {
        if (recipeName.EndsWith("_Recipe", StringComparison.Ordinal))
        {
            return recipeName.Substring(0, recipeName.Length - "_Recipe".Length);
        }

        foreach (var definition in SizeDefinitions)
        {
            var suffix = "_" + definition.suffix;
            if (recipeName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return recipeName.Substring(0, recipeName.Length - suffix.Length);
            }
        }

        return recipeName;
    }

    private static string ExtractPrimarySlotName(UMAWardrobeRecipe recipe)
    {
        var packedRecipe = JsonUtility.FromJson<UMAPackedRecipeBase.UMAPackRecipe>(recipe.recipeString);
        if (packedRecipe != null && packedRecipe.slotsV3 != null)
        {
            for (var i = 0; i < packedRecipe.slotsV3.Length; i++)
            {
                var slot = packedRecipe.slotsV3[i];
                if (slot != null && !string.IsNullOrWhiteSpace(slot.id))
                {
                    return slot.id;
                }
            }
        }

        return "MaleHoodie";
    }

    private static SlotDataAsset FindSlotDataAsset(string slotName)
    {
        var indexer = UMAAssetIndexer.Instance;
        if (indexer != null)
        {
            var indexedAsset = indexer.GetAsset<SlotDataAsset>(slotName);
            if (indexedAsset != null)
            {
                return indexedAsset;
            }
        }

        var guids = AssetDatabase.FindAssets($"{slotName} t:SlotDataAsset");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var slot = AssetDatabase.LoadAssetAtPath<SlotDataAsset>(path);
            if (slot != null && string.Equals(slot.slotName, slotName, StringComparison.Ordinal))
            {
                return slot;
            }
        }

        return null;
    }

    private static void EnsureFolderExists(string folder)
    {
        var normalized = NormalizeAssetPath(folder);
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        var parent = Path.GetDirectoryName(normalized);
        var leaf = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        EnsureFolderExists(parent);
        AssetDatabase.CreateFolder(NormalizeAssetPath(parent), leaf);
    }

    private static string GetGeneratedRootFolder(string baseFolder)
    {
        var normalizedBaseFolder = NormalizeAssetPath(baseFolder);
        return NormalizeAssetPath(Path.Combine(normalizedBaseFolder, GeneratedRootFolderName, HoodieSizesFolderName));
    }

    private static string TryGetExistingGeneratedRoot(string folder)
    {
        var normalizedFolder = NormalizeAssetPath(folder).TrimEnd('/');
        var marker = $"/{GeneratedRootFolderName}/{HoodieSizesFolderName}";
        var markerIndex = normalizedFolder.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        return normalizedFolder.Substring(0, markerIndex + marker.Length);
    }

    private static string GetRecipesFolder(string generatedRootFolder)
    {
        return NormalizeAssetPath(Path.Combine(generatedRootFolder, RecipesFolderName));
    }

    private static string GetMeshModifiersFolder(string generatedRootFolder)
    {
        return NormalizeAssetPath(Path.Combine(generatedRootFolder, MeshModifiersFolderName));
    }

    private static void MoveExistingGeneratedAssets(string recipeRootName, string recipesFolder, string meshModifiersFolder)
    {
        for (var i = 0; i < SizeDefinitions.Length; i++)
        {
            var suffix = SizeDefinitions[i].suffix;
            MoveGeneratedAssetIfPresent(
                NormalizeAssetPath(Path.Combine("Assets", $"{recipeRootName}_{suffix}.asset")),
                NormalizeAssetPath(Path.Combine(recipesFolder, $"{recipeRootName}_{suffix}.asset")));
            MoveGeneratedAssetIfPresent(
                NormalizeAssetPath(Path.Combine("Assets", $"{recipeRootName}_{suffix}_MeshModifier.asset")),
                NormalizeAssetPath(Path.Combine(meshModifiersFolder, $"{recipeRootName}_{suffix}_MeshModifier.asset")));
        }
    }

    private static void MoveGeneratedAssetIfPresent(string sourcePath, string destinationPath)
    {
        if (string.Equals(sourcePath, destinationPath, StringComparison.Ordinal))
        {
            return;
        }

        if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
        {
            return;
        }

        if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
        {
            Debug.LogWarning($"Skipping move because destination already exists: {destinationPath}");
            return;
        }

        EnsureFolderExists(Path.GetDirectoryName(destinationPath));
        var moveError = AssetDatabase.MoveAsset(sourcePath, destinationPath);
        if (string.IsNullOrWhiteSpace(moveError))
        {
            Debug.Log($"Moved generated hoodie asset from '{sourcePath}' to '{destinationPath}'.");
            return;
        }

        Debug.LogWarning($"Could not move generated hoodie asset from '{sourcePath}' to '{destinationPath}': {moveError}");
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace('\\', '/');
    }

    [Serializable]
    private sealed class GeneratedVariant
    {
        public UMAWardrobeRecipe recipe;
        public MeshModifier meshModifier;
        public float scale;
        public string recipePath;
        public string meshModifierPath;
    }
}
