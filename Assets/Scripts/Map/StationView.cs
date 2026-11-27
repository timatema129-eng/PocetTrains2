using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Data;

namespace TrainManager.Map
{
    /// <summary>
    /// Станция на карте: ставит себя в позицию города из CityData, пишет название,
    /// сообщает о нажатии (событие Clicked) и умеет подсвечиваться.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class StationView : MonoBehaviour
    {
        public CityData city;
        public TMP_Text label;

        [Tooltip("Кружок станции (для подсветки). Если есть Button на этом объекте — станцию можно нажимать")]
        public Image dot;

        [Tooltip("Название видно, только когда карта приближена хотя бы до этого масштаба (чтобы надписи не налезали)")]
        public float labelMinZoom = 0f;
        [Tooltip("С какой стороны от точки название: 0 снизу, 1 сверху, 2 справа, 3 слева")]
        public int labelSide = 0;

        public static readonly Color NormalColor = new Color(0.25f, 0.55f, 0.95f);

        /// <summary>Станцию нажали.</summary>
        public event Action<StationView> Clicked;

        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            Apply();

            Button button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        private void Apply()
        {
            if (city == null) return;

            Rect.anchoredPosition = city.mapPosition;
            if (label != null)
                label.text = city.displayName;
        }

        public void SetHighlight(Color color)
        {
            if (dot != null) dot.color = color;
        }
    }
}
