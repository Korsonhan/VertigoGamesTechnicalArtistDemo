using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VertigoDemo.EditorTools
{
    /// <summary>
    /// Import conventions for the demo's art folders. Rules only run on an asset's first
    /// import (before it has a .meta), so later hand-tuning in the Inspector is never overwritten.
    /// </summary>
    public sealed class AssetImportRules : AssetPostprocessor
    {
        const string UISpriteFolder = "Assets/_Project/BattlePass/Art/Sprites/";
        const string WeaponArtFolder = "Assets/_Project/WeaponVFX/Art/";

        // 9-slice borders in source pixels: (left, bottom, right, top).
        static readonly Dictionary<string, Vector4> SliceBorders = new Dictionary<string, Vector4>
        {
            // Rounded panels and frames: the pixel count in the name is the corner radius.
            { "ui_item_square", new Vector4(8, 8, 8, 8) },
            { "ui_item_square_4px", new Vector4(6, 6, 6, 6) },
            { "ui_item_square_8px", new Vector4(8, 8, 8, 8) },
            { "ui_item_square_12px", new Vector4(12, 12, 12, 12) },
            { "ui_item_square_16px", new Vector4(16, 16, 16, 16) },
            { "ui_item_square_24px", new Vector4(24, 24, 24, 24) },
            { "ui_item_square_32px", new Vector4(32, 32, 32, 32) },
            { "ui_item_square_shadow_32px", new Vector4(36, 36, 36, 36) },
            { "ui_item_square_top_16px", new Vector4(16, 0, 16, 16) },
            { "ui_item_frame_thick_12px", new Vector4(12, 12, 12, 12) },
            { "ui_item_frame_thick_16px", new Vector4(16, 16, 16, 16) },
            { "ui_item_frame_thick_24px", new Vector4(24, 24, 24, 24) },
            { "ui_item_frame_thick_32px", new Vector4(32, 32, 32, 32) },
            { "ui_item_frame_thin_16px", new Vector4(24, 24, 24, 24) },
            { "ui_item_frame_thin_16px8", new Vector4(24, 24, 24, 24) },
            { "ui_item_frame_thin_16px_top", new Vector4(16, 0, 16, 20) },
            { "ui_item_frame_thin_24px", new Vector4(28, 28, 28, 28) },
            { "ui_item_card_shadow", new Vector4(24, 24, 24, 24) },
            { "ui_item_level_panel_collected", new Vector4(24, 24, 24, 24) },
            { "ui_item_booster_popup_background_blue_frame", new Vector4(40, 40, 40, 40) },
            { "ui_panel_currency", new Vector4(20, 20, 20, 20) },
            { "ui_tab_dark_gray_outline", new Vector4(24, 24, 24, 24) },

            // Progress bars.
            { "ui_progres_bar_battle_pass_bg", new Vector4(16, 16, 16, 16) },
            { "ui_progres_bar_battle_pass_fill", new Vector4(8, 8, 8, 8) },
            { "ui_bar_chest_progress", new Vector4(14, 14, 14, 14) },
            { "ui_bar_progress_chest_fill", new Vector4(12, 12, 12, 12) },
            { "ui_bar_progress_chest_fill_current", new Vector4(8, 8, 8, 8) },

            // Buttons: dark outline plus a thicker bottom lip.
            { "ui_button_blue", new Vector4(26, 34, 26, 26) },
            { "ui_button_dark_purple", new Vector4(26, 34, 26, 26) },
            { "ui_button_deactive", new Vector4(26, 34, 26, 26) },
            { "ui_button_green", new Vector4(26, 34, 26, 26) },
            { "ui_button_grey", new Vector4(26, 34, 26, 26) },
            { "ui_button_light_blue", new Vector4(26, 34, 26, 26) },
            { "ui_button_red", new Vector4(26, 34, 26, 26) },
            { "ui_button_yellow", new Vector4(26, 34, 26, 26) },

            // Reward card backgrounds, one per rarity.
            { "ui_card_epic", new Vector4(22, 30, 22, 22) },
            { "ui_card_legendary", new Vector4(22, 30, 22, 22) },
            { "ui_card_mythic", new Vector4(22, 30, 22, 22) },
            { "ui_card_rare", new Vector4(22, 30, 22, 22) },
            { "ui_card_uncommon", new Vector4(22, 30, 22, 22) },
            { "ui_event_pass_collectable", new Vector4(22, 30, 22, 22) },
        };

        void OnPreprocessTexture()
        {
            if (!assetImporter.importSettingsMissing)
                return;

            var importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(UISpriteFolder))
                ApplyUISpriteRules(importer, Path.GetFileNameWithoutExtension(assetPath));
            else if (assetPath.StartsWith(WeaponArtFolder))
                ApplyWeaponTextureRules(importer);
        }

        void OnPreprocessModel()
        {
            if (!assetImporter.importSettingsMissing || !assetPath.StartsWith(WeaponArtFolder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            // Diffuse-only asset: no normal map, so tangents would be wasted vertex data.
            importer.importTangents = ModelImporterTangents.None;
            importer.isReadable = false;
        }

        static void ApplyUISpriteRules(TextureImporter importer, string fileName)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            // Tiled Images with an unpacked sprite repeat through UVs, which needs Repeat wrapping.
            importer.wrapMode = fileName.Contains("_tile_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            if (SliceBorders.TryGetValue(fileName, out var border))
                importer.spriteBorder = border;

            // Soft glows and gradients survive low bit rates and never need full resolution on device.
            bool isSoft = fileName.Contains("glow") || fileName.Contains("gradient");
            SetMobileFormat(importer, isSoft ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6, isSoft ? 512 : 2048);
        }

        static void ApplyWeaponTextureRules(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 2;
            SetMobileFormat(importer, TextureImporterFormat.ASTC_6x6, 512);
        }

        static void SetMobileFormat(TextureImporter importer, TextureImporterFormat format, int maxSize)
        {
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = maxSize,
                    format = format,
                    textureCompression = TextureImporterCompression.Compressed,
                    compressionQuality = 50,
                });
            }
        }
    }
}
