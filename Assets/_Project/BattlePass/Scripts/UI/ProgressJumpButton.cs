using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// Waits at the edge of the road while the player's progress is scrolled out of view, pointing
    /// towards it with the number of the level being worked on, like the reference; tapping it scrolls
    /// back. It only touches the UI when it has to appear, switch sides or hide.
    /// </summary>
    public sealed class ProgressJumpButton : MonoBehaviour
    {
        [SerializeField] ScrollRect road;
        [SerializeField] Button button;
        [Tooltip("The tag itself, moved to either edge of the road and shown or hidden.")]
        [SerializeField] RectTransform bubble;
        [Tooltip("The tag's background, mirrored so that its tip points at the progress.")]
        [SerializeField] RectTransform bubbleShape;
        [Tooltip("The numbered circle, kept centred on the square part of the tag.")]
        [SerializeField] RectTransform badge;
        [SerializeField] TMP_Text label;
        [Tooltip("Distance from the road's edge to the tag's centre.")]
        [SerializeField] float edgeInset = 100f;
        [Tooltip("Offset of the numbered circle from the tag's centre, away from the tip.")]
        [SerializeField] float badgeOffset = 9f;

        public event Action Pressed;
        public Button Button => button;

        float targetX;
        int side;
        Coroutine popRoutine;

        void Awake()
        {
            button.onClick.AddListener(() => Pressed?.Invoke());
            bubble.gameObject.SetActive(false);
        }

        /// <summary>Where the progress sits, in road content units, and the level being worked on.</summary>
        public void SetTarget(float contentX, int level)
        {
            targetX = contentX;
            label.text = level.ToString();
        }

        void LateUpdate() => Refresh(animate: true);

        public void Refresh(bool animate)
        {
            float x = targetX + road.content.anchoredPosition.x;
            int wanted = x < 0f ? -1 : x > road.viewport.rect.width ? 1 : 0;
            if (wanted == side)
                return;

            bool wasShown = side != 0;
            side = wanted;
            if (popRoutine != null)
                StopCoroutine(popRoutine);
            popRoutine = null;
            animate &= isActiveAndEnabled;

            if (side == 0)
            {
                if (animate)
                    popRoutine = StartCoroutine(Pop(show: false, from: bubble.localScale.x));
                else
                    bubble.gameObject.SetActive(false);
                return;
            }

            Place(side);
            bubble.gameObject.SetActive(true);
            if (animate)
                popRoutine = StartCoroutine(Pop(show: true, from: wasShown ? 0.7f : 0f));
            else
                bubble.localScale = Vector3.one;
        }

        void Place(int direction)
        {
            var anchor = new Vector2(direction > 0 ? 1f : 0f, bubble.anchorMin.y);
            bubble.anchorMin = anchor;
            bubble.anchorMax = anchor;
            bubble.anchoredPosition = new Vector2(-direction * edgeInset, bubble.anchoredPosition.y);
            bubbleShape.localScale = new Vector3(direction, 1f, 1f);
            badge.anchoredPosition = new Vector2(-direction * badgeOffset, badge.anchoredPosition.y);
        }

        IEnumerator Pop(bool show, float from)
        {
            yield return Tween.Run(show ? 0.25f : 0.15f, t =>
            {
                float scale = show ? Mathf.LerpUnclamped(from, 1f, Ease.OutBack(t)) : Mathf.Lerp(from, 0f, t);
                bubble.localScale = Vector3.one * scale;
            });
            if (!show)
                bubble.gameObject.SetActive(false);
            popRoutine = null;
        }
    }
}
