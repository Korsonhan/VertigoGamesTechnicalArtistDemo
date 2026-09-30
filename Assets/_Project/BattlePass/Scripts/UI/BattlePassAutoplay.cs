using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// Scripted walkthrough for recordings: taps through claiming, inspecting, buying the pass and
    /// buying levels, with a touch ring wherever a tap lands. Taps go through the regular pointer
    /// events, so everything shown is the real interaction path. Press P to play it.
    /// </summary>
    public sealed class BattlePassAutoplay : MonoBehaviour
    {
        [SerializeField] BattlePassScreen screen;
        [SerializeField] Image touchRing;

        bool playing;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
                Play();
        }

        public void Play()
        {
            if (playing)
                return;
            StartCoroutine(Walkthrough());
        }

        IEnumerator Walkthrough()
        {
            playing = true;

            // Start where the road starts: claim the free chest under the pass ticket and look at what
            // comes with the pass.
            screen.ScrollToStart();
            yield return Tween.Wait(1.4f);
            yield return Tap(screen.GetCard(0, RewardTrack.Free));
            yield return Tween.Wait(1.4f);
            yield return Tap(screen.GetCard(0, RewardTrack.Premium));
            yield return Tween.Wait(1.8f);

            // Back to the player's progress with the jump button, and claim the free rewards waiting there.
            yield return Tap(screen.JumpButton);
            yield return Tween.Wait(1.2f);
            yield return Tap(screen.GetCard(2, RewardTrack.Free));
            yield return Tween.Wait(1.6f);
            yield return Tap(screen.GetCard(3, RewardTrack.Free));
            yield return Tween.Wait(1.4f);

            // Inspect a reward that is still locked.
            yield return Tap(screen.GetCard(5, RewardTrack.Premium));
            yield return Tween.Wait(1.8f);

            // Buy the premium pass: everything reached unlocks in a wave, the pass's own rewards included.
            yield return Tap(screen.PremiumButton);
            yield return Tween.Wait(2.6f);
            yield return Tap(screen.GetCard(3, RewardTrack.Premium));
            yield return Tween.Wait(1.6f);
            screen.ScrollToStart();
            yield return Tween.Wait(1.2f);
            yield return Tap(screen.GetCard(0, RewardTrack.Premium, 0));
            yield return Tween.Wait(1.6f);
            yield return Tap(screen.GetCard(0, RewardTrack.Premium, 1));
            yield return Tween.Wait(1.4f);
            yield return Tap(screen.GetCard(1, RewardTrack.Premium));
            yield return Tween.Wait(1.8f);

            // Buy two levels with gems and claim along the way.
            yield return Tap(screen.JumpButton);
            yield return Tween.Wait(1.2f);
            yield return Tap(screen.SkipLevelButton);
            yield return Tween.Wait(2.2f);
            yield return Tap(screen.GetCard(4, RewardTrack.Free));
            yield return Tween.Wait(1.2f);
            yield return Tap(screen.GetCard(4, RewardTrack.Premium));
            yield return Tween.Wait(1.6f);
            yield return Tap(screen.SkipLevelButton);
            yield return Tween.Wait(2.2f);
            yield return Tap(screen.GetCard(5, RewardTrack.Premium));
            yield return Tween.Wait(1.4f);

            // Peek at the rest of the season, then jump back to the player's progress.
            screen.ScrollToLevel(14);
            yield return Tween.Wait(2.2f);
            yield return Tap(screen.JumpButton);
            yield return Tween.Wait(2f);
            playing = false;
        }

        IEnumerator Tap(RewardCardView card) => Tap(card.gameObject, card.Body.position);

        IEnumerator Tap(Button button) => Tap(button.gameObject, button.transform.position);

        IEnumerator Tap(GameObject target, Vector3 position)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            StartCoroutine(Ring(position));
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            yield return Tween.Wait(0.12f);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        IEnumerator Ring(Vector3 position)
        {
            var rect = touchRing.rectTransform;
            rect.position = position;
            touchRing.gameObject.SetActive(true);
            yield return Tween.Run(0.45f, t =>
            {
                rect.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.3f, Ease.OutCubic(t));
                var color = touchRing.color;
                color.a = 0.9f * (1f - t);
                touchRing.color = color;
            });
            touchRing.gameObject.SetActive(false);
        }
    }
}
