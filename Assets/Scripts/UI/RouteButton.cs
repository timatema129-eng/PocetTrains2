using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Data;
using TrainManager.Trains;

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
        public TrainController train;
        public RouteData route;
        public TMP_Text label;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClick);

            if (label != null && route != null)
                label.text = $"{route.DisplayName}\n({route.distance:0})";
        }

        private void Update()
        {
            if (train == null || route == null)
            {
                button.interactable = false;
                return;
            }

            button.interactable = train.CanStartRoute(route, out _);
        }

        private void OnClick()
        {
            if (train != null)
                train.TryStartRoute(route);
        }
    }
}
