using UnityEngine;
using UnityEngine.UI;

namespace TrainManager.UI
{
    /// <summary>Кнопка нижней панели: открывает/закрывает своё окно.</summary>
    [RequireComponent(typeof(Button))]
    public class PanelToggle : MonoBehaviour
    {
        public GameObject target;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (target != null) target.SetActive(!target.activeSelf);
            });
        }
    }
}
