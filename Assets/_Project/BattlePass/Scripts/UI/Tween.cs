using System;
using System.Collections;
using UnityEngine;

namespace VertigoDemo.UI
{
    /// <summary>
    /// Minimal coroutine tweens for one-shot UI transitions, so the project needs no third-party
    /// tweening package. Uses unscaled time so the UI keeps animating while gameplay is paused.
    /// </summary>
    public static class Tween
    {
        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }

        /// <summary>Calls <paramref name="step"/> with an eased 0-1 value every frame for <paramref name="duration"/> seconds.</summary>
        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null)
        {
            ease ??= Ease.Linear;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                step(ease(t / duration));
                yield return null;
            }
            step(ease(1f));
        }
    }

    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - (-2f * t + 2f) * (-2f * t + 2f) * 0.5f;
        public static float OutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>0 → 1 → 0 bump, handy for punch scales and flashes.</summary>
        public static float Bump(float t) => Mathf.Sin(t * Mathf.PI);
    }
}
