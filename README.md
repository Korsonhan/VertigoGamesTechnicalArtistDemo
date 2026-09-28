# Vertigo Games – Technical Artist Demo

Technical Artist demo for Vertigo Games, built with **Unity 6000.3.9f1 (Unity 6.3 LTS)** and the **Universal Render Pipeline (URP)**.

| Task | Scene | Status |
|---|---|---|
| 1. Battle Pass Road and item state FX | `Assets/_Project/BattlePass/Scenes/BattlePass.unity` | In progress |
| 2. Weapon VFX (MCX – Top Scorer) | `Assets/_Project/WeaponVFX/Scenes/WeaponVFX.unity` | Not started |

## Opening the project

1. Install Unity **6000.3.9f1** through Unity Hub.
2. Clone this repository, then in Unity Hub choose **Add → Add project from disk** and select the cloned folder.
3. Open the scene for the task you want to review and press **Play**.

## Project layout

```
Assets/_Project/
  BattlePass/   Task 1: UI sprites and the Battle Pass scene
  WeaponVFX/    Task 2: weapon model, texture, material and the VFX scene
  Shared/       Editor tooling used by both tasks
Assets/Settings/        URP pipeline assets
Assets/TextMesh Pro/    TextMesh Pro essential resources
```

Import settings are applied automatically by `Shared/Editor/AssetImportRules.cs` the first time an asset is imported: UI sprites get no mipmaps, 9-slice borders and ASTC compression on mobile (lower bit rate and max 512 px for soft glows); the weapon FBX skips cameras, lights, animation, materials and tangents.

## What to test

_Filled in as each task is completed._

## Videos

_Links to the captures will be added here. They are recorded inside the project with Unity Recorder, without any post-processing added outside the project._

## Asset ownership

The UI sprites, weapon model, textures and reference material were provided by Vertigo Games for this demo and remain their property. They are included only so the project opens and runs as submitted.
