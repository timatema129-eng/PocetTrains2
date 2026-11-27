using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;

namespace TrainManager.UI
{
    /// <summary>
    /// «Летящий приз»: время от времени (пока игра открыта) появляется кнопка-приз и медленно летит по экрану.
    /// Нажал — иногда просто монеты или доллар, иногда предложение посмотреть рекламу за доллары.
    /// </summary>
    public class FlyingPrize : MonoBehaviour
    {
        public GameRoot root;
        public Button button;
        public AdPrompt prompt;
        [Tooltip("Скорость полёта, пикселей в секунду")]
        public float speed = 60f;

        private RectTransform rect;
        private RectTransform area;
        private float nextShow;
        private float hideAt;
        private Vector2 velocity;

        private void Awake()
        {
            rect = (RectTransform)button.transform;
            area = rect.parent as RectTransform;
            button.onClick.AddListener(OnClick);
            button.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (root == null || !root.IsReady) return;
            if (nextShow <= 0f) nextShow = Time.unscaledTime + root.Database.flyingPrizeMinutes * 60f;

            if (button.gameObject.activeSelf)
            {
                rect.anchoredPosition += velocity * Time.unscaledDeltaTime;
                if (Time.unscaledTime >= hideAt) Hide();
            }
            else if (Time.unscaledTime >= nextShow)
            {
                Show();
            }
        }

        private void Show()
        {
            Vector2 size = area != null ? area.rect.size : new Vector2(1920f, 1080f);
            // Летит слева направо по случайной высоте
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(-50f, Random.Range(size.y * 0.2f, size.y * 0.7f));
            float seconds = root.Database.flyingPrizeSeconds;
            velocity = new Vector2(Mathf.Max(speed, (size.x + 100f) / seconds), 0f);
            hideAt = Time.unscaledTime + seconds;
            button.gameObject.SetActive(true);
            button.transform.SetAsLastSibling();
        }

        private void Hide()
        {
            button.gameObject.SetActive(false);
            nextShow = Time.unscaledTime + root.Database.flyingPrizeMinutes * 60f;
        }

        private void OnClick()
        {
            Hide();
            BonusKind kind = root.Ads.RollFlyingPrize();
            if (kind == BonusKind.AdOffer)
            {
                if (prompt != null)
                    prompt.Ask($"Посмотри рекламу — получи {root.Database.adDollarReward} $", "Смотреть",
                        () => root.Ads.WatchForDollars("flying_prize"));
                return;
            }
            RewardData reward = root.Ads.GrantSimplePrize(kind, "летящий приз");
            if (prompt != null) prompt.Info($"Приз: {root.Ads.Describe(reward)}");
        }
    }
}
