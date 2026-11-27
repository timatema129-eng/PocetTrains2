using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;
using TrainManager.Map;

namespace TrainManager.UI
{
    /// <summary>
    /// Окно «Задания»: активные задания-события (с таймером и наградой) и достижения с прогрессом.
    /// «Показать» наводит карту на город задания и открывает окно станции.
    /// </summary>
    public class QuestWindow : MonoBehaviour
    {
        public GameRoot root;
        public Button toggleButton;
        public RectTransform listContainer;
        public ListRow rowTemplate;
        public MapNavigator navigator;
        public StationWindow stationWindow;

        private readonly List<ListRow> rows = new List<ListRow>();
        private readonly List<QuestInstance> rowQuests = new List<QuestInstance>();
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
            foreach (QuestInstance q in root.Quests.Active) sb.Append(q.questId).Append(':').Append(q.progress).Append(',');
            sb.Append('|').Append(root.State.claimedAchievementIds.Count);
            string sig = sb.ToString();
            if (sig != signature)
            {
                signature = sig;
                Rebuild();
            }

            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            for (int i = 0; i < rows.Count; i++)
                if (rowQuests[i] != null && rows[i].label != null) rows[i].label.text = QuestText(rowQuests[i]);
        }

        private void Rebuild()
        {
            foreach (ListRow r in rows)
                if (r != null) Destroy(r.gameObject);
            rows.Clear();
            rowQuests.Clear();

            AddRow("<b>Задания</b>", null);
            if (root.Quests.Active.Count == 0) AddRow("  Сейчас заданий нет — новые появляются со временем", null);
            foreach (QuestInstance q in root.Quests.Active)
            {
                string cityId = q.cityId;
                ListRow row = AddRow(QuestText(q), q);
                row.Set(QuestText(q), "Показать", () => Show(cityId));
            }

            AddRow("<b>Достижения</b>", null);
            foreach (AchievementData a in root.Database.achievements)
            {
                if (a == null) continue;
                bool done = root.Achievements.IsClaimed(a);
                long value = System.Math.Min(root.Achievements.GetValue(a.stat), a.target);
                AddRow(done
                    ? $"  <color=#6fdc6f>✓ {a.displayName}</color> · {root.Achievements.DescribeReward(a)}"
                    : $"  {a.displayName}: {value}/{a.target} · награда {root.Achievements.DescribeReward(a)}", null);
            }
        }

        private string QuestText(QuestInstance q)
        {
            int s = Mathf.CeilToInt(root.Quests.GetSecondsLeft(q));
            return $"  <color=#ffd24a>!</color> {root.Quests.Describe(q)}\n" +
                   $"  осталось {s / 3600}:{s / 60 % 60:00}:{s % 60:00} · награда {root.Quests.DescribeReward(q)}";
        }

        private void Show(string cityId)
        {
            CityData city = root.Database.GetCity(cityId);
            if (city == null) return;
            if (navigator != null) navigator.CenterOn(city.mapPosition);
            if (stationWindow != null) stationWindow.Open(city);
            gameObject.SetActive(false);
        }

        private ListRow AddRow(string text, QuestInstance quest)
        {
            ListRow row = Instantiate(rowTemplate, listContainer);
            row.gameObject.SetActive(true);
            row.Set(text);
            rows.Add(row);
            rowQuests.Add(quest);
            return row;
        }
    }
}
