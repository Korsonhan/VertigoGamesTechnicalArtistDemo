# Vertigo Games – Technical Artist Demo

Technical Artist demo for Vertigo Games, built with **Unity 6000.3.9f1 (Unity 6.3 LTS)** and the **Universal Render Pipeline (URP)**.

| Task | Scene | Status |
|---|---|---|
| 1. Battle Pass Road and item state FX | `Assets/_Project/BattlePass/Scenes/BattlePass.unity` | Done |
| 2. Weapon VFX (MCX – Top Scorer) | `Assets/_Project/WeaponVFX/Scenes/WeaponVFX.unity` | Done |

## Opening the project

1. Install Unity **6000.3.9f1** through Unity Hub.
2. Clone this repository, then in Unity Hub choose **Add → Add project from disk** and select the cloned folder.
3. Open a scene from the table above, set the Game view to a landscape phone resolution (for example **2340 × 1080**) and press **Play**. To fill the whole editor window, pick **Play Maximized** in the Game view's **Play Focused** menu first.

## Task 1 – Battle Pass Road

### What to test

The placeholder player starts at level 3 with 80/200 XP and no pass, and the level 1 free reward is already claimed, so every state is on screen.

- The screen opens where the road starts, as in the reference: the rewards that come with the pass itself, the pass ticket on the track and a free chest under it. It then glides to the player's progress.
- Drag the road, or use the mouse wheel, to scroll through the 30 levels. While the progress is out of view, a tag at the edge of the road points at it with the level being worked on; tap it to scroll back.
- Tap a reward with a red **!** to claim it. Coins and gems fly into the wallet.
- Tap any other reward to see what it is and what unlocks it.
- Press **GET** on the season card to buy the premium pass: the pass's own rewards and the reached premium rewards unlock in a wave from left to right.
- Press the green **💎 20** button on the progress bar to buy the next level.
- Press **P** to play a scripted walkthrough of all of the above (it taps through the real pointer events and shows a ring where each tap lands).
- Press **R** to restart the scene.

### Reward states

| State | How it reads |
|---|---|
| Locked | Level not reached yet: the reward sits on its rarity's card, slightly dimmed, beyond the progress line. |
| Unlocked | A reached reward switches to the gold collectable card, as in the reference. The moment it becomes available it flashes, breaks its padlock, and fires light rays and sparkles. |
| Premium | Top row, plus the pass's own rewards at the start of the road. Without the pass a reached premium reward shows the padlock and a still **!** badge. |
| Claimable | Red **!** badge with a gentle bob, a soft glow pulse and an occasional shine sweep, each card out of phase with its neighbours. |
| Claimed | Grey card with a green check. Claiming punches the card with a flash, a particle burst and a shockwave ring. |
| Current progress | Filled track, a lighter band behind the reached levels, a pulsing ring on the next level, the skip-level button at the head of the fill, and the jump tag while the progress is out of view. |

### Technical notes

