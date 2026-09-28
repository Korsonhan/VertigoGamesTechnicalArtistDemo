# Vertigo Games – Technical Artist Demo

Technical Artist demo for Vertigo Games, built with **Unity 6000.3.9f1 (Unity 6.3 LTS)** and the **Universal Render Pipeline (URP)**.

| Task | Scene | Status |
|---|---|---|
| 1. Battle Pass Road and item state FX | `Assets/_Project/BattlePass/Scenes/BattlePass.unity` | Done |
| 2. Weapon VFX (MCX – Top Scorer) | `Assets/_Project/WeaponVFX/Scenes/WeaponVFX.unity` | In progress |

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
- **Layout.** 1920 × 1080 reference resolution matched on height, so wider phones see more of the road. Interactive content sits inside the device safe area.
- **Tests.** `BattlePassProgress` holds the rules in plain C# with EditMode tests. A PlayMode test plays the whole flow, checks states and wallet, and logs the idle rendering cost. Run them from **Window → General → Test Runner**.
- **Tooling.** `BattlePass/Editor/BattlePassBuilder.cs` (**Tools → Vertigo Demo → Rebuild Battle Pass**) regenerates the Battle Pass materials, data, atlases, prefabs and scene hierarchy from code.

## Task 2 – Weapon VFX

_In progress._

## Project layout

```
Assets/_Project/
  BattlePass/   Task 1: sprites, atlases, data, prefabs, shaders, scripts, tests and the scene
  WeaponVFX/    Task 2: weapon model, texture, material and the VFX scene
  Shared/       Import rules and shaders used by both tasks
Assets/Settings/        URP pipeline assets
Assets/TextMesh Pro/    TextMesh Pro essential resources
```

Import settings are applied automatically by `Shared/Editor/AssetImportRules.cs` the first time an asset is imported: UI sprites get no mipmaps, 9-slice borders and ASTC compression on mobile (lower bit rate and max 512 px for soft glows); the weapon FBX skips cameras, lights, animation, materials and tangents.

## Videos

_Links to the captures will be added here. They are recorded inside the project with Unity Recorder, without any post-processing added outside the project._

## Asset ownership

The UI sprites, weapon model and textures were provided by Vertigo Games for this demo and remain their property. They are included only so the project opens and runs as submitted.
