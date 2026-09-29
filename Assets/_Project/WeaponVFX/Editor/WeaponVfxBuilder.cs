using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace VertigoDemo.WeaponVFX.EditorTools
{
    /// <summary>
    /// Generates the weapon VFX materials, post-processing profile, weapon prefab (model, wind
    /// ribbons, particle systems) and the inspect scene from code, and renders headless previews.
    /// </summary>
    public static class WeaponVfxBuilder
    {
        const string Root = "Assets/_Project/WeaponVFX";
        const string MaterialFolder = Root + "/Materials";
        const string PrefabFolder = Root + "/Prefabs";
        const string SettingsFolder = Root + "/Settings";
        const string ScenePath = Root + "/Scenes/WeaponVFX.unity";
        const string WeaponPrefabPath = PrefabFolder + "/PF_Weapon_TopScorer.prefab";
        const string ModelPath = Root + "/Art/Models/spcl_rif_mcx_topscorer.fbx";
        const string AlbedoPath = Root + "/Art/Textures/t_spcl_rif_mcx_topscorer_diffuse.tga";
        // The glow sprites that came with the UI brief double as the particle and ribbon textures.
        const string FxTextureFolder = "Assets/_Project/BattlePass/Art/Sprites/";
        const string PreviewFolder = "Library/DemoPreviews";

        const float CameraDistance = 1.2f;
        static readonly Vector3 CoreCenter = new Vector3(0f, 0.034f, 0f);
        static readonly Color BackdropCenter = new Color(0.2f, 0.36f, 0.52f);
        static readonly Color BackdropEdge = new Color(0.03f, 0.07f, 0.13f);

        [MenuItem("Tools/Vertigo Demo/Rebuild Weapon VFX")]
        public static void Rebuild()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(SettingsFolder);

            var weapon = Material("M_Weapon_TopScorer", "VertigoDemo/Weapon/Legendary");
            weapon.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath));
            weapon.SetVector("_CoreCenter", CoreCenter);
            // Kept below the point where tonemapping bleaches it, so the ball stays a saturated yellow.
            weapon.SetColor("_CoreColor", new Color(1.9f, 1.45f, 0.12f));
            weapon.SetColor("_SheenColor", new Color(2.4f, 1.9f, 1f));
            weapon.SetFloat("_SheenWidth", 0.07f);

            var ribbon = Material("M_FX_WindRibbon", "VertigoDemo/FX/Wind Ribbon");
            ribbon.SetTexture("_StreakTex", FxTexture("ui_glow_04"));
            ribbon.SetColor("_LineColor", new Color(1.9f, 1.4f, 0.5f));
            ribbon.SetColor("_SheetColor", new Color(0.8f, 0.55f, 0.16f));
            ribbon.SetFloat("_Intensity", 1f);
            ribbon.SetFloat("_LineWidth", 0.08f);
            ribbon.SetFloat("_SheetOpacity", 0.55f);
            ribbon.SetFloat("_FlowContrast", 0.5f);
            ribbon.SetFloat("_FadeIn", 0.2f);
            ribbon.SetFloat("_FadeOut", 0.35f);

            const string particleShader = "VertigoDemo/FX/Particle Additive";
            var sparkle = ParticleMaterial("M_FX_WeaponSparkle", particleShader, "ui_fx_glow_01", new Color(2.2f, 1.8f, 1.1f), CompareFunction.LessEqual);
            var dust = ParticleMaterial("M_FX_WeaponDust", particleShader, "ui_fx_glow_01", new Color(1.3f, 1.1f, 0.65f), CompareFunction.LessEqual);
            var coreGlow = ParticleMaterial("M_FX_WeaponCoreGlow", particleShader, "ui_fx_glow_01", new Color(1.6f, 1.1f, 0.35f), CompareFunction.Always);

            var backdrop = Material("M_BG_Inspect", "VertigoDemo/FX/Background Gradient");
            backdrop.SetColor("_CenterColor", BackdropCenter);
            backdrop.SetColor("_EdgeColor", BackdropEdge);

            var profile = CreateVolumeProfile(SettingsFolder + "/VP_WeaponInspect.asset");
            BuildWeaponPrefab(weapon, ribbon, sparkle, dust, coreGlow);
            BuildScene(backdrop, profile);

            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponVfxBuilder] Materials, prefab and scene rebuilt.");
        }

        public static void RebuildAndPreview()
        {
            Rebuild();
            RenderPreviews();
        }

        // ------------------------------------------------------------------ materials and settings

        static Material Material(string name, string shaderName)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                // Start again from the shader's defaults, which also drops properties it no longer has.
                var fresh = new Material(shader) { name = material.name };
                EditorUtility.CopySerialized(fresh, material);
                Object.DestroyImmediate(fresh);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material ParticleMaterial(string name, string shaderName, string texture, Color tint, CompareFunction depthTest)
        {
            var material = Material(name, shaderName);
            material.SetTexture("_MainTex", FxTexture(texture));
            material.SetColor("_TintColor", tint);
            material.SetFloat("_ZTest", (float)depthTest);
            return material;
        }

        static VolumeProfile CreateVolumeProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (var component in profile.components.ToArray())
            {
                profile.Remove(component.GetType());
                Object.DestroyImmediate(component, true);
            }

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.7f);
            bloom.highQualityFiltering.Override(false);
            // Mobile budget: start the blur chain at quarter resolution and keep it short.
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(5);

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);

            var grading = Add<ColorAdjustments>(profile);
            grading.contrast.Override(8f);
            grading.saturation.Override(10f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------ weapon prefab

        static void BuildWeaponPrefab(Material weapon, Material ribbon, Material sparkle, Material dust, Material coreGlow)
        {
            var root = new GameObject("PF_Weapon_TopScorer");
            // Everything below lives in the model's own space; the offset centres the rifle on the pivot.
            var modelSpace = new GameObject("ModelSpace");
            modelSpace.transform.SetParent(root.transform, false);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            model.name = "Model";
            model.transform.SetParent(modelSpace.transform, false);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = weapon;
                MakeUnlitRenderer(renderer);
            }
            var bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            modelSpace.transform.localPosition = -bounds.center;

            var ribbons = new GameObject("WindRibbons", typeof(MeshFilter), typeof(MeshRenderer), typeof(WindRibbonMesh));
            ribbons.transform.SetParent(modelSpace.transform, false);
            var ribbonRenderer = ribbons.GetComponent<MeshRenderer>();
            ribbonRenderer.sharedMaterial = ribbon;
            MakeUnlitRenderer(ribbonRenderer);
            ribbons.GetComponent<WindRibbonMesh>().Configure(RibbonDefinitions(), 48);
            // The mesh is regenerated on load; keep the generated (DontSave) mesh out of the prefab.
            ribbons.GetComponent<MeshFilter>().sharedMesh = null;

            var fx = new GameObject("FX");
            fx.transform.SetParent(modelSpace.transform, false);
            BuildSparkles(fx.transform, sparkle, bounds);
            BuildDust(fx.transform, dust, bounds);
            BuildCoreGlow(fx.transform, coreGlow);

            PrefabUtility.SaveAsPrefabAsset(root, WeaponPrefabPath);
            Object.DestroyImmediate(root);
        }

        // Paths in the rifle's space: muzzle +Z, stock -Z, and negative X is the side facing the default
        // camera. Like the reference, the ribbons leave the muzzle, sweep back and down along the front
        // and underside of the rifle and fade out before the grip; one passes over the top and one runs
        // behind the rifle, for depth when it turns.
        static WindRibbonMesh.Ribbon[] RibbonDefinitions() => new[]
        {
            // Along the lower handguard, dipping past the foregrip, across the receiver and up to the grip.
            Ribbon(new[] { P(-0.022f, 0.028f, 0.530f), P(-0.035f, 0.008f, 0.410f), P(-0.047f, -0.030f, 0.280f), P(-0.056f, -0.055f, 0.150f), P(-0.062f, -0.058f, 0.030f), P(-0.055f, -0.040f, -0.080f), P(-0.042f, -0.015f, -0.170f) },
                60f, 25f, 0.030f, 1f, 1f),
            // Short, steep sweep under the foregrip.
            Ribbon(new[] { P(-0.015f, 0.018f, 0.500f), P(-0.028f, -0.015f, 0.410f), P(-0.038f, -0.060f, 0.300f), P(-0.045f, -0.095f, 0.200f), P(-0.048f, -0.110f, 0.120f) },
                70f, 30f, 0.028f, 0.85f, 1.15f),
            // Across the face of the handguard, the scarf and the cage.
            Ribbon(new[] { P(-0.030f, 0.035f, 0.450f), P(-0.040f, 0.030f, 0.330f), P(-0.050f, 0.020f, 0.200f), P(-0.062f, 0.010f, 0.080f), P(-0.068f, 0.012f, -0.020f), P(-0.050f, 0.025f, -0.120f), P(-0.040f, 0.030f, -0.180f) },
                55f, 20f, 0.026f, 1f, 0.9f),
            // Over the barrel and past the sight, slipping behind the scarf.
            Ribbon(new[] { P(-0.012f, 0.050f, 0.520f), P(-0.020f, 0.070f, 0.400f), P(-0.030f, 0.090f, 0.270f), P(-0.020f, 0.095f, 0.170f), P(0.020f, 0.100f, 0.100f) },
                -45f, -20f, 0.022f, 0.7f, 1.2f),
            // Soft wisp coming off the muzzle.
            Ribbon(new[] { P(-0.010f, 0.035f, 0.585f), P(-0.018f, 0.028f, 0.520f), P(-0.026f, 0.018f, 0.450f), P(-0.032f, 0.005f, 0.380f) },
                75f, 60f, 0.035f, 0.5f, 0.8f),
            // Behind the rifle.
            Ribbon(new[] { P(0.015f, 0.020f, 0.500f), P(0.035f, -0.015f, 0.360f), P(0.050f, -0.045f, 0.200f), P(0.055f, -0.060f, 0.050f), P(0.045f, -0.040f, -0.080f) },
                60f, 30f, 0.028f, 0.6f, 1.05f),
        };

        static Vector3 P(float x, float y, float z) => new Vector3(x, y, z);

        static WindRibbonMesh.Ribbon Ribbon(Vector3[] path, float rollStart, float rollEnd, float width, float brightness, float speed) => new WindRibbonMesh.Ribbon
        {
            path = path,
            roll = new Vector2(rollStart, rollEnd),
            width = width,
            brightness = brightness,
            speed = speed,
        };

        // Four-point glints: a camera-facing cross of two thin quads, each squashing the soft round glow
        // into a streak. Eight vertices per sparkle, and no dedicated star texture needed.
        static Mesh StarCrossMesh()
        {
            string path = Root + "/Meshes/SM_FX_StarCross.asset";
            EnsureFolder(Root + "/Meshes");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "SM_FX_StarCross" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            const float half = 0.5f;
            const float thin = 0.1f;
            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-half, -thin, 0f), new Vector3(half, -thin, 0f), new Vector3(-half, thin, 0f), new Vector3(half, thin, 0f),
                new Vector3(-thin, -half, 0f), new Vector3(thin, -half, 0f), new Vector3(-thin, half, 0f), new Vector3(thin, half, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void BuildSparkles(Transform parent, Material material, Bounds bounds)
        {
            var system = NewSystem(parent, "Sparkles", material, 6);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = StarCrossMesh();
            renderer.alignment = ParticleSystemRenderSpace.View;
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.045f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            main.startColor = new Color(1f, 0.95f, 0.8f);
            var emission = system.emission;
            emission.rateOverTime = 2.5f;
            // A few small glints on and just around the rifle, a little above centre like the reference.
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = bounds.center + new Vector3(0f, 0.02f, 0f);
            shape.scale = bounds.size + new Vector3(0.02f, 0.02f, 0f);
            SizeOverLifetime(system, new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            FadeInOut(system, 0.2f, 0.6f);
        }

        static void BuildDust(Transform parent, Material material, Bounds bounds)
        {
            var system = NewSystem(parent, "Dust", material, 14);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.009f);
            main.startColor = new Color(1f, 0.9f, 0.6f, 0.8f);
            var emission = system.emission;
            emission.rateOverTime = 5f;
            // Only around the front of the rifle, where the wind is, so none collects at the grip.
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = bounds.center + new Vector3(0f, 0f, bounds.size.z * 0.2f);
            shape.scale = Vector3.Scale(bounds.size, new Vector3(1.4f, 1f, 0.65f));
            // Drift with the wind, from muzzle to stock, with a little turbulence.
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.015f, 0.02f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, -0.04f);
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.02f;
            noise.frequency = 1.4f;
            noise.scrollSpeed = 0.25f;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
            SizeOverLifetime(system, new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.4f));
            FadeInOut(system, 0.25f, 0.7f);
        }

        // Soft halo over the glowing ball; drawn without depth test so the cage bars cannot hide it.
        static void BuildCoreGlow(Transform parent, Material material)
        {
            var system = NewSystem(parent, "CoreGlow", material, 4);
            system.transform.localPosition = CoreCenter;
            var main = system.main;
            main.startLifetime = 1.2f;
            main.startSize = 0.15f;
            main.startColor = new Color(1f, 0.8f, 0.3f, 0.35f);
            var emission = system.emission;
            emission.rateOverTime = 2.5f;
            var shape = system.shape;
            shape.enabled = false;
            SizeOverLifetime(system, new Keyframe(0f, 0.85f), new Keyframe(0.5f, 1.1f), new Keyframe(1f, 0.85f));
            FadeInOut(system, 0.5f, 0.5f);
        }

        static ParticleSystem NewSystem(Transform parent, string name, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.duration = 2f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var shape = system.shape;
            shape.enabled = true;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            MakeUnlitRenderer(renderer);
            return system;
        }

        static void SizeOverLifetime(ParticleSystem system, params Keyframe[] keys)
        {
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        static void FadeInOut(ParticleSystem system, float fadeInEnd, float fadeOutStart)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeInEnd), new GradientAlphaKey(1f, fadeOutStart), new GradientAlphaKey(0f, 1f) });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        static void MakeUnlitRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(Material backdrop, VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.52f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.3f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.12f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetPositionAndRotation(new Vector3(-CameraDistance, 0.01f, 0f), Quaternion.LookRotation(Vector3.right, Vector3.up));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackdropEdge;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // Any quad in view fills the screen with the gradient shader (it writes clip space directly).
            var backdropQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdropQuad.name = "Backdrop";
            Object.DestroyImmediate(backdropQuad.GetComponent<Collider>());
            backdropQuad.transform.SetParent(cameraObject.transform, false);
            backdropQuad.transform.localPosition = new Vector3(0f, 0f, 1f);
            backdropQuad.transform.localScale = Vector3.one * 0.1f;
            var backdropRenderer = backdropQuad.GetComponent<MeshRenderer>();
            backdropRenderer.sharedMaterial = backdrop;
            MakeUnlitRenderer(backdropRenderer);

            var lightObject = new GameObject("Key Light");
            lightObject.transform.rotation = Quaternion.Euler(32f, 62f, 0f);
            var keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.25f;
            keyLight.color = new Color(1f, 0.95f, 0.88f);
            keyLight.shadows = LightShadows.None;

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var turntable = new GameObject("Turntable");
            var inspect = turntable.AddComponent<InspectTurntable>();
            var showcase = new SerializedObject(turntable.AddComponent<InspectShowcase>());
            showcase.FindProperty("turntable").objectReferenceValue = inspect;
            showcase.ApplyModifiedPropertiesWithoutUndo();
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath), scene);
            weapon.transform.SetParent(turntable.transform, false);

            BuildOverlay();
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void BuildOverlay()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            var canvasObject = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)) { layer = uiLayer };
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var canvas = (RectTransform)canvasObject.transform;

            Text(canvas, "Title", font, "MCX - TOP SCORER <size=72%><color=#EE7A21>LEGENDARY</color></size>", 66f,
                new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(70f, -100f), new Vector2(1200f, 90f), TextAlignmentOptions.MidlineLeft, Color.white);

            var back = UIObject(canvas, "GoBackButton", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(100f, 60f), new Vector2(300f, 120f));
            var frame = back.gameObject.AddComponent<Image>();
            frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FxTextureFolder + "ui_item_frame_thin_16px.png");
            frame.type = Image.Type.Sliced;
            frame.color = new Color(0.35f, 0.5f, 1f);
            frame.raycastTarget = false;
            var label = Text(back, "Label", font, "GO BACK", 44f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 120f), TextAlignmentOptions.Center, Color.white);
            label.fontStyle = FontStyles.Bold;

            Text(canvas, "Hint", font, "DRAG TO ROTATE    1 / 2  SWITCH VIEW", 26f, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 50f),
                new Vector2(800f, 40f), TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.55f));
        }

        static RectTransform UIObject(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static TextMeshProUGUI Text(RectTransform parent, string name, TMP_FontAsset font, string text, float size,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize, TextAlignmentOptions alignment, Color color)
        {
            var label = UIObject(parent, name, anchor, pivot, position, rectSize).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        // ------------------------------------------------------------------ previews

        public static void RenderPreviews()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = Object.FindFirstObjectByType<Camera>();
            var turntable = Object.FindFirstObjectByType<InspectTurntable>().transform;
            foreach (var system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                system.Simulate(2f, false, true);

            foreach (var ribbons in Object.FindObjectsByType<WindRibbonMesh>(FindObjectsSortMode.None))
            {
                var mesh = ribbons.GetComponent<MeshFilter>().sharedMesh;
                var renderer = ribbons.GetComponent<MeshRenderer>();
                Debug.Log($"[WeaponVfxBuilder] Ribbons: mesh {(mesh != null ? $"{mesh.vertexCount} verts, bounds {mesh.bounds}" : "missing")}, " +
                          $"material {renderer.sharedMaterial?.name}, enabled {renderer.enabled}, active {ribbons.isActiveAndEnabled}");
            }

            Capture(camera, "weapon_side.png");
            turntable.localRotation = Quaternion.AngleAxis(-34f, Vector3.up) * Quaternion.AngleAxis(6f, Vector3.forward);
            Capture(camera, "weapon_three_quarter.png");
        }

        static void Capture(Camera camera, string fileName)
        {
            const int width = 1920;
            const int height = 1080;
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
            Debug.Log($"[WeaponVfxBuilder] Preview written to {PreviewFolder}/{fileName}");
        }

        static Texture2D FxTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(FxTextureFolder + name + ".png");

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
