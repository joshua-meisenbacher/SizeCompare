using System;
using System.Collections.Generic;
using System.IO;
using UMA;
using UMA.CharacterSystem;
using UMA.Editors;
using UnityEditor;
using UnityEngine;

public static class RepairedHoodieUmaBuilder
{
    private const string ReferencePrefabPath = "Assets/UMA/Content/Contrib/MaleHoodie/MaleHoodie_Skinned.prefab";
    private const string ReferenceSlotPath = "Assets/UMA/Content/Contrib/MaleHoodie/MaleHoodie_Slot.asset";
    private const string BaseRecipePath = "Assets/MaleHoodie_Recipe.asset";

    private const string OutputRoot = "Assets/Generated/HoodieSizes";
    private const string SlotsFolder = OutputRoot + "/Slots";
    private const string RecipesFolder = OutputRoot + "/Recipes";
    private static readonly VariantDefinition[] Variants =
    {
        new VariantDefinition("S", "MaleHoodie_Slot_S", RecipesFolder + "/MaleHoodie_S.asset", 18f, 28f, 36f),
        new VariantDefinition("M", "MaleHoodie_Slot_M", RecipesFolder + "/MaleHoodie_M.asset", 20f, 29f, 40f),
        new VariantDefinition("L", "MaleHoodie_Slot_L", RecipesFolder + "/MaleHoodie_L.asset", 22f, 30f, 44f),
        new VariantDefinition("XL", "MaleHoodie_Slot_XL", RecipesFolder + "/MaleHoodie_XL.asset", 24f, 31f, 48f),
    };

    private const float ReferenceWidthInches = 20f;
    private const float ReferenceLengthInches = 29f;
    private const float ReferenceChestInches = 40f;

    [MenuItem("Tools/Hoodie/Build UMA Recipes From Repaired Hoodies")]
    public static void BuildAll()
    {
        EnsureFolder(SlotsFolder);
        EnsureFolder(RecipesFolder);

        var referenceSlot = AssetDatabase.LoadAssetAtPath<SlotDataAsset>(ReferenceSlotPath);
        var baseRecipe = AssetDatabase.LoadAssetAtPath<UMAWardrobeRecipe>(BaseRecipePath);
        var referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePrefabPath);

        if (referenceSlot == null || baseRecipe == null || referencePrefab == null)
        {
            throw new Exception("Missing hoodie UMA reference assets.");
        }

        foreach (var variant in Variants)
        {
            BuildVariant(referenceSlot, baseRecipe, referencePrefab, variant);
        }

