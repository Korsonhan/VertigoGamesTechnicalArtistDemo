using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VertigoDemo.BattlePass.UI;
using Object = UnityEngine.Object;

namespace VertigoDemo.BattlePass.Tests
{
    /// <summary>
    /// Plays the Battle Pass scene the way a player would (claim, inspect, buy the pass, buy a level)
    /// and checks the resulting states. Pass -captureFrames on the command line to also save frames
    /// of the transitions to Library/DemoPreviews/Flow for visual review.
    /// </summary>
    public sealed class BattlePassFlowTests
    {
        const int Width = 2340;
        const int Height = 1080;
        const string FrameFolder = "Library/DemoPreviews/Flow";

        Camera camera;
        RenderTexture target;
        bool captureFrames;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("BattlePass");
            captureFrames = Environment.GetCommandLineArgs().Contains("-captureFrames");

            // Render at a phone aspect whatever the batch-mode screen size is: a world-space canvas
            // framed by the orthographic UI camera, drawn into an offscreen target.
            camera = Camera.main;
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas);
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(Width, Height);
            canvasRect.localScale = Vector3.one * (camera.orthographicSize * 2f / Height);
            canvasRect.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
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
        public IEnumerator IdleScreenRenderingCost()
        {
            // Past the opening glide from the start of the road to the player's progress.
            yield return Wait(2.5f);

            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            using var setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            for (int i = 0; i < 5; i++)
                yield return null;

            Debug.Log($"[Perf] Idle Battle Pass screen: {batches.LastValue} batches, {setPassCalls.LastValue} SetPass calls, " +
                      $"{drawCalls.LastValue} draw calls, {vertices.LastValue} vertices");
            Assert.Greater(drawCalls.LastValue, 0, "Render counters were not recorded.");
        }

        [UnityTest]
        public IEnumerator PlayerFlow()
        {
            // The road opens at its start: the pass's own rewards, the free chest under the ticket, and the
            // tag pointing towards the player's progress off to the right.
            yield return Wait(0.6f);
            Capture("00_start");
            var starterChest = Card(0, RewardTrack.Free);
            Assert.AreEqual(RewardState.Claimable, starterChest.State);
            Assert.AreEqual(RewardState.PremiumLocked, Card(0, RewardTrack.Premium, 0).State);
            Assert.IsTrue(JumpTag().gameObject.activeSelf, "The jump tag should point at the progress while it is out of view.");
            Click(starterChest.gameObject);
            yield return Wait(0.8f);
            Assert.AreEqual(RewardState.Claimed, starterChest.State);

            // The road then glides to the progress by itself, and the tag goes away.
            yield return Wait(1.6f);
            Capture("01_intro");
            Assert.IsFalse(JumpTag().gameObject.activeSelf, "The jump tag should hide once the progress is in view.");

            // Claim a free gem reward: the wallet should count up.
            var gemReward = Card(2, RewardTrack.Free);
            Assert.AreEqual(RewardState.Claimable, gemReward.State);
            int gems = Counter("Gems").Value;
            Click(gemReward.gameObject);
            yield return Wait(0.1f);
            Capture("02_claim_windup");
            yield return Wait(0.15f);
            Capture("03_claim_burst");
            yield return Wait(0.6f);
            Capture("04_claimed");
            Assert.AreEqual(RewardState.Claimed, gemReward.State);
            Assert.AreEqual(gems + 2, Counter("Gems").Value);

            // Inspect a locked reward.
            Click(Card(5, RewardTrack.Premium).gameObject);
            yield return Wait(0.3f);
            Capture("05_tooltip");
            Assert.IsTrue(Object.FindFirstObjectByType<RewardTooltip>(FindObjectsInactive.Include).gameObject.activeSelf);

            // Buy the premium pass: reached premium rewards unlock in a wave, the pass's own rewards first.
            Button("GetButton").onClick.Invoke();
            yield return Wait(0.3f);
            Capture("06_premium_wave_start");
            yield return Wait(0.3f);
            Capture("07_premium_wave");
            yield return Wait(1.4f);
            Capture("08_premium_claimable");
            for (int index = 0; index < 4; index++)
                Assert.AreEqual(RewardState.Claimable, Card(0, RewardTrack.Premium, index).State, $"Pass reward {index}");
            for (int level = 1; level <= 3; level++)
                Assert.AreEqual(RewardState.Claimable, Card(level, RewardTrack.Premium).State, $"Premium level {level}");

            // Claim a coin reward: coins fly into the wallet.
            int coins = Counter("Coins").Value;
            Click(Card(1, RewardTrack.Premium).gameObject);
            yield return Wait(0.45f);
            Capture("08b_coins_flying");
            yield return Wait(0.8f);
            Assert.AreEqual(coins + 5000, Counter("Coins").Value);

            // Buy the next level with gems: the new rewards unlock after the fill arrives.
            gems = Counter("Gems").Value;
            Button("SkipLevelButton").onClick.Invoke();
            yield return Wait(0.3f);
            Capture("09_level_up_fill");
            yield return Wait(0.45f);
            Capture("10_level_up_unlock");
            yield return Wait(1.2f);
            Capture("11_level_up_done");
            Assert.AreEqual(RewardState.Claimable, Card(4, RewardTrack.Free).State);
            Assert.AreEqual(RewardState.Claimable, Card(4, RewardTrack.Premium).State);
            Assert.AreEqual(gems - 20, Counter("Gems").Value);

            // Back at the start of the road, claim the mythic character the pass unlocks: the biggest burst.
            var screen = Object.FindFirstObjectByType<BattlePassScreen>();
            screen.ScrollToStart();
            yield return Wait(1f);
            var cleopatra = Card(0, RewardTrack.Premium, 0);
            Click(cleopatra.gameObject);
            yield return Wait(0.25f);
            Capture("12_claim_mythic");
            yield return Wait(1f);
            Assert.AreEqual(RewardState.Claimed, cleopatra.State);

            // Far down the road the tag points back at the progress, and tapping it scrolls there.
            screen.ScrollToLevel(20);
            yield return Wait(1.2f);
            Capture("13_jump_tag");
            var jumpTag = JumpTag();
            Assert.IsTrue(jumpTag.gameObject.activeSelf, "The jump tag should show while the progress is out of view.");
            Click(jumpTag.gameObject);
            yield return Wait(1.2f);
            Assert.IsFalse(jumpTag.gameObject.activeSelf, "Tapping the jump tag should bring the progress back into view.");
        }

        static RewardCardView Card(int level, RewardTrack track, int index = 0) =>
            Object.FindObjectsByType<RewardCardView>(FindObjectsSortMode.None).First(card => card.Slot.Equals(new RewardSlot(level, track, index)));

        static Button JumpTag() => Object.FindFirstObjectByType<ProgressJumpButton>().Button;

        static CurrencyCounter Counter(string name) =>
            Object.FindObjectsByType<CurrencyCounter>(FindObjectsSortMode.None).First(counter => counter.name == name);

        static Button Button(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(button => button.name == name);

        static void Click(GameObject target) =>
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

        static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

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
