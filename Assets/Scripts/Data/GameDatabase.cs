using System.Collections.Generic;
using UnityEngine;

namespace TrainManager.Data
{
    /// <summary>
    /// Каталог всех конфигов игры. Через него код находит конфиг по строковому ID.
    /// Зачем: в сохранение пишутся только ID (ссылку на ассет в файл не сохранить),
    /// а при загрузке по ID находится нужный конфиг.
    /// Новый город, линию, маршрут или поезд нужно добавить сюда в список.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Train Manager/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        [Header("Catalog")]
        public List<RegionData> regions = new List<RegionData>();
        public List<CityData> cities = new List<CityData>();
        public List<RailwayLineData> lines = new List<RailwayLineData>();
        public List<RouteData> routes = new List<RouteData>();
        public List<TrainData> trains = new List<TrainData>();

        [Header("New game")]
        [Tooltip("Где стоит первый поезд в новой игре")]
        public CityData startCity;
        [Tooltip("Какой поезд есть у игрока в новой игре")]
        public TrainData startTrain;

        [Header("Time")]
        [Min(0.01f)]
        [Tooltip("Сколько реальных секунд длится 1 единица времени (дистанция / скорость)")]
        public float secondsPerTimeUnit = 1f;

        private Dictionary<string, CityData> cityById;
        private Dictionary<string, RouteData> routeById;
        private Dictionary<string, TrainData> trainById;

        private void OnEnable() => ClearCache();
        private void OnValidate() => ClearCache();

        public void ClearCache()
        {
            cityById = null;
            routeById = null;
            trainById = null;
        }

        public CityData GetCity(string cityId)
        {
            if (cityById == null) cityById = BuildIndex(cities, c => c.cityId, "City");
            return Find(cityById, cityId);
        }

        public RouteData GetRoute(string routeId)
        {
            if (routeById == null) routeById = BuildIndex(routes, r => r.routeId, "Route");
            return Find(routeById, routeId);
        }

        public TrainData GetTrain(string trainId)
        {
            if (trainById == null) trainById = BuildIndex(trains, t => t.trainId, "Train");
            return Find(trainById, trainId);
        }

        /// <summary>Линия между двумя городами (направление не важно). null — линии нет.</summary>
        public RailwayLineData FindLine(CityData a, CityData b)
        {
            foreach (RailwayLineData line in lines)
            {
                if (line != null && line.Connects(a, b))
                    return line;
            }
            return null;
        }

        private static T Find<T>(Dictionary<string, T> index, string id) where T : class
        {
            if (string.IsNullOrEmpty(id)) return null;
            return index.TryGetValue(id, out T value) ? value : null;
        }

        private Dictionary<string, T> BuildIndex<T>(List<T> list, System.Func<T, string> getId, string kind)
            where T : Object
        {
            var index = new Dictionary<string, T>();
            foreach (T item in list)
            {
                if (item == null) continue;

                string id = getId(item);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[Database] {kind} {item.name}: пустой ID", item);
                    continue;
                }
                if (index.ContainsKey(id))
                {
                    Debug.LogWarning($"[Database] {kind}: ID «{id}» повторяется ({item.name})", item);
                    continue;
                }
                index.Add(id, item);
            }
            return index;
        }
    }
}
