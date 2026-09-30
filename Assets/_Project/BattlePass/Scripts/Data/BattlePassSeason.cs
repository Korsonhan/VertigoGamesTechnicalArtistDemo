using System;
using System.Collections.Generic;
using UnityEngine;

namespace VertigoDemo.BattlePass
{
    /// <summary>
    /// Static content of one season: titles, pricing, the rewards at the start of the road and the
    /// reward pair of every level.
    /// </summary>
    [CreateAssetMenu(menuName = "Vertigo Demo/Battle Pass Season", fileName = "BattlePassSeason")]
    public sealed class BattlePassSeason : ScriptableObject
    {
        [Serializable]
        public sealed class Level
        {
            public RewardDefinition free = new RewardDefinition();
            public RewardDefinition premium = new RewardDefinition();
        }

        /// <summary>Level 0, before the first level: what the pass itself grants, and free rewards to start with.</summary>
        [Serializable]
        public sealed class PassStart
        {
            [Tooltip("Premium rewards that come with the pass itself. The last one sits above the pass ticket on the track.")]
            public List<RewardDefinition> premium = new List<RewardDefinition>();
            [Tooltip("Free rewards ready from the start of the season. The last one sits under the pass ticket.")]
            public List<RewardDefinition> free = new List<RewardDefinition>();
        }

        public string passTitle = "GOLDEN REALM";
        public string seasonTitle = "SEASON 16";
        public string timeLeft = "17D 20H";
        [Min(1)] public int xpPerLevel = 200;
        [Min(0)] public int skipLevelCost = 20;
        public PassStart start = new PassStart();
        public List<Level> levels = new List<Level>();

        public int LevelCount => levels.Count;

        public RewardDefinition GetReward(RewardSlot slot)
        {
            if (slot.Level == 0)
                return slot.Track == RewardTrack.Free ? start.free[slot.Index] : start.premium[slot.Index];
            var level = levels[slot.Level - 1];
            return slot.Track == RewardTrack.Free ? level.free : level.premium;
        }
    }
}
