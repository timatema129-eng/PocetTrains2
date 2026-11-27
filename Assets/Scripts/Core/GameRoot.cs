using System;
using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;
using TrainManager.Trains;

namespace TrainManager.Core
{
    /// <summary>
    /// Точка входа игры. Один объект на сцене. Создаёт сервисы, загружает или начинает игру,
    /// каждый кадр двигает время и сохраняет прогресс.
    /// Все экраны берут данные отсюда (поле GameRoot в инспекторе).
    /// Запускается раньше остальных скриптов (DefaultExecutionOrder).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameRoot : MonoBehaviour
    {
        [SerializeField] private GameDatabase database;

        /// <summary>Путь базы в редакторе — запасной вариант, если ссылка в сцене потерялась.</summary>
        public const string EditorDatabasePath = "Assets/Data/GameDatabase.asset";

        // Заполняет сборщик сцены. По ним редактор перед Play понимает, что сцену надо пересобрать
        [HideInInspector] public int sceneLayoutBuild;
        [HideInInspector] public string mapSourceHash;

        public GameDatabase Database => database;
        public GameState State { get; private set; }
        public GameEvents Events { get; private set; }
        public TrainService Trains { get; private set; }
        public WalletService Wallet { get; private set; }
        public WagonService Wagons { get; private set; }
        public FuelService Fuel { get; private set; }
        public NetworkService Network { get; private set; }
        public PartsService Parts { get; private set; }
        public MaintenanceService Maintenance { get; private set; }
        public ExchangeService Exchange { get; private set; }
        public RewardService Rewards { get; private set; }
        public QuestService Quests { get; private set; }
        public AchievementService Achievements { get; private set; }
        public AdsService Ads { get; private set; }
        public MetroService Metro { get; private set; }

        /// <summary>Все сервисы созданы — можно пользоваться.</summary>
        public bool IsReady => ready && Trains != null;
        private bool ready;

        private IGameClock clock;
        private SaveService saves;

        private void Awake()
        {
#if UNITY_EDITOR
            // Ссылка в сцене потерялась (например, ассеты карты пересоздались) — берём базу по пути
            if (database == null)
                database = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDatabase>(EditorDatabasePath);
#endif
            if (database == null)
            {
                Debug.LogError("[Game] Не найдена база игры (GameDatabase). Нажми Tools → Train Manager → Build Paris Prototype Scene", this);
                enabled = false;
                return;
            }

            clock = new SystemGameClock();
            saves = new SaveService(clock);

            GameState loaded = saves.Load();
            try
            {
                Init(loaded);
            }
            catch (Exception e)
            {
                // Сохранение от старой версии не подошло: откладываем его и начинаем заново, чтобы игра не стояла «пустой»
                Debug.LogException(e);
                Debug.LogError("[Game] Не удалось запустить игру с сохранением — оно отложено (save.json.failed), начинается новая игра");
                Trains = null;
                saves.MoveAside(".failed");
                try { Init(null); }
                catch (Exception e2)
                {
                    Trains = null; // IsReady = false: экраны не трогают полусозданные сервисы
                    Debug.LogException(e2);
                    Debug.LogError("[Game] Новая игра тоже не запустилась — пришли эту ошибку");
                }
            }
        }

        private void Init(GameState loaded)
        {
            Events = new GameEvents();
            State = loaded;
            bool isNewGame = State == null;
            if (isNewGame)
                State = new GameState { createdUnixMs = clock.NowUnixMs };

            Trains = null;
            Wallet = new WalletService(State, Events);
            Fuel = new FuelService(State, database, Wallet);
            Network = new NetworkService(State, database, Wallet, Events);
            Maintenance = new MaintenanceService(State, database, Wallet, Events);
            Trains = new TrainService(State, database, clock, Events, Fuel, Network, Maintenance);
            Wagons = new WagonService(State, database, Wallet, Events, clock, Fuel, Network); // подписывается на прибытие: выгрузка
            Parts = new PartsService(State, database, Wallet, Trains, Events);
            Exchange = new ExchangeService(database, clock, Wallet);
            Rewards = new RewardService(State, database, Wallet, Parts, Events);
            Quests = new QuestService(State, database, clock, Events, Network, Rewards); // подписывается на доставку
            Metro = new MetroService(State, database, clock, Wallet, Events);
            Ads = new AdsService(State, database, clock, Rewards, Wagons, new StubAdService(), Events); // позже — реальный SDK

            if (isNewGame)
                StartNewGame();
            else
                Repair();
            GiveTestKit();

            // Достижения — после стартовой сети, чтобы стартовый регион и бесплатные линии не считались
            Achievements = new AchievementService(State, database, Events, Rewards);

            // Подписки на сохранение — ДО догоняющего Tick, чтобы офлайн-выгрузка тоже сохранилась.
            Events.TrainDeparted += (train, trip) => Save();
            Events.TrainReachedStop += (train, city, isFinal) => Save();
            Events.WagonBought += wagon => Save();
            Events.WagonMoved += wagon => Save();
            Events.StationChanged += city => Save();
            Events.RegionUnlocked += region => Save();
            Events.LineBought += line => Save();
            Events.LineChanged += line => Save();
            Events.PartsChanged += () => Save();
            Events.TrainConditionChanged += train => Save();
            Events.WalletChanged += (currency, balance, delta, reason) => Save(); // обмен, покупки, доход
            Events.QuestsChanged += () => Save();
            Events.AdsChanged += () => Save();
            Events.MetroChanged += cityId => Save();
            Events.AchievementUnlocked += a => Save();

            // Догнать время, пока игра была закрыта: поезда прибудут туда, куда успели доехать,
            // и выгрузят вагоны на пройденных остановках.
            Trains.Tick();
            Quests.Tick();
            Metro.Tick(); // офлайн-доход метро (10% от обычного)

            ready = true;
            Save();
        }

        private void StartNewGame()
        {
            if (database.startTrain == null || database.startCity == null)
            {
                Debug.LogError("[Game] В GameDatabase не назначены Start Train / Start City", database);
                return;
            }

            Network.SetupStartingNetwork();
            Trains.CreateTrain(database.startTrain, database.startCity);
            Wallet.Add(Currency.Coins, database.startCoins, "старт игры");
            Wallet.Add(Currency.Dollars, database.startDollars, "старт игры");
            Debug.Log("[Game] Новая игра");
        }

        /// <summary>Версия тестового набора. Увеличить — и набор выдадут ещё раз (и в старом сохранении).</summary>
        private const int TestKitVersion = 1;
        private const int TestWagonsPerTrain = 3;

        /// <summary>
        /// ДЛЯ ТЕСТА: всего 1 + extraStartTrainsForTesting поездов в стартовом городе и по 3 грузовых вагона
        /// в соседние города на каждом новом поезде. Выдаётся один раз — и в новой игре, и в уже начатой.
        /// </summary>
        private void GiveTestKit()
        {
            if (State.testKitVersion >= TestKitVersion) return;
            if (database.startTrain == null || database.startCity == null) return;
            State.testKitVersion = TestKitVersion;

            int wanted = 1 + Mathf.Max(0, database.extraStartTrainsForTesting);
            var created = new List<TrainInstance>();
            while (State.trains.Count < wanted)
                created.Add(Trains.CreateTrain(database.startTrain, database.startCity));

            // Вагоны — на поезда, которые стоят в стартовом городе без вагонов
            int given = 0;
            foreach (TrainInstance train in State.trains)
            {
                if (train.state == TrainState.Moving || train.state == TrainState.Stranded) continue;
                if (train.currentCityId != database.startCity.cityId) continue;
                if (train.wagonIds.Count > 0) continue;
                given += Wagons.GiveTestWagons(train, database.startCity, TestWagonsPerTrain);
            }
            Debug.Log($"[Game] Тестовый набор: поездов добавлено {created.Count}, вагонов выдано {given}");
        }

        /// <summary>
        /// Починить сохранение после изменения конфигов: например, если модель поезда удалили из базы.
        /// </summary>
        private void Repair()
        {
            for (int i = State.trains.Count - 1; i >= 0; i--)
            {
                TrainInstance train = State.trains[i];
                if (train == null || database.GetTrain(train.modelId) == null)
                {
                    Debug.LogWarning($"[Game] Поезд {(train != null ? train.modelId : "null")} не найден в GameDatabase и удалён из сохранения");
                    State.trains.RemoveAt(i);
                    continue;
                }

                if (train.trip == null) train.trip = new TripState();
                if (float.IsNaN(train.condition)) train.condition = 100f;
                train.condition = Mathf.Clamp(train.condition, 0f, 100f);

                // Встал на линии, но поездка повреждена — ставим в первый город маршрута или в стартовый
                if (train.state == TrainState.Stranded && !train.trip.IsActive)
                {
                    train.currentCityId = database.startCity != null ? database.startCity.cityId : "";
                    train.state = TrainState.Idle;
                    train.breakdown = BreakdownState.Broken;
                }

                // Город убрали из карты — возвращаем поезд в стартовый город.
                if (train.state != TrainState.Moving && train.state != TrainState.Stranded &&
                    database.GetCity(train.currentCityId) == null &&
                    database.startCity != null)
                {
                    Debug.LogWarning($"[Game] Города «{train.currentCityId}» больше нет на карте, поезд {train.instanceId} перенесён в {database.startCity.displayName}");
                    train.currentCityId = database.startCity.cityId;
                    train.state = TrainState.Idle;
                }

                // Город из поездки убрали из карты — поездку отменяем.
                if (train.state == TrainState.Moving && train.trip.IsActive)
                {
                    foreach (string cityId in train.trip.stopIds)
                    {
                        if (database.GetCity(cityId) == null)
                        {
                            train.trip = new TripState();
                            break;
                        }
                    }
                }

                // Поездка повреждена — ставим поезд в первый город маршрута, чтобы он не завис.
                if (train.state == TrainState.Moving && !train.trip.IsActive)
                {
                    Debug.LogWarning($"[Game] У поезда {train.instanceId} повреждена поездка, он возвращён на станцию");
                    if (string.IsNullOrEmpty(train.currentCityId) && database.startCity != null)
                        train.currentCityId = database.startCity.cityId;
                    train.state = TrainState.Idle;
                    train.trip = new TripState();
                }
            }

            RepairWagons();

            Network.RemoveMissing();
            if (State.unlockedRegionIds.Count == 0)
            {
                Debug.Log("[Game] В сохранении нет сети (старая версия) — выдаём стартовую");
                Network.SetupStartingNetwork();
            }

            if (State.trains.Count == 0)
                StartNewGame();

            Debug.Log($"[Game] Продолжение игры, поездов: {State.trains.Count}");
        }

        /// <summary>
        /// Каждый вагон должен быть ровно в одном месте, а его города — существовать.
        /// </summary>
        private void RepairWagons()
        {
            if (State.wagons == null) State.wagons = new List<WagonInstance>();
            if (State.stations == null) State.stations = new List<StationState>();
            State.stations.RemoveAll(st => st == null);
            foreach (StationState st in State.stations)
                if (st.boughtOfferSlots == null) st.boughtOfferSlots = new List<int>();

            for (int i = State.wagons.Count - 1; i >= 0; i--)
            {
                WagonInstance w = State.wagons[i];
                WagonData type = w != null ? database.GetWagonType(w.wagonTypeId) : null;
                // У грузового вагона должен быть город назначения; у служебных (топливный, двигатель) его нет
                bool broken = w == null || type == null ||
                              (type.kind == WagonKind.Cargo && database.GetCity(w.destinationCityId) == null);
                if (!broken && w.location == WagonLocation.Station)
                    broken = database.GetCity(w.locationId) == null;
                if (!broken && w.location == WagonLocation.Train)
                {
                    TrainInstance t = State.FindTrain(w.locationId);
                    broken = t == null || !t.wagonIds.Contains(w.instanceId);
                }
                if (broken)
                {
                    Debug.LogWarning($"[Game] Вагон {(w != null ? w.instanceId : "null")} повреждён в сохранении и удалён");
                    State.wagons.RemoveAt(i);
                }
            }

            foreach (TrainInstance t in State.trains)
            {
                if (t.wagonIds == null) t.wagonIds = new List<string>();
                if (float.IsNaN(t.fuel) || t.fuel < 0f) t.fuel = 0f;
                t.wagonIds.RemoveAll(id =>
                {
                    WagonInstance w = State.FindWagon(id);
                    return w == null || w.location != WagonLocation.Train || w.locationId != t.instanceId;
                });
                Fuel.ClampToCapacity(t);
                Fuel.UpdateOutOfFuel(t);
            }
        }

        private void Update()
        {
            if (!IsReady) return;
            Trains.Tick();
            Quests.Tick();
            Metro.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        public void Save()
        {
            if (saves != null && State != null && IsReady)
                saves.Save(State);
        }
    }
}
