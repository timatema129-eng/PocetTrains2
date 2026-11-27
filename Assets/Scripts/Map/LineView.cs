using UnityEngine;
using UnityEngine.UI;
using TrainManager.Data;

namespace TrainManager.Map
{
    /// <summary>
    /// Линия на карте. Состоит из одной или нескольких картинок (сплошная, пунктир, две полосы).
    /// Цвета — из RailwayLineData; некупленная линия рисуется тусклой.
    /// </summary>
    public class LineView : MonoBehaviour
    {
        public RailwayLineData line;

        private Image[] images;
        private Color[] baseColors;
        private int ownedState = -1;

        private void Awake()
        {
            images = GetComponentsInChildren<Image>(true);
            baseColors = new Color[images.Length];
            for (int i = 0; i < images.Length; i++) baseColors[i] = images[i].color;
        }

        public void SetOwned(bool owned)
        {
            int newState = owned ? 1 : 0;
            if (images == null || newState == ownedState) return;
            ownedState = newState;

            for (int i = 0; i < images.Length; i++)
            {
                Color c = baseColors[i];
                images[i].color = owned ? c : new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 0.25f);
            }
        }
    }
}
