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

        public static readonly Color UnownedColor = new Color(0.12f, 0.12f, 0.16f, 0.5f);

        private Image[] images;
        private Color[] baseColors;
        private bool[] isPrimary;

        private bool hasState;
        private bool lastOwned;
        private bool lastElectrified;
        private Vector3 baseScale;
        private Color lastPrimary;

        private void Awake()
        {
            baseScale = transform.localScale;
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
        public void Apply(bool owned, Color primary, bool electrified = false)
        {
            if (images == null) return;
            if (hasState && owned == lastOwned && electrified == lastElectrified && SameColor(primary, lastPrimary)) return;
            hasState = true;
            lastOwned = owned;
            lastElectrified = electrified;
            lastPrimary = primary;
            // Электрифицированная линия рисуется толще (линия повёрнута — толщина по локальной оси Y)
            transform.localScale = electrified ? new Vector3(baseScale.x, baseScale.y * 1.8f, baseScale.z) : baseScale;

            for (int i = 0; i < images.Length; i++)
            {
                Color c = isPrimary[i] ? new Color(primary.r, primary.g, primary.b, baseColors[i].a) : baseColors[i];
                // Некупленная — тусклая тёмная полупрозрачная (видна и на суше, и на море), купленная — своим цветом
                images[i].color = owned ? c : UnownedColor;
            }
        }

        public void SetOwned(bool owned) => Apply(owned, line != null ? line.color : Color.white);

        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    }
}
