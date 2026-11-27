using System;
using System.Collections.Generic;

namespace TrainManager.Core
{
    /// <summary>
    /// Всё, что относится к прогрессу игрока, в одном месте. Именно это пишется в сохранение.
    /// Здесь только данные и только ID конфигов (не ссылки на ассеты).
    /// Логика — в сервисах (TrainService и т.д.), не здесь.
    /// </summary>
    [Serializable]
    public class GameState
    {
        /// <summary>Версия формата сохранения. Увеличивать, когда меняется структура.</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>Когда начата игра (Unix-время, мс).</summary>
        public long createdUnixMs;

        /// <summary>Когда было последнее сохранение (Unix-время, мс).</summary>
        public long savedUnixMs;

        /// <summary>Счётчик для уникальных ID экземпляров (поездов, вагонов...).</summary>
        public int nextInstanceNumber = 1;

        public List<TrainInstance> trains = new List<TrainInstance>();

        public Wallet wallet = new Wallet();

        // ---- Задел на следующие этапы (пока не используется) ----

        /// <summary>Открытые регионы (regionId).</summary>
        public List<string> unlockedRegionIds = new List<string>();

        /// <summary>Купленные линии (lineId).</summary>
        public List<string> ownedLineIds = new List<string>();

        public string NewInstanceId(string prefix)
        {
            string id = prefix + "_" + nextInstanceNumber;
            nextInstanceNumber++;
            return id;
        }

        public TrainInstance FindTrain(string instanceId)
        {
            foreach (TrainInstance t in trains)
            {
                if (t != null && t.instanceId == instanceId)
                    return t;
            }
            return null;
        }
    }

    /// <summary>
    /// Кошелёк игрока. Пока только структура — операции появятся на этапе экономики.
    /// Обмен только доллары → монеты (kb/03).
    /// </summary>
    [Serializable]
    public class Wallet
    {
        public long coins;
        public long dollars;
        public long scrap;

        /// <summary>Детали поездов: trainId → количество. Хранилище бесконечное.</summary>
        public List<PartStack> parts = new List<PartStack>();
    }

    [Serializable]
    public class PartStack
    {
        public string trainId;
        public int count;
    }
}
