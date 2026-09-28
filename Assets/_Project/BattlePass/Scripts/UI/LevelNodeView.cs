using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VertigoDemo.UI;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>Numbered circle on the progress track.</summary>
    public sealed class LevelNodeView : MonoBehaviour
    {
        [SerializeField] Image circle;
        [SerializeField] TMP_Text label;
        [SerializeField] Sprite reachedSprite;
        [SerializeField] Sprite lockedSprite;
        [SerializeField] Color reachedLabelColor = new Color(0.42f, 0.16f, 0.02f);
        [SerializeField] Color lockedLabelColor = Color.white;
        [Tooltip("Pulsing ring around the level the player is working towards.")]
        [SerializeField] GameObject nextRing;

        public void Setup(int level) => label.text = level.ToString();

        public void SetReached(bool reached)
        {
            circle.sprite = reached ? reachedSprite : lockedSprite;
            label.color = reached ? reachedLabelColor : lockedLabelColor;
        }

        public void SetNext(bool isNext) => nextRing.SetActive(isNext);

        public IEnumerator PlayReached()
        {
            SetReached(true);
            var rect = (RectTransform)transform;
            yield return Tween.Run(0.4f, t => rect.localScale = Vector3.one * (1f + 0.35f * Ease.Bump(t)));
        }
    }
}
