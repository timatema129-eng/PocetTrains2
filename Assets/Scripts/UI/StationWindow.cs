using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;

namespace TrainManager.UI
{
    /// <summary>
    /// Окно станции города. Открывается нажатием на станцию (через MapInteraction).
    /// Показывает: поезда в городе с переключателем ◀ ▶, состав выбранного поезда (отцепить),
    /// склад станции (прицепить, расширить за доллары), список вагонов на покупку, кнопку «Маршрут».
    /// </summary>
    public class StationWindow : MonoBehaviour
    {
        public GameRoot root;
        public TrainSelection selection;
        public RoutePlanner planner;

        [Header("UI")]
        public TMP_Text titleText;
        public Button closeButton;
        public Button prevTrainButton;
        public Button nextTrainButton;
        public TMP_Text trainLabel;
        public Button routeButton;
        public Button expandButton;
        public TMP_Text expandLabel;
        public Button refuelButton;
        public TMP_Text refuelLabel;
        public RectTransform listContainer;
        public ListRow rowTemplate;

        public CityData City { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        private readonly List<ListRow> rows = new List<ListRow>();
        private ListRow offersHeader;
        private string signature;
        private float nextRefresh;

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (prevTrainButton != null) prevTrainButton.onClick.AddListener(() => SwitchTrain(-1));
            if (nextTrainButton != null) nextTrainButton.onClick.AddListener(() => SwitchTrain(+1));
            if (routeButton != null) routeButton.onClick.AddListener(StartRoute);
            if (expandButton != null) expandButton.onClick.AddListener(Expand);
            if (refuelButton != null) refuelButton.onClick.AddListener(() => root.Fuel.TryRefuel(TrainHere()));
        }

