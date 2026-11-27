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
    /// Окно метро города: схема веток (кружки — станции), открытие станций и веток,
    /// касса с пассивным доходом, ТО и выпуск новых поездов. Открывается из окна станции крупного города.
    /// </summary>
    public class MetroWindow : MonoBehaviour
    {
        public GameRoot root;
        public TMP_Text titleText;
        public Button closeButton;
        public RectTransform listContainer;
        public ListRow rowTemplate;

        /// <summary>Цвета веток на схеме.</summary>
        private static readonly string[] BranchColors = { "#e74c3c", "#3498db", "#2ecc71", "#f1c40f", "#9b59b6", "#e67e22", "#1abc9c", "#ec407a" };

        private readonly List<ListRow> rows = new List<ListRow>();
        private CityData city;
        private string signature;
        private float nextRefresh;

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            gameObject.SetActive(false);
        }

        public void Open(CityData target)
        {
            city = target;
            signature = null;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Update()
        {
            if (root == null || !root.IsReady || city == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;

            MetroState m = root.Metro.Get(city);
            var sb = new StringBuilder();
            sb.Append(root.Wallet.Coins);
            if (m != null)
            {
                sb.Append('|').Append(string.Join(",", m.branches)).Append('|').Append(Mathf.FloorToInt(m.condition))
                  .Append('|').Append((long)m.pendingCoins);
            }
            string sig = sb.ToString();
            if (sig == signature) return;
            signature = sig;
            Rebuild(m);
        }

        private void Rebuild(MetroState m)
        {
            foreach (ListRow r in rows)
                if (r != null) Destroy(r.gameObject);
            rows.Clear();

            MetroTier tier = root.Database.GetMetroTier(city);
            if (titleText != null) titleText.text = $"Метро · {city.displayName}" + (tier != null ? $" ({tier.displayName})" : "");

            if (tier == null)
            {
                AddRow("Метро бывает только в крупных городах");
                return;
            }

            if (m == null)
            {
                AddRow($"Метро — пассивный доход, отдельно от перевозок. Здесь можно открыть до {tier.maxBranches} веток " +
                       $"по {tier.maxStationsPerBranch} станций.");
                ListRow build = AddRow("<b>Построить метро</b> (первая ветка, 1 станция)", $"Построить {root.Database.metroBuildCoins}",
                    () => root.Metro.TryBuild(city));
                build.button.interactable = root.Metro.CanBuild(city, out _);
                return;
            }

            float perHour = root.Metro.GetIncomePerHour(m);
            AddRow($"<b>Доход {perHour:0} монет/ч</b> пока игра открыта, {perHour * root.Database.metroOfflineShare:0}/ч пока закрыта " +
                   $"(станций {root.Metro.TotalStations(m)})");
            ListRow cash = AddRow($"  Касса: {(long)m.pendingCoins} монет", "Забрать", () => root.Metro.TryCollect(city));
            cash.button.interactable = m.pendingCoins >= 1.0;

            if (m.condition > 0f)
            {
                long price = root.Metro.GetMaintenancePrice(m);
                ListRow to = AddRow($"  Поезда метро: {m.condition:0}% (изнашиваются вдвое медленнее обычных)",
                    $"ТО {price}", () => root.Metro.TryMaintain(city));
                to.button.interactable = price > 0 && root.Wallet.CanAfford(Currency.Coins, price);
            }
            else
            {
                long price = root.Metro.GetRenewPrice(m);
                ListRow renew = AddRow("  <color=#ff6060>Поезда метро износились — метро стоит</color>",
                    $"Новые поезда {price}", () => root.Metro.TryRenew(city));
                renew.button.interactable = root.Wallet.CanAfford(Currency.Coins, price);
            }

            AddRow("<b>Схема</b>");
            long stationPrice = root.Metro.GetStationPrice(m);
            for (int b = 0; b < m.branches.Count; b++)
            {
                int branch = b;
                string color = BranchColors[b % BranchColors.Length];
                var scheme = new StringBuilder();
                for (int s = 0; s < tier.maxStationsPerBranch; s++)
                    scheme.Append(s < m.branches[b] ? $"<color={color}>●</color>" : "<color=#555>○</color>").Append(s < tier.maxStationsPerBranch - 1 ? "─" : "");
                ListRow row = AddRow($"  <color={color}>Ветка {b + 1}</color>  {scheme}",
                    m.branches[b] < tier.maxStationsPerBranch ? $"+станция {stationPrice}" : "Ветка готова",
                    () => root.Metro.TryAddStation(city, branch));
                row.button.interactable = root.Metro.CanAddStation(city, branch, out _);
            }
            if (m.branches.Count < tier.maxBranches)
            {
                ListRow br = AddRow($"  Новая ветка ({m.branches.Count}/{tier.maxBranches})", $"Ветка {root.Metro.GetBranchPrice(m)}",
                    () => root.Metro.TryAddBranch(city));
                br.button.interactable = root.Metro.CanAddBranch(city, out _);
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
    }
}
