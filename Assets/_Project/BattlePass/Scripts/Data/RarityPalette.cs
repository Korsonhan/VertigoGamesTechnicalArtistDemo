using System;
using UnityEngine;

namespace VertigoDemo.BattlePass
{
    /// <summary>
    /// Card backgrounds and accent colours, shared by the cards, glows and particles. Like the reference,
    /// a reward shows its rarity's card until its level is reached, then the shared collectable card.
    /// </summary>
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

        [Tooltip("Card behind every reward whose level has been reached.")]
        [SerializeField] Sprite collectableCard;
        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        public Sprite CollectableCard => collectableCard;

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
