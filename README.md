# Vertigo Games – Technical Artist Demo

Technical Artist demo for Vertigo Games, built with **Unity 6000.3.9f1 (Unity 6.3 LTS)** and the **Universal Render Pipeline (URP)**.

| Task | Scene | Status |
|---|---|---|
| 1. Battle Pass Road and item state FX | `Assets/_Project/BattlePass/Scenes/BattlePass.unity` | Done |
| 2. Weapon VFX (MCX – Top Scorer) | `Assets/_Project/WeaponVFX/Scenes/WeaponVFX.unity` | Done |

## Opening the project

1. Install Unity **6000.3.9f1** through Unity Hub.
2. Clone this repository, then in Unity Hub choose **Add → Add project from disk** and select the cloned folder.
3. Open a scene from the table above, set the Game view to a landscape phone resolution (for example **2340 × 1080**) and press **Play**.

## Task 1 – Battle Pass Road

### What to test

- Drag the road, or use the mouse wheel, to scroll through the 30 levels. The screen opens by gliding to the player's progress.
- Tap a reward with a red **!** to claim it. Coins and gems fly into the wallet.
- Tap any other reward to see what it is and what unlocks it.
- Press **GET** on the season card to buy the premium pass: the reached premium rewards unlock in a wave from left to right.
- Press the green **💎 20** button on the progress bar to buy the next level.
- Press **P** to play a scripted walkthrough of all of the above (it taps through the real pointer events and shows a ring where each tap lands).
- Press **R** to restart the scene.

### Reward states

| State | How it reads |
|---|---|
| Locked | Level not reached yet: desaturated, dimmed card. |
| Premium | Top row. Without the pass it carries a padlock; reached levels say **UNLOCK NOW**. |
| Unlocked | The moment a reward becomes available it flashes, breaks its padlock, and fires light rays and sparkles. |
| Claimable | Red **!** badge with a gentle bob, a soft glow pulse and an occasional shine sweep, each card out of phase with its neighbours. |
| Claimed | Grey card with a green check. Claiming punches the card with a flash, a particle burst and a shockwave ring. |
| Current progress | Filled track, a lighter band behind the reached levels, a pulsing ring on the next level and the skip-level button at the head of the fill. |

### Technical notes

- **One UI material for the whole road.** `UIFx.shader` reads per-element parameters (state grading, flash, idle intensity, phase and effect flags) from UV1/UV2, written by `UIFxMeshEffect`, so every element can still batch. The idle loops (shine, pulse, bob, the season card's gold statue cycle) run on `_Time`: an idle screen costs no CPU and never rebuilds a canvas. Premultiplied alpha lets additive glows share the batch with alpha-blended cards.
- **Sprite atlases.** Two atlases, one for UI chrome and one for reward renders, took the idle screen from 83 to 30 draw calls. Tiled, rotated and particle textures stay unpacked.
- **Overdraw.** The background is the camera clear colour plus a single tiled pattern layer. Invisible hit areas use a raycast-only graphic that submits no geometry.
- **Particles.** Claim and unlock bursts are pooled, so claiming never instantiates anything. They use a minimal additive URP shader and existing small textures. The flying currency icons live on a nested canvas, so they don't rebuild the road's batches while they move.
- **Textures.** UI sprites have no mipmaps. The UI atlas uses ASTC 4×4 and the reward atlas ASTC 6×6. Soft glows use ASTC 8×8 capped at 512 px.
- **Layout.** 1920 × 1080 reference resolution matched on height, so wider phones see more of the road. Interactive content sits inside the device safe area. The mobile URP asset renders at full resolution: the canvas is drawn by the camera, so a lower render scale would blur the UI.
- **Tests.** `BattlePassProgress` holds the rules in plain C# with EditMode tests. A PlayMode test plays the whole flow, checks states and wallet, and logs the idle rendering cost. Run them from **Window → General → Test Runner**.
- **Tooling.** `BattlePass/Editor/BattlePassBuilder.cs` (**Tools → Vertigo Demo → Rebuild Battle Pass**) regenerates the Battle Pass materials, data, atlases, prefabs and scene hierarchy from code.

## Task 2 – Weapon VFX

### What to test

- Press **Play** in the WeaponVFX scene. The wind flows continuously, and the rifle sways slowly while nobody is touching it.
- Drag to rotate the rifle. Press **1** for the side view and **2** for the three-quarter view of the references.
- Press **P** for a scripted showcase: side view, three-quarter view, a slow full turn and back.

### How it is built

- **Wind ribbons (shader).** `WindRibbonMesh` generates six ribbons that leave the muzzle and sweep back along the front and underside of the rifle, like the reference. Each follows a smooth path through a few control points and rolls from a sheet into a thin line as it goes. They are built as a single mesh: one draw call and about 600 vertices, editable in the Inspector. `WindRibbon.shader` (hand-written HLSL, additive) does the following:
  - draws a thin bright line along one edge of each ribbon, with a soft translucent sheet trailing off the other side;
  - moves light along the ribbon by scrolling the provided streak sprite in two layers at different speeds;
  - fades the ribbon in and out over long, soft ends;
  - flutters it with a travelling wave in the vertex shader.

  Seed, speed and brightness for each ribbon travel in vertex colours, so one material covers all of them.
- **Weapon shader.** The rifle comes with a diffuse map only, so `WeaponLegendary.shader` derives the other layers:
  - warm, saturated texels are read as polished gold, with tinted, tighter specular and a fake sky reflection;
  - the pale ball inside the football cage glows and pulses, masked by an object-space sphere that skips the gold bars;
  - a sheen band sweeps the gold from muzzle to stock, following the wind.

  Lighting is the main light plus per-vertex SH, with no shadows.
- **Secondary effects (Particle System).**
  - A few small four-point glints: each is a camera-facing cross mesh of two quads, so no star texture is needed.
  - Fine dust drifting with the wind around the front of the rifle, with light turbulence.
  - A soft halo over the core.

  All of them simulate in local space, are capped at 4–14 particles each and share one additive URP shader.
- **Scene.** The backdrop is a full-screen radial gradient drawn by a clip-space quad, with no texture and dithered against banding. Post-processing runs inside the project: bloom at quarter resolution with 5 iterations, neutral tonemapping and a vignette.
- **Cost.** 27 draw calls (most of them the bloom chain), about 5,500 triangles and about 15 live particles.
- **Tooling.** `WeaponVFX/Editor/WeaponVfxBuilder.cs` (**Tools → Vertigo Demo → Rebuild Weapon VFX**) regenerates the materials, post-processing profile, weapon prefab and scene. A PlayMode test checks that every effect is alive and logs the rendering cost.

## Project layout

```
Assets/_Project/
  BattlePass/   Task 1: sprites, atlases, data, prefabs, shaders, scripts, tests and the scene
  WeaponVFX/    Task 2: model, texture, shaders, materials, meshes, post-processing, prefab, scripts, tests and the scene
  Shared/       Import rules and the additive particle shader used by both tasks
Assets/Settings/        URP pipeline assets
Assets/TextMesh Pro/    TextMesh Pro essential resources
```

Import settings are applied automatically by `Shared/Editor/AssetImportRules.cs` the first time an asset is imported: UI sprites get no mipmaps, 9-slice borders and ASTC compression on mobile (lower bit rate and max 512 px for soft glows); the weapon FBX skips cameras, lights, animation, materials and tangents.

## Asset ownership

The UI sprites, weapon model and textures were provided by Vertigo Games for this demo and remain their property. They are included only so the project opens and runs as submitted.
