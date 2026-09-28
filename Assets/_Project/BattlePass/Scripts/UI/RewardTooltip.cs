using System.Collections;
using TMPro;
using UnityEngine;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>Details bubble for the selected reward, pointing at its card.</summary>
    public sealed class RewardTooltip : MonoBehaviour
    {
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text rarityLabel;
        [SerializeField] TMP_Text statusLabel;
        [Tooltip("Shown when the bubble sits above the card.")]
        [SerializeField] RectTransform arrowDown;
        [Tooltip("Shown when the bubble sits below the card.")]
        [SerializeField] RectTransform arrowUp;
        [SerializeField] float gap = 16f;
        [SerializeField] float screenMargin = 24f;

        readonly Vector3[] corners = new Vector3[4];
        Coroutine routine;

        public void Show(RewardCardView card, string status)
        {
            var reward = card.Reward;
            string amount = reward.AmountLabel;
            nameLabel.text = amount.Length > 0 ? $"{reward.displayName}  {amount}" : reward.displayName;
            rarityLabel.text = reward.rarity.ToString().ToUpperInvariant();
            rarityLabel.color = Color.Lerp(card.Accent, Color.white, 0.2f);
            statusLabel.text = status;

            gameObject.SetActive(true);
            Place(card);

            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(Tween.Run(0.18f, t =>
            {
                panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, Ease.OutBack(t));
                group.alpha = t;
            }));
        }

        public void Hide()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            gameObject.SetActive(false);
        }

        // Bottom-row cards get the bubble above them, top-row cards below, so it never covers the card itself.
        void Place(RewardCardView card)
        {
            var area = (RectTransform)panel.parent;
            card.Body.GetWorldCorners(corners);
            Vector2 min = area.InverseTransformPoint(corners[0]);
            Vector2 max = area.InverseTransformPoint(corners[2]);

            bool above = card.Track == RewardTrack.Free;
            panel.pivot = new Vector2(0.5f, above ? 0f : 1f);
            arrowDown.gameObject.SetActive(above);
            arrowUp.gameObject.SetActive(!above);

            float centerX = (min.x + max.x) * 0.5f;
            float halfWidth = panel.rect.width * 0.5f;
            Rect bounds = area.rect;
            float x = Mathf.Clamp(centerX, bounds.xMin + screenMargin + halfWidth, bounds.xMax - screenMargin - halfWidth);
            float y = above ? max.y + gap : min.y - gap;
            panel.anchoredPosition = new Vector2(x, y);

            var arrow = above ? arrowDown : arrowUp;
            arrow.anchoredPosition = new Vector2(Mathf.Clamp(centerX - x, -halfWidth + 30f, halfWidth - 30f), arrow.anchoredPosition.y);
        }
    }
}
