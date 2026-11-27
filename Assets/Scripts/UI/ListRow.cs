using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrainManager.UI
{
    /// <summary>Строка списка: текст и (необязательно) одна-две кнопки справа. Используется окном станции и списком поездов.</summary>
    public class ListRow : MonoBehaviour
    {
        public TMP_Text label;
        public Button button;
        public TMP_Text buttonLabel;

        [Tooltip("Вторая кнопка (необязательно)")]
        public Button button2;
        public TMP_Text button2Label;

        /// <summary>Вагон, к которому относится строка (если есть) — для проверки доступности кнопки.</summary>
        [System.NonSerialized] public TrainManager.Core.WagonInstance wagon;

        /// <summary>Тип служебного вагона на покупку (если строка — покупка служебного вагона).</summary>
        [System.NonSerialized] public TrainManager.Data.WagonData serviceType;

        /// <summary>Линия строки: покупка линии или (если куплена) покупка пути и смена цвета.</summary>
        [System.NonSerialized] public TrainManager.Data.RailwayLineData line;

        /// <summary>Действие строки с поездом: "maintain" (техобслуживание) или "repair" (ремонт).</summary>
        [System.NonSerialized] public string action;

        /// <summary>Регион на открытие (если строка — открытие региона).</summary>
        [System.NonSerialized] public TrainManager.Data.RegionData region;

        public void Set(string text, string buttonText = null, UnityEngine.Events.UnityAction onClick = null)
        {
            if (label != null) label.text = text;

            bool hasButton = !string.IsNullOrEmpty(buttonText);
            if (button != null)
            {
                button.gameObject.SetActive(hasButton);
                button.onClick.RemoveAllListeners();
                if (hasButton && onClick != null) button.onClick.AddListener(onClick);
            }
            if (buttonLabel != null) buttonLabel.text = buttonText ?? "";
            SetSecond(null);
        }

        /// <summary>Вторая кнопка: null — скрыть.</summary>
        public void SetSecond(string buttonText, UnityEngine.Events.UnityAction onClick = null)
        {
            bool has = !string.IsNullOrEmpty(buttonText);
            if (button2 != null)
            {
                button2.gameObject.SetActive(has);
                button2.onClick.RemoveAllListeners();
                if (has && onClick != null) button2.onClick.AddListener(onClick);
            }
            if (button2Label != null) button2Label.text = buttonText ?? "";
        }
    }
}
