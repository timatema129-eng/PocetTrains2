using System;
using System.Collections.Generic;
using TrainManager.Trains;

namespace TrainManager.Core
{
    /// <summary>
    /// Конкретный поезд игрока (экземпляр). Модель (скорость, вагоны...) — в TrainData по modelId.
    /// У игрока может быть несколько поездов одной модели: у каждого свой instanceId.
    /// </summary>
    [Serializable]
    public class TrainInstance
    {
        public string instanceId;

        /// <summary>trainId модели из TrainData.</summary>
        public string modelId;

        public TrainState state = TrainState.Idle;

        /// <summary>Город, где стоит поезд. Пока поезд в пути — пустая строка.</summary>
        public string currentCityId;

        /// <summary>Текущая поездка. Если поезд стоит — список городов пустой.</summary>
        public TripState trip = new TripState();

        // ---- Задел на следующие этапы ----
        public float fuel;
        public List<string> wagonIds = new List<string>();

        /// <summary>Состояние (износ) 0..100. Падает от километража, восстанавливается техобслуживанием.</summary>
        public float condition = 100f;

        /// <summary>Поломка: нет / лёгкая / сломан.</summary>
        public BreakdownState breakdown = BreakdownState.None;

        public bool IsMoving => state == TrainState.Moving && trip != null && trip.IsActive;
    }

    /// <summary>
    /// Поездка, записанная через реальное время: «отправлен тогда-то, в каждый город прибывает тогда-то».
    /// Поэтому поезд доезжает, даже если игра была закрыта.
    /// </summary>
    [Serializable]
    public class TripState
    {
        /// <summary>ID ассета маршрута, если поезд отправлен по готовому маршруту. Иначе пусто.</summary>
        public string routeId;

        /// <summary>Города маршрута по порядку (cityId).</summary>
        public List<string> stopIds = new List<string>();

        /// <summary>
        /// Время прибытия в каждый город (Unix-время, мс). [0] — время отправления из первого города.
        /// </summary>
        public List<long> stopTimesUnixMs = new List<long>();

        /// <summary>Индекс последнего города, до которого поезд уже доехал (0 — только отправился).</summary>
        public int reachedStopIndex;

        public bool IsActive => stopIds != null && stopTimesUnixMs != null &&
                                stopIds.Count >= 2 && stopIds.Count == stopTimesUnixMs.Count;

        /// <summary>
        /// Если поезд выехал с лёгкой поломкой и сломается в пути: на каком участке (индекс города, откуда он едет)
        /// и когда. -1 / 0 — доедет.
        /// </summary>
        public int failLegIndex = -1;
        public long failAtUnixMs;
        /// <summary>Доля участка, где поезд встал (для картинки на карте).</summary>
        public float failProgress;

        public bool WillFail => failLegIndex >= 0 && failAtUnixMs > 0;

        public int LastIndex => stopIds.Count - 1;

        public long DepartureUnixMs => stopTimesUnixMs[0];
        public long FinalArrivalUnixMs => stopTimesUnixMs[LastIndex];
    }
}
