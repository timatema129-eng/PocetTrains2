using UnityEngine;
using UnityEngine.UI;
using TrainManager.Data;

namespace TrainManager.Map
{
    /// <summary>
    /// Линия на карте. Состоит из одной или нескольких картинок (сплошная, пунктир, две полосы).
    /// Цвета по умолчанию — из RailwayLineData; игрок может выбрать свой основной цвет.
    /// Некупленная линия рисуется тусклой.
    /// </summary>
    public class LineView : MonoBehaviour
    {
        public RailwayLineData line;

        private Image[] images;
        private Color[] baseColors;
        private bool[] isPrimary;

        private bool hasState;
        private bool lastOwned;
        private Color lastPrimary;

        private void Awake()
        {
            images = GetComponentsInChildren<Image>(true);
            baseColors = new Color[images.Length];
            isPrimary = new bool[images.Length];
            for (int i = 0; i < images.Length; i++)
            {
                baseColors[i] = images[i].color;
                // Основные полосы — те, что нарисованы основным цветом линии (вторая полоса двухцветной — нет)
                isPrimary[i] = line == null || SameColor(baseColors[i], line.color) ||
                               line.pattern != LinePattern.TwoTone;
            }
        }

        /// <summary>Обновить вид. primary — основной цвет (по умолчанию или выбранный игроком).</summary>
        public void Apply(bool owned, Color primary)
        {
            if (images == null) return;
            if (hasState && owned == lastOwned && SameColor(primary, lastPrimary)) return;
            hasState = true;
            lastOwned = owned;
            lastPrimary = primary;

            for (int i = 0; i < images.Length; i++)
            {
                Color c = isPrimary[i] ? new Color(primary.r, primary.g, primary.b, baseColors[i].a) : baseColors[i];
                images[i].color = owned ? c : new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 0.25f);
            }
        }

        public void SetOwned(bool owned) => Apply(owned, line != null ? line.color : Color.white);

        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    }
}
