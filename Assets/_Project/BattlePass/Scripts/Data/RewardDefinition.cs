using System;
using System.Globalization;
using UnityEngine;

namespace VertigoDemo.BattlePass
{
    public enum RewardRarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

    /// <summary>What claiming the reward adds to the wallet; everything else is a plain item.</summary>
    public enum RewardKind { Item, Coins, Gems }

    [Serializable]
    public sealed class RewardDefinition
    {
        public string displayName = "REWARD";
        public Sprite icon;
        public RewardRarity rarity;
        public RewardKind kind;
        [Min(1)] public int amount = 1;

        public string AmountLabel
        {
            get
            {
                if (amount <= 1)
                    return string.Empty;
                string value = amount.ToString("N0", CultureInfo.InvariantCulture);
                return kind == RewardKind.Item ? "x" + value : value;
            }
        }
    }
}
