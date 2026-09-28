using System;
using System.Collections.Generic;
using UnityEngine;

namespace VertigoDemo.BattlePass
{
    /// <summary>Static content of one season: titles, pricing and the reward pair of every level.</summary>
    [CreateAssetMenu(menuName = "Vertigo Demo/Battle Pass Season", fileName = "BattlePassSeason")]
    public sealed class BattlePassSeason : ScriptableObject
    {
        [Serializable]
        public sealed class Level
        {
            public RewardDefinition free = new RewardDefinition();
            public RewardDefinition premium = new RewardDefinition();
        }

        public string passTitle = "GOLDEN REALM";
        public string seasonTitle = "SEASON 16";
        public string timeLeft = "17D 20H";
        [Min(1)] public int xpPerLevel = 200;
        [Min(0)] public int skipLevelCost = 20;
        public List<Level> levels = new List<Level>();

        public int LevelCount => levels.Count;

        public RewardDefinition GetReward(int level, RewardTrack track) =>
            track == RewardTrack.Free ? levels[level - 1].free : levels[level - 1].premium;
    }
}
