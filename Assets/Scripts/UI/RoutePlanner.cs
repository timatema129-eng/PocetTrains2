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
    /// Составление маршрута нажатием на города.
    /// Первый город — тот, где стоит поезд. Дальше можно нажимать только соседние города
    /// (с которыми есть линия) — они подсвечены зелёным. Повторное нажатие на последний город
    /// убирает его из маршрута. Кнопка «Отправить» — в путь, «Сбросить» — начать заново.
    /// </summary>
    public class RoutePlanner : MonoBehaviour
    {
        public GameRoot root;
        public MapView map;
        public TMP_Text planText;
        public Button sendButton;
        public Button clearButton;

        [Tooltip("Какой по счёту поезд игрока отправлять (0 — первый)")]
        [Min(0)] public int trainIndex;

        private static readonly Color PlanColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color NextColor = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color TrainCityColor = new Color(1f, 0.55f, 0.2f);

        private readonly List<CityData> plan = new List<CityData>();

        private void Start()
        {
            if (root == null || map == null)
            {
                Debug.LogError($"[RoutePlanner] {name}: не назначены Root или Map", this);
                enabled = false;
                return;
            }

            foreach (StationView station in map.Stations)
                station.Clicked += OnStationClicked;

            if (sendButton != null) sendButton.onClick.AddListener(Send);
            if (clearButton != null) clearButton.onClick.AddListener(Clear);
        }

        private TrainInstance Train => root.IsReady ? root.Trains.GetTrainByIndex(trainIndex) : null;

        private void OnStationClicked(StationView station)
        {
            TrainInstance train = Train;
            if (train == null || train.IsMoving) return;

            CityData city = station.city;
            SyncStart(train);
            if (plan.Count == 0) return;

            // Повторное нажатие на последний город — убрать его
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
            if (train == null) return;

            if (root.Trains.TryStartTrip(train, plan))
                plan.Clear();
        }

        private void Clear()
        {
            plan.Clear();
        }

        /// <summary>Маршрут всегда начинается с города, где стоит поезд.</summary>
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
            bool canPlan = train != null && !train.IsMoving;
            if (canPlan) SyncStart(train);
            else plan.Clear();

            UpdateHighlights(canPlan);

            if (sendButton != null)
                sendButton.interactable = canPlan && root.Trains.CanStartTrip(train, plan, out _);
            if (clearButton != null)
                clearButton.interactable = canPlan && plan.Count > 1;

            if (planText != null)
                planText.text = BuildText(train, canPlan);
        }

        private void UpdateHighlights(bool canPlan)
        {
            CityData last = plan.Count > 0 ? plan[plan.Count - 1] : null;

            foreach (StationView station in map.Stations)
            {
                Color color = StationView.NormalColor;
                if (canPlan)
                {
                    if (plan.Count > 0 && station.city == plan[0]) color = TrainCityColor;
                    else if (plan.Contains(station.city)) color = PlanColor;
                    else if (last != null && root.Database.FindLine(last, station.city) != null) color = NextColor;
                }
                station.SetHighlight(color);
            }
        }

        private string BuildText(TrainInstance train, bool canPlan)
        {
            if (train == null) return "";
            if (!canPlan) return "Поезд в пути";
            if (plan.Count < 2) return "Нажимай на зелёные города, чтобы составить маршрут";

            var ids = new List<string>(plan.Count);
            foreach (CityData c in plan) ids.Add(c.cityId);

            float distance = root.Trains.GetTripDistance(plan);
            float seconds = root.Trains.GetTripSeconds(train, plan);
            return $"Маршрут: {root.Trains.DescribeStops(ids)}\nДистанция {distance:0} км, в пути ~{seconds:0.0} с";
        }
    }
}
