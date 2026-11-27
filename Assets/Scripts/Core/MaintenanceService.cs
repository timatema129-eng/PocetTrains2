using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;
using TrainManager.Trains;

namespace TrainManager.Core
{
    /// <summary>
    /// Износ, техобслуживание, поломки, ремонт и буксировка (Этап 8, правила Артема 27.11):
    /// - состояние поезда (0..100 %) падает в основном от километража (TrainData.wearPerKm);
    /// - техобслуживание возвращает 100 %; чем раньше его сделать, тем дешевле (цена растёт по квадрату износа);
    /// - по прибытии есть шанс случайной поломки, он растёт с износом; при 0 % поезд всегда ломается;
    /// - лёгкая поломка: ехать можно, но на каждом участке есть шанс встать на линии — тогда буксировка;
    /// - «сломан»: не едет, пока не починят (монеты или лом).
    /// Все числа — черновик в GameDatabase и TrainData.
    /// </summary>
    public class MaintenanceService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly WalletService wallet;
        private readonly GameEvents events;
        private readonly System.Random random = new System.Random();

        public MaintenanceService(GameState state, GameDatabase db, WalletService wallet, GameEvents events)
        {
            this.state = state;
            this.db = db;
            this.wallet = wallet;
            this.events = events;
        }

        private TrainData Model(TrainInstance train) => train != null ? db.GetTrain(train.modelId) : null;

        private float Multiplier(TrainInstance train)
        {
            TrainData model = Model(train);
            return model != null ? model.serviceCostMultiplier : 1f;
        }

        public static string Describe(BreakdownState b)
        {
            switch (b)
            {
                case BreakdownState.Light: return "лёгкая поломка";
                case BreakdownState.Broken: return "сломан";
                default: return "исправен";
            }
        }

        // ================= Износ =================

        /// <summary>Списать износ за поездку (вызывается при отправлении).</summary>
        public void ApplyWear(TrainInstance train, float distanceKm)
        {
            TrainData model = Model(train);
            if (model == null || distanceKm <= 0f) return;
            train.condition = Mathf.Clamp(train.condition - distanceKm * model.wearPerKm, 0f, 100f);
        }

        // ================= Техобслуживание =================

        public long GetMaintenancePrice(TrainInstance train)
        {
            if (train == null) return 0;
            float missing = 100f - train.condition;
            if (missing < 0.5f) return 0;
            float price = db.maintenanceBaseCoins + db.maintenanceCoinsPerMissingSq * missing * missing;
            return (long)Mathf.Ceil(price * Multiplier(train));
        }

        public bool CanMaintain(TrainInstance train, out string reason)
        {
            if (!IsAtStation(train, out reason)) return false;
            long price = GetMaintenancePrice(train);
            if (price <= 0)
            {
                reason = "состояние уже 100%";
                return false;
            }
            if (!wallet.CanAfford(Currency.Coins, price))
            {
                reason = $"нужно {price} монет";
                return false;
            }
            reason = null;
            return true;
        }

        public bool TryMaintain(TrainInstance train)
        {
            if (!CanMaintain(train, out string reason))
            {
                Debug.LogWarning($"[Service] Техобслуживание нельзя: {reason}");
                return false;
            }
            if (!wallet.TrySpend(Currency.Coins, GetMaintenancePrice(train), $"техобслуживание {train.instanceId}"))
                return false;
            train.condition = 100f;
            Debug.Log($"[Service] {train.instanceId}: техобслуживание, состояние 100%");
            events.RaiseTrainConditionChanged(train);
            return true;
        }

        // ================= Поломки =================

        /// <summary>Шанс случайной поломки по прибытии при текущем состоянии.</summary>
        public float GetBreakdownChance(TrainInstance train)
        {
            if (train == null) return 0f;
            float wear = 1f - Mathf.Clamp01(train.condition / 100f);
            return Mathf.Clamp01(db.breakdownChanceAtFull + db.breakdownChanceAtZero * wear * wear);
        }

        /// <summary>Бросок на поломку по прибытии в конечный город.</summary>
        public void RollBreakdownOnArrival(TrainInstance train)
        {
            if (train == null || train.breakdown == BreakdownState.Broken) return;

            BreakdownState result = train.breakdown;
            if (train.condition <= 0f)
                result = BreakdownState.Broken; // износ 100% — поезд дальше не едет
            else if (random.NextDouble() < GetBreakdownChance(train))
                result = train.breakdown == BreakdownState.Light || random.NextDouble() >= db.lightBreakdownShare
                    ? BreakdownState.Broken
                    : BreakdownState.Light;

            if (result == train.breakdown) return;
            train.breakdown = result;
            Debug.Log($"[Service] {train.instanceId}: {Describe(result)} (состояние {train.condition:0}%)");
            events.RaiseTrainConditionChanged(train);
        }

