using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class UnitLeaderboardView : MonoBehaviour
    {
        [SerializeField] private Image unitIconImage;
        [SerializeField] private Image badgeImage;
        [SerializeField] private TextMeshProUGUI indexText;
        [SerializeField] private TextMeshProUGUI incomeText;

        private Sprite defaultBadgeSprite;
        private bool hasCachedDefaultBadgeSprite;

        public Sprite DefaultBadgeSprite
        {
            get
            {
                CacheDefaultBadgeSprite();
                return defaultBadgeSprite;
            }
        }

        public void Apply(int zeroBasedIndex, Sprite unitIcon, Sprite badgeSprite, Color indexColor, int income)
        {
            SetVisible(true);

            if (unitIconImage != null)
            {
                unitIconImage.sprite = unitIcon;
                unitIconImage.enabled = unitIcon != null;
            }

            if (badgeImage != null)
            {
                bool showBadge = zeroBasedIndex < 3 && badgeSprite != null;
                badgeImage.sprite = badgeSprite;
                badgeImage.enabled = showBadge;
                badgeImage.gameObject.SetActive(showBadge);
            }

            if (indexText != null)
            {
                indexText.SetText("{0}", zeroBasedIndex + 1);
                indexText.color = indexColor;
            }

            if (incomeText != null)
            {
                incomeText.SetText("{0}", Mathf.Max(0, income));
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void Awake()
        {
            CacheDefaultBadgeSprite();
        }

        private void CacheDefaultBadgeSprite()
        {
            if (hasCachedDefaultBadgeSprite)
            {
                return;
            }

            defaultBadgeSprite = badgeImage != null ? badgeImage.sprite : null;
            hasCachedDefaultBadgeSprite = true;
        }
    }
}
