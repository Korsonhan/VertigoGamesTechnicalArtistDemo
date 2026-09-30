using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// One reward slot on the road. Applies the look of each state and plays the one-shot
    /// transitions between them; the looping idle effects (shine, glow pulse, badge bob) run in
    /// the UIFx shader, so an idle claimable card costs nothing on the CPU.
    /// </summary>
    public sealed class RewardCardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        const float LockedSaturation = 0.9f;
        const float LockedBrightness = 0.8f;
        const float ClaimedSaturation = 0.2f;
        const float ClaimedBrightness = 0.55f;
        const float GlowAlpha = 0.75f;
        const float RaysAlpha = 0.85f;
        const float CaptionIconGap = 6f;
        static readonly Color DimTextColor = new Color(0.78f, 0.78f, 0.84f, 0.9f);

        [Header("Parts")]
        [SerializeField] RectTransform body;
        [SerializeField] Image background;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text caption;
        [SerializeField] Image captionIcon;
        [SerializeField] Image lockBadge;
        [SerializeField] Image alertBadge;
        [SerializeField] Image claimedMark;
        [SerializeField] Image glow;
        [SerializeField] Image rays;
        [SerializeField] GameObject selectionFrame;

        [Header("Shader parameters")]
        [SerializeField] UIFxMeshEffect backgroundFx;
        [SerializeField] UIFxMeshEffect iconFx;
        [SerializeField] UIFxMeshEffect glowFx;
        [SerializeField] UIFxMeshEffect alertFx;

        [Header("Text")]
        [SerializeField] Color textColor = Color.white;

        public event Action<RewardCardView> Clicked;
        /// <summary>Raised when the unlock transition starts, so the screen can fire its particle burst.</summary>
        public event Action<RewardCardView> Unlocked;

        public RewardSlot Slot { get; private set; }
        public int Level => Slot.Level;
        public RewardTrack Track => Slot.Track;
        public RewardDefinition Reward { get; private set; }
        public RewardState State { get; private set; }
        public Color Accent { get; private set; }
        public RectTransform Body => body;

        Sprite rarityCard;
        Sprite collectableCard;
        bool premiumOwned;
        float pressScale = 1f;
        float punchScale = 1f;
        Coroutine transition;
        Coroutine raysRoutine;
        Coroutine pressRoutine;

        public void Bind(RewardSlot slot, RewardDefinition reward, RarityPalette palette, float phase)
        {
            Slot = slot;
            Reward = reward;

            var style = palette.Get(reward.rarity);
            Accent = style.accent;
            rarityCard = style.cardSprite;
            collectableCard = palette.CollectableCard;
            background.sprite = rarityCard;
            icon.sprite = reward.icon;
            title.text = reward.displayName;
            captionIcon.sprite = reward.captionIcon;

            var glowColor = Color.Lerp(style.accent, Color.white, 0.25f);
            glowColor.a = glow.color.a;
            glow.color = glowColor;

            backgroundFx.Phase = phase;
            iconFx.Phase = phase;
            glowFx.Phase = phase;
            alertFx.Phase = phase;
        }

        /// <summary>Snaps to the look of <paramref name="state"/> without animating.</summary>
        public void ApplyState(RewardState state, bool ownsPremium)
        {
            StopTransition();
            State = state;
            premiumOwned = ownsPremium;

            background.sprite = state == RewardState.Locked ? rarityCard : collectableCard;
            switch (state)
            {
                case RewardState.Locked: SetGrade(LockedSaturation, LockedBrightness); break;
                case RewardState.Claimed: SetGrade(ClaimedSaturation, ClaimedBrightness); break;
                default: SetGrade(1f, 1f); break;
            }

            SetFlash(0f);
            punchScale = 1f;
            ApplyScale();
            ResetBadge(lockBadge, NeedsLock(state));
            ResetBadge(alertBadge, ShowsAlert(state));
            ResetBadge(claimedMark, state == RewardState.Claimed);
            glow.gameObject.SetActive(state == RewardState.Claimable);
            SetAlpha(glow, GlowAlpha);
            rays.gameObject.SetActive(false);
            SetIdle(state == RewardState.Claimable ? 1f : 0f);
            RefreshTexts();
        }

        /// <summary>Animates from the current look to <paramref name="state"/> after <paramref name="delay"/> seconds.</summary>
        public void PlayTransition(RewardState state, bool ownsPremium, float delay)
        {
            IEnumerator routine = state switch
            {
                RewardState.Claimed => ClaimRoutine(delay),
                RewardState.Claimable => UnlockRoutine(delay),
                RewardState.PremiumLocked => ReachRoutine(delay),
                _ => null,
            };

            if (routine == null || !isActiveAndEnabled)
            {
                ApplyState(state, ownsPremium);
                return;
            }

            StopTransition();
            State = state;
            premiumOwned = ownsPremium;
            transition = StartCoroutine(routine);
        }

        /// <summary>Premium pass bought while this level is still out of reach: the padlock pops off.</summary>
        public void RevealPremium(float delay)
        {
            premiumOwned = true;
            if (State != RewardState.Locked || !lockBadge.gameObject.activeSelf)
                return;
            StopTransition();
            transition = StartCoroutine(LockPopRoutine(delay));
        }

        public void SetSelected(bool selected) => selectionFrame.SetActive(selected);

        public void OnPointerDown(PointerEventData eventData) => AnimatePress(0.94f);

        public void OnPointerUp(PointerEventData eventData) => AnimatePress(1f);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging)
                Clicked?.Invoke(this);
        }

        IEnumerator UnlockRoutine(float delay)
        {
            yield return Tween.Wait(delay);
            Unlocked?.Invoke(this);

            float fromSaturation = backgroundFx.Saturation;
            float fromBrightness = backgroundFx.Brightness;
            bool hadLock = lockBadge.gameObject.activeSelf;
            // The card turns collectable under the opening flash, so the swap never shows.
            background.sprite = collectableCard;
            raysRoutine = StartCoroutine(RaysRoutine());

            // Flash, pop and bring the colour back while the padlock breaks away.
            yield return Tween.Run(0.5f, t =>
            {
                float eased = Ease.OutCubic(t);
                SetGrade(Mathf.Lerp(fromSaturation, 1f, eased), Mathf.Lerp(fromBrightness, 1f, eased));
                SetFlash(0.75f * (1f - eased));
                punchScale = 1f + 0.1f * Ease.Bump(t);
                ApplyScale();
                if (hadLock)
                    AnimateLockAway(t);
            });

            ResetBadge(lockBadge, false);
            RefreshTexts();
            glow.gameObject.SetActive(true);
            alertBadge.gameObject.SetActive(true);

            // Settle into the claimable idle loop.
            yield return Tween.Run(0.45f, t =>
            {
                alertBadge.rectTransform.localScale = Vector3.one * Ease.OutBack(t);
                SetAlpha(glow, GlowAlpha * t);
                SetIdle(t);
            });
            transition = null;
        }

        IEnumerator ClaimRoutine(float delay)
        {
            yield return Tween.Wait(delay);

            float fromIdle = backgroundFx.Idle;
            float fromGlow = glow.color.a;

            // Wind-up: swell and flash while the idle effects and badge get out of the way.
            yield return Tween.Run(0.14f, t =>
            {
                punchScale = 1f + 0.14f * Ease.OutQuad(t);
                ApplyScale();
                SetFlash(0.85f * t);
                alertBadge.rectTransform.localScale = Vector3.one * (1f - t);
                SetAlpha(glow, fromGlow * (1f - t));
                SetIdle(fromIdle * (1f - t));
            });

            alertBadge.gameObject.SetActive(false);
            glow.gameObject.SetActive(false);
            claimedMark.gameObject.SetActive(true);
            RefreshTexts();

            // Release: spring back, fade to the claimed look and stamp the check mark.
            yield return Tween.Run(0.4f, t =>
            {
                punchScale = Mathf.LerpUnclamped(1.14f, 1f, Ease.OutBack(t));
                ApplyScale();
                SetFlash(0.85f * (1f - t));
                SetGrade(Mathf.Lerp(1f, ClaimedSaturation, t), Mathf.Lerp(1f, ClaimedBrightness, t));
                claimedMark.rectTransform.localScale = Vector3.one * Ease.OutBack(t);
            });
            transition = null;
        }

        // Level reached but the reward needs the premium pass: the card turns collectable under a flash,
        // the padlock nudges to explain why, and the badge pops in to say there is something to get.
        IEnumerator ReachRoutine(float delay)
        {
            yield return Tween.Wait(delay);

            float fromSaturation = backgroundFx.Saturation;
            float fromBrightness = backgroundFx.Brightness;
            background.sprite = collectableCard;
            ResetBadge(alertBadge, true);
            yield return Tween.Run(0.45f, t =>
            {
                float eased = Ease.OutCubic(t);
                SetGrade(Mathf.Lerp(fromSaturation, 1f, eased), Mathf.Lerp(fromBrightness, 1f, eased));
                SetFlash(0.6f * (1f - eased));
                lockBadge.rectTransform.localScale = Vector3.one * (1f + 0.35f * Ease.Bump(t));
                alertBadge.rectTransform.localScale = Vector3.one * Ease.OutBack(t);
            });
            RefreshTexts();
            transition = null;
        }

        IEnumerator LockPopRoutine(float delay)
        {
            yield return Tween.Wait(delay);
            yield return Tween.Run(0.35f, AnimateLockAway);
            ResetBadge(lockBadge, false);
            transition = null;
        }

        IEnumerator RaysRoutine()
        {
            rays.gameObject.SetActive(true);
            var rect = rays.rectTransform;
            yield return Tween.Run(0.9f, t =>
            {
                rect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.35f, Ease.OutCubic(t));
                rect.localEulerAngles = new Vector3(0f, 0f, -45f * t);
                SetAlpha(rays, RaysAlpha * Ease.Bump(t));
            });
            rays.gameObject.SetActive(false);
            raysRoutine = null;
        }

        void AnimateLockAway(float t)
        {
            var rect = lockBadge.rectTransform;
            rect.localScale = Vector3.one * (1f + 0.5f * t);
            rect.localEulerAngles = new Vector3(0f, 0f, -25f * t);
            SetAlpha(lockBadge, 1f - t);
        }

        void RefreshTexts()
        {
            bool claimed = State == RewardState.Claimed;
            title.color = claimed ? DimTextColor : textColor;
            caption.text = claimed ? "CLAIMED" : Reward.CaptionText;
            caption.color = claimed ? DimTextColor : textColor;
            LayoutCaption(!claimed && Reward.ShowsCaptionIcon);
        }

        // Centres the icon and the number together as one group under the reward.
        void LayoutCaption(bool showIcon)
        {
            captionIcon.gameObject.SetActive(showIcon);
            var captionRect = caption.rectTransform;
            if (!showIcon)
            {
                captionRect.anchoredPosition = new Vector2(0f, captionRect.anchoredPosition.y);
                return;
            }

            var iconRect = captionIcon.rectTransform;
            float iconSize = iconRect.rect.height;
            iconRect.sizeDelta = new Vector2(iconSize, iconRect.sizeDelta.y);
            float textWidth = caption.GetPreferredValues(caption.text).x;
            captionRect.anchoredPosition = new Vector2((iconSize + CaptionIconGap) * 0.5f, captionRect.anchoredPosition.y);
            iconRect.anchoredPosition = new Vector2(-(textWidth + CaptionIconGap) * 0.5f, iconRect.anchoredPosition.y);
        }

        bool NeedsLock(RewardState state) =>
            Track == RewardTrack.Premium && !premiumOwned && state != RewardState.Claimed;

        // Reached rewards carry the badge whether or not they can be claimed yet, like the reference;
        // only claimable ones bob, glow and shine.
        static bool ShowsAlert(RewardState state) =>
            state == RewardState.Claimable || state == RewardState.PremiumLocked;

        void StopTransition()
        {
            if (transition != null)
                StopCoroutine(transition);
            if (raysRoutine != null)
                StopCoroutine(raysRoutine);
            transition = null;
            raysRoutine = null;
        }

        void AnimatePress(float target)
        {
            if (!isActiveAndEnabled)
                return;
            if (pressRoutine != null)
                StopCoroutine(pressRoutine);
            float from = pressScale;
            pressRoutine = StartCoroutine(Tween.Run(0.08f, t =>
            {
                pressScale = Mathf.Lerp(from, target, t);
                ApplyScale();
            }));
        }

        void ApplyScale() => body.localScale = Vector3.one * (pressScale * punchScale);

        void SetGrade(float saturation, float brightness)
        {
            backgroundFx.Saturation = saturation;
            backgroundFx.Brightness = brightness;
            iconFx.Saturation = saturation;
            iconFx.Brightness = brightness;
        }

        void SetFlash(float amount)
        {
            backgroundFx.Flash = amount;
            iconFx.Flash = amount;
        }

        void SetIdle(float amount)
        {
            backgroundFx.Idle = amount;
            iconFx.Idle = amount;
            glowFx.Idle = amount;
            alertFx.Idle = amount;
        }

        static void ResetBadge(Image badge, bool visible)
        {
            badge.gameObject.SetActive(visible);
            var rect = badge.rectTransform;
            rect.localScale = Vector3.one;
            rect.localEulerAngles = Vector3.zero;
            SetAlpha(badge, 1f);
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
