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

            Trains = new TrainService(State, database, clock, Events);

            if (isNewGame)
                StartNewGame();
            else
                Repair();

            // Догнать время, пока игра была закрыта: поезда прибудут туда, куда успели доехать.
            Trains.Tick();

            Events.TrainDeparted += (train, route) => Save();
            Events.TrainReachedStop += (train, city, isFinal) => Save();

            Save();
        }

        private void StartNewGame()
        {
            if (database.startTrain == null || database.startCity == null)
            {
                Debug.LogError("[Game] В GameDatabase не назначены Start Train / Start City", database);
                return;
            }

            Trains.CreateTrain(database.startTrain, database.startCity);
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

            if (State.trains.Count == 0)
                StartNewGame();

            Debug.Log($"[Game] Продолжение игры, поездов: {State.trains.Count}");
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
