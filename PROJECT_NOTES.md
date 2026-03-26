# Project Notes

## Repo
- GitHub repo: `joshua-meisenbacher/SizeCompare`
- Local project root: `/Users/joshuameisenbacher/SizeGuide2`

## Current Goal
- Let users enter body measurements.
- Show body changes on the avatar.
- Show hoodie sizes as fixed garments for fit comparison.
- Keep hoodie size independent from body scaling.

## Current Body System
- Body measurement flow is driven from:
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/MadeScripts/OnboardingManager.cs`
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/MadeScripts/AvatarBodyMeasurementApplier.cs`
- Body changes are currently DNA-driven, not direct bone-scaling driven.
- Reapply-after-rebuild is already implemented and was previously confirmed working.

## Current Hoodie Preview System
- Active preview path is the fixed-garment path, not the old UMA hoodie recipe path.
- Main files:
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/MadeScripts/HoodieSizes.cs`
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/MadeScripts/GarmentSlot.cs`
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/MadeScripts/FixedGarmentPoseDriver.cs`
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/MadeScripts/OnboardingManager.cs`
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/Editor/FixedHoodieDisplayBuilder.cs`

## Fixed Garment Assets
- Generated prefabs:
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/Resources/Generated/FixedHoodieDisplays/Hoodie_S_Fixed.prefab`
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/Resources/Generated/FixedHoodieDisplays/Hoodie_M_Fixed.prefab`
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/Resources/Generated/FixedHoodieDisplays/Hoodie_L_Fixed.prefab`
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/Resources/Generated/FixedHoodieDisplays/Hoodie_XL_Fixed.prefab`
- Builder menu:
  - `Tools > Hoodie > Build Fixed Hoodie Displays`

## Size Control
- The chart currently being treated as control:
  - `S`: width `18`, length `28`, chest `36`
  - `M`: width `20`, length `29`, chest `40`
  - `L`: width `22`, length `30`, chest `44`
  - `XL`: width `24`, length `31`, chest `48`

## Important Recent Findings
- The old UMA hoodie recipe path produced repeated recipe-load noise and unreliable garment behavior.
- The fixed-garment path is now the intended preview path.
- A major bug was duplicate humanoid bone names from the fixed hoodie clone conflicting with UMA's avatar skeleton.
- Scene cleanup was applied to remove stale serialized hoodie recipe references from `SampleScene`.

## Current Known Issues
- Fixed garment fit is still not calibrated well enough.
- Some sizes may distort or disappear depending on live Play-mode state.
- The latest debugging focus has been:
  - eliminating stale UMA hoodie recipe interference
  - eliminating duplicate skeleton-name conflicts
  - calibrating the fixed garment profiles so `S/M/L/XL` behave like actual chart-based fixed garments

## Scene
- Main scene:
  - `/Users/joshuameisenbacher/SizeGuide2/Assets/Scenes/SampleScene.unity`

## Suggested Next Debug Step
- Verify the current Play-mode behavior after the stale UMA recipe cleanup.
- If fixed garments still behave inconsistently, inspect the live instantiated fixed hoodie clone hierarchy and renderer state for each size.
- Then tune `FixedHoodieDisplayBuilder.cs`, not the old UMA recipe hoodie path.

