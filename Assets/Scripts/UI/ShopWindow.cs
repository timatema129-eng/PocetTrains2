using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;

namespace TrainManager.UI
{
    /// <summary>
    /// Магазин: курс и обмен $ → монеты, боксы, место под покупки за реальные деньги (заглушка).
    /// Открывается кнопкой «Магазин». Расширение склада — в окне станции (оно своё у каждой станции).
    /// </summary>
    public class ShopWindow : MonoBehaviour
    {
        public GameRoot root;
        public Button toggleButton;
        public RectTransform listContainer;
        public ListRow rowTemplate;

        [Tooltip("Сколько долларов меняет каждая кнопка обмена (0 — «все»)")]
        public int[] exchangeAmounts = { 1, 5, 10, 0 };

        private readonly List<ListRow> rows = new List<ListRow>();
        private readonly List<int> rowAmounts = new List<int>();
        private readonly List<BoxConfig> rowBoxes = new List<BoxConfig>();
        private ListRow rateRow;
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
            if (gameObject.activeSelf && root != null && root.IsReady) Rebuild();
        }

        private void Update()
        {
            if (root == null || !root.IsReady) return;
            if (rows.Count == 0) Rebuild();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            RefreshLive();
        }

        private void Rebuild()
        {
            foreach (ListRow r in rows)
                if (r != null) Destroy(r.gameObject);
            rows.Clear();
            rowAmounts.Clear();
            rowBoxes.Clear();

            rateRow = AddRow("");
            AddRow("<b>Обмен $ → монеты</b> (обратно нельзя)");
            foreach (int amount in exchangeAmounts)
            {
                int captured = amount;
                ListRow row = AddRow("", "Обменять", () => root.Exchange.TryExchange(Amount(captured)));
                rowAmounts.Add(amount);
                rowBoxes.Add(null);
                row.action = "exchange";
            }

            AddRow("<b>Боксы</b> (детали и сборка — в окне «Боксы»)");
            foreach (BoxConfig box in root.Database.boxes)
            {
                if (box == null) continue;
                BoxConfig captured = box;
                ListRow row = AddRow($"  {box.displayName}", $"Открыть {box.priceDollars} $",
                    () => root.Parts.TryOpenBox(captured, out _));
                rowAmounts.Add(0);
                rowBoxes.Add(box);
                row.action = "box";
            }

            AddRow("<b>Покупки за реальные деньги</b>");
            ListRow iap = AddRow("  Скоро", "—");
            iap.button.interactable = false;

            RefreshLive();
        }

        private long Amount(int amount) => amount > 0 ? amount : root.Wallet.Dollars;

        private void RefreshLive()
        {
            ExchangeService ex = root.Exchange;
            int s = Mathf.CeilToInt(ex.SecondsToNextStep);
            if (rateRow != null && rateRow.label != null)
                rateRow.label.text =
                    $"<b>Курс: 1 $ = {ex.CurrentRate} монет</b> {(ex.IsRising ? "<color=#6fdc6f>▲ растёт</color>" : "<color=#ff6060>▼ падает</color>")}\n" +
                    $"  день {ex.CycleDay} из {root.Database.exchangeCycleDays}, следующее изменение через {s / 3600}:{s / 60 % 60:00}:{s % 60:00} · у тебя {root.Wallet.Dollars} $";

            int k = 0;
            foreach (ListRow row in rows)
            {
                if (row.action == "exchange")
                {
                    long dollars = Amount(rowAmounts[k]);
                    if (row.label != null)
                        row.label.text = rowAmounts[k] > 0
                            ? $"  {dollars} $ → {dollars * ex.CurrentRate} монет"
                            : $"  Все {dollars} $ → {dollars * ex.CurrentRate} монет";
                    row.button.interactable = ex.CanExchange(dollars, out _);
                    k++;
                }
                else if (row.action == "box")
                {
                    row.button.interactable = root.Parts.CanOpenBox(rowBoxes[k], out _);
                    k++;
                }
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
