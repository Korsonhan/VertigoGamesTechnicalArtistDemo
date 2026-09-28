using System.Collections;
using UnityEngine;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// The road's progress bar: the fill, the lighter band behind reached levels and the
    /// skip-level marker riding the head of the fill. Positions are in road content units.
    /// </summary>
    public sealed class ProgressTrackView : MonoBehaviour
    {
        [SerializeField] RectTransform fill;
        [SerializeField] RectTransform reachedArea;
        [SerializeField] RectTransform marker;
        [Tooltip("Narrowest the sliced fill can get before its caps overlap.")]
        [SerializeField] float minFillWidth = 20f;

        public float Position { get; private set; }

        public void SetPosition(float x)
        {
            Position = x;
            float fillWidth = x - fill.anchoredPosition.x;
            fill.gameObject.SetActive(fillWidth >= minFillWidth);
            fill.sizeDelta = new Vector2(Mathf.Max(minFillWidth, fillWidth), fill.sizeDelta.y);
            reachedArea.sizeDelta = new Vector2(Mathf.Max(0f, x), reachedArea.sizeDelta.y);
            marker.anchoredPosition = new Vector2(x, marker.anchoredPosition.y);
        }

        public IEnumerator AnimateTo(float x, float duration)
        {
            float from = Position;
            yield return Tween.Run(duration, t => SetPosition(Mathf.Lerp(from, x, t)), Ease.InOutCubic);
        }

        public void SetMarkerVisible(bool visible) => marker.gameObject.SetActive(visible);
    }
}
