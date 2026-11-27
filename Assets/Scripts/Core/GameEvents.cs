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
        /// <summary>Поезд отправился. trip — куда и когда прибывает в каждый город.</summary>
        public event Action<TrainInstance, TripState> TrainDeparted;

        /// <summary>
        /// Поезд доехал до города маршрута. isFinal = true — это конечная.
        /// Здесь позже будет автоматическая выгрузка вагонов.
        /// </summary>
        public event Action<TrainInstance, CityData, bool> TrainReachedStop;

        /// <summary>Баланс изменился: валюта, новый баланс, изменение (+/-), причина.</summary>
        public event Action<Currency, long, long, string> WalletChanged;

        /// <summary>Вагон куплен (уже в поезде или на складе станции).</summary>
        public event Action<WagonInstance> WagonBought;

        /// <summary>Вагон доставлен в город назначения: вагон, город, полученные монеты.</summary>
        public event Action<WagonInstance, CityData, long> WagonDelivered;

        /// <summary>Вагон перемещён между поездом и складом станции.</summary>
        public event Action<WagonInstance> WagonMoved;

        /// <summary>Изменилась станция (например, расширен склад).</summary>
        public event Action<CityData> StationChanged;

        public void RaiseStationChanged(CityData city) => StationChanged?.Invoke(city);

        /// <summary>Открыт регион (для достижений: «открыл регион → доллары»).</summary>
        public event Action<RegionData> RegionUnlocked;

        /// <summary>Куплена линия.</summary>
        public event Action<RailwayLineData> LineBought;

        public void RaiseRegionUnlocked(RegionData region) => RegionUnlocked?.Invoke(region);
        public void RaiseLineBought(RailwayLineData line) => LineBought?.Invoke(line);

        /// <summary>Изменились детали или лом (бокс, сборка, разборка).</summary>
        public event Action PartsChanged;
        /// <summary>Собран новый поезд.</summary>
        public event Action<TrainInstance> TrainAssembled;
        /// <summary>Поезд разобран (уже удалён из GameState).</summary>
        public event Action<TrainInstance> TrainDisassembled;

        public void RaisePartsChanged() => PartsChanged?.Invoke();
        public void RaiseTrainAssembled(TrainInstance train) => TrainAssembled?.Invoke(train);
        public void RaiseTrainDisassembled(TrainInstance train) => TrainDisassembled?.Invoke(train);

        /// <summary>Изменилось состояние поезда: износ, поломка, встал на линии, отбуксирован.</summary>
        public event Action<TrainInstance> TrainConditionChanged;

        public void RaiseTrainConditionChanged(TrainInstance train) => TrainConditionChanged?.Invoke(train);

        /// <summary>Задания изменились: появилось, прогресс, выполнено, истекло.</summary>
        public event Action QuestsChanged;
        /// <summary>Получено достижение.</summary>
        public event Action<AchievementData> AchievementUnlocked;

        public void RaiseQuestsChanged() => QuestsChanged?.Invoke();
        public void RaiseAchievementUnlocked(AchievementData a) => AchievementUnlocked?.Invoke(a);

        /// <summary>У линии изменились пути или цвет.</summary>
        public event Action<RailwayLineData> LineChanged;

        public void RaiseLineChanged(RailwayLineData line) => LineChanged?.Invoke(line);

        public void RaiseWalletChanged(Currency currency, long balance, long delta, string reason) =>
            WalletChanged?.Invoke(currency, balance, delta, reason);

        public void RaiseWagonBought(WagonInstance wagon) => WagonBought?.Invoke(wagon);

        public void RaiseWagonDelivered(WagonInstance wagon, CityData city, long coins) =>
            WagonDelivered?.Invoke(wagon, city, coins);

        public void RaiseWagonMoved(WagonInstance wagon) => WagonMoved?.Invoke(wagon);

        public void RaiseTrainDeparted(TrainInstance train, TripState trip) =>
            TrainDeparted?.Invoke(train, trip);

        public void RaiseTrainReachedStop(TrainInstance train, CityData city, bool isFinal) =>
            TrainReachedStop?.Invoke(train, city, isFinal);
    }
}