        RegisterGeneratedAssetsWithIndexer();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RepairedHoodieUmaBuilder] Built hoodie UMA slots and recipes from repaired meshes.");
    }

    private static void BuildVariant(
        SlotDataAsset referenceSlot,
        UMAWardrobeRecipe baseRecipe,
        GameObject referencePrefab,
        VariantDefinition variant)
    {
        var slotPath = $"{SlotsFolder}/{variant.SlotName}_Slot.asset";
        EnsureReferenceSlotCopy(slotPath);
        var slot = AssetDatabase.LoadAssetAtPath<SlotDataAsset>(slotPath);
        if (slot == null)
        {
            throw new Exception($"Could not load generated slot at {slotPath}");
        }

        var workingRoot = UnityEngine.Object.Instantiate(referencePrefab);
        var seamsRoot = UnityEngine.Object.Instantiate(referencePrefab);

        Mesh generatedMesh = null;

        try
        {
            var workingRenderer = FindPrimaryRenderer(workingRoot);
            var seamsRenderer = FindPrimaryRenderer(seamsRoot);

            if (workingRenderer == null || seamsRenderer == null || seamsRenderer.sharedMesh == null || workingRenderer.sharedMesh == null)
            {
                throw new Exception($"Invalid reference renderer setup for size {variant.Size}");
            }

            generatedMesh = BuildVariantMeshFromCanonical(workingRenderer.sharedMesh, variant);
            ConfigureRendererBindings(workingRenderer, workingRoot.transform, seamsRenderer);
            ConfigureRendererBindings(seamsRenderer, seamsRoot.transform);
            workingRenderer.sharedMesh = generatedMesh;
            workingRenderer.updateWhenOffscreen = true;
            seamsRenderer.updateWhenOffscreen = true;

            if (workingRenderer.rootBone == null || !HasResolvedBones(workingRenderer.bones))
            {
                throw new Exception($"Resolved renderer bindings are invalid for size {variant.Size}");
            }

            slot.Assign(referenceSlot);
            slot.slotName = variant.SlotName;
            slot.nameHash = UMAUtils.StringToHash(slot.slotName);
            slot.material = referenceSlot.material;
            slot.normalReferenceMesh = seamsRenderer;

            UMASlotProcessingUtil.UpdateSlotData(
                slot,
                workingRenderer,
                referenceSlot.material,
                seamsRenderer,
                GetRootBoneName(referenceSlot, seamsRenderer),
                true);

            EditorUtility.SetDirty(slot);
            var recipe = CreateOrUpdateRecipe(baseRecipe, variant, slot.slotName);
            RegisterWithIndexer(slot, recipe);
            Debug.Log($"[RepairedHoodieUmaBuilder] Built {variant.Size} using slot '{slot.slotName}'.", slot);
        }
        finally
        {
            if (generatedMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(generatedMesh);
            }

            if (workingRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(workingRoot);
            }

            if (seamsRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(seamsRoot);
            }
        }
    }

    private static UMAWardrobeRecipe CreateOrUpdateRecipe(UMAWardrobeRecipe baseRecipe, VariantDefinition variant, string slotName)
    {
        EnsureReferenceRecipeCopy(variant.RecipeAssetPath);
        var recipe = AssetDatabase.LoadAssetAtPath<UMAWardrobeRecipe>(variant.RecipeAssetPath);
        if (recipe == null)
        {
            throw new Exception($"Could not load generated recipe at {variant.RecipeAssetPath}");
        }

        EditorUtility.CopySerialized(baseRecipe, recipe);
        recipe.name = $"MaleHoodie_{variant.Size}";
        recipe.DisplayValue = recipe.name;
        recipe.wardrobeSlot = baseRecipe.wardrobeSlot;
        recipe.MeshModifiers = new List<MeshModifier>();
        recipe.UserField = $"Built by RepairedHoodieUmaBuilder from the canonical UMA hoodie mesh using chart-based size targets for {variant.Size}.";

        var packed = baseRecipe.PackedLoad();
        if (packed.slotsV3 != null)
        {
            for (var i = 0; i < packed.slotsV3.Length; i++)
            {
                var slot = packed.slotsV3[i];
                if (slot != null && !string.IsNullOrEmpty(slot.id) && slot.id == "MaleHoodie")
                {
                    slot.id = slotName;
                }
            }
        }

        recipe.PackedSave(packed, null);
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static void EnsureReferenceSlotCopy(string slotPath)
    {
        if (AssetDatabase.LoadAssetAtPath<SlotDataAsset>(slotPath) != null)
        {
            return;
        }

        if (!AssetDatabase.CopyAsset(ReferenceSlotPath, slotPath))
        {
            throw new Exception($"Could not copy reference slot asset to {slotPath}");
        }
    }

    private static void EnsureReferenceRecipeCopy(string recipePath)
    {
        if (AssetDatabase.LoadAssetAtPath<UMAWardrobeRecipe>(recipePath) != null)
        {
            return;
        }

        if (!AssetDatabase.CopyAsset(BaseRecipePath, recipePath))
        {
            throw new Exception($"Could not copy base recipe asset to {recipePath}");
        }
    }

    private static SkinnedMeshRenderer FindPrimaryRenderer(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh != null && renderer.name.IndexOf("hoodie", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return renderer;
            }
        }

        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh != null)
            {
                return renderer;
            }
        }

        return null;
    }

    private static Mesh BuildVariantMeshFromCanonical(Mesh referenceMesh, VariantDefinition variant)
    {
        var result = UnityEngine.Object.Instantiate(referenceMesh);
        result.name = variant.SlotName + "_Mesh";

        var widthScale = variant.WidthInches / ReferenceWidthInches;
        var lengthScale = variant.LengthInches / ReferenceLengthInches;
        var chestScale = variant.ChestInches / ReferenceChestInches;
        var widthDelta = widthScale - 1f;
        var lengthDelta = lengthScale - 1f;
        var chestDelta = chestScale - 1f;

        var bounds = referenceMesh.bounds;
        var center = bounds.center;
        var size = bounds.size;
        var halfWidth = Mathf.Max(size.x * 0.5f, 0.0001f);
        var halfDepth = Mathf.Max(size.z * 0.5f, 0.0001f);
        var vertices = referenceMesh.vertices;
        var adjusted = new Vector3[vertices.Length];

        for (var i = 0; i < vertices.Length; i++)
        {
            var vertex = vertices[i];
            var relative = vertex - center;
            var y01 = size.y > 0.0001f ? Mathf.InverseLerp(bounds.min.y, bounds.max.y, vertex.y) : 0.5f;
            var xAbs01 = Mathf.Clamp01(Mathf.Abs(relative.x) / halfWidth);
            var zAbs01 = Mathf.Clamp01(Mathf.Abs(relative.z) / halfDepth);

            var hoodBand = Band(y01, 0.90f, 0.14f);
            var shoulderBand = Band(y01, 0.76f, 0.14f);
            var chestBand = Band(y01, 0.58f, 0.20f);
            var waistBand = Band(y01, 0.34f, 0.18f);
            var hemBand = Band(y01, 0.10f, 0.12f);
            var torsoBand = Mathf.Max(chestBand, waistBand);

            var sleeveBand = SmoothStepRange(0.56f, 0.78f, xAbs01);
            var cuffBand = SmoothStepRange(0.84f, 0.98f, xAbs01);
            var torsoOnlyBand = Mathf.Clamp01((1f - sleeveBand * 0.9f) * (0.45f * chestBand + 0.38f * waistBand + 0.24f * hemBand + 0.18f * shoulderBand));

            var xScale = 1f;
            xScale += widthDelta * (0.95f * torsoOnlyBand + 0.30f * shoulderBand + 0.55f * sleeveBand + 0.20f * cuffBand);

            var zScale = 1f;
            zScale += chestDelta * (0.92f * chestBand + 0.58f * waistBand + 0.34f * hemBand);
            zScale += widthDelta * (0.12f * hemBand + 0.08f * zAbs01 * chestBand);

            var xOffset = Mathf.Sign(relative.x) * (
                halfWidth * widthDelta * (0.10f * shoulderBand + 0.16f * cuffBand + 0.08f * sleeveBand));
            var zOffset = Mathf.Sign(relative.z) * (
                halfDepth * chestDelta * (0.14f * chestBand + 0.08f * waistBand));

            var yOffset =
                size.y * lengthDelta * (
                    -0.62f * hemBand
                    -0.22f * waistBand
                    -0.08f * chestBand
                    +0.06f * sleeveBand * cuffBand);

            var hoodDamping = 1f - hoodBand * 0.92f;
            xScale = 1f + (xScale - 1f) * hoodDamping;
            zScale = 1f + (zScale - 1f) * hoodDamping;
            xOffset *= hoodDamping;
            zOffset *= hoodDamping;
            yOffset *= hoodDamping;

            adjusted[i] = new Vector3(
                center.x + relative.x * xScale + xOffset,
                center.y + relative.y + yOffset,
                center.z + relative.z * zScale + zOffset);
        }

        result.vertices = adjusted;
        result.bindposes = referenceMesh.bindposes;
        result.boneWeights = referenceMesh.boneWeights;
        result.RecalculateNormals();
        result.RecalculateTangents();
        result.RecalculateBounds();
        return result;
    }

    private static float Band(float value01, float center01, float halfWidth01)
    {
        if (halfWidth01 <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - Mathf.Abs(value01 - center01) / halfWidth01);
    }

    private static float SmoothStepRange(float start, float end, float value)
    {
        if (value <= start)
        {
            return 0f;
        }

        if (value >= end)
        {
            return 1f;
        }

        var t = Mathf.InverseLerp(start, end, value);
        return t * t * (3f - 2f * t);
    }

    private static string GetRootBoneName(SlotDataAsset referenceSlot, SkinnedMeshRenderer seamsRenderer)
    {
        if (referenceSlot != null && referenceSlot.meshData != null && !string.IsNullOrEmpty(referenceSlot.meshData.RootBoneName))
        {
            return referenceSlot.meshData.RootBoneName;
        }

        if (seamsRenderer != null && seamsRenderer.rootBone != null)
        {
            return seamsRenderer.rootBone.name;
        }

        return "Hips";
    }

    private static void ConfigureRendererBindings(SkinnedMeshRenderer renderer, Transform root, SkinnedMeshRenderer sourceBindingRenderer = null)
    {
        if (renderer == null || root == null)
        {
            return;
        }

        var transforms = root.GetComponentsInChildren<Transform>(true);
        var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
        for (var i = 0; i < transforms.Length; i++)
        {
            var transform = transforms[i];
            if (transform != null && !byName.ContainsKey(transform.name))
            {
                byName.Add(transform.name, transform);
            }
        }

        var sourceBones = sourceBindingRenderer != null ? sourceBindingRenderer.bones : renderer.bones;
        if (sourceBones != null && sourceBones.Length > 0)
        {
            var resolvedBones = new Transform[sourceBones.Length];
            for (var i = 0; i < sourceBones.Length; i++)
            {
                var sourceBone = sourceBones[i];
                if (sourceBone != null && byName.TryGetValue(sourceBone.name, out var resolvedBone))
                {
                    resolvedBones[i] = resolvedBone;
                }
            }

            renderer.bones = resolvedBones;
        }

        var sourceRootBone = sourceBindingRenderer != null ? sourceBindingRenderer.rootBone : renderer.rootBone;
        if (sourceRootBone != null && byName.TryGetValue(sourceRootBone.name, out var resolvedRootBone))
        {
            renderer.rootBone = resolvedRootBone;
        }
        else if (byName.TryGetValue("Hips", out var hips))
        {
            renderer.rootBone = hips;
        }
        else if (renderer.bones != null)
        {
            for (var i = 0; i < renderer.bones.Length; i++)
            {
                if (renderer.bones[i] != null)
                {
                    renderer.rootBone = renderer.bones[i];
                    break;
                }
            }
        }
    }

    private static bool HasResolvedBones(Transform[] bones)
    {
        if (bones == null || bones.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static void RegisterWithIndexer(SlotDataAsset slot, UMAWardrobeRecipe recipe)
    {
        var indexer = UMAAssetIndexer.Instance;
        if (indexer == null)
        {
            Debug.LogWarning("[RepairedHoodieUmaBuilder] UMAAssetIndexer.Instance is null; generated assets were not registered.");
            return;
        }

        if (slot != null)
        {
            indexer.AddAsset(typeof(SlotDataAsset), slot.slotName, AssetDatabase.GetAssetPath(slot), slot);
        }

        if (recipe != null)
        {
            indexer.AddAsset(typeof(UMAWardrobeRecipe), recipe.name, AssetDatabase.GetAssetPath(recipe), recipe);
        }

        EditorUtility.SetDirty(indexer);
    }

    private static void RegisterGeneratedAssetsWithIndexer()
    {
        var indexer = UMAAssetIndexer.Instance;
        if (indexer == null)
        {
            return;
        }

        foreach (var variant in Variants)
        {
            var slot = AssetDatabase.LoadAssetAtPath<SlotDataAsset>($"{SlotsFolder}/{variant.SlotName}_Slot.asset");
            var recipe = AssetDatabase.LoadAssetAtPath<UMAWardrobeRecipe>(variant.RecipeAssetPath);
            RegisterWithIndexer(slot, recipe);
        }

        indexer.ForceSave();
    }

    private readonly struct VariantDefinition
    {
        public readonly string Size;
        public readonly string SlotName;
        public readonly string RecipeAssetPath;
        public readonly float WidthInches;
        public readonly float LengthInches;
        public readonly float ChestInches;

        public VariantDefinition(string size, string slotName, string recipeAssetPath, float widthInches, float lengthInches, float chestInches)
        {
            Size = size;
            SlotName = slotName;
            RecipeAssetPath = recipeAssetPath;
            WidthInches = widthInches;
            LengthInches = lengthInches;
            ChestInches = chestInches;
        }
    }
}
