using UnityEngine;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// One column of the road: premium reward, track marker and free reward. The columns at the start
    /// of the road, before level 1, hold what the pass itself grants and may leave a slot or the marker empty.
    /// </summary>
    public sealed class RoadLevelView : MonoBehaviour
    {
        const float GoldenRatio = 0.618034f;

        [SerializeField] RewardCardView premiumCard;
        [SerializeField] RewardCardView freeCard;
        [SerializeField] LevelNodeView node;

        public int Level { get; private set; }
        public int Column { get; private set; }
        public LevelNodeView Node => node;

        public void Setup(int level, int column)
        {
            Level = level;
            Column = column;
        }

        /// <summary>Shows the card for <paramref name="slot"/> with <paramref name="reward"/>, or hides it when there is none.</summary>
        public RewardCardView BindCard(RewardSlot slot, RewardDefinition reward, RarityPalette palette)
        {
            var card = slot.Track == RewardTrack.Free ? freeCard : premiumCard;
            card.gameObject.SetActive(reward != null);
            if (reward == null)
                return null;

            // Golden-ratio phases spread the idle loops so neighbouring cards never shine in sync.
            float phase = Mathf.Repeat(Column * GoldenRatio + (slot.Track == RewardTrack.Free ? 0.37f : 0f), 1f);
            card.Bind(slot, reward, palette, phase);
            return card;
        }
    }
}
