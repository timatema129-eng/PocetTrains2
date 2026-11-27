using System;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Trains
{
    /// <summary>
    /// Логика одного поезда: где он, едет ли, по какому маршруту и сколько проехал.
    /// Ничего не рисует — за картинку отвечает TrainView.
    /// Правила: у поезда один активный маршрут; пока он в пути, новый маршрут не запустить;
    /// отправить можно только из города, где поезд стоит.
    /// </summary>
    public class TrainController : MonoBehaviour
    {
        [Header("Config")]
        public TrainData trainData;
        public CityData startCity;

        [Header("Test")]
        [Min(0.01f)]
        [Tooltip("Сколько реальных секунд длится 1 единица времени (дистанция / скорость)")]
        public float secondsPerTimeUnit = 1f;

        public TrainState State { get; private set; } = TrainState.Idle;

        /// <summary>Город, где стоит поезд. Пока поезд в пути — null.</summary>
        public CityData CurrentCity { get; private set; }

        /// <summary>Маршрут, по которому поезд едет сейчас. Когда стоит — null.</summary>
        public RouteData ActiveRoute { get; private set; }

        /// <summary>Прогресс поездки от 0 до 1.</summary>
        public float Progress { get; private set; }

        /// <summary>Длительность текущей поездки в секундах.</summary>
        public float TravelDuration { get; private set; }

        public event Action<TrainController> StateChanged;

        private float elapsed;

        private void Awake()
        {
            CurrentCity = startCity;

            if (trainData == null)
                Debug.LogError($"[Train] {name}: не назначен Train Data", this);
            if (startCity == null)
                Debug.LogError($"[Train] {name}: не назначен Start City", this);
        }

        private void Start()
        {
            string trainName = trainData != null ? trainData.displayName : name;
            string cityName = CurrentCity != null ? CurrentCity.displayName : "?";
            Debug.Log($"[Train] {trainName} стоит в городе {cityName}");
        }

        /// <summary>Можно ли сейчас отправить поезд по маршруту. reason — почему нельзя.</summary>
        public bool CanStartRoute(RouteData route, out string reason)
        {
            if (route == null || !route.IsValid)
            {
                reason = "маршрут не настроен";
                return false;
            }
            if (trainData == null || trainData.baseSpeed <= 0f)
            {
                reason = "у поезда не настроены данные или скорость";
                return false;
            }
            if (State == TrainState.Moving)
            {
                reason = "поезд уже в пути";
                return false;
            }
            if (CurrentCity != route.from)
            {
                reason = $"поезд не в городе {route.from.displayName}";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Отправить поезд. Возвращает false, если нельзя (причина пишется в Console).</summary>
        public bool TryStartRoute(RouteData route)
        {
            if (!CanStartRoute(route, out string reason))
            {
                Debug.LogWarning($"[Train] Отправление невозможно: {reason}");
                return false;
            }

            ActiveRoute = route;
            elapsed = 0f;
            Progress = 0f;
            TravelDuration = route.distance / trainData.baseSpeed * secondsPerTimeUnit;
            CurrentCity = null; // поезд покинул станцию и не находится ни в одном городе

            SetState(TrainState.Moving);
            Debug.Log($"[Train] {trainData.displayName} отправлен: {route.DisplayName}. " +
                      $"Дистанция {route.distance}, скорость {trainData.baseSpeed}, в пути {TravelDuration:0.0} с");
            return true;
        }

        private void Update()
        {
            if (State != TrainState.Moving) return;

            elapsed += Time.deltaTime;
            Progress = TravelDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / TravelDuration);

            if (Progress >= 1f)
                Arrive();
        }

        private void Arrive()
        {
            RouteData finished = ActiveRoute;

            CurrentCity = finished.to;
            ActiveRoute = null;
            Progress = 1f;

            SetState(TrainState.Arrived);
            Debug.Log($"[Train] {trainData.displayName} прибыл в {CurrentCity.displayName}");
        }

        private void SetState(TrainState newState)
        {
            State = newState;
            StateChanged?.Invoke(this);
        }
    }
}
