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
        public List<WagonData> wagons = new List<WagonData>();

        [Header("New game")]
        [Tooltip("Где стоит первый поезд в новой игре")]
        public CityData startCity;
        [Tooltip("Какой поезд есть у игрока в новой игре")]
        public TrainData startTrain;

        [Tooltip("Монеты в новой игре")]
        public long startCoins = 500;
        [Tooltip("Доллары в новой игре")]
        public long startDollars = 20;
        [Tooltip("ДЛЯ ТЕСТА: сколько дополнительных поездов той же модели дать в новой игре (проверка переключения поездов)")]
        [Min(0)] public int extraStartTrainsForTesting = 1;

        [Header("Economy (черновик баланса, числа меняются здесь)")]
        [Tooltip("Цена вагона = база + за км × расстояние до города назначения (по прямой)")]
        [Min(0f)] public float wagonBasePrice = 20f;
        [Min(0f)] public float wagonPricePerKm = 0.1f;
        [Tooltip("Доход за доставку = база + за км × расстояние от города покупки до города назначения")]
        [Min(0f)] public float deliveryBaseIncome = 40f;
        [Min(0f)] public float deliveryIncomePerKm = 0.3f;
        [Tooltip("Сколько вагонов в списке предложений станции")]
        [Min(1)] public int wagonOfferCount = 6;
        [Tooltip("Из скольких ближайших городов случайно выбираются города назначения")]
        [Min(1)] public int wagonOfferPool = 20;
        [Tooltip("Раз во сколько минут (реального времени) обновляется список вагонов")]
        [Min(1)] public int wagonOfferRefreshMinutes = 30;

        [Header("Fuel (черновик баланса)")]
        [Tooltip("Топлива на 1 км пути")]
        [Min(0f)] public float fuelPerKm = 0.01f;
        [Tooltip("Толчок: топливо на каждое НОВОЕ отправление (проезд промежуточной станции — без толчка)")]
        [Min(0f)] public float fuelPushBase = 2f;
        [Tooltip("Добавка к толчку за каждый грузовой вагон в составе")]
        [Min(0f)] public float fuelPushPerWagon = 0.25f;
        [Tooltip("Цена 1 единицы топлива при заправке, монет")]
        [Min(0f)] public float fuelPriceCoins = 2f;

        [Header("Network (черновик баланса)")]
        [Tooltip("Цена линии = база + за км × дистанция линии, монет")]
        [Min(0f)] public float linePriceBase = 50f;
        [Min(0f)] public float linePricePerKm = 0.5f;
        [Tooltip("В новой игре бесплатно даются все линии из стартового города")]
        public bool freeLinesFromStartCity = true;
        [Tooltip("Цена второго пути на линии (сколько поездов могут ехать одновременно), монет")]
        [Min(0f)] public float trackPriceBase = 300f;
        [Tooltip("Насколько дорожает каждый следующий путь, монет")]
        [Min(0f)] public float trackPriceStep = 200f;
        [Tooltip("Максимум путей на одной линии")]
        [Min(1)] public int maxTracksPerLine = 5;

        [Header("Stations")]
        [Tooltip("Мест для вагонов на складе станции в начале")]
        [Min(0)] public int stationBaseSlots = 5;
        [Tooltip("Сколько мест добавляет одно расширение склада")]
        [Min(1)] public int stationExpandSlots = 5;
        [Tooltip("Цена первого расширения, доллары")]
        [Min(0)] public int stationExpandPriceDollars = 10;
        [Tooltip("На сколько долларов дорожает каждое следующее расширение (0 — всегда одна цена)")]
        [Min(0)] public int stationExpandPriceStep = 0;

        [Header("Time")]
        [Min(0.01f)]
        [Tooltip("Сколько реальных секунд длится 1 единица времени (дистанция / скорость)")]
        public float secondsPerTimeUnit = 1f;

        private Dictionary<string, CityData> cityById;
        private Dictionary<string, RouteData> routeById;
        private Dictionary<string, TrainData> trainById;
        private Dictionary<string, WagonData> wagonById;
        private Dictionary<CityData, List<RailwayLineData>> linesByCity;

        private void OnEnable() => ClearCache();
        private void OnValidate() => ClearCache();

        public void ClearCache()
        {
            cityById = null;
            routeById = null;
            trainById = null;
            wagonById = null;
            linesByCity = null;
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

        public WagonData GetWagonType(string wagonTypeId)
        {
            if (wagonById == null) wagonById = BuildIndex(wagons, w => w.wagonTypeId, "Wagon");
            return Find(wagonById, wagonTypeId);
        }

        /// <summary>Первый грузовой тип вагона — его покупает игрок, пока типов один.</summary>
        public WagonData DefaultCargoWagon => FirstWagonOfKind(WagonKind.Cargo);

        public WagonData FirstWagonOfKind(WagonKind kind)
        {
            foreach (WagonData w in wagons)
                if (w != null && w.kind == kind) return w;
            return null;
        }

        /// <summary>Линия между двумя городами (направление не важно). null — линии нет.</summary>
        public RailwayLineData FindLine(CityData a, CityData b)
        {
            if (a == null || b == null) return null;
            foreach (RailwayLineData line in GetLines(a))
            {
                if (line.Connects(a, b))
                    return line;
            }
            return null;
        }

        private static readonly List<RailwayLineData> NoLines = new List<RailwayLineData>();

        /// <summary>Все линии, которые выходят из города.</summary>
        public IReadOnlyList<RailwayLineData> GetLines(CityData city)
        {
            if (linesByCity == null)
            {
                linesByCity = new Dictionary<CityData, List<RailwayLineData>>();
                foreach (RailwayLineData line in lines)
                {
                    if (line == null || !line.IsValid) continue;
                    AddLine(line.cityA, line);
                    AddLine(line.cityB, line);
                }
            }
            return city != null && linesByCity.TryGetValue(city, out List<RailwayLineData> list) ? list : NoLines;
        }

        private void AddLine(CityData city, RailwayLineData line)
        {
            if (!linesByCity.TryGetValue(city, out List<RailwayLineData> list))
            {
                list = new List<RailwayLineData>();
                linesByCity.Add(city, list);
            }
            list.Add(line);
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
