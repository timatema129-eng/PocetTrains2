using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;
using TrainManager.Map;
using TrainManager.Trains;

namespace TrainManager.UI
{
    /// <summary>
    /// Список всех поездов игрока: где каждый и что делает. «Показать» — выбрать поезд,
    /// навести на него карту и (если он стоит) открыть окно его станции.
    /// Открывается кнопкой «Поезда».
    /// </summary>
    public class TrainListWindow : MonoBehaviour
    {
        public GameRoot root;
        public TrainSelection selection;
        public MapNavigator navigator;
        public StationWindow stationWindow;
        public Button toggleButton;
        public RectTransform listContainer;
        public ListRow rowTemplate;

        private readonly List<ListRow> rows = new List<ListRow>();
        private readonly List<string> rowTrainIds = new List<string>();
        private string signature;
        private float nextRefresh;

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            signature = null;
        }

        private void Update()
        {
            if (root == null || !root.IsReady) return;

            var sb = new StringBuilder();
            foreach (TrainInstance t in root.State.trains) sb.Append(t.instanceId).Append(',');
            string sig = sb.ToString();
            if (sig != signature)
            {
                signature = sig;
                Rebuild();
            }

            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;

            for (int i = 0; i < rows.Count; i++)
            {
                TrainInstance train = root.State.FindTrain(rowTrainIds[i]);
                if (train != null && rows[i].label != null) rows[i].label.text = Describe(train);
            }
        }

        private void Rebuild()
        {
            foreach (ListRow r in rows)
                if (r != null) Destroy(r.gameObject);
            rows.Clear();
            rowTrainIds.Clear();

            foreach (TrainInstance train in root.State.trains)
            {
                string id = train.instanceId;
                ListRow row = Instantiate(rowTemplate, listContainer);
                row.gameObject.SetActive(true);
                row.Set(Describe(train), "Показать", () => Show(id));
                rows.Add(row);
                rowTrainIds.Add(id);
            }
        }

        private void Show(string trainId)
        {
            TrainInstance train = root.State.FindTrain(trainId);
            if (train == null) return;

            if (selection != null) selection.Select(train);

            root.Trains.GetPosition(train, out string fromId, out string toId, out float t);
            CityData from = root.Database.GetCity(fromId);
            CityData to = root.Database.GetCity(toId);
            if (navigator != null && from != null)
                navigator.CenterOn(to != null ? Vector2.Lerp(from.mapPosition, to.mapPosition, t) : from.mapPosition);

            if (!train.IsMoving && from != null && stationWindow != null)
                stationWindow.Open(from);

            gameObject.SetActive(false);
        }

        private string Describe(TrainInstance train)
        {
            string name = $"«{root.Trains.GetDisplayName(train)}» {train.instanceId}";
            string where;
            if (train.IsMoving)
            {
                root.Trains.GetPosition(train, out _, out string nextId, out _);
                CityData next = root.Database.GetCity(nextId);
                where = $"в пути → {(next != null ? next.displayName : "?")}, {root.Trains.GetTripProgress(train) * 100f:0}%";
            }
            else
            {
                CityData city = root.Database.GetCity(train.currentCityId);
                where = $"{(city != null ? city.displayName : "?")}, {(train.state == TrainState.OutOfFuel ? "нет топлива" : "стоит")}";
            }
            return $"{name}\n{where} · вагонов {root.Wagons.GetTrainUsedSlots(train)}/{root.Wagons.GetTrainCapacity(train)}";
        }
    }
}
