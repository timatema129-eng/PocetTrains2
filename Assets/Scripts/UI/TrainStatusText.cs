using TMPro;
using UnityEngine;
using TrainManager.Trains;

namespace TrainManager.UI
{
    /// <summary>
    /// Текст со статусом поезда — чтобы всё было видно и проверяемо на экране.
    /// </summary>
    public class TrainStatusText : MonoBehaviour
    {
        public TrainController train;
        public TMP_Text text;

        private void Update()
        {
            if (train == null || text == null) return;

            string trainName = train.trainData != null ? train.trainData.displayName : train.name;
            string line;

            switch (train.State)
            {
                case TrainState.Moving:
                    line = $"В пути: {train.ActiveRoute.DisplayName} — {train.Progress * 100f:0}% " +
                           $"(всего {train.TravelDuration:0.0} с)";
                    break;
                case TrainState.Arrived:
                    line = $"Прибыл: {CityName(train)}";
                    break;
                case TrainState.OutOfFuel:
                    line = "Нет топлива";
                    break;
                default:
                    line = $"Стоит: {CityName(train)}";
                    break;
            }

            text.text = $"Поезд «{trainName}»\nСостояние: {train.State}\n{line}";
        }

        private static string CityName(TrainController t)
        {
            return t.CurrentCity != null ? t.CurrentCity.displayName : "?";
        }
    }
}
