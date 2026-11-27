using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrainManager.UI
{
    /// <summary>
    /// Показывает первую ошибку из Console прямо на экране (красная плашка под верхней панелью).
    /// Нужна для проверки на телефоне, где Console не видно: сделай скриншот и пришли. Нажатие — скрыть.
    /// </summary>
    [DefaultExecutionOrder(-2000)] // раньше GameRoot, чтобы поймать ошибки запуска
    public class ErrorOverlay : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text text;

        private string first;
        private int count;

        private void Awake()
        {
            Application.logMessageReceived += OnLog;
            Button button = panel != null ? panel.GetComponent<Button>() : null;
            if (button != null) button.onClick.AddListener(() => panel.SetActive(false));
            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            count++;
            if (first == null)
            {
                string where = stackTrace;
                if (!string.IsNullOrEmpty(where))
                {
                    string[] lines = where.Split('\n');
                    where = lines.Length > 2 ? lines[0] + "\n" + lines[1] : where;
                }
                first = message + (string.IsNullOrEmpty(where) ? "" : "\n" + where);
            }
            if (text != null)
                text.text = $"Ошибка ({count}): {first}\n<size=70%>Скриншот этого — Claude. Нажми, чтобы скрыть.</size>";
            if (panel != null) panel.SetActive(true);
        }
    }
}
