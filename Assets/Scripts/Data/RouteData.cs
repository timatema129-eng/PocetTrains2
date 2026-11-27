using UnityEngine;

namespace TrainManager.Data
{
    /// <summary>
    /// Фиксированный маршрут из одного города в другой.
    /// Никакой случайности: откуда, куда и какая дистанция — задано заранее.
    /// </summary>
    [CreateAssetMenu(fileName = "Route_", menuName = "Train Manager/Route Data")]
    public class RouteData : ScriptableObject
    {
        [Header("ID")]
        public string routeId;

        [Header("Cities")]
        public CityData from;
        public CityData to;

        [Header("Distance")]
        [Min(1f)]
        [Tooltip("Дистанция в игровых единицах. Время в пути = дистанция / скорость поезда")]
        public float distance = 100f;

        public bool IsValid => from != null && to != null && from != to && distance > 0f;

        public string DisplayName =>
            $"{(from != null ? from.displayName : "?")} — {(to != null ? to.displayName : "?")}";
    }
}
