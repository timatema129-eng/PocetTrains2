using System;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>Что даёт бонус (станция или летящий приз).</summary>
    public enum BonusKind
    {
        None,
        Coins,   // просто монеты
        Dollar,  // просто доллар
        AdOffer  // предложение: посмотри рекламу — получи доллары
    }

    /// <summary>
    /// Рекламные награды (Этап 11, описание Артема):
    /// - ежедневная серия: N просмотров в день, за последний — супербонус; обновляется каждый день (по часам устройства);
    /// - «посмотри рекламу — получи 15 $» (с дневным лимитом);
    /// - бонус в списке вагонов станции и «летящий приз»: иногда реклама, иногда просто монеты или доллар.
    /// Реклама только добровольная. Числа — черновик в GameDatabase → Ads.
    /// </summary>
    public class AdsService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly IGameClock clock;
        private readonly RewardService rewards;
        private readonly WagonService wagons;
        private readonly IAdService ads;
        private readonly GameEvents events;
        private readonly System.Random random = new System.Random();

        public AdsService(GameState state, GameDatabase db, IGameClock clock, RewardService rewards,
            WagonService wagons, IAdService ads, GameEvents events)
        {
            this.state = state;
            this.db = db;
            this.clock = clock;
            this.rewards = rewards;
            this.wagons = wagons;
            this.ads = ads;
            this.events = events;
            if (state.ads == null) state.ads = new AdState();
        }

        // ================= День =================

        /// <summary>Номер дня по часам устройства (местное время): 20261127.</summary>
        public int TodayKey
        {
            get
            {
                DateTime d = DateTimeOffset.FromUnixTimeMilliseconds(clock.NowUnixMs).ToLocalTime().Date;
                return d.Year * 10000 + d.Month * 100 + d.Day;
            }
        }

        private AdState Today()
        {
            AdState a = state.ads;
            int key = TodayKey;
            if (a.dayKey != key)
            {
                a.dayKey = key;
                a.seriesWatched = 0;
                a.dollarAdsWatched = 0;
            }
            return a;
        }

        public float SecondsToNewDay
        {
            get
            {
                DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(clock.NowUnixMs).ToLocalTime();
                DateTimeOffset next = new DateTimeOffset(now.Date.AddDays(1), now.Offset);
                return (float)Math.Max(0, (next - now).TotalSeconds);
            }
        }

        // ================= Ежедневная серия =================

        public int SeriesLength => db.adSeriesRewards != null ? db.adSeriesRewards.Count : 0;
        public int SeriesWatched => Today().seriesWatched;
        public bool SeriesDone => SeriesWatched >= SeriesLength;

        public RewardData NextSeriesReward => SeriesDone ? null : db.adSeriesRewards[SeriesWatched];

        public void WatchSeries()
        {
            if (SeriesDone) return;
            ads.ShowRewarded("daily_series", ok =>
            {
                if (!ok || SeriesDone) return;
                AdState a = Today();
                RewardData reward = db.adSeriesRewards[a.seriesWatched];
                a.seriesWatched++;
                bool super = a.seriesWatched == SeriesLength;
                rewards.Grant(reward, super ? "реклама: супербонус серии" : $"реклама: серия {a.seriesWatched}/{SeriesLength}");
                events.RaiseAdsChanged();
            });
        }

        // ================= Реклама за доллары =================

        public int DollarAdsLeft => Mathf.Max(0, db.adDollarsPerDay - Today().dollarAdsWatched);

        public void WatchForDollars(string placement = "dollars")
        {
            if (DollarAdsLeft <= 0) return;
            ads.ShowRewarded(placement, ok =>
            {
                if (!ok || DollarAdsLeft <= 0) return;
                Today().dollarAdsWatched++;
                rewards.Grant(new RewardData { dollars = db.adDollarReward }, $"реклама: {db.adDollarReward} $");
                events.RaiseAdsChanged();
            });
        }

        // ================= Бонус в списке вагонов станции =================

        /// <summary>Бонус станции в текущем периоде списка (одинаков до обновления списка; один раз).</summary>
        public BonusKind GetStationBonus(CityData city)
        {
            if (city == null) return BonusKind.None;
            long period = wagons.CurrentOfferPeriod;
            StationState st = state.GetOrCreateStation(city.cityId);
            if (st.bonusClaimedPeriod == period) return BonusKind.None;

            var rnd = new System.Random(unchecked(WagonService.Seed(city.cityId, period) * 31 + 17)); // стабильно до обновления списка
            if (rnd.NextDouble() >= db.stationBonusChance) return BonusKind.None;
            BonusKind kind = Pick(rnd.NextDouble());
            if (kind == BonusKind.AdOffer && DollarAdsLeft <= 0) kind = BonusKind.Coins;
            return kind;
        }

        public void ClaimStationBonus(CityData city)
        {
            BonusKind kind = GetStationBonus(city);
            if (kind == BonusKind.None) return;
            StationState st = state.GetOrCreateStation(city.cityId);
            long period = wagons.CurrentOfferPeriod;

            if (kind == BonusKind.AdOffer)
            {
                ads.ShowRewarded("station_bonus", ok =>
                {
                    if (!ok || DollarAdsLeft <= 0 || st.bonusClaimedPeriod == period) return;
                    st.bonusClaimedPeriod = period;
                    Today().dollarAdsWatched++;
                    rewards.Grant(new RewardData { dollars = db.adDollarReward }, $"реклама на станции {city.displayName}");
                    events.RaiseAdsChanged();
                });
                return;
            }

            st.bonusClaimedPeriod = period;
            rewards.Grant(SimpleReward(kind), $"подарок на станции {city.displayName}");
            events.RaiseAdsChanged();
        }

        // ================= Летящий приз =================

        /// <summary>Что в летящем призе (решается при нажатии).</summary>
        public BonusKind RollFlyingPrize()
        {
            BonusKind kind = Pick(random.NextDouble());
            if (kind == BonusKind.AdOffer && DollarAdsLeft <= 0) kind = BonusKind.Coins;
            return kind;
        }

        /// <summary>Выдать простой приз (монеты или доллар). Рекламу — через WatchForDollars.</summary>
        public RewardData GrantSimplePrize(BonusKind kind, string reason)
        {
            RewardData reward = SimpleReward(kind);
            rewards.Grant(reward, reason);
            events.RaiseAdsChanged();
            return reward;
        }

        public string Describe(RewardData reward) => rewards.Describe(reward);

        private RewardData SimpleReward(BonusKind kind)
        {
            if (kind == BonusKind.Dollar) return new RewardData { dollars = db.bonusDollars };
            int coins = random.Next(db.bonusCoinsMin, Mathf.Max(db.bonusCoinsMin, db.bonusCoinsMax) + 1);
            return new RewardData { coins = coins };
        }

        private BonusKind Pick(double r)
        {
            float coins = db.bonusShareCoins, dollar = db.bonusShareDollar, ad = db.bonusShareAd;
            float total = coins + dollar + ad;
            if (total <= 0f) return BonusKind.Coins;
            double x = r * total;
            if (x < coins) return BonusKind.Coins;
            if (x < coins + dollar) return BonusKind.Dollar;
            return BonusKind.AdOffer;
        }
    }
}
