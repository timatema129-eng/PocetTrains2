using UnityEngine;
using UnityEngine.UI;
using TrainManager.Data;

namespace TrainManager.Map
{
    /// <summary>Линия на карте. Цвет — из RailwayLineData; некупленная линия рисуется тусклой.</summary>
    [RequireComponent(typeof(Image))]
    public class LineView : MonoBehaviour
    {
        public RailwayLineData line;

        private Image image;

        private void Awake()
        {
            image = GetComponent<Image>();
        }

        public void SetOwned(bool owned)
        {
            if (image == null || line == null) return;

            Color c = line.color;
            image.color = owned ? c : new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 0.25f);
        }
    }
}
