using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// Wallet readout in the top bar. The value changes immediately; the readout counts up after an
    /// optional delay (so it can wait for flying reward icons) and shakes when a purchase is refused.
    /// </summary>
    public sealed class CurrencyCounter : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] RectTransform icon;

        public int Value { get; private set; }
        public RectTransform Icon => icon;

        int displayed;
        Coroutine routine;

        public void Set(int value)
        {
            Stop();
            Value = displayed = value;
            label.text = Format(value);
        }

        public void Add(int amount, float displayDelay = 0f) => AnimateTo(Value + amount, displayDelay);

        public bool TrySpend(int amount)
        {
            if (Value < amount)
            {
                Stop();
                routine = StartCoroutine(Shake());
                return false;
            }
            AnimateTo(Value - amount, 0f);
            return true;
        }

        void AnimateTo(int target, float delay)
        {
            Stop();
            Value = target;
            routine = StartCoroutine(Count(delay));
        }

        IEnumerator Count(float delay)
        {
            yield return Tween.Wait(delay);
            int from = displayed;
            int to = Value;
            yield return Tween.Run(0.6f, t =>
            {
                displayed = Mathf.RoundToInt(Mathf.Lerp(from, to, Ease.OutCubic(t)));
                label.text = Format(displayed);
                icon.localScale = Vector3.one * (1f + 0.25f * Ease.Bump(Mathf.Clamp01(t * 2.5f)));
            });
            routine = null;
        }

        IEnumerator Shake()
        {
            yield return Tween.Run(0.35f, t =>
                icon.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 40f) * 14f * (1f - t)));
            routine = null;
        }

        void Stop()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            icon.localScale = Vector3.one;
            icon.localEulerAngles = Vector3.zero;
            label.text = Format(displayed);
        }

        static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
