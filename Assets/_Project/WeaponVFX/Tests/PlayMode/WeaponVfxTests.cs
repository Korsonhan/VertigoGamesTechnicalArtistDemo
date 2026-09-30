using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace VertigoDemo.WeaponVFX.Tests
{
    /// <summary>
    /// Runs the weapon inspect scene, checks that the effects are alive, and logs the rendering cost.
    /// Pass -captureFrames on the command line to save frames over time to Library/DemoPreviews/Weapon.
    /// </summary>
    public sealed class WeaponVfxTests
    {
        const int Width = 1920;
        const int Height = 1080;
        const string FrameFolder = "Library/DemoPreviews/Weapon";

        Camera camera;
        RenderTexture target;
        bool captureFrames;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("WeaponVFX");
            captureFrames = Environment.GetCommandLineArgs().Contains("-captureFrames");
            camera = Camera.main;
            target = new RenderTexture(Width, Height, 24);
            camera.targetTexture = target;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Release()
        {
            camera.targetTexture = null;
            target.Release();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EffectsRunAndStayCheap()
        {
            yield return new WaitForSecondsRealtime(1f);

            var ribbons = Object.FindFirstObjectByType<WindRibbonMesh>();
            Assert.IsNotNull(ribbons, "Wind ribbons missing.");
            Assert.Greater(ribbons.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0, "Wind ribbons were not generated.");
            foreach (var system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                Assert.Greater(system.particleCount, 0, $"{system.name} emits nothing.");

            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            using var setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            for (int i = 0; i < 5; i++)
                yield return null;
            int liveParticles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Sum(s => s.particleCount);
            Debug.Log($"[Perf] Weapon inspect scene: {batches.LastValue} batches, {setPassCalls.LastValue} SetPass calls, " +
                      $"{drawCalls.LastValue} draw calls, {triangles.LastValue} triangles, {liveParticles} live particles");

            // Budgets for a mobile inspect screen, with some headroom over the current cost.
            Assert.Greater(drawCalls.LastValue, 0, "Render counters were not recorded.");
            Assert.LessOrEqual(drawCalls.LastValue, 35, "Too many draw calls.");
            Assert.LessOrEqual(triangles.LastValue, 10000, "Too many triangles.");
            Assert.LessOrEqual(liveParticles, 64, "Too many live particles.");

            // Frames over time from the side view, then the three-quarter view.
            for (int i = 0; i < 4; i++)
            {
                Capture($"side_{i}");
                yield return new WaitForSecondsRealtime(0.4f);
            }
            // One full sheen period (4.5 s) at a fine interval, to review the sweep.
            if (captureFrames)
            {
                for (int i = 0; i < 15; i++)
                {
                    Capture($"sweep_{i:00}");
                    yield return new WaitForSecondsRealtime(0.3f);
                }
            }

            Object.FindFirstObjectByType<InspectTurntable>().ShowView(1);
            yield return new WaitForSecondsRealtime(1.2f);
            for (int i = 0; i < 2; i++)
            {
                Capture($"three_quarter_{i}");
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        void Capture(string name)
        {
            if (!captureFrames)
                return;

            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(FrameFolder);
            File.WriteAllBytes(Path.Combine(FrameFolder, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
