using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;
using TrainManager.Map;

namespace TrainManager.UI
{
    /// <summary>
    /// Режим составления маршрута для одного поезда (включается кнопкой «Маршрут» в окне станции).
    /// Первый город — тот, где стоит поезд. Дальше можно нажимать только соседние города
    /// (с которыми есть линия) — они подсвечены зелёным. Повторное нажатие на последний город
    /// убирает его. «Отправить» — в путь, «Сбросить» — заново, «Отмена» — выйти из режима.
    /// Пока режим выключен, панель скрыта, а нажатия на станции открывают окно станции.
    /// </summary>
    public class RoutePlanner : MonoBehaviour
    {
        public GameRoot root;
        public MapView map;
        public TrainSelection selection;
        public TMP_Text planText;
        public Button sendButton;
        public Button clearButton;
        public Button cancelButton;

        private static readonly Color PlanColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color NextColor = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color TrainCityColor = new Color(1f, 0.55f, 0.2f);

        private readonly List<CityData> plan = new List<CityData>();
        private string trainId;

        public bool IsPlanning => gameObject.activeSelf && !string.IsNullOrEmpty(trainId);

        private void Awake()
        {
            if (sendButton != null) sendButton.onClick.AddListener(Send);
            if (clearButton != null) clearButton.onClick.AddListener(() => plan.Clear());
            if (cancelButton != null) cancelButton.onClick.AddListener(End);
        }

        /// <summary>Начать составлять маршрут для поезда.</summary>
        public void Begin(TrainInstance train)
        {
            if (train == null || train.IsMoving) return;

            trainId = train.instanceId;
            plan.Clear();
            if (selection != null) selection.Select(train);
            gameObject.SetActive(true);
        }

        /// <summary>Выйти из режима и снять подсветку.</summary>
        public void End()
        {
            trainId = null;
            plan.Clear();
            if (map != null)
                foreach (StationView station in map.Stations)
                    station.SetHighlight(StationView.NormalColor);
            gameObject.SetActive(false);
        }

        private TrainInstance Train => root != null && root.IsReady ? root.State.FindTrain(trainId) : null;

        /// <summary>Нажата станция (вызывает MapInteraction, когда режим включён).</summary>
        public void OnStationClicked(StationView station)
        {
            TrainInstance train = Train;
            if (train == null || train.IsMoving) return;

            CityData city = station.city;
            SyncStart(train);
            if (plan.Count == 0) return;

            if (plan.Count > 1 && plan[plan.Count - 1] == city)
            {
                plan.RemoveAt(plan.Count - 1);
                return;
            }

            if (root.Database.FindLine(plan[plan.Count - 1], city) != null)
                plan.Add(city);
            else
                Debug.Log($"[Route] Нет линии {plan[plan.Count - 1].displayName} — {city.displayName}");
        }

        private void Send()
        {
            TrainInstance train = Train;
            if (train != null && root.Trains.TryStartTrip(train, plan))
                End();
        }

        private void SyncStart(TrainInstance train)
        {
            CityData here = root.Database.GetCity(train.currentCityId);
            if (here == null)
            {
                plan.Clear();
                return;
            }
            if (plan.Count == 0 || plan[0] != here)
            {
                plan.Clear();
                plan.Add(here);
            }
        }

        private void Update()
        {
            TrainInstance train = Train;
            if (train == null || train.IsMoving)
            {
                End();
                return;
            }

            SyncStart(train);
            UpdateHighlights();

            if (sendButton != null) sendButton.interactable = root.Trains.CanStartTrip(train, plan, out _);
            if (clearButton != null) clearButton.interactable = plan.Count > 1;
            if (planText != null) planText.text = BuildText(train);
        }

        private void UpdateHighlights()
        {
            CityData last = plan.Count > 0 ? plan[plan.Count - 1] : null;

            foreach (StationView station in map.Stations)
            {
                Color color = StationView.NormalColor;
                if (plan.Count > 0 && station.city == plan[0]) color = TrainCityColor;
                else if (plan.Contains(station.city)) color = PlanColor;
                else if (last != null && root.Database.FindLine(last, station.city) != null) color = NextColor;
                station.SetHighlight(color);
            }
        }

        private string BuildText(TrainInstance train)
        {
            string who = $"Маршрут для «{root.Trains.GetDisplayName(train)}» ({train.instanceId})";
            if (plan.Count < 2) return who + "\nНажимай на зелёные города";

            var ids = new List<string>(plan.Count);
            foreach (CityData c in plan) ids.Add(c.cityId);

            float distance = root.Trains.GetTripDistance(plan);
            float seconds = root.Trains.GetTripSeconds(train, plan);
            float need = root.Fuel.GetTripFuel(train, distance);
            string fuelText = root.Fuel.HasFuelFor(train, need)
                ? $"топливо {need:0.#} из {train.fuel:0.#}"
                : $"<color=#ff6060>топлива не хватит: нужно {need:0.#}, есть {train.fuel:0.#}</color>";
            return $"{who}: {root.Trains.DescribeStops(ids)}\n" +
                   $"Дистанция {distance:0} км, в пути ~{seconds:0.0} с, {fuelText} (толчок {root.Fuel.GetPushFuel(train):0.#})";
        }
    }
}
