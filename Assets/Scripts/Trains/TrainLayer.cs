using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Map;
using TrainManager.UI;

namespace TrainManager.Trains
{
    /// <summary>
    /// Рисует на карте все поезда игрока: на каждый поезд — копия шаблона с TrainView.
    /// Выбранный поезд подсвечивается.
    /// </summary>
    public class TrainLayer : MonoBehaviour
    {
        public GameRoot root;
        public MapView map;
        public TrainSelection selection;
        public TrainView template;

        public Color normalColor = new Color(0.85f, 0.2f, 0.2f);
        public Color selectedColor = new Color(1f, 0.9f, 0.2f);

        private readonly Dictionary<string, TrainView> views = new Dictionary<string, TrainView>();

        private void Start()
        {
            if (template != null) template.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (root == null || !root.IsReady || template == null) return;

            // Новые поезда — новые картинки
            var trains = root.State.trains;
            for (int i = 0; i < trains.Count; i++)
            {
                TrainInstance train = trains[i];
                if (!views.TryGetValue(train.instanceId, out TrainView view))
                {
                    view = Instantiate(template, template.transform.parent);
                    view.name = "Train_" + train.instanceId;
                    view.root = root;
                    view.map = map;
                    view.trainId = train.instanceId;
                    view.gameObject.SetActive(true);
                    views.Add(train.instanceId, view);
                }
                view.offset = new Vector2(0f, (i % 4) * 12f);
            }

            // Удалённые поезда — убрать картинки
            var gone = new List<string>();
            foreach (var pair in views)
                if (root.State.FindTrain(pair.Key) == null) gone.Add(pair.Key);
            foreach (string id in gone)
            {
                Destroy(views[id].gameObject);
                views.Remove(id);
            }

            // Подсветка выбранного, он рисуется поверх остальных
            string selectedId = selection != null && selection.Selected != null ? selection.SelectedId : null;
            foreach (var pair in views)
            {
                Image image = pair.Value.GetComponent<Image>();
                bool isSelected = pair.Key == selectedId;
                if (image != null) image.color = isSelected ? selectedColor : normalColor;
                if (isSelected) pair.Value.transform.SetAsLastSibling();
            }
        }
    }
}
