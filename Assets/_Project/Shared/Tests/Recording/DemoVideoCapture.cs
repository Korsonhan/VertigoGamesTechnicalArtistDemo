#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VertigoDemo.BattlePass.UI;
using VertigoDemo.WeaponVFX;
using Object = UnityEngine.Object;

namespace VertigoDemo.Recording
{
    /// <summary>
    /// Headless counterpart of Tools > Vertigo Demo > Record Videos, for machines where the editor
    /// cannot stay in focus. Plays each scene's scripted walkthrough at a fixed 30 fps game time,
    /// renders every frame offscreen and encodes it with Unity's MediaEncoder into Recordings/.
    /// Skipped unless the editor is started with -recordVideos.
    /// </summary>
    [Category("Capture")]
    public sealed class DemoVideoCapture
    {
        const int FrameRate = 30;
        const int MaxSeconds = 60;

        [SetUp]
        public void RequireFlag()
        {
            if (!Environment.GetCommandLineArgs().Contains("-recordVideos"))
                Assert.Ignore("Start the editor with -recordVideos to capture the demo videos.");
        }

        [UnityTest]
        public IEnumerator BattlePass()
        {
            yield return SceneManager.LoadSceneAsync("BattlePass");
            var camera = Camera.main;
            // The screen's canvas is drawn by its camera; frame it as a world-space canvas at the phone aspect.
            FrameCanvas(RootCanvas(), camera, 2340, 1080);
            var autoplay = Object.FindFirstObjectByType<BattlePassAutoplay>();
            yield return Encode("BattlePass", 2340, 1080, camera, null, autoplay.Play, () => autoplay.IsFinished);
        }

        [UnityTest]
        public IEnumerator WeaponVFX()
        {
            yield return SceneManager.LoadSceneAsync("WeaponVFX");
            var camera = Camera.main;
            // The title is a screen-space overlay canvas, which cameras never draw. Render it with its own
            // camera onto a transparent target and composite it over the post-processed frame, so the
            // text stays out of the bloom and vignette like it does on screen.
            var overlay = new GameObject("CaptureOverlayCamera").AddComponent<Camera>();
            overlay.transform.position = new Vector3(0f, 500f, -10f);
            overlay.orthographic = true;
            overlay.orthographicSize = 5f;
            overlay.clearFlags = CameraClearFlags.SolidColor;
            overlay.backgroundColor = Color.clear;
            overlay.allowHDR = false;
            overlay.allowMSAA = false;
            overlay.cullingMask = 1 << LayerMask.NameToLayer("UI");
            FrameCanvas(RootCanvas(), overlay, 1920, 1080);
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));

            var showcase = Object.FindFirstObjectByType<InspectShowcase>();
            yield return Encode("WeaponVFX", 1920, 1080, camera, overlay, showcase.Play, () => showcase.IsFinished);
        }

        static IEnumerator Encode(string name, int width, int height, Camera camera, Camera overlay, Action start, Func<bool> finished)
        {
            var target = new RenderTexture(width, height, 24);
            camera.enabled = false;
            camera.targetTexture = target;

            RenderTexture overlayTarget = null;
            Material composite = null;
            if (overlay != null)
            {
                overlayTarget = new RenderTexture(width, height, 24);
                overlay.enabled = false;
                overlay.targetTexture = overlayTarget;
                composite = new Material(Shader.Find("Hidden/VertigoDemo/PremultipliedOver"));
            }

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", name + ".mp4"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var attributes = new VideoTrackAttributes
            {
                frameRate = new MediaRational(FrameRate),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High,
            };

            var frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            int frames = 0;
            Time.captureDeltaTime = 1f / FrameRate;
            start();
            using (var encoder = new MediaEncoder(path, attributes))
            {
                // Keep a second of tail after the walkthrough so the last animation settles.
                int tail = FrameRate;
                while (frames < MaxSeconds * FrameRate && tail > 0)
                {
                    yield return null;
                    camera.Render();
                    if (overlay != null)
                    {
                        overlay.Render();
                        Graphics.Blit(overlayTarget, target, composite);
                    }
                    RenderTexture.active = target;
                    frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    frame.Apply(false);
                    RenderTexture.active = null;
                    encoder.AddFrame(frame);
                    frames++;
                    if (finished())
                        tail--;
                }
            }
            Time.captureDeltaTime = 0f;

            camera.targetTexture = null;
            target.Release();
            if (overlay != null)
            {
                overlay.targetTexture = null;
                overlayTarget.Release();
                Object.Destroy(composite);
            }
            Object.Destroy(frame);
            Debug.Log($"[DemoVideoCapture] {name}: {frames} frames ({frames / (float)FrameRate:0.0} s) written to {path}");
            Assert.IsTrue(finished(), $"{name} walkthrough did not finish within {MaxSeconds} s.");
        }

        static Canvas RootCanvas() =>
            Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(canvas => canvas.isRootCanvas);

        static void FrameCanvas(Canvas canvas, Camera camera, int width, int height)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one * (camera.orthographicSize * 2f / height);
            rect.position = new Vector3(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z + 10f);
        }
    }
}
#endif
