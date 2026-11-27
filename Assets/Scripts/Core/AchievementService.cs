using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>
    /// Достижения (Этап 10) на шине событий. Пример Артема: открыл новый регион → получил доллары.
    /// Счётчики — GameState.stats, список достижений — GameDatabase.achievements.
    /// Награда выдаётся сразу, когда счётчик дошёл до цели.
    /// Создаётся после стартовой сети, чтобы стартовый регион и бесплатные линии не считались.
    /// </summary>
    public class AchievementService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly GameEvents events;
        private readonly RewardService rewards;

        public AchievementService(GameState state, GameDatabase db, GameEvents events, RewardService rewards)
        {
            this.state = state;
            this.db = db;
            this.events = events;
            this.rewards = rewards;
            if (state.stats == null) state.stats = new PlayerStats();
            if (state.claimedAchievementIds == null) state.claimedAchievementIds = new List<string>();

            // Старое сохранение: регионы уже открывались до появления счётчиков
            state.stats.regionsUnlocked = System.Math.Max(state.stats.regionsUnlocked, state.unlockedRegionIds.Count - 1);

            events.RegionUnlocked += r => { state.stats.regionsUnlocked++; CheckAll(); };
            events.LineBought += l => { state.stats.linesBought++; CheckAll(); };
            events.WagonDelivered += (w, c, income) => { state.stats.wagonsDelivered++; CheckAll(); };
            events.TrainAssembled += t => { state.stats.trainsAssembled++; CheckAll(); };
            events.TrainDeparted += (t, trip) => { state.stats.distanceKm += TripKm(trip); CheckAll(); };
            events.QuestsChanged += CheckAll;

            CheckAll();
        }

        public long GetValue(AchievementStat stat)
        {
            PlayerStats s = state.stats;
            switch (stat)
            {
                case AchievementStat.RegionsUnlocked: return s.regionsUnlocked;
                case AchievementStat.LinesBought: return s.linesBought;
                case AchievementStat.WagonsDelivered: return s.wagonsDelivered;
                case AchievementStat.TrainsAssembled: return s.trainsAssembled;
                case AchievementStat.DistanceKm: return (long)s.distanceKm;
                case AchievementStat.QuestsCompleted: return s.questsCompleted;
                default: return 0;
            }
        }

        public bool IsClaimed(AchievementData a) => a != null && state.claimedAchievementIds.Contains(a.achievementId);

        public string DescribeReward(AchievementData a) => rewards.Describe(a != null ? a.reward : null);

        private bool checking;

        public void CheckAll()
        {
            if (checking) return; // награда может вызвать события — без повторного входа
            checking = true;
            try
            {
                foreach (AchievementData a in db.achievements)
                {
                    if (a == null || string.IsNullOrEmpty(a.achievementId) || IsClaimed(a)) continue;
                    if (GetValue(a.stat) < a.target) continue;

                    state.claimedAchievementIds.Add(a.achievementId);
                    rewards.Grant(a.reward, $"достижение «{a.displayName}»");
                    Debug.Log($"[Achievement] Получено: {a.displayName}");
                    events.RaiseAchievementUnlocked(a);
                }
            }
            finally
            {
                checking = false;
            }
        }

        private float TripKm(TripState trip)
        {
            if (trip == null || !trip.IsActive) return 0f;
            float km = 0f;
            for (int i = 1; i < trip.stopIds.Count; i++)
            {
                RailwayLineData line = db.FindLine(db.GetCity(trip.stopIds[i - 1]), db.GetCity(trip.stopIds[i]));
                if (line != null) km += line.distance;
            }
            return km;
        }
    }
}
