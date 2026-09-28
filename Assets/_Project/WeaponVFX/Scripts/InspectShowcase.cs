using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VertigoDemo.WeaponVFX
{
    /// <summary>
    /// Scripted showcase for recordings: the rifle holds the side view, turns to the three-quarter
    /// view, makes a slow full turn and returns to the side view. Press P to play it.
    /// </summary>
    public sealed class InspectShowcase : MonoBehaviour
    {
        [SerializeField] InspectTurntable turntable;

        public bool IsPlaying { get; private set; }
        public bool IsFinished { get; private set; }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
                Play();
        }

        public void Play()
        {
            if (IsPlaying)
                return;
            StartCoroutine(Showcase());
        }

        IEnumerator Showcase()
        {
            IsPlaying = true;
            IsFinished = false;

            turntable.ShowView(0);
            yield return new WaitForSeconds(5f);
            turntable.ShowView(1);
            yield return new WaitForSeconds(4.5f);
            yield return turntable.Spin(360f, 7f);
            turntable.ShowView(0);
            yield return new WaitForSeconds(3.5f);

            IsPlaying = false;
            IsFinished = true;
        }
    }
}