- **One UI material for the whole road.** `UIFx.shader` reads per-element parameters (state grading, flash, idle intensity, phase and effect flags) from UV1/UV2, written by `UIFxMeshEffect`, so every element can still batch. The idle loops (shine, pulse, bob, the season card's gold statue cycle) run on `_Time`: an idle screen costs no CPU and never rebuilds a canvas. Premultiplied alpha lets additive glows share the batch with alpha-blended cards.
- **Sprite atlases.** Two atlases, one for UI chrome and one for reward renders, took the idle screen from 83 to 29 draw calls. Tiled, rotated and particle textures stay unpacked.
- **The start of the road.** The season data has a level 0 for what the pass itself grants, which can hold several rewards per track. The road lays these out before level 1, with the pass ticket as their track marker.
- **Reward data.** Level 0 and levels 1–10 follow the reference video. Where the reference shows a render that was not provided, the closest provided one stands in: the uncommon chest for the common chest, the Solaris magazine for the Spine attachment. The rest of the season is placeholder data built from the provided renders.
- **Overdraw.** The background is the camera clear colour plus a single tiled pattern layer. Invisible hit areas use a raycast-only graphic that submits no geometry.
- **Particles.** Claim and unlock bursts are pooled, so claiming never instantiates anything. They use a minimal additive URP shader and existing small textures. The flying currency icons live on a nested canvas, so they don't rebuild the road's batches while they move.
- **Textures.** UI sprites have no mipmaps. The UI atlas uses ASTC 4×4 and the reward atlas ASTC 6×6. Soft glows use ASTC 8×8 capped at 512 px.
- **Layout.** 1920 × 1080 reference resolution in Expand mode. On phones it matches the height, so wider phones see more of the road; on narrower screens such as 4:3 tablets it matches the width, so the top bar still fits. Interactive content sits inside the device safe area, and the screen turns with the device between the two landscape orientations. The mobile URP asset renders at full resolution: the canvas is drawn by the camera, so a lower render scale would blur the UI.
- **Tests.** `BattlePassProgress` holds the rules in plain C# with EditMode tests. PlayMode tests play the whole flow (the chest at the start of the road, claiming, buying the pass and a level, the jump tag), check states and wallet, and log the idle rendering cost. Run them from **Window → General → Test Runner**.
- **Tooling.** `BattlePass/Editor/BattlePassBuilder.cs` (**Tools → Vertigo Demo → Rebuild Battle Pass**) regenerates the Battle Pass materials, data, atlases, prefabs and scene hierarchy from code.

## Task 2 – Weapon VFX

### What to test

- Press **Play** in the WeaponVFX scene. The wind flows continuously, and the rifle sways slowly while nobody is touching it.
- Drag to rotate the rifle. Press **1** for the side view and **2** for the three-quarter view of the references.
- Press **P** for a scripted showcase: side view, three-quarter view, a slow full turn and back.

### How it is built

- **Wind ribbons (shader).** `WindRibbonMesh` generates six ribbons that leave the muzzle and sweep back along the front and underside of the rifle, like the reference, plus four wisps: two leaving the muzzle and two trailing off the top of the stock. Each follows a smooth path through a few control points; the ribbons roll from a sheet into a thin line as they go, while the wisps stay face-on. They are built as a single mesh: one draw call and about 1,000 vertices, editable in the Inspector. `WindRibbon.shader` (hand-written HLSL, additive) does the following:
  - draws a thin bright line along one edge of each ribbon, with a soft translucent sheet trailing off the other side;
  - breaks each wisp into a few soft strands made of tapered pieces that drift at their own pace, for the fragmented, translucent wind of the reference;
  - moves light along the ribbon by scrolling the provided streak sprite in two layers at different speeds;
  - fades the ribbon in and out over long, soft ends;
  - flutters it with a travelling wave in the vertex shader, wisps more loosely.

  Seed, speed, brightness and the wisp flag for each ribbon travel in vertex colours, so one material covers all of them.
- **Weapon shader.** The rifle comes with a diffuse map only, so `WeaponLegendary.shader` derives the other layers:
  - warm, saturated texels are read as polished gold, with tinted, tighter specular and a fake sky reflection;
  - the pale ball inside the football cage glows a saturated yellow and pulses, masked by an object-space sphere that skips the gold bars;
  - a sheen band sweeps the gold from muzzle to stock, following the wind.

  Lighting is the main light plus per-vertex SH, with no shadows.
- **Secondary effects (Particle System).**
  - Small four-point glints around the middle of the rifle: each is a camera-facing cross mesh of two quads, so no star texture is needed.
  - Fine dust drifting with the wind around the front and middle of the rifle, with light turbulence.
  - A soft halo over the core.

  All of them simulate in local space, are capped at 4–22 particles each and share one additive URP shader.
- **Scene.** The backdrop is a full-screen radial gradient drawn by a clip-space quad, with no texture and dithered against banding. Post-processing runs inside the project: bloom at quarter resolution with 5 iterations, neutral tonemapping, a vignette and a touch of contrast and saturation.
- **Cost.** 27 draw calls (most of them the bloom chain), about 5,900 triangles and about 21 live particles.
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

Import settings are applied automatically by `Shared/Editor/AssetImportRules.cs` the first time an asset is imported: UI sprites get no mipmaps, 9-slice borders and ASTC compression on mobile (lower bit rate and max 512 px for soft glows); the weapon FBX skips cameras, lights, animation, materials and tangents, and its texture keeps mipmaps with ASTC 6×6 capped at 512 px.

## Asset ownership

The UI sprites, weapon model and textures were provided by Vertigo Games for this demo and remain their property. They are included only so the project opens and runs as submitted.
