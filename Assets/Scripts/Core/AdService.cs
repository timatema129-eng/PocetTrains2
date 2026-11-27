using System;
using UnityEngine;

namespace TrainManager.Core
{
    /// <summary>
    /// Показ rewarded-рекламы. Сейчас — заглушка: реклама «просмотрена» сразу.
    /// Позже здесь будет реальный SDK (выбрать), остальная логика не изменится.
    /// </summary>
    public interface IAdService
    {
        /// <summary>Показать рекламу. onFinished(true) — досмотрена, награду можно выдать.</summary>
        void ShowRewarded(string placement, Action<bool> onFinished);
    }

    public class StubAdService : IAdService
    {
        public void ShowRewarded(string placement, Action<bool> onFinished)
        {
            Debug.Log($"[Ads] (заглушка) реклама «{placement}» просмотрена");
            onFinished?.Invoke(true);
        }
    }
}
