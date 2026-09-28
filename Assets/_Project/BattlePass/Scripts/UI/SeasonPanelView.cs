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
        [SerializeField] Button getButton;
        [SerializeField] Image getButtonImage;
        [SerializeField] TMP_Text getLabel;
        [SerializeField] Sprite ownedButtonSprite;
        [SerializeField] GameObject offerGroup;

        public event Action GetPressed;

        Sprite offerButtonSprite;

        void Awake() => getButton.onClick.AddListener(() => GetPressed?.Invoke());

        public void SetSeason(string title) => seasonLabel.text = title;

        public void SetPremium(bool owned)
        {
            if (offerButtonSprite == null)
                offerButtonSprite = getButtonImage.sprite;

            getButton.interactable = !owned;
            getButtonImage.sprite = owned ? ownedButtonSprite : offerButtonSprite;
            getLabel.text = owned ? "ACTIVE" : "GET";
            offerGroup.SetActive(!owned);
        }
    }
}
