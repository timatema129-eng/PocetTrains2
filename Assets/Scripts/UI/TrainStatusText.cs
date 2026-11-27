using TMPro;
using UnityEngine;
using TrainManager.Core;
using TrainManager.Data;
using TrainManager.Trains;

namespace TrainManager.UI
{
    /// <summary>
    /// Текст со статусом поезда — чтобы всё было видно и проверяемо на экране.
    /// </summary>
    public class TrainStatusText : MonoBehaviour
    {
        public GameRoot root;
        public TMP_Text text;

        [Tooltip("Какой по счёту поезд игрока показывать (0 — первый)")]
        [Min(0)] public int trainIndex;

        private void Update()
        {
            if (root == null || text == null || !root.IsReady) return;

            TrainInstance train = root.Trains.GetTrainByIndex(trainIndex);
            if (train == null)
            {
                text.text = "Нет поезда";
                return;
            }

            string line;
            switch (train.state)
            {
                case TrainState.Moving:
                    root.Trains.GetPosition(train, out _, out string nextId, out _);
                    line = $"В пути: {root.Trains.DescribeStops(train.trip.stopIds)} — " +
                           $"{root.Trains.GetTripProgress(train) * 100f:0}%\n" +
                           $"Следующая остановка: {CityName(nextId)}, до конечной {root.Trains.GetSecondsLeft(train):0.0} с";
                    break;
                case TrainState.Arrived:
                    line = $"Прибыл: {CityName(train.currentCityId)}";
                    break;
                case TrainState.OutOfFuel:
                    line = "Нет топлива";
                    break;
                default:
                    line = $"Стоит: {CityName(train.currentCityId)}";
                    break;
            }

            text.text = $"Поезд «{root.Trains.GetDisplayName(train)}» ({train.instanceId})\n" +
                        $"Состояние: {train.state}, вагонов {root.Wagons.GetTrainUsedSlots(train)}/{root.Wagons.GetTrainCapacity(train)}\n{line}";
        }

        private string CityName(string cityId)
        {
            CityData city = root.Database.GetCity(cityId);
            return city != null ? city.displayName : "?";
        }
    }
}
