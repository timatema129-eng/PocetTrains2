using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrainManager.Map
{
    /// <summary>
    /// Перемещение по карте мира: перетаскивание (через ScrollRect),
    /// колесо мыши и щипок двумя пальцами — масштаб. При запуске карта центрируется на поезде.
    /// Висит на объекте-окне карты (viewport), content — сама карта.
    /// </summary>
    public class MapNavigator : MonoBehaviour
    {
        public ScrollRect scroll;
        public RectTransform content;
        public GameRoot root;

        [Min(0.05f)] public float minScale = 0.22f;
        [Min(0.1f)] public float maxScale = 2.5f;
        [Min(0.1f)] public float startScale = 1f;
        [Tooltip("Насколько меняется масштаб за один щелчок колеса")]
        [Min(1.01f)] public float wheelStep = 1.15f;

        private float lastPinchDistance = -1f;

        private void Start()
        {
            if (content == null) return;

            content.localScale = Vector3.one * Mathf.Clamp(startScale, minScale, maxScale);
            CenterOnTrain();
        }

        /// <summary>Поставить в центр экрана город, где стоит (или откуда едет) первый поезд.</summary>
        public void CenterOnTrain()
        {
            if (root == null || !root.IsReady) return;

            TrainInstance train = root.Trains.GetTrainByIndex(0);
            if (train == null) return;

            root.Trains.GetPosition(train, out string cityId, out _, out _);
            var city = root.Database.GetCity(cityId);
            if (city != null) CenterOn(city.mapPosition);
        }

        /// <summary>Поставить точку карты в центр экрана.</summary>
        public void CenterOn(Vector2 mapPosition)
        {
            content.anchoredPosition = -mapPosition * content.localScale.x;
        }

        private void Update()
        {
            if (content == null) return;
#if ENABLE_INPUT_SYSTEM
            HandleWheel();
            HandlePinch();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void HandleWheel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f) return;

            ZoomAt(mouse.position.ReadValue(), wheel > 0f ? wheelStep : 1f / wheelStep);
        }

        private void HandlePinch()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null) return;

            int count = 0;
            Vector2 p0 = Vector2.zero, p1 = Vector2.zero;
            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (count == 0) p0 = touch.position.ReadValue();
                else if (count == 1) p1 = touch.position.ReadValue();
                count++;
            }

            if (count < 2)
            {
                lastPinchDistance = -1f;
                if (scroll != null) scroll.enabled = true;
                return;
            }

            // Пока два пальца — не перетаскиваем, только масштабируем
            if (scroll != null)
            {
                scroll.StopMovement();
                scroll.enabled = false;
            }

            float distance = Vector2.Distance(p0, p1);
            if (lastPinchDistance > 0f && distance > 0f)
                ZoomAt((p0 + p1) * 0.5f, distance / lastPinchDistance);
            lastPinchDistance = distance;
        }
#endif

        /// <summary>Изменить масштаб так, чтобы точка под курсором/пальцами осталась на месте.</summary>
        public void ZoomAt(Vector2 screenPosition, float factor)
        {
            float oldScale = content.localScale.x;
            float newScale = Mathf.Clamp(oldScale * factor, minScale, maxScale);
            if (Mathf.Approximately(newScale, oldScale)) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPosition, null, out Vector2 before);
            content.localScale = Vector3.one * newScale;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPosition, null, out Vector2 after);
            content.anchoredPosition += (after - before) * newScale;
        }
    }
}
