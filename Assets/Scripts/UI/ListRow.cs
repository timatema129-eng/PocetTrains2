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
