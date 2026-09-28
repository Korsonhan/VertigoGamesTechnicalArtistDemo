using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using VertigoDemo.BattlePass.UI;
using VertigoDemo.WeaponVFX;

namespace VertigoDemo.EditorTools
{
    /// <summary>
    /// Records the demo videos inside the project with Unity Recorder: opens each scene, enters play
    /// mode, runs its scripted walkthrough and writes an MP4 to Recordings/ at a constant 30 fps.
    /// Nothing is captured or post-processed outside the project.
    /// </summary>
    [InitializeOnLoad]
    public static class DemoVideoRecorder
    {
        const string QueueKey = "VertigoDemo.RecordingQueue";
        const string QuitKey = "VertigoDemo.RecordingQuitWhenDone";
        // Real-time safety net only: with a constant capture rate the video timing is right however
        // slowly the editor renders (an unfocused editor can drop to a few frames per second).
        const double TimeoutSeconds = 600.0;

        readonly struct Clip
        {
            public readonly string Name;
            public readonly string ScenePath;
            public readonly int Width;
            public readonly int Height;

            public Clip(string name, string scenePath, int width, int height)
            {
                Name = name;
                ScenePath = scenePath;
                Width = width;
                Height = height;
            }
        }

        // The Battle Pass is captured at a 19.5:9 phone aspect, the weapon at 16:9.
        static readonly Clip[] Clips =
        {
            new Clip("BattlePass", "Assets/_Project/BattlePass/Scenes/BattlePass.unity", 2340, 1080),
            new Clip("WeaponVFX", "Assets/_Project/WeaponVFX/Scenes/WeaponVFX.unity", 1920, 1080),
        };

        static RecorderController controller;
        static double clipStartTime;

        static DemoVideoRecorder() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("Tools/Vertigo Demo/Record Videos/All")]
        static void RecordAll() => Begin(Clips.Select(clip => clip.Name), quitWhenDone: false);

        [MenuItem("Tools/Vertigo Demo/Record Videos/Battle Pass")]
        static void RecordBattlePass() => Begin(new[] { "BattlePass" }, quitWhenDone: false);

        [MenuItem("Tools/Vertigo Demo/Record Videos/Weapon VFX")]
        static void RecordWeaponVfx() => Begin(new[] { "WeaponVFX" }, quitWhenDone: false);

        // Command-line entry points (-executeMethod): record, then close the editor.
        public static void RecordAllAndQuit() =>
            EditorApplication.delayCall += () => Begin(Clips.Select(clip => clip.Name), quitWhenDone: true);

        public static void RecordBattlePassAndQuit() =>
            EditorApplication.delayCall += () => Begin(new[] { "BattlePass" }, quitWhenDone: true);

        public static void RecordWeaponVfxAndQuit() =>
            EditorApplication.delayCall += () => Begin(new[] { "WeaponVFX" }, quitWhenDone: true);

        static void Begin(IEnumerable<string> names, bool quitWhenDone)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            SessionState.SetString(QueueKey, string.Join(";", names));
            SessionState.SetBool(QuitKey, quitWhenDone);
            PlayNext();
        }

        static void PlayNext()
        {
            string[] queue = Queue();
            if (queue.Length == 0)
            {
                Finish();
                return;
            }

            EditorSceneManager.OpenScene(Find(queue[0]).ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void Finish()
        {
            Debug.Log("[DemoVideoRecorder] All recordings finished.");
            if (SessionState.GetBool(QuitKey, false))
                EditorApplication.Exit(0);
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            string[] queue = Queue();
            if (queue.Length == 0)
                return;

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                StartClip(Find(queue[0]));
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                string[] remaining = queue.Skip(1).ToArray();
                SessionState.SetString(QueueKey, string.Join(";", remaining));
                if (remaining.Length > 0)
                    EditorApplication.delayCall += PlayNext;
                else
                    Finish();
            }
        }

        static void StartClip(Clip clip)
        {
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = clip.Name;
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = clip.Width, OutputHeight = clip.Height };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", clip.Name));

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = 30f;
            settings.CapFrameRate = true;

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            clipStartTime = EditorApplication.timeSinceStartup;
            Debug.Log($"[DemoVideoRecorder] Recording {clip.Name} to {movie.OutputFile}.mp4");

            var autoplay = Object.FindFirstObjectByType<BattlePassAutoplay>();
            if (autoplay != null)
                autoplay.Play();
            var showcase = Object.FindFirstObjectByType<InspectShowcase>();
            if (showcase != null)
                showcase.Play();

            EditorApplication.update += WatchClip;
        }

        static void WatchClip()
        {
            bool timedOut = EditorApplication.timeSinceStartup - clipStartTime > TimeoutSeconds;
            if (!WalkthroughFinished() && !timedOut)
                return;

            EditorApplication.update -= WatchClip;
            controller?.StopRecording();
            controller = null;
            if (timedOut)
                Debug.LogWarning("[DemoVideoRecorder] Walkthrough timed out; recording stopped.");
            EditorApplication.ExitPlaymode();
        }

        static bool WalkthroughFinished()
        {
            var autoplay = Object.FindFirstObjectByType<BattlePassAutoplay>();
            if (autoplay != null)
                return autoplay.IsFinished;
            var showcase = Object.FindFirstObjectByType<InspectShowcase>();
            return showcase == null || showcase.IsFinished;
        }

        static string[] Queue() =>
            SessionState.GetString(QueueKey, string.Empty).Split(';').Where(name => name.Length > 0).ToArray();

        static Clip Find(string name) => Clips.First(clip => clip.Name == name);
    }
}
