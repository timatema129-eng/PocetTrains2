using UnityEngine;
using UnityEngine.UI;

namespace TrainManager.UI
{
    /// <summary>Кнопка «×» в заголовке окна: закрывает окно.</summary>
    [RequireComponent(typeof(Button))]
    public class ClosePanelButton : MonoBehaviour
    {
        public GameObject target;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (target != null) target.SetActive(false);
            });
        }
    }
}
