using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>Предложение купить вагон в город назначения.</summary>
    public struct WagonOffer
    {
        /// <summary>Позиция в списке станции (0..N-1). Купленная позиция исчезает до обновления списка.</summary>
        public int slot;
        public WagonData type;
        public CityData origin;
        public CityData destination;
        public float distanceKm;
        public long price;
        public long income;
    }

    /// <summary>
    /// Вагоны: покупка, прицепка/отцепка, склад станции и автоматическая выгрузка.
    /// Правила:
    /// - вагон всегда ровно в одном месте (склад станции или поезд);
    /// - прицепить/отцепить можно только у поезда, который стоит в этом городе;
    /// - лимит состава — maxWagons модели; лимит склада — stationBaseSlots + докупленные места;
    /// - на каждой остановке маршрута выгружаются ТОЛЬКО вагоны, назначенные в этот город;
    /// - список вагонов на станции обновляется по реальному времени, купленная позиция исчезает до обновления.
    /// Цены и доход — черновые формулы из GameDatabase (раздел Economy).
    /// </summary>
    public class WagonService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly WalletService wallet;
        private readonly GameEvents events;
        private readonly IGameClock clock;

        public WagonService(GameState state, GameDatabase db, WalletService wallet, GameEvents events, IGameClock clock)
        {
            this.state = state;
            this.db = db;
            this.wallet = wallet;
            this.events = events;
            this.clock = clock;

            events.TrainReachedStop += OnTrainReachedStop;
        }

        // ================= Где какие вагоны =================

        public List<WagonInstance> GetTrainWagons(TrainInstance train)
        {
            var result = new List<WagonInstance>();
            if (train == null) return result;

            foreach (string id in train.wagonIds)
            {
                WagonInstance w = state.FindWagon(id);
                if (w != null) result.Add(w);
            }
            return result;
        }

        public List<WagonInstance> GetStationWagons(string cityId)
        {
            var result = new List<WagonInstance>();
            foreach (WagonInstance w in state.wagons)
            {
                if (w != null && w.location == WagonLocation.Station && w.locationId == cityId)
                    result.Add(w);
            }
            return result;
        }

        /// <summary>Сколько мест в составе занято (каждый вагон — 1 место, включая топливные и двигатели).</summary>
        public int GetTrainUsedSlots(TrainInstance train) => train != null ? train.wagonIds.Count : 0;

        /// <summary>Сколько вагонов поезд может тянуть. Этап 5: + дополнительные двигатели.</summary>
        public int GetTrainCapacity(TrainInstance train)
        {
            TrainData model = train != null ? db.GetTrain(train.modelId) : null;
            return model != null ? model.maxWagons : 0;
        }

        public int GetStationCapacity(string cityId)
        {
            int extra = 0;
            foreach (StationState st in state.stations)
            {
                if (st != null && st.cityId == cityId)
                {
                    extra = st.extraSlots;
                    break;
                }
            }
            return db.stationBaseSlots + extra;
        }

        public int GetStationUsedSlots(string cityId) => GetStationWagons(cityId).Count;

        // ================= Цены и предложения =================

        public long GetPrice(WagonData type, CityData origin, CityData destination)
        {
            float km = Geo.DistanceKm(origin, destination);
            float mult = type != null ? type.priceMultiplier : 1f;
            return (long)Mathf.Round((db.wagonBasePrice + db.wagonPricePerKm * km) * mult);
        }

        public long GetIncome(WagonData type, CityData origin, CityData destination)
        {
            float km = Geo.DistanceKm(origin, destination);
            float mult = type != null ? type.incomeMultiplier : 1f;
            return (long)Mathf.Round((db.deliveryBaseIncome + db.deliveryIncomePerKm * km) * mult);
        }

        /// <summary>Номер текущего периода списка предложений (меняется раз в wagonOfferRefreshMinutes).</summary>
        public long CurrentOfferPeriod
        {
            get
            {
                long periodMs = (long)Mathf.Max(1, db.wagonOfferRefreshMinutes) * 60L * 1000L;
                return clock.NowUnixMs / periodMs;
            }
        }

        /// <summary>Сколько секунд до обновления списка вагонов.</summary>
        public float SecondsToOfferRefresh
        {
            get
            {
                long periodMs = (long)Mathf.Max(1, db.wagonOfferRefreshMinutes) * 60L * 1000L;
                long next = (CurrentOfferPeriod + 1) * periodMs;
                return Mathf.Max(0f, (next - clock.NowUnixMs) / 1000f);
            }
        }

        /// <summary>
        /// Список вагонов на станции. Города назначения случайно выбираются из ближайших (wagonOfferPool),
        /// но случайность «зерновая»: для одного города и одного периода список всегда одинаковый,
        /// поэтому его не нужно сохранять, а после перезапуска он не меняется.
        /// Уже купленные позиции в список не попадают.
        /// </summary>
        public List<WagonOffer> GetOffers(CityData city)
        {
            var result = new List<WagonOffer>();
            WagonData type = db.DefaultCargoWagon;
            if (city == null || type == null) return result;

            List<CityData> pool = GetNearestCities(city);
            if (pool.Count == 0) return result;

            long period = CurrentOfferPeriod;
            var random = new System.Random(Seed(city.cityId, period));
            List<int> bought = GetBoughtSlots(city.cityId, period);

            for (int slot = 0; slot < db.wagonOfferCount; slot++)
            {
                // Выбор делается для каждой позиции всегда, даже купленной, чтобы остальные позиции не «съезжали».
                CityData dest = pool[random.Next(pool.Count)];
                if (bought != null && bought.Contains(slot)) continue;

                result.Add(new WagonOffer
                {
                    slot = slot,
                    type = type,
                    origin = city,
                    destination = dest,
                    distanceKm = Geo.DistanceKm(city, dest),
                    price = GetPrice(type, city, dest),
                    income = GetIncome(type, city, dest)
                });
            }
            return result;
        }

        private readonly Dictionary<CityData, List<CityData>> nearestCache = new Dictionary<CityData, List<CityData>>();

        /// <summary>Ближайшие города (wagonOfferPool штук), посчитано один раз на город.</summary>
        private List<CityData> GetNearestCities(CityData city)
        {
            if (nearestCache.TryGetValue(city, out List<CityData> cached)) return cached;

            var pool = new List<CityData>();
            foreach (CityData c in db.cities)
                if (c != null && c != city) pool.Add(c);
            pool.Sort((a, b) => Geo.DistanceKm(city, a).CompareTo(Geo.DistanceKm(city, b)));
            if (pool.Count > db.wagonOfferPool) pool.RemoveRange(db.wagonOfferPool, pool.Count - db.wagonOfferPool);

            nearestCache[city] = pool;
            return pool;
        }

        private List<int> GetBoughtSlots(string cityId, long period)
        {
            foreach (StationState st in state.stations)
            {
                if (st != null && st.cityId == cityId)
                    return st.offerPeriod == period ? st.boughtOfferSlots : null;
            }
            return null;
        }

        private void MarkBought(string cityId, int slot)
        {
            StationState st = state.GetOrCreateStation(cityId);
            long period = CurrentOfferPeriod;
            if (st.offerPeriod != period)
            {
                st.offerPeriod = period;
                st.boughtOfferSlots.Clear();
            }
            if (!st.boughtOfferSlots.Contains(slot))
                st.boughtOfferSlots.Add(slot);
        }

        /// <summary>Стабильное зерно из ID города и номера периода (string.GetHashCode не стабилен между запусками).</summary>
        private static int Seed(string text, long period)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char ch in text)
                {
                    hash ^= ch;
                    hash *= 16777619;
                }
                hash ^= (uint)period;
                hash *= 16777619;
                hash ^= (uint)(period >> 32);
                hash *= 16777619;
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        // ================= Склад станции: расширение =================

        public long GetExpandPrice(string cityId)
        {
            int expansions = 0;
            foreach (StationState st in state.stations)
            {
                if (st != null && st.cityId == cityId)
                {
                    expansions = st.expansions;
                    break;
                }
            }
            return db.stationExpandPriceDollars + (long)db.stationExpandPriceStep * expansions;
        }

        /// <summary>Расширить склад станции на stationExpandSlots мест за доллары.</summary>
        public bool TryExpandStation(CityData city)
        {
            if (city == null) return false;

            long price = GetExpandPrice(city.cityId);
            if (!wallet.TrySpend(Currency.Dollars, price, $"расширение склада {city.displayName}"))
                return false;

            StationState st = state.GetOrCreateStation(city.cityId);
            st.extraSlots += db.stationExpandSlots;
            st.expansions++;
            Debug.Log($"[Station] Склад {city.displayName} расширен: {GetStationCapacity(city.cityId)} мест");
            events.RaiseStationChanged(city);
            return true;
        }

        // ================= Покупка =================

        /// <summary>
        /// Можно ли купить вагон: хватает монет и есть место — в поезде, который стоит в городе, или на складе станции.
        /// </summary>
        public bool CanBuy(WagonOffer offer, TrainInstance trainHere, out string reason)
        {
            if (offer.type == null || offer.origin == null || offer.destination == null)
            {
                reason = "предложение не настроено";
                return false;
            }
            List<int> bought = GetBoughtSlots(offer.origin.cityId, CurrentOfferPeriod);
            if (bought != null && bought.Contains(offer.slot))
            {
                reason = "этот вагон уже куплен";
                return false;
            }
            if (offer.origin == offer.destination)
            {
                reason = "город назначения совпадает с городом покупки";
                return false;
            }
            if (!wallet.CanAfford(Currency.Coins, offer.price))
            {
                reason = $"не хватает монет ({offer.price})";
                return false;
            }
            if (!HasTrainSpace(trainHere, offer.origin.cityId) && !HasStationSpace(offer.origin.cityId))
            {
                reason = "нет места ни в поезде, ни на складе станции";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// Купить вагон. Сначала пробует прицепить к поезду trainHere, если места нет — кладёт на склад станции.
        /// </summary>
        public WagonInstance TryBuy(WagonOffer offer, TrainInstance trainHere)
        {
            if (!CanBuy(offer, trainHere, out string reason))
            {
                Debug.LogWarning($"[Wagon] Покупка невозможна: {reason}");
                return null;
            }
            if (!wallet.TrySpend(Currency.Coins, offer.price, $"вагон → {offer.destination.displayName}"))
                return null;

            var wagon = new WagonInstance
            {
                instanceId = state.NewInstanceId("wagon"),
                wagonTypeId = offer.type.wagonTypeId,
                destinationCityId = offer.destination.cityId,
                originCityId = offer.origin.cityId,
                incomeCoins = offer.income
            };
            state.wagons.Add(wagon);
            MarkBought(offer.origin.cityId, offer.slot);

            if (HasTrainSpace(trainHere, offer.origin.cityId))
                PutOnTrain(wagon, trainHere);
            else
                PutOnStation(wagon, offer.origin.cityId);

            Debug.Log($"[Wagon] Куплен вагон → {offer.destination.displayName} ({wagon.instanceId}), " +
                      $"{(wagon.location == WagonLocation.Train ? "прицеплен к поезду" : "на складе станции")}, " +
                      $"доход при доставке {wagon.incomeCoins}");
            events.RaiseWagonBought(wagon);
            return wagon;
        }

        // ================= Прицепка / отцепка =================

        /// <summary>Отцепить вагон от поезда на склад станции, где поезд стоит.</summary>
        public bool TryDetach(TrainInstance train, WagonInstance wagon)
        {
            if (train == null || wagon == null || wagon.location != WagonLocation.Train || wagon.locationId != train.instanceId)
            {
                Debug.LogWarning("[Wagon] Этот вагон не в этом поезде");
                return false;
            }
            if (train.IsMoving || string.IsNullOrEmpty(train.currentCityId))
            {
                Debug.LogWarning("[Wagon] Отцепить можно только у стоящего поезда");
                return false;
            }
            if (!HasStationSpace(train.currentCityId))
            {
                Debug.LogWarning("[Wagon] На складе станции нет места");
                return false;
            }

            train.wagonIds.Remove(wagon.instanceId);
            PutOnStation(wagon, train.currentCityId);
            events.RaiseWagonMoved(wagon);
            return true;
        }

        /// <summary>Прицепить вагон со склада станции к поезду, который стоит в этом городе.</summary>
        public bool TryAttach(TrainInstance train, WagonInstance wagon)
        {
            if (train == null || wagon == null || wagon.location != WagonLocation.Station)
            {
                Debug.LogWarning("[Wagon] Вагон не на складе станции");
                return false;
            }
            if (!HasTrainSpace(train, wagon.locationId))
            {
                Debug.LogWarning("[Wagon] Поезд не в этом городе, в пути или состав полон");
                return false;
            }

            PutOnTrain(wagon, train);
            events.RaiseWagonMoved(wagon);
            return true;
        }

        // ================= Выгрузка =================

        /// <summary>Поезд доехал до города маршрута — выгрузить вагоны, назначенные сюда.</summary>
        private void OnTrainReachedStop(TrainInstance train, CityData city, bool isFinal)
        {
            if (train == null || city == null) return;

            for (int i = train.wagonIds.Count - 1; i >= 0; i--)
            {
                WagonInstance wagon = state.FindWagon(train.wagonIds[i]);
                if (wagon == null)
                {
                    train.wagonIds.RemoveAt(i); // ссылка на несуществующий вагон — чистим
                    continue;
                }
                if (wagon.destinationCityId != city.cityId) continue;

                train.wagonIds.RemoveAt(i);
                state.wagons.Remove(wagon);
                wallet.Add(Currency.Coins, wagon.incomeCoins, $"доставка в {city.displayName}");
                Debug.Log($"[Wagon] Вагон {wagon.instanceId} выгружен в {city.displayName}, +{wagon.incomeCoins} монет");
                events.RaiseWagonDelivered(wagon, city, wagon.incomeCoins);
            }
        }

        // ================= Внутреннее =================

        private bool HasTrainSpace(TrainInstance train, string cityId)
        {
            return train != null && !train.IsMoving && train.currentCityId == cityId &&
                   GetTrainUsedSlots(train) < GetTrainCapacity(train);
        }

        private bool HasStationSpace(string cityId) => GetStationUsedSlots(cityId) < GetStationCapacity(cityId);

        private static void PutOnTrain(WagonInstance wagon, TrainInstance train)
        {
            wagon.location = WagonLocation.Train;
            wagon.locationId = train.instanceId;
            if (!train.wagonIds.Contains(wagon.instanceId))
                train.wagonIds.Add(wagon.instanceId);
        }

        private static void PutOnStation(WagonInstance wagon, string cityId)
        {
            wagon.location = WagonLocation.Station;
            wagon.locationId = cityId;
        }
    }
}
