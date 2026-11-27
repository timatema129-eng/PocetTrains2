using UnityEngine;

namespace TrainManager.Map
{
    /// <summary>
    /// Точки станций и названия держат постоянный размер на экране (до масштаба FullSizeZoom; дальше уменьшаются вместе с картой).
    /// Прячет названия городов, когда карта отдалена: у каждой станции свой порог (StationView.labelMinZoom),
    /// его считает сборщик сцены так, чтобы видимые надписи не налезали друг на друга.
    /// </summary>
    public class StationLabelZoom : MonoBehaviour
    {
        public MapView map;
        public RectTransform content;

        /// <summary>Ниже этого масштаба точки и надписи уменьшаются вместе с картой.</summary>
        public const float FullSizeZoom = 0.6f;

        /// <summary>Во сколько раз точка/надпись на экране меньше обычного при масштабе карты zoom.</summary>
        public static float ScreenFactor(float zoom) => Mathf.Min(1f, zoom / FullSizeZoom);

        /// <summary>
        /// Сдвиг центра надписи от центра точки в пикселях экрана. side: 0 снизу, 1 сверху, 2 справа, 3 слева.
        /// k — ScreenFactor (точка меньше при отдалении, надпись — нет).
        /// </summary>
        public static Vector2 LabelOffset(int side, Vector2 labelSize, float dotSize, float k)
        {
            float d = dotSize * k / 2f;
            switch (side)
            {
                case 1: return new Vector2(0f, d + labelSize.y / 2f);
                case 2: return new Vector2(d + labelSize.x / 2f + 2f, 0f);
                case 3: return new Vector2(-(d + labelSize.x / 2f + 2f), 0f);
                default: return new Vector2(0f, -(d + labelSize.y / 2f));
            }
        }

        private float lastScale = -1f;

        private void LateUpdate()
        {
            if (map == null || content == null) return;
            float scale = content.localScale.x;
            if (Mathf.Abs(scale - lastScale) < 0.001f) return;
            lastScale = scale;

            float stationScale = scale > 0.0001f ? ScreenFactor(scale) / scale : 1f;
            foreach (StationView station in map.Stations)
            {
                if (station == null) continue;
                station.transform.localScale = new Vector3(stationScale, stationScale, 1f);
                if (station.label == null) continue;

                bool show = scale >= station.labelMinZoom;
                station.label.gameObject.SetActive(show);
                if (!show) continue;
                // Надпись — постоянного размера на экране, сбоку от точки (родитель — точка, её масштаб на экране = k)
                float k = ScreenFactor(scale);
                RectTransform labelRect = station.label.rectTransform;
                float dotSize = station.Rect.sizeDelta.x;
                labelRect.localScale = new Vector3(1f / k, 1f / k, 1f);
                labelRect.anchoredPosition = LabelOffset(station.labelSide, labelRect.sizeDelta, dotSize, k) / k;
            }
        }
    }
}
