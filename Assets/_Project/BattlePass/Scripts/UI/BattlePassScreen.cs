using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// Builds the road from the season data and orchestrates the interactions: claiming, selecting,
    /// buying a level and buying the premium pass. Player state is placeholder data for the demo.
    /// </summary>
    public sealed class BattlePassScreen : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] BattlePassSeason season;
        [SerializeField] RarityPalette palette;

        [Header("Road")]
        [SerializeField] ScrollRect road;
        [SerializeField] RectTransform levelContainer;
        [SerializeField] RoadLevelView levelPrefab;
        [SerializeField] ProgressTrackView track;
        [SerializeField] Button skipButton;
        [SerializeField] TMP_Text skipCostLabel;
        [SerializeField] PointerClickRelay emptyRoadClicks;
        [SerializeField] float firstLevelX = 190f;
        [SerializeField] float levelSpacing = 380f;
        [SerializeField] float endPadding = 190f;
        [Tooltip("Where the progress head settles in the viewport after scrolling, 0 = left edge.")]
        [SerializeField, Range(0f, 1f)] float focusPoint = 0.42f;

        [Header("Panels")]
        [SerializeField] TopBarView topBar;
        [SerializeField] SeasonPanelView seasonPanel;
        [SerializeField] RewardTooltip tooltip;
        [SerializeField] RewardFxPool fx;

        [Header("Placeholder player state")]
        [SerializeField, Min(0)] int startLevel = 3;
        [SerializeField, Min(0)] int startXp = 80;
        [SerializeField] int[] claimedFreeLevels = { 1 };
        [SerializeField, Min(0)] int startCoins = 3403;
        [SerializeField, Min(0)] int startGems = 180;

        [Header("Timing")]
        [SerializeField] float levelUpDuration = 0.6f;
        [SerializeField] float premiumWaveStagger = 0.12f;

        readonly List<RoadLevelView> levels = new List<RoadLevelView>();
        BattlePassProgress progress;
        RewardCardView selected;
        Coroutine scrollRoutine;
        float sequenceDelay;
        float sequenceStagger;
        int sequenceIndex;
        bool built;

        void Awake() => Build();

        IEnumerator Start()
        {
            // Open at the start of the road, then glide to the player's progress.
            SetScroll(0f);
            yield return Tween.Wait(0.35f);
            ScrollToProgress(animate: true);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        /// <summary>Builds and snaps the road to the player's progress without animating (editor previews).</summary>
        public void BuildForPreview()
        {
            Build();
            ScrollToProgress(animate: false);
        }

        void Build()
        {
            if (built)
                return;
            built = true;

            progress = new BattlePassProgress(season.LevelCount, season.xpPerLevel, startLevel, startXp);
            foreach (int level in claimedFreeLevels)
            {
                if (level >= 1 && level <= progress.Level)
                    progress.MarkClaimed(level, RewardTrack.Free);
            }
            progress.RewardStateChanged += OnRewardStateChanged;

            var content = road.content;
            content.sizeDelta = new Vector2(LevelX(season.LevelCount) + endPadding, content.sizeDelta.y);

            for (int level = 1; level <= season.LevelCount; level++)
            {
                var view = Instantiate(levelPrefab, levelContainer);
                view.name = $"Level_{level:00}";
                var rect = (RectTransform)view.transform;
                rect.anchoredPosition = new Vector2(LevelX(level), rect.anchoredPosition.y);
                view.Bind(level, season.levels[level - 1], palette);
                view.Premium.Clicked += OnCardClicked;
                view.Free.Clicked += OnCardClicked;
                view.Premium.Unlocked += OnCardUnlocked;
                view.Free.Unlocked += OnCardUnlocked;
                levels.Add(view);
            }

            topBar.SetTitles(season.passTitle, season.timeLeft);
            topBar.Coins.Set(startCoins);
            topBar.Gems.Set(startGems);
            seasonPanel.SetSeason(season.seasonTitle);
            seasonPanel.GetPressed += OnGetPremium;
            skipCostLabel.text = season.skipLevelCost.ToString();
            skipButton.onClick.AddListener(OnSkipLevel);
            emptyRoadClicks.Clicked += () => Select(null);
            tooltip.Hide();

            RefreshAll();
        }

        void RefreshAll()
        {
            foreach (var level in levels)
            {
                level.Premium.ApplyState(progress.GetState(level.Level, RewardTrack.Premium), progress.PremiumOwned);
                level.Free.ApplyState(progress.GetState(level.Level, RewardTrack.Free), progress.PremiumOwned);
                level.Node.SetReached(level.Level <= progress.Level);
                level.Node.SetNext(level.Level == progress.Level + 1);
            }

            track.SetPosition(ProgressX());
            track.SetMarkerVisible(!progress.IsMaxLevel);
            topBar.SetXp(progress.Xp, progress.XpPerLevel, progress.Level + 1, progress.IsMaxLevel);
            topBar.SetPremium(progress.PremiumOwned);
            seasonPanel.SetPremium(progress.PremiumOwned);
        }

        void OnCardClicked(RewardCardView card)
        {
            if (card.State == RewardState.Claimable)
            {
                Select(null);
                if (progress.TryClaim(card.Level, card.Track))
                {
                    fx.PlayClaim(card.Body.position, Color.Lerp(card.Accent, Color.white, 0.35f));
                    Grant(card.Reward);
                }
                return;
            }
            Select(card == selected ? null : card);
        }

        void OnCardUnlocked(RewardCardView card) =>
            fx.PlayUnlock(card.Body.position, Color.Lerp(card.Accent, Color.white, 0.5f));

        void OnRewardStateChanged(int level, RewardTrack rewardTrack, RewardState from, RewardState to)
        {
            float delay = sequenceDelay + sequenceStagger * sequenceIndex++;
            levels[level - 1].Card(rewardTrack).PlayTransition(to, progress.PremiumOwned, delay);
        }

        void OnSkipLevel()
        {
            if (progress.IsMaxLevel || !topBar.Gems.TrySpend(season.skipLevelCost))
                return;

            Select(null);
            int previousLevel = progress.Level;
            BeginSequence(levelUpDuration, 0.1f);
            progress.BuyLevel();
            EndSequence();
            StartCoroutine(LevelUpRoutine(previousLevel));
        }

        IEnumerator LevelUpRoutine(int previousLevel)
        {
            skipButton.interactable = false;
            ScrollToProgress(animate: true);
            topBar.PlayLevelUp(progress.Xp, progress.XpPerLevel, progress.Level + 1, progress.IsMaxLevel);

            yield return track.AnimateTo(ProgressX(), levelUpDuration);

            for (int level = previousLevel + 1; level <= progress.Level; level++)
                StartCoroutine(levels[level - 1].Node.PlayReached());
            foreach (var level in levels)
                level.Node.SetNext(level.Level == progress.Level + 1);

            track.SetMarkerVisible(!progress.IsMaxLevel);
            skipButton.interactable = true;
        }

        void OnGetPremium()
        {
            if (progress.PremiumOwned)
                return;

            Select(null);
            // Reached premium rewards unlock in a wave from left to right.
            BeginSequence(0.15f, premiumWaveStagger);
            progress.UnlockPremium();
            EndSequence();

            seasonPanel.SetPremium(true);
            topBar.SetPremium(true);

            // Levels still out of reach just lose their padlock.
            float delay = 0.15f + premiumWaveStagger * progress.Level;
            foreach (var level in levels)
            {
                if (level.Level > progress.Level)
                {
                    level.Premium.RevealPremium(delay);
                    delay += premiumWaveStagger * 0.5f;
                }
            }
        }

        void Select(RewardCardView card)
        {
            if (selected != null)
                selected.SetSelected(false);

            selected = card;
            if (card == null)
            {
                tooltip.Hide();
                return;
            }

            card.SetSelected(true);
            tooltip.Show(card, Describe(card));
        }

        string Describe(RewardCardView card) => card.State switch
        {
            RewardState.Locked when card.Track == RewardTrack.Premium && !progress.PremiumOwned =>
                $"Reach level {card.Level} with the Premium Pass",
            RewardState.Locked => $"Reach level {card.Level} to unlock",
            RewardState.PremiumLocked => "Get the Premium Pass to claim",
            RewardState.Claimed => "Already claimed",
            _ => "Tap to claim",
        };

        void Grant(RewardDefinition reward)
        {
            if (reward.kind == RewardKind.Coins)
                topBar.Coins.Add(reward.amount);
            else if (reward.kind == RewardKind.Gems)
                topBar.Gems.Add(reward.amount);
        }

        void BeginSequence(float delay, float stagger)
        {
            sequenceDelay = delay;
            sequenceStagger = stagger;
            sequenceIndex = 0;
        }

        void EndSequence() => BeginSequence(0f, 0f);

        void ScrollToProgress(bool animate)
        {
            float viewportWidth = road.viewport.rect.width;
            float maxOffset = Mathf.Max(0f, road.content.rect.width - viewportWidth);
            float target = Mathf.Clamp(ProgressX() - viewportWidth * focusPoint, 0f, maxOffset);

            if (scrollRoutine != null)
                StopCoroutine(scrollRoutine);
            road.StopMovement();

            if (!animate)
            {
                SetScroll(target);
                return;
            }

            float from = -road.content.anchoredPosition.x;
            scrollRoutine = StartCoroutine(Tween.Run(0.8f, t => SetScroll(Mathf.Lerp(from, target, t)), Ease.InOutCubic));
        }

        void SetScroll(float offset)
        {
            var position = road.content.anchoredPosition;
            road.content.anchoredPosition = new Vector2(-offset, position.y);
        }

        float LevelX(int level) => firstLevelX + (level - 1) * levelSpacing;

        float ProgressX() => Mathf.Max(0f, LevelX(progress.Level) + progress.LevelFraction * levelSpacing);
    }
}
