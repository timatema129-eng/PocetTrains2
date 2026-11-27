using UnityEngine;
using TrainManager.Map;

namespace TrainManager.Trains
{
    /// <summary>
    /// Картинка поезда на карте. Только показывает то, что посчитал TrainController:
    /// стоит — рисуем на станции, едет — между городами по прогрессу.
    /// Сама анимация ничего не решает (не определяет прибытие).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TrainView : MonoBehaviour
    {
        public TrainController train;
        public MapView map;

        private RectTransform rect;

        private void Awake()
        {
            rect = (RectTransform)transform;

            if (train == null)
                Debug.LogError($"[TrainView] {name}: не назначен Train", this);
            if (map == null)
                Debug.LogError($"[TrainView] {name}: не назначен Map", this);
        }

        private void LateUpdate()
        {
            if (train == null || map == null) return;

            if (train.State == TrainState.Moving && train.ActiveRoute != null)
            {
                if (map.TryGetStationPosition(train.ActiveRoute.from, out Vector2 from) &&
                    map.TryGetStationPosition(train.ActiveRoute.to, out Vector2 to))
                {
                    rect.anchoredPosition = Vector2.Lerp(from, to, train.Progress);
                }
            }
            else if (map.TryGetStationPosition(train.CurrentCity, out Vector2 station))
            {
                rect.anchoredPosition = station;
            }
        }
    }
}
