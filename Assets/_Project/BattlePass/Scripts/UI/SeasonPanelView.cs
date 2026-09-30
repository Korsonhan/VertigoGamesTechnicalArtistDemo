using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>The fixed season card on the left, with the premium pass offer.</summary>
    public sealed class SeasonPanelView : MonoBehaviour
    {
        [SerializeField] TMP_Text seasonLabel;
        [SerializeField] Button premiumButton;
        [SerializeField] Image premiumButtonImage;
        [SerializeField] TMP_Text premiumButtonLabel;
        [SerializeField] Sprite ownedButtonSprite;
        [SerializeField] GameObject offerGroup;

        public event Action PremiumPressed;

        public Button PremiumButton => premiumButton;

        Sprite offerButtonSprite;

        void Awake() => premiumButton.onClick.AddListener(() => PremiumPressed?.Invoke());

        public void SetSeason(string title) => seasonLabel.text = title;

        public void SetPremium(bool owned)
        {
            if (offerButtonSprite == null)
                offerButtonSprite = premiumButtonImage.sprite;

            premiumButton.interactable = !owned;
            premiumButtonImage.sprite = owned ? ownedButtonSprite : offerButtonSprite;
            premiumButtonLabel.text = owned ? "ACTIVE" : "GET";
            offerGroup.SetActive(!owned);
        }
    }
}
