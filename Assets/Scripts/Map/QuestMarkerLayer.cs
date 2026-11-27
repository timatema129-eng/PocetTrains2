using System.Collections.Generic;
using UnityEngine;
using TrainManager.Core;

namespace TrainManager.Map
{
    /// <summary>Значки «!» над городами с активными заданиями. Копирует шаблон на каждое задание.</summary>
    public class QuestMarkerLayer : MonoBehaviour
    {
        public GameRoot root;
        public MapView map;
        public RectTransform template;
        public Vector2 offset = new Vector2(0f, 26f);

        private readonly Dictionary<string, RectTransform> markers = new Dictionary<string, RectTransform>();
        private float nextRefresh;

        private void Start()
        {
            if (template != null) template.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (root == null || !root.IsReady || template == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;

            var alive = new HashSet<string>();
            foreach (QuestInstance q in root.Quests.Active)
            {
                if (!map.TryGetStationPosition(q.cityId, out Vector2 pos)) continue;
                alive.Add(q.questId);
                if (!markers.TryGetValue(q.questId, out RectTransform marker))
                {
                    marker = Instantiate(template, template.parent);
                    marker.name = "Quest_" + q.questId;
                    marker.gameObject.SetActive(true);
                    markers.Add(q.questId, marker);
                }
                marker.anchoredPosition = pos + offset;
                marker.SetAsLastSibling();
            }

            var gone = new List<string>();
            foreach (var pair in markers)
                if (!alive.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (string id in gone)
            {
                Destroy(markers[id].gameObject);
                markers.Remove(id);
            }
        }
    }
}
