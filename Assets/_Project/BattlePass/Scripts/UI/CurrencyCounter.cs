using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>Wallet readout in the top bar: counts up on rewards and shakes when a purchase is refused.</summary>
    public sealed class CurrencyCounter : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] RectTransform icon;

        public int Value { get; private set; }

        Coroutine routine;

        public void Set(int value)
        {
            Stop();
            Value = value;
            label.text = Format(value);
        }

        public void Add(int amount) => AnimateTo(Value + amount);

        public bool TrySpend(int amount)
        {
            if (Value < amount)
            {
                Stop();
                routine = StartCoroutine(Shake());
                return false;
            }
            AnimateTo(Value - amount);
            return true;
        }

        void AnimateTo(int target)
        {
            Stop();
            int from = Value;
            Value = target;
            routine = StartCoroutine(Tween.Run(0.6f, t =>
            {
                label.text = Format(Mathf.RoundToInt(Mathf.Lerp(from, target, Ease.OutCubic(t))));
                icon.localScale = Vector3.one * (1f + 0.25f * Ease.Bump(Mathf.Clamp01(t * 2.5f)));
            }));
        }

        IEnumerator Shake()
        {
            yield return Tween.Run(0.35f, t =>
                icon.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 40f) * 14f * (1f - t)));
        }

        void Stop()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            icon.localScale = Vector3.one;
            icon.localEulerAngles = Vector3.zero;
            label.text = Format(Value);
        }

        static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
