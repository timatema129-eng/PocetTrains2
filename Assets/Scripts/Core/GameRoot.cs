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

        public GameDatabase Database => database;
        public GameState State { get; private set; }
        public GameEvents Events { get; private set; }
        public TrainService Trains { get; private set; }
        public WalletService Wallet { get; private set; }
        public WagonService Wagons { get; private set; }
        public FuelService Fuel { get; private set; }
        public NetworkService Network { get; private set; }

        /// <summary>Все сервисы созданы — можно пользоваться.</summary>
        public bool IsReady => Trains != null;

        private IGameClock clock;
        private SaveService saves;

        private void Awake()
        {
            if (database == null)
            {
                Debug.LogError("[Game] У GameRoot не назначен Database", this);
                enabled = false;
                return;
            }

            clock = new SystemGameClock();
            saves = new SaveService(clock);
            Events = new GameEvents();

            State = saves.Load();
            bool isNewGame = State == null;
            if (isNewGame)
                State = new GameState { createdUnixMs = clock.NowUnixMs };

            Wallet = new WalletService(State, Events);
            Fuel = new FuelService(State, database, Wallet);
            Network = new NetworkService(State, database, Wallet, Events);
            Trains = new TrainService(State, database, clock, Events, Fuel, Network);
            Wagons = new WagonService(State, database, Wallet, Events, clock, Fuel, Network); // подписывается на прибытие: выгрузка

            if (isNewGame)
                StartNewGame();
            else
                Repair();

            // Подписки на сохранение — ДО догоняющего Tick, чтобы офлайн-выгрузка тоже сохранилась.
            Events.TrainDeparted += (train, trip) => Save();
            Events.TrainReachedStop += (train, city, isFinal) => Save();
            Events.WagonBought += wagon => Save();
            Events.WagonMoved += wagon => Save();
            Events.StationChanged += city => Save();
            Events.RegionUnlocked += region => Save();
            Events.LineBought += line => Save();

            // Догнать время, пока игра была закрыта: поезда прибудут туда, куда успели доехать,
            // и выгрузят вагоны на пройденных остановках.
            Trains.Tick();

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
            for (int i = 0; i < database.extraStartTrainsForTesting; i++)
                Trains.CreateTrain(database.startTrain, database.startCity);
            Wallet.Add(Currency.Coins, database.startCoins, "старт игры");
            Wallet.Add(Currency.Dollars, database.startDollars, "старт игры");
            Debug.Log("[Game] Новая игра");
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

                // Город убрали из карты — возвращаем поезд в стартовый город.
                if (train.state != TrainState.Moving && database.GetCity(train.currentCityId) == null &&
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
            Trains.Tick();
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
            if (saves != null && State != null)
                saves.Save(State);
        }
    }
}