        /// <summary>
        /// Поезд с лёгкой поломкой: решить при отправлении, встанет ли он в пути и где.
        /// stopTimes — времена городов маршрута.
        /// </summary>
        public void PlanFailure(TrainInstance train, TripState trip)
        {
            trip.failLegIndex = -1;
            trip.failAtUnixMs = 0;
            if (train.breakdown != BreakdownState.Light) return;

            for (int leg = 0; leg < trip.LastIndex; leg++)
            {
                if (random.NextDouble() >= db.lightFailChancePerLeg) continue;
                long start = trip.stopTimesUnixMs[leg];
                long end = trip.stopTimesUnixMs[leg + 1];
                float t = 0.2f + 0.6f * (float)random.NextDouble();
                trip.failLegIndex = leg;
                trip.failProgress = t;
                trip.failAtUnixMs = start + (long)((end - start) * t);
                if (trip.failAtUnixMs <= start) trip.failAtUnixMs = start + 1;
                return;
            }
        }

        /// <summary>Поезд встал на линии (вызывает TrainService, когда наступило время поломки).</summary>
        public void Strand(TrainInstance train)
        {
            train.state = TrainState.Stranded;
            train.breakdown = BreakdownState.Broken;
            Debug.Log($"[Service] {train.instanceId} сломался в пути — нужна буксировка");
            events.RaiseTrainConditionChanged(train);
        }

        // ================= Ремонт =================

        public long GetRepairCoins(TrainInstance train)
        {
            if (train == null || train.breakdown == BreakdownState.None) return 0;
            int baseCost = train.breakdown == BreakdownState.Light ? db.repairLightCoins : db.repairBrokenCoins;
            return (long)Mathf.Ceil(baseCost * Multiplier(train));
        }

        public long GetRepairScrap(TrainInstance train)
        {
            if (train == null || train.breakdown == BreakdownState.None) return 0;
            int baseCost = train.breakdown == BreakdownState.Light ? db.repairLightScrap : db.repairBrokenScrap;
            return (long)Mathf.Ceil(baseCost * Multiplier(train));
        }

        public bool CanRepair(TrainInstance train, bool withScrap, out string reason)
        {
            if (!IsAtStation(train, out reason)) return false;
            if (train.breakdown == BreakdownState.None)
            {
                reason = "поезд исправен";
                return false;
            }
            if (withScrap ? state.wallet.scrap < GetRepairScrap(train)
                          : !wallet.CanAfford(Currency.Coins, GetRepairCoins(train)))
            {
                reason = withScrap ? $"нужно {GetRepairScrap(train)} лома" : $"нужно {GetRepairCoins(train)} монет";
                return false;
            }
            reason = null;
            return true;
        }

        public bool TryRepair(TrainInstance train, bool withScrap)
        {
            if (!CanRepair(train, withScrap, out string reason))
            {
                Debug.LogWarning($"[Service] Ремонт нельзя: {reason}");
                return false;
            }
            if (withScrap)
            {
                state.wallet.scrap -= GetRepairScrap(train);
                events.RaisePartsChanged(); // лом изменился
            }
            else if (!wallet.TrySpend(Currency.Coins, GetRepairCoins(train), $"ремонт {train.instanceId}"))
            {
                return false;
            }

            train.breakdown = BreakdownState.None;
            Debug.Log($"[Service] {train.instanceId} отремонтирован ({(withScrap ? "лом" : "монеты")})");
            events.RaiseTrainConditionChanged(train);
            return true;
        }

        // ================= Буксировка =================

        public long GetTowPrice(TrainInstance train) => (long)Mathf.Ceil(db.towCoins * Multiplier(train));

        public bool CanTow(TrainInstance train, out string reason)
        {
            if (train == null || train.state != TrainState.Stranded)
            {
                reason = "поезд не стоит на линии";
                return false;
            }
            if (!wallet.CanAfford(Currency.Coins, GetTowPrice(train)))
            {
                reason = $"нужно {GetTowPrice(train)} монет";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>Отбуксировать в город, откуда поезд ехал по этому участку. Там его нужно починить.</summary>
        public bool TryTow(TrainInstance train)
        {
            if (!CanTow(train, out string reason))
            {
                Debug.LogWarning($"[Service] Буксировка нельзя: {reason}");
                return false;
            }
            if (!wallet.TrySpend(Currency.Coins, GetTowPrice(train), $"буксировка {train.instanceId}"))
                return false;

            TripState trip = train.trip;
            int leg = trip != null && trip.IsActive ? Mathf.Clamp(trip.failLegIndex, 0, trip.LastIndex) : -1;
            string cityId = leg >= 0 ? trip.stopIds[leg] : (db.startCity != null ? db.startCity.cityId : "");
            train.currentCityId = cityId;
            train.trip = new TripState();
            train.state = TrainState.Idle;
            Debug.Log($"[Service] {train.instanceId} отбуксирован в {cityId}");
            events.RaiseTrainConditionChanged(train);
            return true;
        }

        private static bool IsAtStation(TrainInstance train, out string reason)
        {
            if (train == null)
            {
                reason = "поезд не найден";
                return false;
            }
            if (train.IsMoving || train.state == TrainState.Stranded || string.IsNullOrEmpty(train.currentCityId))
            {
                reason = "поезд не на станции";
                return false;
            }
            reason = null;
            return true;
        }
    }
}
