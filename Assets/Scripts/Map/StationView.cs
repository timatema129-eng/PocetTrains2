using TMPro;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Map
{
    /// <summary>
    /// Станция на карте: ставит себя в позицию города из CityData и пишет название.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class StationView : MonoBehaviour
    {
        public CityData city;
        public TMP_Text label;

        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            Apply();
        }

        private void Apply()
        {
            if (city == null) return;

            Rect.anchoredPosition = city.mapPosition;
            if (label != null)
                label.text = city.displayName;
        }
    }
}
