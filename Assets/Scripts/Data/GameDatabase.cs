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

        [Header("Boxes and parts (черновик баланса)")]
        [Tooltip("Три бокса: обычный, редкий, эпический. Открываются только за доллары")]
        public List<BoxConfig> boxes = new List<BoxConfig>
        {
            new BoxConfig { boxId = "box_common", displayName = "Обычный бокс", rarity = TrainRarity.Common, priceDollars = 5 },
            new BoxConfig { boxId = "box_rare", displayName = "Редкий бокс", rarity = TrainRarity.Rare, priceDollars = 20 },
            new BoxConfig { boxId = "box_epic", displayName = "Эпический бокс", rarity = TrainRarity.Epic, priceDollars = 100 },
        };
        [Tooltip("Сколько лома даёт разборка поезда за каждую деталь, нужную для его сборки")]
        [Min(0)] public int scrapPerPart = 1;

        [Header("Wear and repair (черновик баланса)")]
        [Tooltip("Техобслуживание (до 100%) = база + k × (недостающие %)². Чем раньше, тем дешевле")]
        [Min(0f)] public float maintenanceBaseCoins = 10f;
        [Min(0f)] public float maintenanceCoinsPerMissingSq = 0.03f;
        [Tooltip("Шанс случайной поломки по прибытии при 100% состояния")]
        [Range(0f, 1f)] public float breakdownChanceAtFull = 0.02f;
        [Tooltip("Добавка к шансу при 0% состояния (растёт по квадрату износа)")]
        [Range(0f, 1f)] public float breakdownChanceAtZero = 0.35f;
        [Tooltip("Какая доля случайных поломок — лёгкие (остальные — «сломан»)")]
        [Range(0f, 1f)] public float lightBreakdownShare = 0.5f;
        [Tooltip("С лёгкой поломкой: шанс сломаться на каждом участке пути")]
        [Range(0f, 1f)] public float lightFailChancePerLeg = 0.25f;
        [Tooltip("Ремонт лёгкой поломки: монеты или лом")]
        [Min(0)] public int repairLightCoins = 25;
        [Min(0)] public int repairLightScrap = 2;
        [Tooltip("Ремонт «сломан»: монеты или лом")]
        [Min(0)] public int repairBrokenCoins = 60;
        [Min(0)] public int repairBrokenScrap = 5;
        [Tooltip("Буксировка поезда, вставшего на линии, до города, откуда он ехал")]
        [Min(0)] public int towCoins = 100;

        [Header("Exchange and selling (черновик баланса)")]
        [Tooltip("Курс: монет за 1 $. Минимум (начало и конец цикла)")]
        [Min(1)] public int exchangeMinRate = 120;
        [Tooltip("Курс: максимум возможного пика цикла")]
        [Min(1)] public int exchangeMaxRate = 400;
        [Tooltip("Длина цикла курса, реальных дней")]
        [Min(1)] public int exchangeCycleDays = 3;
        [Tooltip("Курс меняется раз в столько минут")]
        [Min(1)] public int exchangeStepMinutes = 60;
        [Tooltip("Продажа вагона: доля от цены покупки")]
        [Range(0f, 1f)] public float wagonSellShare = 0.5f;

        [Header("Quests (черновик: частота, количество, длительность — уточнить)")]
        [Tooltip("Сколько заданий может быть одновременно")]
        [Min(0)] public int maxActiveQuests = 2;
        [Tooltip("Раз во сколько реальных минут появляется новое задание (если есть место)")]
        [Min(1)] public int questSpawnMinutes = 60;
        public List<QuestTemplate> questTemplates = new List<QuestTemplate>
        {
            new QuestTemplate { templateId = "deliver_epic", kind = QuestKind.DeliverTo, minWagons = 3, maxWagons = 5,
                durationMinutes = 180, reward = new RewardData { boxIds = new List<string> { "box_epic" } } },
            new QuestTemplate { templateId = "take_epic", kind = QuestKind.TakeFrom, minWagons = 3, maxWagons = 5,
                durationMinutes = 180, reward = new RewardData { boxIds = new List<string> { "box_epic" } } },
        };

        [Header("Achievements (черновик наград)")]
        public List<AchievementData> achievements = new List<AchievementData>
        {
            new AchievementData { achievementId = "region_1", displayName = "Новый регион", stat = AchievementStat.RegionsUnlocked, target = 1, reward = new RewardData { dollars = 5 } },
            new AchievementData { achievementId = "region_3", displayName = "Три региона", stat = AchievementStat.RegionsUnlocked, target = 3, reward = new RewardData { dollars = 15 } },
            new AchievementData { achievementId = "lines_10", displayName = "10 линий", stat = AchievementStat.LinesBought, target = 10, reward = new RewardData { coins = 300 } },
            new AchievementData { achievementId = "delivered_10", displayName = "10 вагонов доставлено", stat = AchievementStat.WagonsDelivered, target = 10, reward = new RewardData { coins = 200 } },
            new AchievementData { achievementId = "delivered_100", displayName = "100 вагонов доставлено", stat = AchievementStat.WagonsDelivered, target = 100, reward = new RewardData { dollars = 5 } },
            new AchievementData { achievementId = "assembled_1", displayName = "Первый собранный поезд", stat = AchievementStat.TrainsAssembled, target = 1, reward = new RewardData { coins = 200 } },
            new AchievementData { achievementId = "km_1000", displayName = "1000 км", stat = AchievementStat.DistanceKm, target = 1000, reward = new RewardData { dollars = 2 } },
            new AchievementData { achievementId = "km_10000", displayName = "10 000 км", stat = AchievementStat.DistanceKm, target = 10000, reward = new RewardData { dollars = 10 } },
            new AchievementData { achievementId = "quest_1", displayName = "Первое задание", stat = AchievementStat.QuestsCompleted, target = 1, reward = new RewardData { dollars = 5 } },
        };

        [Header("Ads (черновик: лимиты и награды — уточнить)")]
        [Tooltip("Ежедневная серия: награда за каждый просмотр по порядку; последняя — супербонус")]
        public List<RewardData> adSeriesRewards = new List<RewardData>
        {
            new RewardData { coins = 100 },
            new RewardData { dollars = 2 },
            new RewardData { coins = 200 },
            new RewardData { dollars = 10, boxIds = new List<string> { "box_rare" } },
        };
        [Tooltip("«Посмотри рекламу — получи N $»")]
        [Min(0)] public int adDollarReward = 15;
        [Tooltip("Сколько раз в день можно смотреть рекламу за доллары (вместе с бонусами станций и летящим призом)")]
        [Min(0)] public int adDollarsPerDay = 3;
        [Tooltip("Шанс, что у станции в текущем списке вагонов есть бонус")]
        [Range(0f, 1f)] public float stationBonusChance = 0.2f;
        [Tooltip("Летящий приз: раз во сколько минут игры появляется и сколько секунд висит")]
        [Min(1)] public int flyingPrizeMinutes = 5;
        [Min(1)] public int flyingPrizeSeconds = 20;
        [Tooltip("Доли исходов бонуса: просто монеты / просто доллар / предложение рекламы")]
        [Min(0f)] public float bonusShareCoins = 0.5f;
        [Min(0f)] public float bonusShareDollar = 0.2f;
        [Min(0f)] public float bonusShareAd = 0.3f;
        [Min(0)] public int bonusCoinsMin = 30;
        [Min(0)] public int bonusCoinsMax = 80;
        [Min(0)] public int bonusDollars = 1;

        [Header("Time")]
        [Min(0.01f)]
        [Tooltip("Сколько реальных секунд длится 1 единица времени (дистанция / скорость)")]
        public float secondsPerTimeUnit = 1f;

        public QuestTemplate GetQuestTemplate(string templateId)
        {
            foreach (QuestTemplate q in questTemplates)
                if (q != null && q.templateId == templateId) return q;
            return null;
        }

        public BoxConfig GetBox(string boxId)
        {
            foreach (BoxConfig b in boxes)
                if (b != null && b.boxId == boxId) return b;
            return null;
        }

        /// <summary>Все модели поездов этой редкости (из них выпадают детали бокса).</summary>
        public List<TrainData> GetTrainsOfRarity(TrainRarity rarity)
        {
            var result = new List<TrainData>();
            foreach (TrainData t in trains)
                if (t != null && t.rarity == rarity && !string.IsNullOrEmpty(t.trainId)) result.Add(t);
            return result;
        }

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
