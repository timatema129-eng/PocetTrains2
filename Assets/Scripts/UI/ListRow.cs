using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrainManager.UI
{
    /// <summary>Строка списка: текст и (необязательно) кнопка справа. Используется окном станции.</summary>
    public class ListRow : MonoBehaviour
    {
        public TMP_Text label;
        public Button button;
        public TMP_Text buttonLabel;

        /// <summary>Вагон, к которому относится строка (если есть) — для проверки доступности кнопки.</summary>
        [System.NonSerialized] public TrainManager.Core.WagonInstance wagon;

        /// <summary>Тип служебного вагона на покупку (если строка — покупка служебного вагона).</summary>
        [System.NonSerialized] public TrainManager.Data.WagonData serviceType;

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
        }
    }
}
