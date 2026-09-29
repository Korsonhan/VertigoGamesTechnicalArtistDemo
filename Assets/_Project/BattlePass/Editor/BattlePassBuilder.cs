using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using VertigoDemo.BattlePass.UI;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.EditorTools
{
    /// <summary>
    /// Generates the Battle Pass materials, data, prefabs and scene hierarchy from code,
    /// so the layout is reproducible and can be previewed headlessly.
    /// </summary>
    public static class BattlePassBuilder
    {
        const string Root = "Assets/_Project/BattlePass";
        const string SpriteFolder = Root + "/Art/Sprites/";
        const string MaterialFolder = Root + "/Materials";
        const string PrefabFolder = Root + "/Prefabs";
        const string FxPrefabFolder = Root + "/Prefabs/FX";
        const string DataFolder = Root + "/Data";
        const string ScenePath = Root + "/Scenes/BattlePass.unity";
        const string ScreenPrefabPath = PrefabFolder + "/PF_BattlePassScreen.prefab";
        const string PreviewFolder = "Library/DemoPreviews";

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // Everything the screen draws, split by texel density: crisp UI chrome in one atlas,
        // large reward renders in another. Tiled, rotated or particle textures stay unpacked.
        static readonly string[] UIAtlasSprites =
        {
            "ui_button_back", "ui_button_blue", "ui_button_grey", "ui_button_yellow", "ui_button_green", "ui_button_add",
            "ui_button_indicator", "ui_icon_generic_info_blue_dot", "ui_icon_generic_red_dot", "ui_icon_generic_locked",
            "ui_icon_check_green_highres", "ui_icon_subscription_small", "ui_icon_battlepass_xp", "ui_icon_battlepass_next_arrow",
            "ui_icon_battlepass_shadow", "ui_icon_time", "ui_icon_currency_hard_0", "ui_icon_currency_soft_0",
            "ui_progres_bar_battle_pass_bg", "ui_progres_bar_battle_pass_fill", "ui_item_circle_eventpass_progress",
            "ui_item_circle_eventpass_claim", "ui_item_circle_empty_8px", "ui_item_square_8px", "ui_item_square_16px",
            "ui_item_square_24px", "ui_item_square_shadow_32px", "ui_item_frame_thick_16px", "ui_item_frame_thin_16px",
            "ui_item_arrow_01_bottom", "ui_item_arrow_01_top", "ui_battle_pass_flag_red", "ui_img_battle_pass_offer_discount_bg",
            "ui_event_pass_collectable", "ui_card_uncommon", "ui_card_rare", "ui_card_epic", "ui_card_legendary", "ui_card_mythic",
            "ui_button_battlepass_indicator_white", "ui_icon_generic_cards", "ui_icon_currency_lucky_draw_0",
        };

        static readonly string[] RewardAtlasSprites =
        {
            "ui_icon_char_cleopatra", "ui_icon_special_solaris_render", "ui_icon_att_special_solaris_att_02_mag_2",
            "ui_icon_mask_anubis_render", "ui_icon_battle_pass_ability", "ui_icon_cons_primary_dynamite_large",
            "ui_icon_currency_soft_1", "ui_icon_currency_hard_1", "ui_icon_currency_lucky_draw_1_1",
            "ui_icon_chest_uncommon", "ui_icon_chest_epic", "ui_icon_chest_legendary",
            "ui_mega_pack_uncommon", "ui_mega_pack_rare", "ui_card_pack_epic",
        };

        static Material uiMaterial;
        static Material textMaterial;
        static TMP_FontAsset font;
        static int uiLayer;

        [MenuItem("Tools/Vertigo Demo/Rebuild Battle Pass")]
        public static void Rebuild()
        {
            uiLayer = LayerMask.NameToLayer("UI");
            EnsureFolder(MaterialFolder);
            EnsureFolder(FxPrefabFolder);
            EnsureFolder(DataFolder);

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            uiMaterial = CreateMaterial(MaterialFolder + "/M_UI_Fx.mat", Shader.Find("VertigoDemo/UI/Fx"));
            textMaterial = CreateTextMaterial(MaterialFolder + "/M_Text_Outline.mat");

            var particleShader = Shader.Find("VertigoDemo/FX/Particle Additive");
            var sparkle = CreateMaterial(MaterialFolder + "/M_FX_Sparkle.mat", particleShader, Texture("ui_rank_glow"));
            var softGlow = CreateMaterial(MaterialFolder + "/M_FX_SoftGlow.mat", particleShader, Texture("ui_fx_glow_01"));
            var ring = CreateMaterial(MaterialFolder + "/M_FX_Ring.mat", particleShader, Texture("ui_item_frame_thick_tutorial_only_circle_small"));

            var palette = CreatePalette();
            var season = CreateSeason();

            EnsureFolder(Root + "/Atlases");
            BuildAtlas(Root + "/Atlases/SA_BattlePass_UI.spriteatlasv2", UIAtlasSprites, TextureImporterFormat.ASTC_4x4);
            BuildAtlas(Root + "/Atlases/SA_BattlePass_Rewards.spriteatlasv2", RewardAtlasSprites, TextureImporterFormat.ASTC_6x6);

            var claimBurst = BuildBurst(FxPrefabFolder + "/PF_FX_RewardClaim.prefab", sparkle, softGlow, ring, claim: true);
            var unlockBurst = BuildBurst(FxPrefabFolder + "/PF_FX_RewardUnlock.prefab", sparkle, softGlow, ring, claim: false);
            var card = BuildRewardCard();
            var node = BuildLevelNode();
            var level = BuildRoadLevel(card, node);
            BuildScreen(season, palette, level, claimBurst, unlockBurst);

            PlaceInScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[BattlePassBuilder] Prefabs, data and scene rebuilt.");
        }

        public static void RebuildAndPreview()
        {
            Rebuild();
            RenderPreview("battlepass.png");
        }

        // ------------------------------------------------------------------ materials and data

        static Material CreateMaterial(string path, Shader shader, Texture texture = null)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (texture != null)
                material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Chunky game-UI lettering from the stock font: dilated face, dark outline and a drop shadow.
        static Material CreateTextMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, path);
            }
            material.CopyPropertiesFromMaterial(font.material);
            material.shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            material.SetFloat("_FaceDilate", 0.18f);
            material.SetColor("_OutlineColor", new Color(0.13f, 0.05f, 0.24f, 1f));
            material.SetFloat("_OutlineWidth", 0.24f);
            material.SetColor("_UnderlayColor", new Color(0.08f, 0.02f, 0.16f, 0.8f));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -0.9f);
            material.SetFloat("_UnderlayDilate", 0.25f);
            material.SetFloat("_UnderlaySoftness", 0f);
            material.EnableKeyword("OUTLINE_ON");
            material.EnableKeyword("UNDERLAY_ON");
            ShaderUtilities.GetShaderPropertyIDs();
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static RarityPalette CreatePalette()
        {
            var palette = LoadOrCreate<RarityPalette>(DataFolder + "/RarityPalette.asset");
            var entries = new (RewardRarity rarity, string sprite, Color accent)[]
            {
                (RewardRarity.Common, "ui_event_pass_collectable", new Color(1f, 0.82f, 0.25f)),
                (RewardRarity.Uncommon, "ui_card_uncommon", new Color(0.45f, 1f, 0.4f)),
                (RewardRarity.Rare, "ui_card_rare", new Color(0.35f, 0.75f, 1f)),
                (RewardRarity.Epic, "ui_card_epic", new Color(0.8f, 0.45f, 1f)),
                (RewardRarity.Legendary, "ui_card_legendary", new Color(1f, 0.62f, 0.2f)),
                (RewardRarity.Mythic, "ui_card_mythic", new Color(1f, 0.35f, 0.35f)),
            };

            var serialized = new SerializedObject(palette);
            serialized.FindProperty("collectableCard").objectReferenceValue = Sprite("ui_event_pass_collectable");
            var array = serialized.FindProperty("entries");
            array.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("rarity").enumValueIndex = (int)entries[i].rarity;
                element.FindPropertyRelative("cardSprite").objectReferenceValue = Sprite(entries[i].sprite);
                element.FindPropertyRelative("accent").colorValue = entries[i].accent;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return palette;
        }

        static BattlePassSeason CreateSeason()
        {
            var season = LoadOrCreate<BattlePassSeason>(DataFolder + "/BattlePassSeason_S16.asset");
            season.passTitle = "GOLDEN REALM";
            season.seasonTitle = "SEASON 16";
            season.timeLeft = "17D 20H";
            season.xpPerLevel = 200;
            season.skipLevelCost = 20;

            RewardDefinition Reward(string name, string icon, RewardRarity rarity, RewardKind kind, int amount,
                string captionIcon = null, string caption = "") =>
                new RewardDefinition
                {
                    displayName = name, icon = Sprite(icon), rarity = rarity, kind = kind, amount = amount,
                    captionIcon = captionIcon != null ? Sprite(captionIcon) : null, caption = caption,
                };
            // Amounts carry their small icon, like the reference; big piles of gold get the legendary card.
            RewardDefinition Gold(int amount) =>
                Reward("GOLD", "ui_icon_currency_soft_1", amount >= 5000 ? RewardRarity.Legendary : RewardRarity.Rare, RewardKind.Coins, amount, "ui_icon_currency_soft_0");
            RewardDefinition Diamond(int amount) =>
                Reward("DIAMOND", "ui_icon_currency_hard_1", RewardRarity.Epic, RewardKind.Gems, amount, "ui_icon_currency_hard_0");
            RewardDefinition LuckyGem(int amount) =>
                Reward("LUCKY GEM", "ui_icon_currency_lucky_draw_1_1", RewardRarity.Uncommon, RewardKind.Item, amount, "ui_icon_currency_lucky_draw_0");
            RewardDefinition Item(string name, string icon, RewardRarity rarity, string caption = "") =>
                Reward(name, icon, rarity, RewardKind.Item, 1, caption: caption);
            // A character, weapon or consumable is unlocked once; after that it comes as cards.
            RewardDefinition Unlock(string name, string icon, RewardRarity rarity) => Item(name, icon, rarity, "UNLOCK NOW");
            RewardDefinition Cards(string name, string icon, RewardRarity rarity, int amount) =>
                Reward(name, icon, rarity, RewardKind.Item, amount, "ui_icon_generic_cards");

            const string cleopatra = "ui_icon_char_cleopatra";
            const string solaris = "ui_icon_special_solaris_render";
            const string anubis = "ui_icon_mask_anubis_render";
            const string dynamite = "ui_icon_cons_primary_dynamite_large";
            RewardDefinition Attachment() => Item("SOLARIS MAG", "ui_icon_att_special_solaris_att_02_mag_2", RewardRarity.Uncommon, "ATTACHMENT");
            RewardDefinition UncommonChest() => Item("UNCOMMON CHEST", "ui_icon_chest_uncommon", RewardRarity.Uncommon);
            RewardDefinition EpicChest() => Item("EPIC CHEST", "ui_icon_chest_epic", RewardRarity.Epic);
            RewardDefinition LegendaryChest() => Item("LEGENDARY CHEST", "ui_icon_chest_legendary", RewardRarity.Legendary);
            RewardDefinition UncommonPack() => Item("UNCOMMON BOOSTER PACK", "ui_mega_pack_uncommon", RewardRarity.Uncommon);
            RewardDefinition RarePack() => Item("RARE BOOSTER PACK", "ui_mega_pack_rare", RewardRarity.Rare);
            RewardDefinition EpicPack() => Item("EPIC CARD PACK", "ui_card_pack_epic", RewardRarity.Epic);

            // Level 0, where the reference opens: the pass itself unlocks Cleopatra and Solaris and adds
            // cards for both, and a free chest waits under the pass ticket.
            season.start = new BattlePassSeason.PassStart
            {
                premium = new List<RewardDefinition>
                {
                    Unlock("CLEOPATRA", cleopatra, RewardRarity.Mythic),
                    Unlock("SOLARIS", solaris, RewardRarity.Legendary),
                    Cards("CLEOPATRA", cleopatra, RewardRarity.Mythic, 2),
                    Cards("SOLARIS", solaris, RewardRarity.Legendary, 2),
                },
                free = new List<RewardDefinition> { UncommonChest() },
            };

            // Levels 1-10 follow the reference video; the rest of the season carries on in the same spirit.
            var levels = new (RewardDefinition premium, RewardDefinition free)[]
            {
                (Gold(5000), Gold(1000)),
                (Diamond(10), Diamond(2)),
                (LuckyGem(10), LuckyGem(2)),
                (Unlock("DYNAMITE", dynamite, RewardRarity.Epic), Diamond(2)),
                (Attachment(), UncommonPack()),
                (EpicChest(), Gold(1000)),
                (RarePack(), Diamond(2)),
                (Cards("DYNAMITE", dynamite, RewardRarity.Epic, 2), LuckyGem(2)),
                (Cards("CLEOPATRA", cleopatra, RewardRarity.Mythic, 3), Gold(1000)),
                (EpicChest(), UncommonChest()),
                (Gold(6000), Gold(1500)),
                (Diamond(12), Diamond(3)),
                (Unlock("ANUBIS MASK", anubis, RewardRarity.Legendary), LuckyGem(3)),
                (RarePack(), Gold(1500)),
                (LegendaryChest(), UncommonPack()),
                (Cards("SOLARIS", solaris, RewardRarity.Legendary, 3), Diamond(3)),
                (LuckyGem(15), Gold(2000)),
                (EpicPack(), UncommonChest()),
                (Gold(8000), LuckyGem(3)),
                (Unlock("ABILITY", "ui_icon_battle_pass_ability", RewardRarity.Epic), Diamond(4)),
                (Cards("DYNAMITE", dynamite, RewardRarity.Epic, 3), Gold(2000)),
                (Diamond(15), RarePack()),
                (Cards("ANUBIS MASK", anubis, RewardRarity.Legendary, 2), LuckyGem(4)),
                (EpicChest(), Gold(2500)),
                (Gold(10000), Diamond(5)),
                (RarePack(), UncommonChest()),
                (Cards("CLEOPATRA", cleopatra, RewardRarity.Mythic, 5), Gold(3000)),
                (LegendaryChest(), LuckyGem(5)),
                (Diamond(25), EpicPack()),
                (Cards("SOLARIS", solaris, RewardRarity.Legendary, 5), LegendaryChest()),
            };
            season.levels = levels.Select(pair => new BattlePassSeason.Level { premium = pair.premium, free = pair.free }).ToList();
            EditorUtility.SetDirty(season);
            return season;
        }

        static void BuildAtlas(string path, IEnumerable<string> sprites, TextureImporterFormat mobileFormat)
        {
            var atlas = new SpriteAtlasAsset();
            atlas.Add(sprites.Select(name => (Object)Sprite(name)).ToArray());
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            importer.includeInBuild = true;
            // No rotation or tight packing: sliced and filled UGUI images need upright rectangles.
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true,
                padding = 4,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
            };
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                importer.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = 2048,
                    format = mobileFormat,
                    textureCompression = TextureImporterCompression.Compressed,
                    compressionQuality = 50,
                });
            }
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ particle bursts

        static ParticleSystem BuildBurst(string path, Material sparkle, Material softGlow, Material ring, bool claim)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path)) { layer = uiLayer };

            // Root system: rarity-tinted sparkles (the pool recolours it per reward).
            var sparkles = root.AddComponent<ParticleSystem>();
            ConfigureCommon(sparkles, sparkle, 1f, sortingOrder: 21);
            var main = sparkles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = claim ? new ParticleSystem.MinMaxCurve(380f, 760f) : new ParticleSystem.MinMaxCurve(160f, 380f);
            main.startSize = claim ? new ParticleSystem.MinMaxCurve(26f, 60f) : new ParticleSystem.MinMaxCurve(18f, 40f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            int count = claim ? 22 : 12;
            main.maxParticles = count + 4;
            var emission = sparkles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 40f;
            shape.radiusThickness = 1f;
            var drag = sparkles.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 0f;
            drag.dampen = 0.08f;
            var size = sparkles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)));
            var spin = sparkles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var color = sparkles.colorOverLifetime;
            color.enabled = true;
            color.color = FadeOut(0.6f);

            // Soft flash behind the card.
            var flash = AddChildSystem(root, "Flash");
            ConfigureCommon(flash, softGlow, 0.4f, sortingOrder: 20);
            var flashMain = flash.main;
            flashMain.startLifetime = 0.3f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = claim ? 460f : 320f;
            flashMain.startColor = new Color(1f, 0.92f, 0.7f, 0.9f);
            flashMain.maxParticles = 1;
            SingleBurst(flash);
            var flashSize = flash.sizeOverLifetime;
            flashSize.enabled = true;
            flashSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.1f));
            var flashColor = flash.colorOverLifetime;
            flashColor.enabled = true;
            flashColor.color = FadeOut(0.2f);

            // Expanding shockwave ring, claim only.
            if (claim)
            {
                var wave = AddChildSystem(root, "Ring");
                ConfigureCommon(wave, ring, 0.5f, sortingOrder: 20);
                var waveMain = wave.main;
                waveMain.startLifetime = 0.42f;
                waveMain.startSpeed = 0f;
                waveMain.startSize = 150f;
                waveMain.startColor = new Color(1f, 1f, 1f, 0.8f);
                waveMain.maxParticles = 1;
                SingleBurst(wave);
                var waveSize = wave.sizeOverLifetime;
                waveSize.enabled = true;
                waveSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f, 0f, 6f), new Keyframe(1f, 3.2f, 0f, 0f)));
                var waveColor = wave.colorOverLifetime;
                waveColor.enabled = true;
                waveColor.color = FadeOut(0.1f);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ParticleSystem>();
        }

        static ParticleSystem AddChildSystem(GameObject parent, string name)
        {
            var child = new GameObject(name) { layer = uiLayer };
            child.transform.SetParent(parent.transform, false);
            return child.AddComponent<ParticleSystem>();
        }

        static void ConfigureCommon(ParticleSystem system, Material material, float duration, int sortingOrder)
        {
            var main = system.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = Color.white;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.maxParticleSize = 1f;
        }

        static void SingleBurst(ParticleSystem system)
        {
            var emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = system.shape;
            shape.enabled = false;
        }

        static ParticleSystem.MinMaxGradient FadeOut(float holdUntil)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        // ------------------------------------------------------------------ prefabs

        static RewardCardView BuildRewardCard()
        {
            var root = NewUI("PF_RewardCard", null);
            root.sizeDelta = new Vector2(290f, 350f);
            var view = root.gameObject.AddComponent<RewardCardView>();

            // The soft square's slices are drawn at double size so its falloff is wide, and the rect only
            // overhangs the card by the tail of that falloff: what shows is a halo, not a lit panel.
            var glow = AddImage(Stretch(NewUI("Glow", root), -30f, -30f, -30f, -30f), "ui_item_square_shadow_32px", new Color(1f, 0.85f, 0.35f, 0.9f), sliced: true);
            glow.pixelsPerUnitMultiplier = 0.45f;
            var glowFx = AddFx(glow, UIFxFlags.Pulse | UIFxFlags.Additive);
            var rays = AddImage(Place(NewUI("Rays", root), Center, Center, Vector2.zero, new Vector2(560f, 560f)), "ui_glow_02", new Color(1f, 0.9f, 0.55f, 0f));
            AddFx(rays, UIFxFlags.Additive);

            var body = Stretch(NewUI("Body", root));
            var background = AddImage(Stretch(NewUI("Background", body)), null, sliced: true, raycast: true);
            var backgroundFx = AddFx(background, UIFxFlags.Shine, body);
            var icon = AddImage(Anchors(NewUI("Icon", body), new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.83f)), null, preserveAspect: true);
            var iconFx = AddFx(icon, UIFxFlags.Shine, body);
            var title = AddText(Anchors(NewUI("Title", body), new Vector2(0.13f, 0.8f), new Vector2(0.87f, 0.97f)), "REWARD", 40f, Color.white, TextAlignmentOptions.Center, autoSizeMin: 11f);
            var caption = AddText(Anchors(NewUI("Caption", body), new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.2f)), "x10", 38f, Color.white, TextAlignmentOptions.Center, autoSizeMin: 18f);
            // Coin, gem or card in front of the amount; the card lays it out next to the text at runtime.
            var captionIcon = AddImage(Anchors(NewUI("CaptionIcon", body), new Vector2(0.5f, 0.045f), new Vector2(0.5f, 0.185f)), "ui_icon_currency_soft_0", preserveAspect: true);
            captionIcon.rectTransform.sizeDelta = new Vector2(46f, 0f);

            var claimedMark = AddImage(Anchors(NewUI("ClaimedMark", body), new Vector2(0.26f, 0.3f), new Vector2(0.74f, 0.72f)), "ui_icon_check_green_highres", preserveAspect: true);
            var lockBadge = AddImage(Anchors(NewUI("LockBadge", body), new Vector2(-0.14f, 0.83f), new Vector2(0.14f, 1.11f)), "ui_icon_generic_locked", preserveAspect: true);
            // Small, like the reference: every reached reward carries one, so a big badge would be noise.
            var alertBadge = AddImage(Anchors(NewUI("AlertBadge", body), new Vector2(0.885f, 0.9f), new Vector2(1.045f, 1.05f)), "ui_icon_generic_red_dot", preserveAspect: true);
            var alertFx = AddFx(alertBadge, UIFxFlags.Bob);
            var selection = AddImage(Stretch(NewUI("SelectionFrame", body), -12f, -12f, -12f, -12f), "ui_item_frame_thick_16px", Color.white, sliced: true);
            AddFx(selection, UIFxFlags.Pulse, idle: 1f);

            Wire(view,
                ("body", body), ("background", background), ("icon", icon), ("title", title), ("caption", caption),
                ("captionIcon", captionIcon), ("lockBadge", lockBadge), ("alertBadge", alertBadge), ("claimedMark", claimedMark),
                ("glow", glow), ("rays", rays), ("selectionFrame", selection.gameObject),
                ("backgroundFx", backgroundFx), ("iconFx", iconFx), ("glowFx", glowFx), ("alertFx", alertFx));

            foreach (var hidden in new Graphic[] { glow, rays, claimedMark, lockBadge, alertBadge, selection, captionIcon })
                hidden.gameObject.SetActive(false);

            return SavePrefab<RewardCardView>(root, PrefabFolder + "/PF_RewardCard.prefab");
        }

        static LevelNodeView BuildLevelNode()
        {
            var root = NewUI("PF_LevelNode", null);
            root.sizeDelta = new Vector2(84f, 84f);
            var view = root.gameObject.AddComponent<LevelNodeView>();

            var ring = AddImage(Place(NewUI("NextRing", root), Center, Center, Vector2.zero, new Vector2(132f, 132f)), "ui_item_circle_empty_8px", new Color(1f, 0.86f, 0.35f, 1f));
            AddFx(ring, UIFxFlags.Pulse | UIFxFlags.Additive, idle: 1f);
            var circle = AddImage(Stretch(NewUI("Circle", root)), "ui_item_circle_eventpass_progress");
            var label = AddText(Stretch(NewUI("Label", root)), "1", 42f, Color.white, TextAlignmentOptions.Center);
            // The pass ticket marks the start of the road instead of a number, as in the reference.
            var ticket = AddImage(Place(NewUI("Ticket", root), Center, Center, new Vector2(0f, 4f), new Vector2(132f, 132f)), "ui_icon_battlepass_shadow", preserveAspect: true);
            ticket.rectTransform.localEulerAngles = new Vector3(0f, 0f, -8f);

            Wire(view,
                ("circle", circle), ("label", label), ("nextRing", ring.gameObject), ("ticket", ticket.gameObject),
                ("reachedSprite", Sprite("ui_item_circle_eventpass_claim")), ("lockedSprite", Sprite("ui_item_circle_eventpass_progress")));
            ring.gameObject.SetActive(false);
            ticket.gameObject.SetActive(false);
            return SavePrefab<LevelNodeView>(root, PrefabFolder + "/PF_LevelNode.prefab");
        }

        static RoadLevelView BuildRoadLevel(RewardCardView cardPrefab, LevelNodeView nodePrefab)
        {
            var root = NewUI("PF_RoadLevel", null);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = Center;
            root.sizeDelta = new Vector2(340f, 800f);
            var view = root.gameObject.AddComponent<RoadLevelView>();

            var premium = InstantiateNested(cardPrefab, root, "PremiumCard");
            Place((RectTransform)premium.transform, Center, Center, new Vector2(0f, 190f), new Vector2(290f, 350f));
            var node = InstantiateNested(nodePrefab, root, "Node");
            Place((RectTransform)node.transform, Center, Center, new Vector2(0f, -60f), new Vector2(84f, 84f));
            var free = InstantiateNested(cardPrefab, root, "FreeCard");
            Place((RectTransform)free.transform, Center, Center, new Vector2(0f, -232f), new Vector2(210f, 210f));

            Wire(view, ("premiumCard", premium), ("freeCard", free), ("node", node));
            return SavePrefab<RoadLevelView>(root, PrefabFolder + "/PF_RoadLevel.prefab");
        }

        static BattlePassScreen BuildScreen(BattlePassSeason season, RarityPalette palette, RoadLevelView columnPrefab, ParticleSystem claimBurst, ParticleSystem unlockBurst)
        {
            var root = Stretch(NewUI("PF_BattlePassScreen", null));
            var screen = root.gameObject.AddComponent<BattlePassScreen>();

            // The camera clears to the base purple, so the background costs a single tiled pattern layer.
            var pattern = AddImage(Stretch(NewUI("BackgroundPattern", root)), "ui_background_tile_battlepass", new Color(1f, 1f, 1f, 0.07f));
            pattern.type = Image.Type.Tiled;
            pattern.pixelsPerUnitMultiplier = 0.55f;

            // Interactive content stays inside the device safe area; background and FX span the whole screen.
            var safeArea = Stretch(NewUI("SafeArea", root));
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            var road = BuildRoad(safeArea, out var track, out var columnContainer, out var skipButton, out var skipCost, out var jumpButton, out var emptyRoadClicks);
            var seasonPanel = BuildSeasonPanel(safeArea);
            var topBar = BuildTopBar(safeArea);
            var tooltip = BuildTooltip(safeArea);

            // Moving FX get a nested canvas, so animating them never rebuilds the road's batches.
            var fxLayer = Stretch(NewUI("FxLayer", root));
            var fxCanvas = fxLayer.gameObject.AddComponent<Canvas>();
            fxCanvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            var fx = fxLayer.gameObject.AddComponent<RewardFxPool>();
            Wire(fx, ("claimBurst", claimBurst), ("unlockBurst", unlockBurst));
            var flyTemplate = AddImage(Place(NewUI("FlyIconTemplate", fxLayer), Center, Center, Vector2.zero, new Vector2(72f, 72f)), "ui_icon_currency_soft_0", preserveAspect: true);
            flyTemplate.gameObject.SetActive(false);
            var currencyFly = fxLayer.gameObject.AddComponent<CurrencyFlyFx>();
            Wire(currencyFly, ("iconTemplate", flyTemplate));

            // Scripted walkthrough for recordings (P), with a ring showing where each tap lands.
            var touchRing = AddImage(Place(NewUI("TouchRing", fxLayer), Center, Center, Vector2.zero, new Vector2(120f, 120f)), "ui_item_circle_empty_8px", new Color(1f, 1f, 1f, 0.9f));
            touchRing.gameObject.SetActive(false);
            var autoplay = root.gameObject.AddComponent<BattlePassAutoplay>();
            Wire(autoplay, ("screen", screen), ("touchRing", touchRing));

            Wire(screen,
                ("season", season), ("palette", palette), ("road", road), ("columnContainer", columnContainer),
                ("columnPrefab", columnPrefab), ("track", track), ("skipButton", skipButton), ("skipCostLabel", skipCost),
                ("jumpButton", jumpButton), ("emptyRoadClicks", emptyRoadClicks), ("topBar", topBar), ("seasonPanel", seasonPanel),
                ("tooltip", tooltip), ("fx", fx), ("currencyFly", currencyFly));

            return SavePrefab<BattlePassScreen>(root, ScreenPrefabPath);
        }

        static ScrollRect BuildRoad(RectTransform parent, out ProgressTrackView track, out RectTransform columnContainer,
            out Button skipButton, out TMP_Text skipCost, out ProgressJumpButton jumpButton, out PointerClickRelay emptyRoadClicks)
        {
            var rect = NewUI("Road", parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(420f, 30f);
            rect.offsetMax = new Vector2(0f, -238f);
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;

            var viewport = Stretch(NewUI("Viewport", rect));
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(60, 0);
            viewport.gameObject.AddComponent<EmptyGraphic>();
            emptyRoadClicks = viewport.gameObject.AddComponent<PointerClickRelay>();

            var content = NewUI("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(2000f, 0f);
            scroll.viewport = viewport;
            scroll.content = content;

            var reached = NewUI("ReachedArea", content);
            reached.anchorMin = new Vector2(0f, 0f);
            reached.anchorMax = new Vector2(0f, 1f);
            reached.pivot = new Vector2(0f, 0.5f);
            reached.sizeDelta = new Vector2(600f, 0f);
            AddImage(reached, null, new Color(1f, 1f, 1f, 0.06f));
            var edge = NewUI("Edge", reached);
            edge.anchorMin = new Vector2(1f, 0f);
            edge.anchorMax = new Vector2(1f, 1f);
            edge.sizeDelta = new Vector2(4f, 0f);
            AddImage(edge, null, new Color(1f, 1f, 1f, 0.3f));

            var trackBackground = NewUI("TrackBackground", content);
            trackBackground.anchorMin = new Vector2(0f, 0.5f);
            trackBackground.anchorMax = new Vector2(1f, 0.5f);
            trackBackground.anchoredPosition = new Vector2(0f, -60f);
            trackBackground.sizeDelta = new Vector2(0f, 46f);
            AddImage(trackBackground, "ui_progres_bar_battle_pass_bg", sliced: true);

            var fill = Place(NewUI("TrackFill", content), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -60f), new Vector2(400f, 36f));
            AddImage(fill, "ui_progres_bar_battle_pass_fill", sliced: true);

            columnContainer = Stretch(NewUI("Columns", content));

            var marker = Place(NewUI("SkipLevelButton", content), new Vector2(0f, 0.5f), Center, new Vector2(0f, -60f), new Vector2(190f, 190f));
            var markerImage = AddImage(marker, "ui_button_indicator", raycast: true, preserveAspect: true);
            skipButton = AddButton(marker, markerImage);
            AddImage(Place(NewUI("GemIcon", marker), Center, Center, new Vector2(-36f, 4f), new Vector2(54f, 54f)), "ui_icon_currency_hard_0", preserveAspect: true);
            skipCost = AddText(Place(NewUI("Cost", marker), Center, Center, new Vector2(22f, 6f), new Vector2(80f, 60f)), "20", 46f, Color.white, TextAlignmentOptions.Center);

            track = content.gameObject.AddComponent<ProgressTrackView>();
            Wire(track, ("fill", fill), ("reachedArea", reached), ("marker", marker));

            // Tag pointing at the player's progress while it is scrolled out of view. It sits outside the
            // viewport, so the mask never clips it, and moves between the road's edges.
            var jumpRoot = Stretch(NewUI("ProgressJump", rect));
            jumpButton = jumpRoot.gameObject.AddComponent<ProgressJumpButton>();
            var bubble = Place(NewUI("Tag", jumpRoot), new Vector2(1f, 0.5f), Center, new Vector2(-100f, 110f), new Vector2(124f, 104f));
            var shape = AddImage(Stretch(NewUI("Shape", bubble)), "ui_button_battlepass_indicator_white", raycast: true);
            var jump = AddButton(bubble, shape);
            var badge = Place(NewUI("Badge", bubble), Center, Center, new Vector2(-9f, 0f), new Vector2(74f, 74f));
            AddImage(badge, "ui_item_circle_eventpass_progress");
            var level = AddText(Stretch(NewUI("Level", badge)), "4", 40f, Color.white, TextAlignmentOptions.Center);
            Wire(jumpButton, ("road", scroll), ("button", jump), ("bubble", bubble), ("bubbleShape", shape.rectTransform),
                ("badge", badge), ("label", level));
            bubble.gameObject.SetActive(false);
            return scroll;
        }

        static SeasonPanelView BuildSeasonPanel(RectTransform parent)
        {
            var panel = Place(NewUI("SeasonPanel", parent), Vector2.zero, Vector2.zero, new Vector2(64f, 28f), new Vector2(400f, 800f));
            var view = panel.gameObject.AddComponent<SeasonPanelView>();

            AddImage(Stretch(NewUI("Frame", panel)), "ui_card_legendary", sliced: true);
            AddImage(Stretch(NewUI("Inner", panel), 14f, 16f, 14f, 100f), "ui_card_mythic", sliced: true);

            var header = NewUI("Header", panel);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(14f, -96f);
            header.offsetMax = new Vector2(-14f, -12f);
            AddImage(header, "ui_item_square_16px", new Color(1f, 0.8f, 0.22f), sliced: true);
            var seasonLabel = AddText(Stretch(NewUI("SeasonLabel", header), 20f, 0f, 22f, 0f), "SEASON 16", 48f, Color.white, TextAlignmentOptions.MidlineRight);

            AddText(Place(NewUI("CharacterName", panel), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -108f), new Vector2(340f, 64f)),
                "CLEOPATRA", 52f, Color.white, TextAlignmentOptions.MidlineRight);
            var tag = Place(NewUI("RarityTag", panel), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -176f), new Vector2(150f, 46f));
            AddImage(tag, "ui_item_square_8px", new Color(0.5f, 0.03f, 0.08f), sliced: true);
            AddText(Stretch(NewUI("Label", tag)), "MYTHIC", 30f, Color.white, TextAlignmentOptions.Center);

            // The reference alternates the character with a gold statue; the UIFx gold cycle does it with one sprite.
            var character = AddImage(Anchors(NewUI("Character", panel), new Vector2(0.0f, 0.2f), new Vector2(1f, 0.8f)), "ui_icon_char_cleopatra", preserveAspect: true);
            var characterFx = AddFx(character, UIFxFlags.GoldCycle, idle: 1f);
            characterFx.Phase = 0.1f;

            AddImage(Place(NewUI("CoinDecor", panel), new Vector2(0f, 0f), Center, new Vector2(12f, 560f), new Vector2(92f, 92f)), "ui_icon_currency_soft_0", preserveAspect: true);
            AddImage(Place(NewUI("GemDecor", panel), new Vector2(0f, 0f), Center, new Vector2(18f, 450f), new Vector2(78f, 78f)), "ui_icon_currency_hard_0", preserveAspect: true);
            var ticket = Place(NewUI("PassTicket", panel), new Vector2(0f, 1f), Center, new Vector2(2f, 18f), new Vector2(150f, 150f));
            ticket.localEulerAngles = new Vector3(0f, 0f, 12f);
            AddImage(ticket, "ui_icon_battlepass_shadow", preserveAspect: true);

            var getRect = Place(NewUI("GetButton", panel), new Vector2(0.5f, 0f), Center, new Vector2(20f, 132f), new Vector2(250f, 96f));
            var getImage = AddImage(getRect, "ui_button_yellow", sliced: true, raycast: true);
            var getButton = AddButton(getRect, getImage);
            var getLabel = AddText(Stretch(NewUI("Label", getRect), 0f, 8f, 0f, 0f), "GET", 54f, Color.white, TextAlignmentOptions.Center);

            var offer = Stretch(NewUI("Offer", panel));
            var flag = Place(NewUI("DiscountFlag", offer), new Vector2(0.5f, 0f), Center, new Vector2(0f, 46f), new Vector2(390f, 68f));
            AddImage(flag, "ui_battle_pass_flag_red");
            AddText(Stretch(NewUI("Label", flag), 30f, 12f, 30f, 6f), "SPECIAL DISCOUNT!", 30f, Color.white, TextAlignmentOptions.Center, autoSizeMin: 18f);
            var badge = Place(NewUI("ValueBadge", offer), new Vector2(0f, 0f), Center, new Vector2(62f, 150f), new Vector2(132f, 134f));
            badge.localEulerAngles = new Vector3(0f, 0f, 10f);
            AddImage(badge, "ui_img_battle_pass_offer_discount_bg", preserveAspect: true);
            AddText(Stretch(NewUI("Label", badge), 14f, 14f, 14f, 14f), "x100\nVALUE", 26f, Color.white, TextAlignmentOptions.Center, autoSizeMin: 14f);

            Wire(view,
                ("seasonLabel", seasonLabel), ("getButton", getButton), ("getButtonImage", getImage), ("getLabel", getLabel),
                ("ownedButtonSprite", Sprite("ui_button_green")), ("offerGroup", offer.gameObject));
            return view;
        }

        static TopBarView BuildTopBar(RectTransform parent)
        {
            var bar = NewUI("TopBar", parent);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 250f);
            var view = bar.gameObject.AddComponent<TopBarView>();
            var topLeft = new Vector2(0f, 1f);
            var topCenter = new Vector2(0.5f, 1f);
            var topRight = new Vector2(1f, 1f);

            // Back and title.
            var back = Place(NewUI("BackButton", bar), topLeft, topLeft, new Vector2(0f, -8f), new Vector2(104f, 104f));
            AddButton(back, AddImage(back, "ui_button_back", raycast: true));
            var title = AddText(Place(NewUI("Title", bar), topLeft, new Vector2(0f, 0.5f), new Vector2(124f, -60f), new Vector2(460f, 70f)),
                "GOLDEN REALM", 52f, Color.white, TextAlignmentOptions.MidlineLeft);
            float titleWidth = title.GetPreferredValues("GOLDEN REALM").x;
            AddImage(Place(NewUI("InfoButton", bar), topLeft, Center, new Vector2(124f + titleWidth + 40f, -58f), new Vector2(56f, 56f)), "ui_icon_generic_info_blue_dot", preserveAspect: true);

            // Premium pass status.
            AddImage(Place(NewUI("PremiumIcon", bar), topLeft, Center, new Vector2(160f, -156f), new Vector2(92f, 92f)), "ui_icon_subscription_small", preserveAspect: true);
            AddText(Place(NewUI("PremiumTitle", bar), topLeft, new Vector2(0f, 0.5f), new Vector2(212f, -138f), new Vector2(260f, 44f)),
                "PREMIUM x2", 32f, new Color(1f, 0.84f, 0.25f), TextAlignmentOptions.MidlineLeft);
            var premiumStatus = AddText(Place(NewUI("PremiumStatus", bar), topLeft, new Vector2(0f, 0.5f), new Vector2(212f, -172f), new Vector2(200f, 34f)),
                "INACTIVE", 24f, Color.white, TextAlignmentOptions.MidlineLeft);
            AddImage(Place(NewUI("PremiumXp", bar), topLeft, Center, new Vector2(482f, -150f), new Vector2(66f, 60f)), "ui_icon_battlepass_xp", preserveAspect: true);

            // Tabs.
            BuildTab(bar, "ArenaPassTab", "ARENA PASS", "ui_button_blue", new Vector2(-132f, -60f), Color.white);
            BuildTab(bar, "MissionsTab", "MISSIONS", "ui_button_grey", new Vector2(132f, -60f), new Color(0.85f, 0.87f, 1f));

            // Season XP.
            AddImage(Place(NewUI("XpIcon", bar), topCenter, Center, new Vector2(-206f, -154f), new Vector2(96f, 86f)), "ui_icon_battlepass_xp", preserveAspect: true);
            var xpBar = Place(NewUI("XpBar", bar), topCenter, Center, new Vector2(-40f, -154f), new Vector2(240f, 50f));
            AddImage(xpBar, "ui_progres_bar_battle_pass_bg", sliced: true);
            var xpFill = Place(NewUI("Fill", xpBar), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(96f, 40f));
            AddImage(xpFill, "ui_progres_bar_battle_pass_fill", sliced: true);
            var xpLabel = AddText(Stretch(NewUI("Label", xpBar)), "80/200", 32f, Color.white, TextAlignmentOptions.Center);
            AddImage(Place(NewUI("NextArrow", bar), topCenter, Center, new Vector2(104f, -154f), new Vector2(28f, 32f)), "ui_icon_battlepass_next_arrow");
            var levelBadge = Place(NewUI("NextLevelBadge", bar), topCenter, Center, new Vector2(160f, -154f), new Vector2(80f, 80f));
            AddImage(levelBadge, "ui_item_circle_eventpass_progress");
            var nextLevel = AddText(Stretch(NewUI("Label", levelBadge)), "4", 40f, Color.white, TextAlignmentOptions.Center);

            // Time left.
            AddText(Place(NewUI("TimeLeftLabel", bar), topCenter, new Vector2(1f, 0.5f), new Vector2(-6f, -214f), new Vector2(240f, 40f)),
                "TIME LEFT :", 30f, Color.white, TextAlignmentOptions.MidlineRight);
            AddImage(Place(NewUI("TimeIcon", bar), topCenter, Center, new Vector2(20f, -214f), new Vector2(40f, 40f)), "ui_icon_time", preserveAspect: true);
            var timeLeft = AddText(Place(NewUI("TimeLeft", bar), topCenter, new Vector2(0f, 0.5f), new Vector2(46f, -214f), new Vector2(240f, 40f)),
                "17D 20H", 30f, Color.white, TextAlignmentOptions.MidlineLeft);

            // Wallet.
            var gems = BuildCurrency(bar, "Gems", "ui_icon_currency_hard_0", new Vector2(-26f, -60f));
            var coins = BuildCurrency(bar, "Coins", "ui_icon_currency_soft_0", new Vector2(-344f, -60f));

            Wire(view,
                ("passTitle", title), ("timeLeft", timeLeft), ("xpFill", xpFill), ("xpFillWidth", 230f), ("xpLabel", xpLabel),
                ("nextLevelLabel", nextLevel), ("nextLevelBadge", levelBadge), ("premiumStatus", premiumStatus),
                ("coins", coins), ("gems", gems));
            return view;
        }

        static void BuildTab(RectTransform bar, string name, string label, string sprite, Vector2 position, Color textColor)
        {
            var tab = Place(NewUI(name, bar), new Vector2(0.5f, 1f), Center, position, new Vector2(250f, 92f));
            AddButton(tab, AddImage(tab, sprite, sliced: true, raycast: true));
            AddText(Stretch(NewUI("Label", tab), 10f, 10f, 10f, 4f), label, 32f, textColor, TextAlignmentOptions.Center, autoSizeMin: 20f);
            AddImage(Place(NewUI("Alert", tab), new Vector2(1f, 1f), Center, new Vector2(-8f, -6f), new Vector2(48f, 48f)), "ui_icon_generic_red_dot", preserveAspect: true);
        }

        static CurrencyCounter BuildCurrency(RectTransform bar, string name, string iconSprite, Vector2 position)
        {
            var group = Place(NewUI(name, bar), new Vector2(1f, 1f), new Vector2(1f, 0.5f), position, new Vector2(274f, 66f));
            AddImage(Stretch(NewUI("Panel", group)), "ui_item_square_16px", new Color(0.08f, 0.03f, 0.25f, 0.55f), sliced: true);
            var icon = Place(NewUI("Icon", group), new Vector2(0f, 0.5f), Center, new Vector2(8f, 0f), new Vector2(90f, 90f));
            AddImage(icon, iconSprite, preserveAspect: true);
            var label = AddText(Stretch(NewUI("Value", group), 56f, 0f, 82f, 0f), "0", 38f, Color.white, TextAlignmentOptions.MidlineRight);
            var add = Place(NewUI("AddButton", group), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(4f, 0f), new Vector2(72f, 72f));
            AddButton(add, AddImage(add, "ui_button_add", raycast: true, preserveAspect: true));

            var counter = group.gameObject.AddComponent<CurrencyCounter>();
            Wire(counter, ("label", label), ("icon", icon));
            return counter;
        }

        static RewardTooltip BuildTooltip(RectTransform parent)
        {
            var root = Stretch(NewUI("Tooltip", parent));
            var tooltip = root.gameObject.AddComponent<RewardTooltip>();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var panelColor = new Color(0.1f, 0.06f, 0.26f, 0.97f);
            var panel = Place(NewUI("Panel", root), Center, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(420f, 150f));
            AddImage(panel, "ui_item_square_24px", panelColor, sliced: true);
            AddImage(Stretch(NewUI("Border", panel)), "ui_item_frame_thin_16px", new Color(1f, 1f, 1f, 0.28f), sliced: true);
            var arrowDown = Place(NewUI("ArrowDown", panel), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(40f, 20f));
            AddImage(arrowDown, "ui_item_arrow_01_bottom", panelColor);
            var arrowUp = Place(NewUI("ArrowUp", panel), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -2f), new Vector2(40f, 20f));
            AddImage(arrowUp, "ui_item_arrow_01_top", panelColor);

            var nameLabel = AddText(Anchors(NewUI("Name", panel), new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.93f)), "REWARD", 34f, Color.white, TextAlignmentOptions.Center, autoSizeMin: 20f);
            var rarityLabel = AddText(Anchors(NewUI("Rarity", panel), new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.6f)), "EPIC", 26f, Color.white, TextAlignmentOptions.Center);
            var statusLabel = AddText(Anchors(NewUI("Status", panel), new Vector2(0.05f, 0.07f), new Vector2(0.95f, 0.36f)), "Tap to claim", 24f, new Color(0.85f, 0.87f, 1f), TextAlignmentOptions.Center, autoSizeMin: 16f);

            Wire(tooltip,
                ("panel", panel), ("group", group), ("nameLabel", nameLabel), ("rarityLabel", rarityLabel),
                ("statusLabel", statusLabel), ("arrowDown", arrowDown), ("arrowUp", arrowUp));
            return tooltip;
        }

        // ------------------------------------------------------------------ scene and preview

        static void PlaceInScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Opening a scene unloads unreferenced assets, so load the prefab only after it.
            var screenPrefab = AssetDatabase.LoadAssetAtPath<BattlePassScreen>(ScreenPrefabPath);
            var canvas = RootCanvas();
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(canvas.transform.GetChild(i).gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab.gameObject, scene);
            instance.name = "BattlePassScreen";
            instance.transform.SetParent(canvas.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void RenderPreview(string fileName)
        {
            const int width = 2340;
            const int height = 1080;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = Object.FindFirstObjectByType<Camera>();
            var canvas = RootCanvas();
            var screen = Object.FindFirstObjectByType<BattlePassScreen>();

            // Offscreen capture: a world-space canvas framed by the orthographic UI camera.
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(width, height);
            canvasRect.localScale = Vector3.one * (camera.orthographicSize * 2f / height);
            canvasRect.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);

            // Edit mode runs no LateUpdate, so the jump tag is refreshed by hand after each scroll.
            var jump = Object.FindFirstObjectByType<ProgressJumpButton>();
            screen.BuildForPreview();
            jump.Refresh(animate: false);
            Capture(camera, width, height, fileName);

            // Second shot from the start of the road, where the pass rewards and the ticket sit.
            var road = Object.FindFirstObjectByType<ScrollRect>();
            road.content.anchoredPosition = new Vector2(0f, road.content.anchoredPosition.y);
            jump.Refresh(animate: false);
            Capture(camera, width, height, Path.GetFileNameWithoutExtension(fileName) + "_start.png");
        }

        static void Capture(Camera camera, int width, int height, string fileName)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();

            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            Directory.CreateDirectory(PreviewFolder);
            File.WriteAllBytes(Path.Combine(PreviewFolder, fileName), image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Debug.Log($"[BattlePassBuilder] Preview written to {PreviewFolder}/{fileName}");
        }

        // ------------------------------------------------------------------ helpers

        // The screen nests its own canvases (FX layer), so always look for the scene's root canvas.
        static Canvas RootCanvas() =>
            Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(canvas => canvas.isRootCanvas);

        static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = uiLayer };
            var rect = (RectTransform)go.transform;
            if (parent != null)
                rect.SetParent(parent, false);
            return rect;
        }

        static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Center;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform Anchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = Center;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image AddImage(RectTransform rect, string sprite, Color? color = null, bool sliced = false, bool raycast = false, bool preserveAspect = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? Sprite(sprite) : null;
            image.material = uiMaterial;
            image.color = color ?? Color.white;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = raycast;
            image.preserveAspect = preserveAspect;
            return image;
        }

        static TextMeshProUGUI AddText(RectTransform rect, string text, float size, Color color, TextAlignmentOptions alignment, float autoSizeMin = 0f)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = textMaterial;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            if (autoSizeMin > 0f)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = autoSizeMin;
                label.fontSizeMax = size;
            }
            return label;
        }

        static Button AddButton(RectTransform rect, Graphic target)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = target;
            rect.gameObject.AddComponent<PressFeedback>();
            return button;
        }

        static UIFxMeshEffect AddFx(Graphic graphic, UIFxFlags flags, RectTransform space = null, float idle = 0f)
        {
            var fx = graphic.gameObject.AddComponent<UIFxMeshEffect>();
            fx.Configure(space, flags);
            fx.Idle = idle;
            return fx;
        }

        static T InstantiateNested<T>(T prefab, Transform parent, string name) where T : Component
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<T>();
        }

        static T SavePrefab<T>(RectTransform root, string path) where T : Component
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<T>();
        }

        static void Wire(Object target, params (string field, object value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[BattlePassBuilder] {target.GetType().Name} has no serialized field '{field}'.");
                    continue;
                }
                switch (value)
                {
                    case Object reference: property.objectReferenceValue = reference; break;
                    case float number: property.floatValue = number; break;
                    case int integer: property.intValue = integer; break;
                    case bool flag: property.boolValue = flag; break;
                    case Color color: property.colorValue = color; break;
                    case string text: property.stringValue = text; break;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
            if (sprite == null)
                Debug.LogError($"[BattlePassBuilder] Missing sprite '{name}'.");
            return sprite;
        }

        static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteFolder + name + ".png");

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
