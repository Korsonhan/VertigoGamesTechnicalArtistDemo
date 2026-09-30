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
        [SerializeField] RectTransform columnContainer;
        [SerializeField] RoadLevelView columnPrefab;
        [SerializeField] ProgressTrackView track;
        [SerializeField] Button skipButton;
        [SerializeField] TMP_Text skipCostLabel;
        [SerializeField] ProgressJumpButton jumpButton;
        [SerializeField] PointerClickRelay emptyRoadClicks;
        [SerializeField] float firstColumnX = 190f;
        [SerializeField] float columnSpacing = 380f;
        [SerializeField] float endPadding = 190f;
        [Tooltip("Where the progress head settles in the viewport after scrolling, 0 = left edge.")]
        [SerializeField, Range(0f, 1f)] float focusPoint = 0.42f;
        [Tooltip("How long the road rests at its start, on the pass rewards, before gliding to the player's progress.")]
        [SerializeField] float openingHold = 1.2f;

        [Header("Panels")]
        [SerializeField] TopBarView topBar;
        [SerializeField] SeasonPanelView seasonPanel;
        [SerializeField] RewardTooltip tooltip;
        [SerializeField] RewardFxPool fx;
        [SerializeField] CurrencyFlyFx currencyFly;

        [Header("Placeholder player state")]
        [SerializeField, Min(0)] int startLevel = 3;
        [SerializeField, Min(0)] int startXp = 80;
        [SerializeField] int[] claimedFreeLevels = { 1 };
        [SerializeField, Min(0)] int startCoins = 3403;
        [SerializeField, Min(0)] int startGems = 180;

        [Header("Timing")]
        [SerializeField] float levelUpDuration = 0.6f;
        [SerializeField] float premiumWaveStagger = 0.12f;

        readonly List<RoadLevelView> columns = new List<RoadLevelView>();
        readonly Dictionary<RewardSlot, RewardCardView> cards = new Dictionary<RewardSlot, RewardCardView>();
        BattlePassProgress progress;
        RewardCardView selected;
        Coroutine scrollRoutine;
        int startColumns;
        float sequenceDelay;
        float sequenceStagger;
        int sequenceIndex;
        bool built;

        void Awake() => Build();

        IEnumerator Start()
        {
            SetScroll(0f);
            yield return Tween.Wait(openingHold);
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

        // Handles for the scripted walkthrough (BattlePassAutoplay) and tests.
        public RewardCardView GetCard(int level, RewardTrack rewardTrack, int index = 0) => cards[new RewardSlot(level, rewardTrack, index)];
        public Button SkipLevelButton => skipButton;
        public Button PremiumButton => seasonPanel.PremiumButton;
        public Button JumpButton => jumpButton.Button;

        public void ScrollToLevel(int level) => ScrollTo(LevelX(level), animate: true);

        /// <summary>Scrolls all the way back, to the rewards that come with the pass.</summary>
        public void ScrollToStart() => ScrollTo(0f, animate: true);

        public void ScrollToProgress() => ScrollToProgress(animate: true);

        void Build()
        {
            if (built)
                return;
            built = true;

            progress = new BattlePassProgress(season.LevelCount, season.xpPerLevel, startLevel, startXp,
                startPremium: season.start.premium.Count, startFree: season.start.free.Count);
            foreach (int level in claimedFreeLevels)
            {
                if (level >= 1 && level <= progress.Level)
                    progress.MarkClaimed(level, RewardTrack.Free);
            }
            progress.RewardStateChanged += OnRewardStateChanged;

            // The start of the road lines up what the pass itself grants before level 1. Both rows are
            // right-aligned, so the last premium and free rewards share the column carrying the pass ticket.
            int startPremium = season.start.premium.Count;
            int startFree = season.start.free.Count;
            startColumns = Mathf.Max(startPremium, startFree);
            for (int column = 0; column < startColumns; column++)
            {
                var view = AddColumn(0, $"Start_{column}");
                AddCard(view, RewardTrack.Premium, column - (startColumns - startPremium));
                AddCard(view, RewardTrack.Free, column - (startColumns - startFree));
                if (column == startColumns - 1)
                    view.Node.ShowTicket();
                else
                    view.Node.gameObject.SetActive(false);
            }

            for (int level = 1; level <= season.LevelCount; level++)
            {
                var view = AddColumn(level, $"Level_{level:00}");
                view.Node.Setup(level);
                AddCard(view, RewardTrack.Premium, 0);
                AddCard(view, RewardTrack.Free, 0);
            }

            var content = road.content;
            content.sizeDelta = new Vector2(ColumnX(columns.Count - 1) + endPadding, content.sizeDelta.y);

            topBar.SetTitles(season.passTitle, season.timeLeft);
            topBar.Coins.Set(startCoins);
            topBar.Gems.Set(startGems);
            seasonPanel.SetSeason(season.seasonTitle);
            seasonPanel.PremiumPressed += OnGetPremium;
            skipCostLabel.text = season.skipLevelCost.ToString();
            skipButton.onClick.AddListener(OnSkipLevel);
            jumpButton.Pressed += ScrollToProgress;
            emptyRoadClicks.Clicked += () => Select(null);
            tooltip.Hide();

            RefreshAll();
        }

        RoadLevelView AddColumn(int level, string name)
        {
            int column = columns.Count;
            var view = Instantiate(columnPrefab, columnContainer);
            view.name = name;
            var rect = (RectTransform)view.transform;
            rect.anchoredPosition = new Vector2(ColumnX(column), rect.anchoredPosition.y);
            view.Setup(level, column);
            columns.Add(view);
            return view;
        }

        // A negative index leaves the slot empty (a start column without a reward on that track).
        void AddCard(RoadLevelView view, RewardTrack rewardTrack, int index)
        {
            var slot = new RewardSlot(view.Level, rewardTrack, index);
            var card = view.BindCard(slot, index >= 0 ? season.GetReward(slot) : null, palette);
            if (card == null)
                return;
            card.Clicked += OnCardClicked;
            card.Unlocked += OnCardUnlocked;
            cards.Add(slot, card);
        }

        void RefreshAll()
        {
            foreach (var pair in cards)
                pair.Value.ApplyState(progress.GetState(pair.Key), progress.PremiumOwned);
            RefreshNodes();

            track.SetPosition(ProgressX());
            track.SetMarkerVisible(!progress.IsMaxLevel);
            topBar.SetXp(progress.Xp, progress.XpPerLevel, progress.Level + 1, progress.IsMaxLevel);
            topBar.SetPremium(progress.PremiumOwned);
            seasonPanel.SetPremium(progress.PremiumOwned);
            RefreshJumpTarget();
        }

        void RefreshNodes()
        {
            foreach (var column in columns)
            {
                if (column.Level == 0)
                    continue;
                column.Node.SetReached(column.Level <= progress.Level);
                column.Node.SetNext(column.Level == progress.Level + 1);
            }
        }

        void RefreshJumpTarget() =>
            jumpButton.SetTarget(ProgressX(), Mathf.Min(progress.Level + 1, progress.LevelCount));

        void OnCardClicked(RewardCardView card)
        {
            if (card.State == RewardState.Claimable)
            {
                Select(null);
                if (progress.TryClaim(card.Slot))
                {
                    fx.PlayClaim(card.Body.position, Color.Lerp(card.Accent, Color.white, 0.35f));
                    Grant(card);
                }
                return;
            }
            Select(card == selected ? null : card);
        }

        void OnCardUnlocked(RewardCardView card) =>
            fx.PlayUnlock(card.Body.position, Color.Lerp(card.Accent, Color.white, 0.5f));

        void OnRewardStateChanged(RewardSlot slot, RewardState from, RewardState to)
        {
            float delay = sequenceDelay + sequenceStagger * sequenceIndex++;
            cards[slot].PlayTransition(to, progress.PremiumOwned, delay);
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
            RefreshJumpTarget();

            yield return track.AnimateTo(ProgressX(), levelUpDuration);

            for (int level = previousLevel + 1; level <= progress.Level; level++)
                StartCoroutine(LevelColumn(level).Node.PlayReached());
            RefreshNodes();

            track.SetMarkerVisible(!progress.IsMaxLevel);
            skipButton.interactable = true;
        }

        void OnGetPremium()
        {
            if (progress.PremiumOwned)
                return;

            Select(null);
            // Reached premium rewards unlock in a wave from left to right, starting with the pass's own.
            BeginSequence(0.15f, premiumWaveStagger);
            progress.UnlockPremium();
            int waveLength = sequenceIndex;
            EndSequence();

            seasonPanel.SetPremium(true);
            topBar.SetPremium(true);

            // Levels still out of reach just lose their padlock, carrying on from the wave.
            float delay = 0.15f + premiumWaveStagger * waveLength;
            for (int level = progress.Level + 1; level <= progress.LevelCount; level++)
            {
                cards[new RewardSlot(level, RewardTrack.Premium)].RevealPremium(delay);
                delay += premiumWaveStagger * 0.5f;
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
            RewardState.PremiumLocked when card.Level == 0 => "Comes with the Premium Pass",
            RewardState.PremiumLocked => "Get the Premium Pass to claim",
            RewardState.Claimed => "Already claimed",
            _ => "Tap to claim",
        };

        // Currency rewards fly into the wallet; the readout counts up as the first icon lands.
        void Grant(RewardCardView card)
        {
            var reward = card.Reward;
            var counter = reward.kind switch
            {
                RewardKind.Coins => topBar.Coins,
                RewardKind.Gems => topBar.Gems,
                _ => null,
            };
            if (counter == null)
                return;

            var sprite = counter.Icon.GetComponent<Image>().sprite;
            int icons = reward.kind == RewardKind.Coins ? 8 : 6;
            float arrival = currencyFly.Play(sprite, card.Body.position, counter.Icon, icons);
            counter.Add(reward.amount, arrival);
        }

        void BeginSequence(float delay, float stagger)
        {
            sequenceDelay = delay;
            sequenceStagger = stagger;
            sequenceIndex = 0;
        }

        void EndSequence() => BeginSequence(0f, 0f);

        void ScrollToProgress(bool animate) => ScrollTo(ProgressX(), animate);

        // Scrolls so that content position x sits at the focus point of the viewport.
        void ScrollTo(float x, bool animate)
        {
            float viewportWidth = road.viewport.rect.width;
            float maxOffset = Mathf.Max(0f, road.content.rect.width - viewportWidth);
            float target = Mathf.Clamp(x - viewportWidth * focusPoint, 0f, maxOffset);

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

        float ColumnX(int column) => firstColumnX + column * columnSpacing;

        // Level 0 is the pass ticket's column; without start columns it sits one step before level 1.
        int LevelColumnIndex(int level) => startColumns - 1 + level;

        RoadLevelView LevelColumn(int level) => columns[LevelColumnIndex(level)];

        float LevelX(int level) => ColumnX(LevelColumnIndex(level));

        float ProgressX() => Mathf.Max(0f, LevelX(progress.Level) + progress.LevelFraction * columnSpacing);
    }
}
