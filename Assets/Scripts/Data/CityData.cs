using UnityEngine;

namespace TrainManager.Data
{
    /// <summary>
    /// Конфиг города (шаблон). Здесь только неизменяемые данные.
    /// Прогресс игрока (открыт ли город и т.п.) здесь НЕ хранится.
    /// </summary>
    [CreateAssetMenu(fileName = "City_", menuName = "Train Manager/City Data")]
    public class CityData : ScriptableObject
    {
        [Header("ID")]
        [Tooltip("Уникальный ID города, латиницей: paris, london, berlin")]
        public string cityId;

        [Header("Info")]
        public string displayName;
        public RegionData region;

        [Header("Map")]
        [Tooltip("Позиция станции на карте (UI, пиксели от центра экрана при 1920x1080)")]
        public Vector2 mapPosition;
    }
}