        public void Open(CityData city)
        {
            if (city == null) return;

            City = city;
            signature = null;
            gameObject.SetActive(true);

            // Если выбранный поезд не здесь — выбрать первый поезд этого города
            List<TrainInstance> here = TrainsHere();
            TrainInstance selected = selection != null ? selection.Selected : null;
            if (selection != null && here.Count > 0 && (selected == null || !here.Contains(selected)))
                selection.Select(here[0]);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Поезда, которые стоят в этом городе.</summary>
        public List<TrainInstance> TrainsHere()
        {
            var result = new List<TrainInstance>();
            if (root == null || !root.IsReady || City == null) return result;

            foreach (TrainInstance t in root.State.trains)
                if (!t.IsMoving && t.currentCityId == City.cityId) result.Add(t);
            return result;
        }

        /// <summary>Выбранный поезд, если он стоит в этом городе; иначе null.</summary>
        private TrainInstance TrainHere()
        {
            TrainInstance selected = selection != null ? selection.Selected : null;
            return selected != null && !selected.IsMoving && City != null && selected.currentCityId == City.cityId
                ? selected : null;
        }

        private void SwitchTrain(int step)
        {
            List<TrainInstance> here = TrainsHere();
            if (here.Count == 0) return;

            int index = here.IndexOf(TrainHere());
            index = index < 0 ? 0 : (index + step + here.Count) % here.Count;
            selection.Select(here[index]);
        }

        private void StartRoute()
        {
            TrainInstance train = TrainHere();
            if (train == null || planner == null) return;

            Close();
            planner.Begin(train);
        }

        private void Expand()
        {
            if (City != null) root.Wagons.TryExpandStation(City);
        }

        private void Update()
        {
            if (root == null || !root.IsReady || City == null) return;

            string sig = BuildSignature();
            if (sig != signature)
            {
                signature = sig;
                Rebuild();
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.5f;
                RefreshLive();
            }
        }

        /// <summary>Строка, которая меняется, когда меняется содержимое окна — тогда список пересобирается.</summary>
        private string BuildSignature()
        {
            var sb = new StringBuilder();
            sb.Append(City.cityId).Append('|');
            TrainInstance train = TrainHere();
            sb.Append(train != null ? train.instanceId : "-").Append('|');
            if (train != null) sb.Append(string.Join(",", train.wagonIds)).Append('|');
            foreach (WagonInstance w in root.Wagons.GetStationWagons(City.cityId)) sb.Append(w.instanceId).Append(',');
            sb.Append('|').Append(root.Wagons.GetStationCapacity(City.cityId));
            sb.Append('|').Append(root.Wagons.CurrentOfferPeriod);
            foreach (WagonOffer o in root.Wagons.GetOffers(City)) sb.Append(o.slot).Append(',');
            sb.Append('|').Append(TrainsHere().Count);
            sb.Append('|').Append(root.Wallet.Coins); // доступность служебных вагонов зависит от денег
            sb.Append('|').Append(root.State.unlockedRegionIds.Count).Append('|').Append(root.State.ownedLineIds.Count);
            return sb.ToString();
        }

        private void Rebuild()
        {
            foreach (ListRow r in rows)
                if (r != null) Destroy(r.gameObject);
            rows.Clear();
            offersHeader = null;

            if (titleText != null) titleText.text = $"Станция {City.displayName}";

            TrainInstance train = TrainHere();

            // ---- Регион ----
            if (!root.Network.IsCityAccessible(City))
            {
                RegionData region = City.region;
                ListRow regionRow = AddRow($"<b>Регион «{region.displayName}» закрыт</b>", $"Открыть {region.priceCoins}",
                    () => root.Network.TryUnlockRegion(region));
                regionRow.region = region;
            }

            // ---- Состав выбранного поезда ----
            if (train != null)
            {
                AddRow($"<b>Состав: {root.Wagons.GetTrainUsedSlots(train)}/{root.Wagons.GetTrainCapacity(train)}</b>");
                List<WagonInstance> cargo = root.Wagons.GetTrainWagons(train);
                if (cargo.Count == 0) AddRow("  пусто");
                foreach (WagonInstance w in cargo)
                {
                    WagonInstance captured = w;
                    AddRow($"  {DescribeWagon(w)}", "Отцепить", () => root.Wagons.TryDetach(TrainHere(), captured)).wagon = w;
                }
            }

            // ---- Служебные вагоны ----
            if (train != null)
            {
                TrainData model = root.Trains.GetModel(train);
                AddRow($"<b>Служебные вагоны</b> (топливных {root.Fuel.CountWagons(train, WagonKind.Fuel)}/{(model != null ? model.maxFuelWagons : 0)}, " +
                       $"двигателей {root.Fuel.CountWagons(train, WagonKind.Engine)}/{(model != null ? model.maxExtraEngines : 0)})");
                AddServiceRow(WagonKind.Fuel, train, model != null ? $"+{model.fuelPerFuelWagon} к запасу топлива" : "");
                AddServiceRow(WagonKind.Engine, train, model != null ? $"+{model.wagonsPerExtraEngine} к лимиту вагонов, занимает 1 место" : "");
            }

            // ---- Склад станции ----
            AddRow($"<b>Склад станции: {root.Wagons.GetStationUsedSlots(City.cityId)}/{root.Wagons.GetStationCapacity(City.cityId)}</b>");
            List<WagonInstance> stored = root.Wagons.GetStationWagons(City.cityId);
            if (stored.Count == 0) AddRow("  пусто");
            foreach (WagonInstance w in stored)
            {
                WagonInstance captured = w;
                AddRow($"  {DescribeWagon(w)}", train != null ? "Прицепить" : null,
                    () => root.Wagons.TryAttach(TrainHere(), captured)).wagon = w;
            }

            // ---- Дороги из города ----
            AddRow("<b>Дороги из города</b>");
            foreach (RailwayLineData line in root.Database.GetLines(City))
            {
                CityData other = line.cityA == City ? line.cityB : line.cityA;
                string text = $"  — {other.displayName} · {line.distance:0} км";
                if (root.Network.IsLineOwned(line))
                {
                    AddRow(text + " · куплена");
                }
                else if (!root.Network.CanBuyLine(line, out string why) && why.StartsWith("нужен регион"))
                {
                    AddRow($"{text} · {why}");
                }
                else
                {
                    RailwayLineData captured = line;
                    AddRow(text, $"Купить {root.Network.GetLinePrice(line)}",
                        () => root.Network.TryBuyLine(captured)).line = line;
                }
            }

            // ---- Покупка ----
            offersHeader = AddRow("<b>Купить вагон</b>");
            List<WagonOffer> offers = root.Wagons.GetOffers(City);
            if (offers.Count == 0) AddRow("  всё раскуплено — ждите обновления");
            foreach (WagonOffer offer in offers)
            {
                WagonOffer captured = offer;
                AddRow($"  → {offer.destination.displayName} ({offer.distanceKm:0} км) · доход {offer.income}",
                    $"Купить {offer.price}", () => root.Wagons.TryBuy(captured, TrainHere()));
            }

            RefreshLive();
        }

        /// <summary>То, что меняется часто: подписи, таймер, доступность кнопок.</summary>
        private void RefreshLive()
        {
            List<TrainInstance> here = TrainsHere();
            TrainInstance train = TrainHere();

            if (trainLabel != null)
            {
                trainLabel.text = train != null
                    ? $"Поезд {here.IndexOf(train) + 1}/{here.Count}: «{root.Trains.GetDisplayName(train)}» ({train.instanceId})"
                    : "Поездов на станции нет";
            }
            if (prevTrainButton != null) prevTrainButton.interactable = here.Count > 1;
            if (nextTrainButton != null) nextTrainButton.interactable = here.Count > 1;
            if (routeButton != null) routeButton.interactable = train != null;

            if (refuelButton != null)
            {
                float amount = 0f;
                long price = 0;
                if (train != null) root.Fuel.GetRefuelQuote(train, out amount, out price);
                refuelButton.interactable = train != null && amount > 0.01f && root.Wallet.Coins > 0;
                if (refuelLabel != null)
                    refuelLabel.text = train != null
                        ? $"Топливо {train.fuel:0.#}/{root.Fuel.GetCapacity(train):0.#} · заправить за {price}"
                        : "Заправка";
            }

            long expandPrice = root.Wagons.GetExpandPrice(City.cityId);
            if (expandLabel != null) expandLabel.text = $"+{root.Database.stationExpandSlots} мест за {expandPrice} $";
            if (expandButton != null) expandButton.interactable = root.Wallet.CanAfford(Currency.Dollars, expandPrice);

            if (offersHeader != null)
            {
                int s = Mathf.CeilToInt(root.Wagons.SecondsToOfferRefresh);
                offersHeader.Set($"<b>Купить вагон</b>  (новые через {s / 60}:{s % 60:00})");
            }

            // Доступность кнопок «Купить» и «Прицепить»
            List<WagonOffer> offers = root.Wagons.GetOffers(City);
            int offerIndex = 0;
            foreach (ListRow row in rows)
            {
                if (row == null || row.button == null || !row.button.gameObject.activeSelf || row.buttonLabel == null) continue;

                string b = row.buttonLabel.text;
                if (row.serviceType == null && row.line == null && b.StartsWith("Купить") && offerIndex < offers.Count)
                    row.button.interactable = root.Wagons.CanBuy(offers[offerIndex++], train, out _);
                else if (b == "Прицепить" && row.wagon != null)
                    row.button.interactable = root.Wagons.CanTrainTake(train, root.Wagons.GetWagonType(row.wagon), City.cityId, out _);
                else if (b == "Отцепить" && row.wagon != null)
                    row.button.interactable = root.Wagons.GetStationUsedSlots(City.cityId) <
                                              root.Wagons.GetStationCapacity(City.cityId) &&
                                              root.Wagons.CanDetach(train, row.wagon, out _);
                else if (row.serviceType != null)
                    row.button.interactable = root.Wagons.CanBuyService(row.serviceType, train, out _);
                else if (row.line != null)
                    row.button.interactable = root.Network.CanBuyLine(row.line, out _);
                else if (row.region != null)
                    row.button.interactable = root.Network.CanUnlockRegion(row.region, out _);
            }
        }

        private ListRow AddRow(string text, string buttonText = null, UnityEngine.Events.UnityAction onClick = null)
        {
            ListRow row = Instantiate(rowTemplate, listContainer);
            row.gameObject.SetActive(true);
            row.Set(text, buttonText, onClick);
            rows.Add(row);
            return row;
        }

        private void AddServiceRow(WagonKind kind, TrainInstance train, string effect)
        {
            WagonData type = root.Database.FirstWagonOfKind(kind);
            if (type == null) return;

            ListRow row = AddRow($"  {type.displayName}: {effect}", $"Купить {type.fixedPriceCoins}",
                () => root.Wagons.TryBuyService(type, TrainHere()));
            row.serviceType = type;
        }

        private string DescribeWagon(WagonInstance w)
        {
            WagonData type = root.Wagons.GetWagonType(w);
            if (type != null && type.kind != WagonKind.Cargo) return type.displayName;
            return $"→ {CityName(w.destinationCityId)} · доход {w.incomeCoins}";
        }

        private string CityName(string cityId)
        {
            CityData c = root.Database.GetCity(cityId);
            return c != null ? c.displayName : cityId;
        }
    }
}
