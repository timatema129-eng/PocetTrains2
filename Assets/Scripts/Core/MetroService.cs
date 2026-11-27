using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>
    /// Метро (Этап 14, описание Артема 27.11). Отдельная система, не связана с междугородней логистикой:
    /// - только в крупных городах (GameDatabase.metroCities), насколько можно развить — зависит от города (MetroTier);
    /// - игрок «аркадно» открывает станции и ветки за монеты;
    /// - пассивный доход: около 30 % от среднего дохода обычной логистики при эталонном числе станций,
    ///   растёт с развитием; пока игра закрыта (АФК) — около 10 % от этого;
    /// - поезда метро изнашиваются вдвое медленнее обычных, нужно ТО; при 0 % метро встаёт —
    ///   нужно выпустить новые поезда.
    /// Доход копится в «кассе» метро, игрок забирает его кнопкой.
    /// </summary>
    public class MetroService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly IGameClock clock;
        private readonly WalletService wallet;
        private readonly GameEvents events;

        private const long HourMs = 3600L * 1000L;
        /// <summary>Пауза дольше этого считается офлайном (игра свёрнута или закрыта).</summary>
        private const long OfflineGapMs = 10L * 1000L;

        public MetroService(GameState state, GameDatabase db, IGameClock clock, WalletService wallet, GameEvents events)
        {
            this.state = state;
            this.db = db;
            this.clock = clock;
            this.wallet = wallet;
            this.events = events;
            if (state.metros == null) state.metros = new List<MetroState>();
            if (state.logisticsIncome == null) state.logisticsIncome = new List<IncomeBucket>();
            events.WagonDelivered += (wagon, city, income) => AddLogisticsIncome(income);
        }

        // ================= Доход логистики (для расчёта метро) =================

        private void AddLogisticsIncome(long coins)
        {
            if (coins <= 0) return;
            long hour = clock.NowUnixMs / HourMs;
            IncomeBucket last = state.logisticsIncome.Count > 0 ? state.logisticsIncome[state.logisticsIncome.Count - 1] : null;
            if (last != null && last.hour == hour) last.coins += coins;
            else state.logisticsIncome.Add(new IncomeBucket { hour = hour, coins = coins });
            state.logisticsIncome.RemoveAll(b => b.hour < hour - 23);
        }

        /// <summary>Средний доход логистики в час за последние сутки (по часам, когда игрок что-то возил).</summary>
        public float AverageLogisticsPerHour
        {
            get
            {
                long hour = clock.NowUnixMs / HourMs;
                long sum = 0, oldest = hour;
                foreach (IncomeBucket b in state.logisticsIncome)
                {
                    if (b.hour < hour - 23) continue;
                    sum += b.coins;
                    if (b.hour < oldest) oldest = b.hour;
                }
                long hours = System.Math.Max(1, hour - oldest + 1);
                return (float)sum / hours;
            }
        }

        // ================= Метро города =================

        public bool CanHaveMetro(CityData city) => db.GetMetroTier(city) != null;

        public MetroState Get(CityData city)
        {
            if (city == null) return null;
            foreach (MetroState m in state.metros)
                if (m != null && m.cityId == city.cityId) return m;
            return null;
        }

        public int TotalStations(MetroState m)
        {
            if (m == null) return 0;
            int n = 0;
            foreach (int s in m.branches) n += s;
            return n;
        }

        // ---- Постройка ----

        public bool CanBuild(CityData city, out string reason)
        {
            if (!CanHaveMetro(city)) { reason = "метро только в крупных городах"; return false; }
            if (Get(city) != null) { reason = "метро уже есть"; return false; }
            if (!wallet.CanAfford(Currency.Coins, db.metroBuildCoins)) { reason = $"нужно {db.metroBuildCoins} монет"; return false; }
            reason = null;
            return true;
        }

        public bool TryBuild(CityData city)
        {
            if (!CanBuild(city, out string reason)) { Debug.LogWarning($"[Metro] {reason}"); return false; }
            if (!wallet.TrySpend(Currency.Coins, db.metroBuildCoins, $"метро {city.displayName}")) return false;
            var m = new MetroState { cityId = city.cityId };
            m.branches.Add(1);
            state.metros.Add(m);
            Debug.Log($"[Metro] Построено метро в городе {city.displayName}");
            events.RaiseMetroChanged(city.cityId);
            return true;
        }

        // ---- Станции ----

        public long GetStationPrice(MetroState m) => db.metroStationBase + (long)db.metroStationStep * TotalStations(m);

        public bool CanAddStation(CityData city, int branch, out string reason)
        {
            MetroState m = Get(city);
            MetroTier tier = db.GetMetroTier(city);
            if (m == null || tier == null || branch < 0 || branch >= m.branches.Count) { reason = "нет такой ветки"; return false; }
            if (m.branches[branch] >= tier.maxStationsPerBranch) { reason = $"на ветке максимум {tier.maxStationsPerBranch} станций"; return false; }
            if (!wallet.CanAfford(Currency.Coins, GetStationPrice(m))) { reason = $"нужно {GetStationPrice(m)} монет"; return false; }
            reason = null;
            return true;
        }

        public bool TryAddStation(CityData city, int branch)
        {
            if (!CanAddStation(city, branch, out string reason)) { Debug.LogWarning($"[Metro] {reason}"); return false; }
            MetroState m = Get(city);
            if (!wallet.TrySpend(Currency.Coins, GetStationPrice(m), $"станция метро {city.displayName}")) return false;
            m.branches[branch]++;
            events.RaiseMetroChanged(city.cityId);
            return true;
        }

        // ---- Ветки ----

        public long GetBranchPrice(MetroState m) =>
            db.metroBranchBase + (long)db.metroBranchStep * System.Math.Max(0, (m != null ? m.branches.Count : 0) - 1);

        public bool CanAddBranch(CityData city, out string reason)
        {
            MetroState m = Get(city);
            MetroTier tier = db.GetMetroTier(city);
            if (m == null || tier == null) { reason = "метро не построено"; return false; }
            if (m.branches.Count >= tier.maxBranches) { reason = $"максимум {tier.maxBranches} веток"; return false; }
            if (!wallet.CanAfford(Currency.Coins, GetBranchPrice(m))) { reason = $"нужно {GetBranchPrice(m)} монет"; return false; }
            reason = null;
            return true;
        }

        public bool TryAddBranch(CityData city)
        {
            if (!CanAddBranch(city, out string reason)) { Debug.LogWarning($"[Metro] {reason}"); return false; }
            MetroState m = Get(city);
            if (!wallet.TrySpend(Currency.Coins, GetBranchPrice(m), $"ветка метро {city.displayName}")) return false;
            m.branches.Add(1); // новая ветка сразу с одной станцией
            events.RaiseMetroChanged(city.cityId);
            return true;
        }

        // ================= Доход =================

        /// <summary>Доход метро в час, пока игра открыта.</summary>
        public float GetIncomePerHour(MetroState m)
        {
            if (m == null || m.condition <= 0f) return 0f;
            CityData city = db.GetCity(m.cityId);
            MetroTier tier = db.GetMetroTier(city);
            int stations = TotalStations(m);
            float byLogistics = AverageLogisticsPerHour * db.metroShareOfLogistics * stations / db.metroReferenceStations;
            float floor = db.metroMinIncomePerStationHour * stations;
            float rate = Mathf.Max(byLogistics, floor) * (tier != null ? tier.incomeMultiplier : 1f);
            float conditionFactor = 0.5f + 0.5f * Mathf.Clamp01(m.condition / 100f); // изношенные поезда возят меньше
            return rate * conditionFactor;
        }

        /// <summary>Начислить доход и износ за прошедшее время. Каждый кадр и после загрузки.</summary>
        public void Tick()
        {
            long now = clock.NowUnixMs;
            if (state.metroLastTickUnixMs <= 0 || now < state.metroLastTickUnixMs)
            {
                state.metroLastTickUnixMs = now;
                return;
            }
            long delta = now - state.metroLastTickUnixMs;
            if (delta < 1000) return; // раз в секунду достаточно
            state.metroLastTickUnixMs = now;

            bool offline = delta > OfflineGapMs;
            if (offline) delta = System.Math.Min(delta, db.metroMaxOfflineHours * HourMs);
            double hours = delta / (double)HourMs;
            float share = offline ? db.metroOfflineShare : 1f;

            foreach (MetroState m in state.metros)
            {
                if (m == null) continue;
                m.pendingCoins += GetIncomePerHour(m) * share * hours;
                m.condition = Mathf.Max(0f, m.condition - (float)(db.metroWearPerHour * hours));
            }
            if (offline && state.metros.Count > 0)
                Debug.Log($"[Metro] Офлайн {hours:0.0} ч: метро копило доход ({db.metroOfflineShare * 100f:0}% от обычного)");
        }

        public bool TryCollect(CityData city)
        {
            MetroState m = Get(city);
            if (m == null) return false;
            long coins = (long)System.Math.Floor(m.pendingCoins);
            if (coins <= 0) return false;
            m.pendingCoins -= coins;
            wallet.Add(Currency.Coins, coins, $"доход метро {city.displayName}");
            events.RaiseMetroChanged(city.cityId);
            return true;
        }

        // ================= Обслуживание =================

        public long GetMaintenancePrice(MetroState m)
        {
            if (m == null || m.condition <= 0f) return 0;
            float missing = 100f - m.condition;
            if (missing < 0.5f) return 0;
            float price = (db.maintenanceBaseCoins + db.maintenanceCoinsPerMissingSq * missing * missing) * db.metroMaintenanceMultiplier;
            return (long)Mathf.Ceil(price * Mathf.Max(1, TotalStations(m)) / 5f);
        }

        public bool TryMaintain(CityData city)
        {
            MetroState m = Get(city);
            long price = GetMaintenancePrice(m);
            if (m == null || price <= 0 || !wallet.TrySpend(Currency.Coins, price, $"ТО метро {city.displayName}")) return false;
            m.condition = 100f;
            events.RaiseMetroChanged(city.cityId);
            return true;
        }

        public long GetRenewPrice(MetroState m) => (long)db.metroRenewCoins * System.Math.Max(1, m != null ? m.branches.Count : 1);

        /// <summary>Поезда метро износились до 0% — выпустить новые.</summary>
        public bool TryRenew(CityData city)
        {
            MetroState m = Get(city);
            if (m == null || m.condition > 0f) return false;
            if (!wallet.TrySpend(Currency.Coins, GetRenewPrice(m), $"новые поезда метро {city.displayName}")) return false;
            m.condition = 100f;
            events.RaiseMetroChanged(city.cityId);
            return true;
        }
    }
}
