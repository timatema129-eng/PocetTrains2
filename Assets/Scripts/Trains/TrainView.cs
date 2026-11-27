using UnityEngine;
using TrainManager.Core;
using TrainManager.Map;

namespace TrainManager.Trains
{
    /// <summary>
    /// Картинка поезда на карте. Только показывает то, что посчитал TrainService:
    /// стоит — рисуем на станции, едет — между двумя городами текущего участка.
    /// Сама анимация ничего не решает (не определяет прибытие).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TrainView : MonoBehaviour
    {
        public GameRoot root;
        public MapView map;

        [Tooltip("Какой по счёту поезд игрока показывать (0 — первый)")]
        [Min(0)] public int trainIndex;

        private RectTransform rect;

        private void Awake()
        {
            rect = (RectTransform)transform;

            if (root == null)
                Debug.LogError($"[TrainView] {name}: не назначен Root", this);
            if (map == null)
                Debug.LogError($"[TrainView] {name}: не назначен Map", this);
        }

        private void LateUpdate()
        {
            if (root == null || map == null || !root.IsReady) return;

            TrainInstance train = root.Trains.GetTrainByIndex(trainIndex);
            if (train == null) return;

            root.Trains.GetPosition(train, out string fromId, out string toId, out float t);

            if (map.TryGetStationPosition(fromId, out Vector2 from) &&
                map.TryGetStationPosition(toId, out Vector2 to))
            {
                rect.anchoredPosition = Vector2.Lerp(from, to, t);
            }
        }
    }
}
