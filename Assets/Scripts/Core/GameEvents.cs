using System;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>
    /// Шина игровых событий. Системы сообщают, что произошло, а другие системы
    /// (сохранение, UI, позже достижения и задания) на это подписываются.
    /// Не static: один экземпляр живёт в GameRoot.
    /// </summary>
    public class GameEvents
    {
        /// <summary>Поезд отправился по маршруту.</summary>
        public event Action<TrainInstance, RouteData> TrainDeparted;

        /// <summary>
        /// Поезд доехал до города маршрута. isFinal = true — это конечная.
        /// Здесь позже будет автоматическая выгрузка вагонов.
        /// </summary>
        public event Action<TrainInstance, CityData, bool> TrainReachedStop;

        public void RaiseTrainDeparted(TrainInstance train, RouteData route) =>
            TrainDeparted?.Invoke(train, route);

        public void RaiseTrainReachedStop(TrainInstance train, CityData city, bool isFinal) =>
            TrainReachedStop?.Invoke(train, city, isFinal);
    }
}
