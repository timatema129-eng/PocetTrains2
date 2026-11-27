using UnityEngine;

namespace TrainManager.Data
{
    /// <summary>
    /// Железнодорожная линия — участок дороги между двумя соседними городами.
    /// Линия не принадлежит поезду: по ней могут ехать несколько поездов.
    /// Направление не важно: линия Париж–Лондон работает и в обратную сторону.
    /// Куплена ли линия и электрифицирована ли — хранится в GameState (на будущее).
    /// </summary>
    [CreateAssetMenu(fileName = "Line_", menuName = "Train Manager/Railway Line Data")]
    public class RailwayLineData : ScriptableObject
    {
        [Header("ID")]
        public string lineId;

        [Header("Where")]
        public RegionData region;
        public CityData cityA;
        public CityData cityB;

        [Header("Distance")]
        [Min(1f)]
        [Tooltip("Дистанция в игровых единицах. Время на участке = дистанция / скорость поезда")]
        public float distance = 100f;

        [Header("Visual")]
        [Tooltip("Цвет линии на карте. Только картинка, на логику не влияет")]
        public Color color = new Color(0.9f, 0.75f, 0.3f);

        public bool IsValid => cityA != null && cityB != null && cityA != cityB && distance > 0f;

        /// <summary>Соединяет ли линия эти два города (в любом направлении).</summary>
        public bool Connects(CityData a, CityData b)
        {
            if (!IsValid || a == null || b == null) return false;
            return (cityA == a && cityB == b) || (cityA == b && cityB == a);
        }
    }
}
