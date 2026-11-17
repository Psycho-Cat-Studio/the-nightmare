# Graphics and display settings

Unity 6000.6.7f1 / URP 17.6. Open Settings from the main menu or pause menu.
The reusable uGUI view (`Assets/Resources/UI/SettingsPanel.prefab`) uses the existing menu
background, Nevermore font and return artwork; values/arrows use the regular existing UI font. Gameplay, Display and Effects tabs support
mouse, scoped arrow/Tab navigation and a visible scrollbar. Escape closes Settings before dismissing pause.

## Shared rendering

The local world camera is Base and its WeaponCamera is Overlay, clearing depth so first-person
weapons do not clip into the room. A single shared post-processing pass runs on the weapon
camera after the combined 3D image. The HUD is composed afterwards. UI/minimap cameras do not
receive gameplay post-processing. There is no separate weapon target, compositor or effects profile.

`RuntimeGraphics` owns runtime copies of the pipeline, renderer/features and
`Assets/Resources/Rendering/WorldEffects.asset`; preferences never edit their source assets.
All rendering quality levels use Forward+ with per-pixel additional lights. Rendering quality controls
shadows, textures and model detail; it does not change the separate Effects switches. V-Sync is disabled
at every level so Frame limit remains independent, including Unlimited. The saved limit is reapplied
after a quality change and at startup. Render scale is 1; FSR and sharpening are removed.

## Effects

- Anti-aliasing: Off / FXAA. Old saved TAA becomes FXAA because URP TAA cannot use camera stacks.
- Bloom, vignette and automatic exposure default on.
- Ambient occlusion, depth of field, motion blur, Neutral tonemapping, chromatic aberration and
  dithering default off. These are shared switches, with no weapon-specific options or legacy presets.
- With automatic exposure off, Exposure adjusts the shared image from -3 to +3 EV (default +1).
  Neutral tonemapping uses the existing `graphics.colorGrading` preference key for compatibility.

Automatic exposure meters an unprocessed, full-view 32x32 HDR world-only camera twice per second
using asynchronous GPU readback. The HUD and weapon do not enter the measurement. It retains
PPSv1's MaxRGB measurement, 45-95% trimming, dynamic-key formula, bounds and adaptation speeds;
reduced spatial/temporal sampling remains an approximation. Unsupported devices use manual EV.

Settings live under `graphics.*`, display settings under `display.*`, and gameplay settings under
`settings.*` in PlayerPrefs. Closing Settings, losing focus and quitting save preferences. Retired
FSR/weapon keys are ignored; unrelated preferences are never deleted.

## Resolution

Choose a reported or common resolution and borderless fullscreen/windowed mode, then Apply.
A 15-second confirmation protects against an unusable mode. Keep saves the actual applied size;
timeout, Revert, Escape, closing Settings or leaving the scene restores the previous mode. Repeated
unconfirmed changes retain the same original rollback target. Startup bounds a saved mode to the
current monitor. Borderless uses the desktop display mode; windowed mode resizes the window.

## Lighting and particles

Gameplay uses Skybox ambient and refreshes the environment after scene loads. There is no
Enlighten realtime GI and no contribution baked from switchable capture candles. CaptureArea's
12 candle-light overrides use intensity 0.032 with their original range 1, calibrated for URP's
inverse-square attenuation. Forward+ avoids per-object light selection popping in candle groups.
Enemy dissolve includes the clustered-light variant required by Forward+.

The original muzzle flash still uses ParticleFirework, ParticleFlare and additive URP particles.
Removing the intermediate alpha-composited weapon texture fixes the black rectangle without
replacing the effect. Pistol single mode is unchanged; B still selects its existing burst/auto modes.

## Verification

Run the opt-in native-player integration test:

```bash
./Builds/StandaloneLinux64/TheNightmare -screen-fullscreen 0 -screen-width 800 -screen-height 450 \
  -verifyGraphics -logFile "$PWD/Logs/graphics-player.log"
```

It restores preferences and exits with a status code. See `Logs/graphics-verification.json` for camera
stack, single-shot latch/ammo, effect switches, all quality levels, GPU adaptation, rendered bloom
halo pixels and native display rollback/persistence checks. Settings screenshots are saved in Logs.
Add `-keepVerificationHost` to keep a passing host alive on port 7799 for multiplayer QA.
A passing component flag alone is not visual acceptance. See [URP-FINALIZATION.md](URP-FINALIZATION.md)
and [URP-VERIFICATION.md](URP-VERIFICATION.md) for implementation history and remaining manual QA.
