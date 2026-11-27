using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;

namespace TrainManager.UI
{
    /// <summary>
    /// Кнопка «отправить поезд по маршруту».
    /// Сама подписывается на нажатие (в On Click ничего добавлять не нужно)
    /// и становится неактивной, когда отправить нельзя.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RouteButton : MonoBehaviour
    {
        public GameRoot root;
        public RouteData route;
        public TMP_Text label;

        [Tooltip("Какой по счёту поезд игрока отправлять (0 — первый)")]
        [Min(0)] public int trainIndex;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClick);
        }

        private void Start()
        {
            if (label == null || route == null) return;

            float distance = root != null && root.IsReady ? root.Trains.GetRouteDistance(route) : -1f;
            label.text = distance > 0f
                ? $"{route.DisplayName}\n({distance:0})"
                : $"{route.DisplayName}\n(нет линии)";
        }

        private void Update()
        {
            if (root == null || route == null || !root.IsReady)
            {
                button.interactable = false;
                return;
            }

            TrainInstance train = root.Trains.GetTrainByIndex(trainIndex);
            button.interactable = root.Trains.CanStartRoute(train, route, out _);
        }

        private void OnClick()
        {
            if (root == null || !root.IsReady) return;
            root.Trains.TryStartRoute(root.Trains.GetTrainByIndex(trainIndex), route);
        }
    }
}
