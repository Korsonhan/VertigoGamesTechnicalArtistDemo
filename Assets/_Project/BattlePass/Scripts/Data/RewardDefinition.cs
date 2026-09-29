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
        [Tooltip("Shown at the bottom of the card instead of the amount, e.g. UNLOCK NOW for a new character.")]
        public string caption = "";
        [Tooltip("Small icon in front of the amount at the bottom of the card, e.g. a coin or a card.")]
        public Sprite captionIcon;

        /// <summary>The amount as the details bubble words it: "5,000" for currency, "x2" for items.</summary>
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

        /// <summary>
        /// Bottom line of the card: the caption if there is one, otherwise the amount. Next to its icon
        /// the amount is a bare number, like the reference ("2" beside a card, "5,000" beside a coin).
        /// </summary>
        public string CaptionText
        {
            get
            {
                if (!string.IsNullOrEmpty(caption))
                    return caption;
                if (ShowsCaptionIcon)
                    return amount.ToString("N0", CultureInfo.InvariantCulture);
                return AmountLabel;
            }
        }

        public bool ShowsCaptionIcon => captionIcon != null && string.IsNullOrEmpty(caption);
    }
}
