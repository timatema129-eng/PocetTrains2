using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TrainManager.UI
{
    /// <summary>
    /// Небольшое окно-вопрос: «Посмотри рекламу — получи 15 $?» [Смотреть] [Нет],
    /// или просто сообщение «Приз: 50 монет» [ОК].
    /// </summary>
    public class AdPrompt : MonoBehaviour
    {
        public TMP_Text label;
        public Button yesButton;
        public TMP_Text yesLabel;
        public Button noButton;

        // Окно создаётся скрытым (сборщик сцены); Awake срабатывает при первом открытии
        private void Awake()
        {
            if (noButton != null) noButton.onClick.AddListener(Hide);
        }

        /// <summary>Вопрос с двумя кнопками. onYes вызывается после закрытия окна.</summary>
        public void Ask(string text, string yesText, UnityAction onYes)
        {
            Open(text, yesText, true, onYes);
        }

        /// <summary>Сообщение с одной кнопкой «ОК».</summary>
        public void Info(string text)
        {
            Open(text, "ОК", false, null);
        }

        private void Open(string text, string yesText, bool showNo, UnityAction onYes)
        {
            if (label != null) label.text = text;
            if (yesLabel != null) yesLabel.text = yesText;
            if (noButton != null) noButton.gameObject.SetActive(showNo);
            if (yesButton != null)
            {
                yesButton.onClick.RemoveAllListeners();
                yesButton.onClick.AddListener(() =>
                {
                    Hide();
                    onYes?.Invoke();
                });
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
