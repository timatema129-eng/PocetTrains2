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
    /// Временная панель вагонов (до окна станции на Этапе 4):
    /// что в составе, что на складе станции и кнопки «купить вагон в город X».
    /// Купленный вагон прицепляется к поезду, а если состав полон — ложится на склад станции.
    /// Кнопки создаются из шаблона buttonTemplate (неактивный объект с Button и TMP_Text внутри).
    /// </summary>
    public class WagonPanel : MonoBehaviour
    {
        public GameRoot root;
        public TMP_Text infoText;
        public RectTransform offersContainer;
        public Button buttonTemplate;

        [Tooltip("Какой по счёту поезд игрока (0 — первый)")]
        [Min(0)] public int trainIndex;

        private readonly List<Button> spawned = new List<Button>();
        private string shownCityId;
        private float nextRefresh;

        private void Start()
        {
            if (buttonTemplate != null) buttonTemplate.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (root == null || !root.IsReady) return;

            TrainInstance train = root.Trains.GetTrainByIndex(trainIndex);
            string cityId = train != null && !train.IsMoving ? train.currentCityId : null;

            if (cityId != shownCityId)
            {
                shownCityId = cityId;
                RebuildOffers(cityId);
            }

            // Обновлять текст и доступность кнопок несколько раз в секунду, а не каждый кадр
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;

            if (infoText != null) infoText.text = BuildInfo(train);
            RefreshButtons(train);
        }

        private void RebuildOffers(string cityId)
        {
            foreach (Button b in spawned)
                if (b != null) Destroy(b.gameObject);
            spawned.Clear();

            if (buttonTemplate == null || offersContainer == null) return;

            CityData city = root.Database.GetCity(cityId);
            if (city == null) return;

            foreach (WagonOffer offer in root.Wagons.GetOffers(city))
            {
                Button button = Instantiate(buttonTemplate, offersContainer);
                button.gameObject.SetActive(true);
                button.name = "Offer_" + offer.destination.cityId;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = $"Вагон → {offer.destination.displayName} ({offer.distanceKm:0} км)\n" +
                                 $"цена {offer.price} · доход {offer.income}";

                WagonOffer captured = offer;
                button.onClick.AddListener(() => Buy(captured));
                spawned.Add(button);
            }
        }

        private void Buy(WagonOffer offer)
        {
            TrainInstance train = root.Trains.GetTrainByIndex(trainIndex);
            root.Wagons.TryBuy(offer, train);
            nextRefresh = 0f;
        }

        private void RefreshButtons(TrainInstance train)
        {
            CityData city = root.Database.GetCity(shownCityId);
            if (city == null) return;

            List<WagonOffer> offers = root.Wagons.GetOffers(city);
            for (int i = 0; i < spawned.Count && i < offers.Count; i++)
                spawned[i].interactable = root.Wagons.CanBuy(offers[i], train, out _);
        }

        private string BuildInfo(TrainInstance train)
        {
            if (train == null) return "Нет поезда";

            var sb = new StringBuilder();
            sb.Append($"Состав: {root.Wagons.GetTrainUsedSlots(train)}/{root.Wagons.GetTrainCapacity(train)}\n");
            sb.Append(Describe(root.Wagons.GetTrainWagons(train)));

            if (train.IsMoving)
            {
                sb.Append("\nПоезд в пути — покупка вагонов в городе после прибытия");
                return sb.ToString();
            }

            string cityId = train.currentCityId;
            CityData city = root.Database.GetCity(cityId);
            sb.Append($"\n\nСклад станции {(city != null ? city.displayName : "?")}: " +
                      $"{root.Wagons.GetStationUsedSlots(cityId)}/{root.Wagons.GetStationCapacity(cityId)}\n");
            sb.Append(Describe(root.Wagons.GetStationWagons(cityId)));
            sb.Append("\n\nКупить вагон:");
            return sb.ToString();
        }

        /// <summary>«Берлин ×2, Амстердам ×1» или «пусто».</summary>
        private string Describe(List<WagonInstance> wagons)
        {
            if (wagons.Count == 0) return "пусто";

            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (WagonInstance w in wagons)
            {
                if (!counts.ContainsKey(w.destinationCityId))
                {
                    counts[w.destinationCityId] = 0;
                    order.Add(w.destinationCityId);
                }
                counts[w.destinationCityId]++;
            }

            var parts = new List<string>();
            foreach (string id in order)
            {
                CityData c = root.Database.GetCity(id);
                parts.Add($"{(c != null ? c.displayName : id)} ×{counts[id]}");
            }
            return string.Join(", ", parts);
        }
    }
}
