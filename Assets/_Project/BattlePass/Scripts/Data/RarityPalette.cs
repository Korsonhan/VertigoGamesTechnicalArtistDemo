using System;
using UnityEngine;

namespace VertigoDemo.BattlePass
{
    /// <summary>Card background and accent colour per rarity, shared by the cards, glows and particles.</summary>
    [CreateAssetMenu(menuName = "Vertigo Demo/Rarity Palette", fileName = "RarityPalette")]
    public sealed class RarityPalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public RewardRarity rarity;
            public Sprite cardSprite;
            public Color accent;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        public Entry Get(RewardRarity rarity)
        {
            foreach (var entry in entries)
            {
                if (entry.rarity == rarity)
                    return entry;
            }
            return entries.Length > 0 ? entries[0] : default;
        }
    }
}
