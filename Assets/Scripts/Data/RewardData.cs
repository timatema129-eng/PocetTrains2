using System;
using System.Collections.Generic;

namespace TrainManager.Data
{
    /// <summary>
    /// Универсальная награда (задания, достижения, позже реклама): любые валюты и ресурсы сразу.
    /// Пустые поля = 0. Вагоны в награду — позже (нужно решить, куда и с каким городом назначения).
    /// </summary>
    [Serializable]
    public class RewardData
    {
        public long coins;
        public long dollars;
        public long scrap;
        [UnityEngine.Tooltip("Боксы в награду (boxId из GameDatabase.boxes) — открываются бесплатно")]
        public List<string> boxIds = new List<string>();
        [UnityEngine.Tooltip("Детали поездов: trainId")]
        public string partsTrainId;
        public int partsCount;

        public bool IsEmpty => coins <= 0 && dollars <= 0 && scrap <= 0 && (boxIds == null || boxIds.Count == 0) &&
                               (string.IsNullOrEmpty(partsTrainId) || partsCount <= 0);
    }

    /// <summary>Тип задания. Новый тип = новое значение + проверка в EventService.</summary>
    public enum QuestKind
    {
        /// <summary>Привезти N вагонов в город (вагоны с назначением в этот город).</summary>
        DeliverTo,
        /// <summary>Увезти N вагонов из города (купленные в этом городе и доставленные куда угодно).</summary>
        TakeFrom
    }

    /// <summary>Шаблон задания-события. Список — GameDatabase.questTemplates.</summary>
    [Serializable]
    public class QuestTemplate
    {
        public string templateId;
        public QuestKind kind;
        [UnityEngine.Min(1)] public int minWagons = 2;
        [UnityEngine.Min(1)] public int maxWagons = 4;
        [UnityEngine.Tooltip("Сколько реальных минут даётся на задание")]
        [UnityEngine.Min(1)] public int durationMinutes = 120;
        public RewardData reward = new RewardData();
    }

    /// <summary>Что считает достижение.</summary>
    public enum AchievementStat
    {
        RegionsUnlocked,  // открыто регионов (кроме стартового)
        LinesBought,      // куплено линий
        WagonsDelivered,  // доставлено вагонов
        TrainsAssembled,  // собрано поездов
        DistanceKm,       // проехано км (всеми поездами)
        QuestsCompleted   // выполнено заданий
    }

    [Serializable]
    public class AchievementData
    {
        public string achievementId;
        public string displayName;
        public AchievementStat stat;
        [UnityEngine.Min(1)] public long target = 1;
        public RewardData reward = new RewardData();
    }
}
