using System.Collections;
using TMPro;
using UnityEngine;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>Title, season XP bar, time left, premium status and wallet.</summary>
    public sealed class TopBarView : MonoBehaviour
    {
        [SerializeField] TMP_Text passTitle;
        [SerializeField] TMP_Text timeLeft;

        [Header("XP")]
        [SerializeField] RectTransform xpFill;
        [SerializeField] float xpFillWidth = 214f;
        [SerializeField] TMP_Text xpLabel;
        [SerializeField] TMP_Text nextLevelLabel;
        [SerializeField] RectTransform nextLevelBadge;

        [Header("Premium")]
        [SerializeField] TMP_Text premiumStatus;
        [SerializeField] Color premiumInactiveColor = Color.white;
        [SerializeField] Color premiumActiveColor = new Color(0.45f, 1f, 0.35f);

        [Header("Wallet")]
        [SerializeField] CurrencyCounter coins;
        [SerializeField] CurrencyCounter gems;

        public CurrencyCounter Coins => coins;
        public CurrencyCounter Gems => gems;

        Coroutine levelUpRoutine;

        public void SetTitles(string title, string time)
        {
            passTitle.text = title;
            timeLeft.text = time;
        }

        public void SetXp(int xp, int xpPerLevel, int nextLevel, bool maxLevel)
        {
            float fraction = maxLevel ? 1f : (float)xp / xpPerLevel;
            xpFill.gameObject.SetActive(fraction > 0.01f);
            xpFill.sizeDelta = new Vector2(Mathf.Max(24f, xpFillWidth * fraction), xpFill.sizeDelta.y);
            xpLabel.text = maxLevel ? "MAX" : $"{xp}/{xpPerLevel}";
            nextLevelLabel.text = maxLevel ? "-" : nextLevel.ToString();
        }

        public void PlayLevelUp(int xp, int xpPerLevel, int nextLevel, bool maxLevel)
        {
            SetXp(xp, xpPerLevel, nextLevel, maxLevel);
            if (levelUpRoutine != null)
                StopCoroutine(levelUpRoutine);
            levelUpRoutine = StartCoroutine(BumpBadge());
        }

        public void SetPremium(bool owned)
        {
            premiumStatus.text = owned ? "ACTIVE" : "INACTIVE";
            premiumStatus.color = owned ? premiumActiveColor : premiumInactiveColor;
        }

        IEnumerator BumpBadge()
        {
            yield return Tween.Run(0.4f, t => nextLevelBadge.localScale = Vector3.one * (1f + 0.35f * Ease.Bump(t)));
        }
    }
}
