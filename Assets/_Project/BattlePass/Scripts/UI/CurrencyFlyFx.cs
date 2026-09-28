using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// Coins and gems burst out of a claimed card and fly into the wallet. Uses a pool of images
    /// on the FX layer's own nested canvas, so the moving icons never rebuild the road's canvas.
    /// </summary>
    public sealed class CurrencyFlyFx : MonoBehaviour
    {
        [SerializeField] Image iconTemplate;
        [SerializeField, Min(1)] int poolSize = 16;
        [Tooltip("How far icons scatter around the card before flying, in canvas units.")]
        [SerializeField] float burstRadius = 150f;
        [SerializeField] float burstDuration = 0.2f;
        [SerializeField] float flightDuration = 0.55f;
        [SerializeField] float stagger = 0.05f;

        Image[] pool;
        int next;

        void Awake()
        {
            pool = new Image[poolSize];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(iconTemplate, transform);
                pool[i].name = $"FlyIcon_{i}";
                pool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Launches the icons and returns the seconds until the first one lands.</summary>
        public float Play(Sprite sprite, Vector3 from, RectTransform target, int count)
        {
            if (pool == null)
                return 0f;

            count = Mathf.Clamp(count, 1, pool.Length);
            for (int i = 0; i < count; i++)
            {
                var icon = pool[next];
                next = (next + 1) % pool.Length;
                StartCoroutine(Fly(icon, sprite, from, target, i * stagger));
            }
            return burstDuration + flightDuration;
        }

        IEnumerator Fly(Image icon, Sprite sprite, Vector3 from, RectTransform target, float delay)
        {
            var rect = icon.rectTransform;
            icon.sprite = sprite;
            rect.SetAsLastSibling();
            icon.gameObject.SetActive(true);

            // Canvas units to world units, so the motion reads the same at any screen size.
            float unit = transform.lossyScale.x;
            Vector3 scatter = from + (Vector3)(Random.insideUnitCircle * (burstRadius * unit));

            yield return Tween.Run(burstDuration, t =>
            {
                rect.position = Vector3.LerpUnclamped(from, scatter, Ease.OutCubic(t));
                rect.localScale = Vector3.one * Ease.OutBack(t);
            });
            yield return Tween.Wait(delay);

            // Curve through a point above the scatter position, accelerating into the wallet.
            Vector3 start = rect.position;
            Vector3 bend = start + (Vector3)(Random.insideUnitCircle * (160f * unit)) + Vector3.up * (220f * unit);
            yield return Tween.Run(flightDuration, t =>
            {
                float eased = Ease.InCubic(t);
                Vector3 end = target.position;
                rect.position = Vector3.Lerp(Vector3.Lerp(start, bend, eased), Vector3.Lerp(bend, end, eased), eased);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, eased);
            });

            icon.gameObject.SetActive(false);
        }
    }
}
