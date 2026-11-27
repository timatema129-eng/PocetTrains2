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

        public List<WagonInstance> wagons = new List<WagonInstance>();

        public List<StationState> stations = new List<StationState>();

        // ---- Задел на следующие этапы (пока не используется) ----

        /// <summary>Открытые регионы (regionId).</summary>
        public List<string> unlockedRegionIds = new List<string>();

        /// <summary>Купленные линии (lineId).</summary>
        public List<string> ownedLineIds = new List<string>();

        /// <summary>Пути и цвета купленных линий (только для тех, где что-то менялось).</summary>
        public List<LineState> lineStates = new List<LineState>();

        /// <summary>Активные задания-события.</summary>
        public List<QuestInstance> quests = new List<QuestInstance>();
        /// <summary>Когда появится следующее задание (Unix, мс).</summary>
        public long nextQuestUnixMs;

        /// <summary>Счётчики для достижений и полученные достижения.</summary>
        public PlayerStats stats = new PlayerStats();
        public List<string> claimedAchievementIds = new List<string>();

        /// <summary>Метро в городах.</summary>
        public List<MetroState> metros = new List<MetroState>();
        /// <summary>Когда метро последний раз начисляло доход (для офлайна), Unix мс.</summary>
        public long metroLastTickUnixMs;
        /// <summary>Доход логистики по часам за последние сутки (для дохода метро).</summary>
        public List<IncomeBucket> logisticsIncome = new List<IncomeBucket>();

        /// <summary>Реклама: сколько просмотрено сегодня.</summary>
        public AdState ads = new AdState();

        /// <summary>ДЛЯ ТЕСТА: какая версия стартового тестового набора (поезда + вагоны) уже выдана.</summary>
        public int testKitVersion;

        public string NewInstanceId(string prefix)
        {
            string id = prefix + "_" + nextInstanceNumber;
            nextInstanceNumber++;
            return id;
        }

        public WagonInstance FindWagon(string instanceId)
        {
            foreach (WagonInstance w in wagons)
            {
                if (w != null && w.instanceId == instanceId)
                    return w;
            }
            return null;
        }

        public StationState GetOrCreateStation(string cityId)
        {
            foreach (StationState st in stations)
            {
                if (st != null && st.cityId == cityId)
                    return st;
            }
            var created = new StationState { cityId = cityId };
            stations.Add(created);
            return created;
        }

        public LineState FindLineState(string lineId)
        {
            foreach (LineState ls in lineStates)
                if (ls != null && ls.lineId == lineId) return ls;
            return null;
        }

        public LineState GetOrCreateLineState(string lineId)
        {
            LineState ls = FindLineState(lineId);
            if (ls == null)
            {
                ls = new LineState { lineId = lineId };
                lineStates.Add(ls);
            }
            return ls;
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
    /// Кошелёк игрока. Только данные — менять баланс через WalletService (там проверки и события).
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

        /// <summary>Боксы, полученные в награду (boxId, по одному на запись) — открываются бесплатно.</summary>
        public List<string> boxes = new List<string>();
    }

    /// <summary>Задание-событие в городе.</summary>
    [Serializable]
    public class QuestInstance
    {
        public string questId;
        public string templateId;
        public string cityId;
        public int required;
        public int progress;
        public long startUnixMs;
        public long endUnixMs;
    }

    /// <summary>Метро одного города.</summary>
    [Serializable]
    public class MetroState
    {
        public string cityId;
        /// <summary>Станций на каждой ветке.</summary>
        public List<int> branches = new List<int>();
        /// <summary>Состояние поездов метро 0..100.</summary>
        public float condition = 100f;
        /// <summary>Накопленный доход — игрок забирает кнопкой.</summary>
        public double pendingCoins;
    }

    [Serializable]
    public class IncomeBucket
    {
        public long hour;   // номер часа Unix-времени
        public long coins;
    }

    /// <summary>Просмотры рекламы за текущий день (день по часам устройства).</summary>
    [Serializable]
    public class AdState
    {
        public int dayKey;
        public int seriesWatched;
        public int dollarAdsWatched;
    }

    /// <summary>Счётчики игрока для достижений.</summary>
    [Serializable]
    public class PlayerStats
    {
        public long regionsUnlocked;
        public long linesBought;
        public long wagonsDelivered;
        public long trainsAssembled;
        public double distanceKm;
        public long questsCompleted;
    }

    [Serializable]
    public class PartStack
    {
        public string trainId;
        public int count;
    }
}
