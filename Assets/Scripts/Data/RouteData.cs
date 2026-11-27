using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TrainManager.Data
{
    /// <summary>
    /// Маршрут — цепочка городов: первый — откуда, последний — куда, между ними — промежуточные остановки.
    /// Между каждой парой соседних городов должна существовать линия (RailwayLineData).
    /// Дистанция маршрута не задаётся вручную, а складывается из дистанций линий.
    /// </summary>
    [CreateAssetMenu(fileName = "Route_", menuName = "Train Manager/Route Data")]
    public class RouteData : ScriptableObject
    {
        [Header("ID")]
        public string routeId;

        [Header("Stops")]
        [Tooltip("Города по порядку. Минимум два. Пример: Лондон, Париж, Берлин")]
        public List<CityData> stops = new List<CityData>();

        public int StopCount => stops != null ? stops.Count : 0;

        public CityData First => StopCount > 0 ? stops[0] : null;
        public CityData Last => StopCount > 0 ? stops[StopCount - 1] : null;

        /// <summary>Минимум два города, без пустых ячеек и без одного и того же города подряд.</summary>
        public bool IsValid
        {
            get
            {
                if (StopCount < 2) return false;
                for (int i = 0; i < stops.Count; i++)
                {
                    if (stops[i] == null) return false;
                    if (i > 0 && stops[i] == stops[i - 1]) return false;
                }
                return true;
            }
        }

        public string DisplayName
        {
            get
            {
                if (StopCount == 0) return "?";
                var sb = new StringBuilder();
                for (int i = 0; i < stops.Count; i++)
                {
                    if (i > 0) sb.Append(" — ");
                    sb.Append(stops[i] != null ? stops[i].displayName : "?");
                }
                return sb.ToString();
            }
        }
    }
}
