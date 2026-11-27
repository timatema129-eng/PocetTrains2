using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>
    /// Сеть игрока: открытые регионы и купленные линии.
    /// Правила:
    /// - регион открывается за монеты (priceCoins региона; 0 — открыт с начала);
    /// - линию можно купить, только если открыты регионы обоих её городов (так работают и межконтинентальные линии);
    /// - ездить можно только по купленным линиям;
    /// - вагоны предлагаются только в города открытых регионов.
    /// Новая игра: открыт регион стартового города и регионы с ценой 0, линии из стартового города бесплатно.
    /// </summary>
    public class NetworkService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly WalletService wallet;
        private readonly GameEvents events;

        public NetworkService(GameState state, GameDatabase db, WalletService wallet, GameEvents events)
        {
            this.state = state;
            this.db = db;
            this.wallet = wallet;
            this.events = events;
            if (state.unlockedRegionIds == null) state.unlockedRegionIds = new List<string>();
            if (state.ownedLineIds == null) state.ownedLineIds = new List<string>();
            if (state.lineStates == null) state.lineStates = new List<LineState>();
        }

        // ================= Проверки =================

        public bool IsRegionUnlocked(RegionData region) =>
            region == null || state.unlockedRegionIds.Contains(region.regionId);

        /// <summary>Город доступен, если открыт его регион (город без региона доступен всегда).</summary>
        public bool IsCityAccessible(CityData city) => city != null && IsRegionUnlocked(city.region);

        public bool IsLineOwned(RailwayLineData line) => line != null && state.ownedLineIds.Contains(line.lineId);

        /// <summary>Линия между городами, если она есть и куплена; иначе null.</summary>
        public RailwayLineData FindOwnedLine(CityData a, CityData b)
        {
            RailwayLineData line = db.FindLine(a, b);
            return IsLineOwned(line) ? line : null;
        }

        // ================= Линии =================

        public long GetLinePrice(RailwayLineData line)
        {
            if (line == null) return 0;
            return (long)Mathf.Round(db.linePriceBase + db.linePricePerKm * line.distance);
        }

        public bool CanBuyLine(RailwayLineData line, out string reason)
        {
            if (line == null || !line.IsValid)
            {
                reason = "линия не настроена";
                return false;
            }
            if (IsLineOwned(line))
            {
                reason = "линия уже куплена";
                return false;
            }
            if (!IsCityAccessible(line.cityA))
            {
                reason = $"нужен регион {RegionName(line.cityA)}";
                return false;
            }
            if (!IsCityAccessible(line.cityB))
            {
                reason = $"нужен регион {RegionName(line.cityB)}";
                return false;
            }
            long price = GetLinePrice(line);
            if (!wallet.CanAfford(Currency.Coins, price))
            {
                reason = $"не хватает монет ({price})";
                return false;
            }
            reason = null;
            return true;
        }

        public bool TryBuyLine(RailwayLineData line)
        {
            if (!CanBuyLine(line, out string reason))
            {
                Debug.LogWarning($"[Network] Купить линию нельзя: {reason}");
                return false;
            }
            if (!wallet.TrySpend(Currency.Coins, GetLinePrice(line), $"линия {line.cityA.displayName} — {line.cityB.displayName}"))
                return false;

            Own(line);
            Debug.Log($"[Network] Куплена линия {line.cityA.displayName} — {line.cityB.displayName} ({line.distance:0} км)");
            events.RaiseLineBought(line);
            return true;
        }

        // ================= Пути (сколько поездов одновременно) =================

        /// <summary>Сколько поездов могут одновременно ехать по линии. В начале 1.</summary>
        public int GetTrackCount(RailwayLineData line)
        {
            LineState ls = line != null ? state.FindLineState(line.lineId) : null;
            return 1 + (ls != null ? ls.extraTracks : 0);
        }

        public long GetTrackPrice(RailwayLineData line)
        {
            int extra = GetTrackCount(line) - 1;
            return (long)Mathf.Round(db.trackPriceBase + db.trackPriceStep * extra);
        }

        public bool CanAddTrack(RailwayLineData line, out string reason)
        {
            if (!IsLineOwned(line))
            {
                reason = "линия не куплена";
                return false;
            }
            if (GetTrackCount(line) >= db.maxTracksPerLine)
            {
                reason = $"максимум {db.maxTracksPerLine} путей";
                return false;
            }
            long price = GetTrackPrice(line);
            if (!wallet.CanAfford(Currency.Coins, price))
            {
                reason = $"не хватает монет ({price})";
                return false;
            }
            reason = null;
            return true;
        }

        public bool TryAddTrack(RailwayLineData line)
        {
            if (!CanAddTrack(line, out string reason))
            {
                Debug.LogWarning($"[Network] Добавить путь нельзя: {reason}");
                return false;
            }
            if (!wallet.TrySpend(Currency.Coins, GetTrackPrice(line), $"путь {line.cityA.displayName} — {line.cityB.displayName}"))
                return false;

            state.GetOrCreateLineState(line.lineId).extraTracks++;
            Debug.Log($"[Network] Линия {line.cityA.displayName} — {line.cityB.displayName}: путей {GetTrackCount(line)}");
            events.RaiseLineChanged(line);
            return true;
        }

        // ================= Цвет линии (выбор игрока) =================

        /// <summary>Цвет, выбранный игроком; false — у линии цвет по умолчанию.</summary>
        public bool TryGetCustomColor(RailwayLineData line, out Color color)
        {
            LineState ls = line != null ? state.FindLineState(line.lineId) : null;
            if (ls != null && ls.colorIndex >= 0 && ls.colorIndex < LinePalette.Colors.Count)
            {
                color = LinePalette.Colors[ls.colorIndex];
                return true;
            }
            color = line != null ? line.color : Color.white;
            return false;
        }

        /// <summary>Следующий цвет палитры; после последнего — снова цвет по умолчанию.</summary>
        public void CycleLineColor(RailwayLineData line)
        {
            if (!IsLineOwned(line)) return;

            LineState ls = state.GetOrCreateLineState(line.lineId);
            ls.colorIndex++;
            if (ls.colorIndex >= LinePalette.Colors.Count) ls.colorIndex = -1;
            events.RaiseLineChanged(line);
        }

        public void ResetLineColor(RailwayLineData line)
        {
            LineState ls = line != null ? state.FindLineState(line.lineId) : null;
            if (ls == null) return;
            ls.colorIndex = -1;
            events.RaiseLineChanged(line);
        }

        // ================= Регионы =================

        public bool CanUnlockRegion(RegionData region, out string reason)
        {
            if (region == null)
            {
                reason = "регион не задан";
                return false;
            }
            if (IsRegionUnlocked(region))
            {
                reason = "регион уже открыт";
                return false;
            }
            if (!wallet.CanAfford(Currency.Coins, region.priceCoins))
            {
                reason = $"не хватает монет ({region.priceCoins})";
                return false;
            }
            reason = null;
            return true;
        }

        public bool TryUnlockRegion(RegionData region)
        {
            if (!CanUnlockRegion(region, out string reason))
            {
                Debug.LogWarning($"[Network] Открыть регион нельзя: {reason}");
                return false;
            }
            if (!wallet.TrySpend(Currency.Coins, region.priceCoins, $"регион {region.displayName}"))
                return false;

            Unlock(region);
            return true;
        }

        // ================= Старт =================

        /// <summary>
        /// Стартовая сеть: регион стартового города, все бесплатные регионы и (по настройке) линии из стартового города.
        /// Вызывается в новой игре и для старых сохранений, где сети ещё не было.
        /// </summary>
        public void SetupStartingNetwork()
        {
            if (db.startCity != null && db.startCity.region != null)
                Unlock(db.startCity.region);
            foreach (RegionData region in db.regions)
                if (region != null && region.priceCoins <= 0) Unlock(region);

            if (db.freeLinesFromStartCity && db.startCity != null)
            {
                foreach (RailwayLineData line in db.GetLines(db.startCity))
                    if (IsCityAccessible(line.cityA) && IsCityAccessible(line.cityB)) Own(line);
            }
            Debug.Log($"[Network] Стартовая сеть: регионов {state.unlockedRegionIds.Count}, линий {state.ownedLineIds.Count}");
        }

        /// <summary>Убрать из сохранения ID регионов и линий, которых больше нет на карте.</summary>
        public void RemoveMissing()
        {
            var regionIds = new HashSet<string>();
            foreach (RegionData r in db.regions) if (r != null) regionIds.Add(r.regionId);
            var lineIds = new HashSet<string>();
            foreach (RailwayLineData l in db.lines) if (l != null) lineIds.Add(l.lineId);

            state.unlockedRegionIds.RemoveAll(id => !regionIds.Contains(id));
            state.ownedLineIds.RemoveAll(id => !lineIds.Contains(id));
            state.lineStates.RemoveAll(ls => ls == null || !lineIds.Contains(ls.lineId));
        }

        private void Unlock(RegionData region)
        {
            if (region == null || state.unlockedRegionIds.Contains(region.regionId)) return;

            state.unlockedRegionIds.Add(region.regionId);
            Debug.Log($"[Network] Открыт регион {region.displayName}");
            events.RaiseRegionUnlocked(region);
        }

        private void Own(RailwayLineData line)
        {
            if (line != null && !state.ownedLineIds.Contains(line.lineId))
                state.ownedLineIds.Add(line.lineId);
        }

        private static string RegionName(CityData city) =>
            city != null && city.region != null ? city.region.displayName : "?";
    }
}
