using UnityEngine;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>One column of the road: premium reward, level node and free reward.</summary>
    public sealed class RoadLevelView : MonoBehaviour
    {
        const float GoldenRatio = 0.618034f;

        [SerializeField] RewardCardView premiumCard;
        [SerializeField] RewardCardView freeCard;
        [SerializeField] LevelNodeView node;

        public int Level { get; private set; }
        public RewardCardView Premium => premiumCard;
        public RewardCardView Free => freeCard;
        public LevelNodeView Node => node;

        public RewardCardView Card(RewardTrack track) => track == RewardTrack.Free ? freeCard : premiumCard;

        public void Bind(int level, BattlePassSeason.Level rewards, RarityPalette palette)
        {
            Level = level;
            node.Setup(level);

            // Golden-ratio phases spread the idle loops so neighbouring cards never shine in sync.
            float phase = Mathf.Repeat(level * GoldenRatio, 1f);
            premiumCard.Bind(level, RewardTrack.Premium, rewards.premium, palette, phase);
            freeCard.Bind(level, RewardTrack.Free, rewards.free, palette, Mathf.Repeat(phase + 0.37f, 1f));
        }
    }
}
