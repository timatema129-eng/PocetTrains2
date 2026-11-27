using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;

namespace TrainManager.UI
{
    /// <summary>
    /// Окно «Боксы»: открыть бокс за доллары, посмотреть детали и собрать поезд.
    /// Открывается кнопкой «Боксы».
    /// </summary>
    public class BoxWindow : MonoBehaviour
    {
        public GameRoot root;
        public Button toggleButton;
        public RectTransform listContainer;
        public ListRow rowTemplate;

        private readonly List<ListRow> rows = new List<ListRow>();
        private readonly List<BoxConfig> rowBoxes = new List<BoxConfig>();
        private readonly List<TrainData> rowModels = new List<TrainData>();
        private string signature;
        private string lastDrop;

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
            sb.Append(root.Wallet.Dollars).Append('|').Append(root.Parts.Scrap).Append('|').Append(lastDrop);
            foreach (PartStack s in root.Parts.AllParts) sb.Append('|').Append(s.trainId).Append(':').Append(s.count);
            string sig = sb.ToString();
            if (sig == signature) return;
            signature = sig;
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (ListRow r in rows)
                if (r != null) Destroy(r.gameObject);
            rows.Clear();

            AddRow($"<b>Боксы</b> · {root.Wallet.Dollars} $ · лом {root.Parts.Scrap}");
            foreach (BoxConfig box in root.Database.boxes)
            {
                if (box == null) continue;
                BoxConfig captured = box;
                ListRow row = AddRow($"  {box.displayName} ({RarityName(box.rarity)})", $"Открыть {box.priceDollars} $",
                    () => Open(captured));
                row.button.interactable = root.Parts.CanOpenBox(box, out _);
            }
            if (!string.IsNullOrEmpty(lastDrop)) AddRow($"  <color=#ffd24a>{lastDrop}</color>");

            AddRow("<b>Детали поездов</b>");
            foreach (TrainData model in root.Database.trains)
            {
                if (model == null) continue;
                TrainData captured = model;
                ListRow row = AddRow(
                    $"  «{model.displayName}» ({RarityName(model.rarity)}) · деталей {root.Parts.GetParts(model.trainId)}/{model.partsRequiredForAssembly}",
                    "Собрать", () => root.Parts.TryAssemble(captured));
                row.button.interactable = root.Parts.CanAssemble(model, out _);
            }
            if (root.Database.startCity != null)
                AddRow($"  Собранный поезд появится в городе {root.Database.startCity.displayName}");
        }

        private void Open(BoxConfig box)
        {
            if (root.Parts.TryOpenBox(box, out TrainData dropped))
                lastDrop = $"Выпала деталь «{dropped.displayName}»: {root.Parts.GetParts(dropped.trainId)}/{dropped.partsRequiredForAssembly}";
        }

        private ListRow AddRow(string text, string buttonText = null, UnityEngine.Events.UnityAction onClick = null)
        {
            ListRow row = Instantiate(rowTemplate, listContainer);
            row.gameObject.SetActive(true);
            row.Set(text, buttonText, onClick);
            rows.Add(row);
            return row;
        }

        public static string RarityName(TrainRarity rarity)
        {
            switch (rarity)
            {
                case TrainRarity.Rare: return "редкий";
                case TrainRarity.Epic: return "эпический";
                default: return "обычный";
            }
        }
    }
}
