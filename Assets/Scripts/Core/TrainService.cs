using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;
using TrainManager.Trains;

namespace TrainManager.Core
{
    /// <summary>
    /// Правила движения поездов. Обычный C#-класс (не MonoBehaviour): работает с GameState,
    /// конфигами из GameDatabase и временем из IGameClock.
    /// Правила: у поезда один активный маршрут; пока он в пути, новый не запустить;
    /// отправить можно только из города, где поезд стоит; между соседними городами маршрута нужна линия.
    /// </summary>
    public class TrainService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly IGameClock clock;
        private readonly GameEvents events;
        private readonly FuelService fuel;

        public TrainService(GameState state, GameDatabase db, IGameClock clock, GameEvents events, FuelService fuel)
        {
            this.state = state;
            this.db = db;
            this.clock = clock;
            this.events = events;
            this.fuel = fuel;
        }

        public IReadOnlyList<TrainInstance> Trains => state.trains;

        public TrainInstance GetTrainByIndex(int index)
        {
            return index >= 0 && index < state.trains.Count ? state.trains[index] : null;
        }

        public TrainData GetModel(TrainInstance train) => train != null ? db.GetTrain(train.modelId) : null;

        public string GetDisplayName(TrainInstance train)
        {
            TrainData model = GetModel(train);
            return model != null ? model.displayName : (train != null ? train.modelId : "?");
        }

        /// <summary>Добавить игроку новый поезд модели model в городе city.</summary>
        public TrainInstance CreateTrain(TrainData model, CityData city)
        {
            var train = new TrainInstance
            {
                instanceId = state.NewInstanceId("train"),
                modelId = model.trainId,
                currentCityId = city.cityId,
                state = TrainState.Idle,
                fuel = model.maxFuel
            };
            state.trains.Add(train);
            Debug.Log($"[Train] Новый поезд {model.displayName} ({train.instanceId}) в городе {city.displayName}");
            return train;
        }

        // ================= Отправление =================

        /// <summary>Можно ли отправить поезд по готовому маршруту (ассет RouteData).</summary>
        public bool CanStartRoute(TrainInstance train, RouteData route, out string reason)
        {
            if (route == null || !route.IsValid)
            {
                reason = "маршрут не настроен";
                return false;
            }
            return CanStartTrip(train, route.stops, out reason);
        }

        /// <summary>Отправить поезд по готовому маршруту (ассет RouteData).</summary>
        public bool TryStartRoute(TrainInstance train, RouteData route)
        {
            if (route == null || !route.IsValid)
            {
                Debug.LogWarning("[Train] Отправление невозможно: маршрут не настроен");
                return false;
            }
            return TryStartTrip(train, route.stops, route.routeId);
        }

        /// <summary>
        /// Можно ли отправить поезд по цепочке городов stops (первый — где поезд стоит).
        /// reason — почему нельзя.
        /// </summary>
        public bool CanStartTrip(TrainInstance train, IReadOnlyList<CityData> stops, out string reason)
        {
            if (train == null)
            {
                reason = "поезд не найден";
                return false;
            }
            if (stops == null || stops.Count < 2)
            {
                reason = "в маршруте меньше двух городов";
                return false;
            }
            for (int i = 0; i < stops.Count; i++)
            {
                if (stops[i] == null)
                {
                    reason = "в маршруте пустой город";
                    return false;
                }
            }

            TrainData model = GetModel(train);
            if (model == null || model.baseSpeed <= 0f)
            {
                reason = $"нет модели «{train.modelId}» в GameDatabase или скорость 0";
                return false;
            }
            if (train.IsMoving)
            {
                reason = "поезд уже в пути";
                return false;
            }
            if (train.currentCityId != stops[0].cityId)
            {
                reason = $"поезд не в городе {stops[0].displayName}";
                return false;
            }
            for (int i = 1; i < stops.Count; i++)
            {
                if (db.FindLine(stops[i - 1], stops[i]) == null)
                {
                    reason = $"нет линии {stops[i - 1].displayName} — {stops[i].displayName}";
                    return false;
                }
            }

            float need = fuel.GetTripFuel(train, GetTripDistance(stops));
            if (!fuel.HasFuelFor(train, need))
            {
                reason = $"не хватает топлива: нужно {need:0.#}, есть {train.fuel:0.#}";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Отправить поезд по цепочке городов. routeId — ID ассета маршрута, если он есть (может быть пустым).
        /// Возвращает false, если нельзя (причина пишется в Console).
        /// </summary>
        public bool TryStartTrip(TrainInstance train, IReadOnlyList<CityData> stops, string routeId = null)
        {
            if (!CanStartTrip(train, stops, out string reason))
            {
                Debug.LogWarning($"[Train] Отправление невозможно: {reason}");
                return false;
            }

            TrainData model = GetModel(train);
            float tripFuel = fuel.GetTripFuel(train, GetTripDistance(stops));
            fuel.Consume(train, tripFuel);

            long now = clock.NowUnixMs;
            long t = now;

            var trip = new TripState { routeId = routeId ?? "", reachedStopIndex = 0 };
            trip.stopIds.Add(stops[0].cityId);
            trip.stopTimesUnixMs.Add(now);

            for (int i = 1; i < stops.Count; i++)
            {
                float distance = db.FindLine(stops[i - 1], stops[i]).distance;
                t += LegDurationMs(distance, model.baseSpeed);
                trip.stopIds.Add(stops[i].cityId);
                trip.stopTimesUnixMs.Add(t);
            }

            train.trip = trip;
            train.currentCityId = ""; // поезд покинул станцию и не находится ни в одном городе
            train.state = TrainState.Moving;

            string name = DescribeStops(trip.stopIds);
            Debug.Log($"[Train] {model.displayName} отправлен: {name}. " +
                      $"Дистанция {GetTripDistance(stops):0}, скорость {model.baseSpeed}, в пути {(t - now) / 1000f:0.0} с, " +
                      $"топливо −{tripFuel:0.#} (толчок {fuel.GetPushFuel(train):0.#}), осталось {train.fuel:0.#}");

            events.RaiseTrainDeparted(train, trip);
            return true;
        }

        /// <summary>Длительность участка в мс: дистанция / скорость × secondsPerTimeUnit.</summary>
        public long LegDurationMs(float distance, float speed)
        {
            if (speed <= 0f) return 0;
            double seconds = distance / speed * db.secondsPerTimeUnit;
            return (long)System.Math.Round(seconds * 1000.0);
        }

        /// <summary>Сумма дистанций линий маршрута. -1, если какой-то линии нет.</summary>
        public float GetRouteDistance(RouteData route)
        {
            if (route == null || !route.IsValid) return -1f;
            return GetTripDistance(route.stops);
        }

        /// <summary>Сумма дистанций линий цепочки городов. -1, если какой-то линии нет.</summary>
        public float GetTripDistance(IReadOnlyList<CityData> stops)
        {
            if (stops == null || stops.Count < 2) return -1f;

            float sum = 0f;
            for (int i = 1; i < stops.Count; i++)
            {
                RailwayLineData line = db.FindLine(stops[i - 1], stops[i]);
                if (line == null) return -1f;
                sum += line.distance;
            }
            return sum;
        }

        /// <summary>Сколько секунд займёт поездка этого поезда по цепочке. -1, если нельзя посчитать.</summary>
        public float GetTripSeconds(TrainInstance train, IReadOnlyList<CityData> stops)
        {
            TrainData model = GetModel(train);
            float distance = GetTripDistance(stops);
            if (model == null || model.baseSpeed <= 0f || distance < 0f) return -1f;
            return LegDurationMs(distance, model.baseSpeed) / 1000f;
        }

        /// <summary>Названия городов через тире: «Лондон — Париж — Берлин».</summary>
        public string DescribeStops(IReadOnlyList<string> cityIds)
        {
            if (cityIds == null || cityIds.Count == 0) return "?";
            var parts = new string[cityIds.Count];
            for (int i = 0; i < cityIds.Count; i++)
            {
                CityData city = db.GetCity(cityIds[i]);
                parts[i] = city != null ? city.displayName : cityIds[i];
            }
            return string.Join(" — ", parts);
        }

        // ================= Движение =================

        /// <summary>
        /// Проверить все поезда по текущему времени. Вызывается каждый кадр и сразу после загрузки:
        /// если игра была закрыта, поезд «догонит» все остановки, которые проехал за это время.
        /// </summary>
        public void Tick()
        {
            long now = clock.NowUnixMs;
            foreach (TrainInstance train in state.trains)
            {
                if (train != null && train.IsMoving)
                    Advance(train, now);
            }
        }

        private void Advance(TrainInstance train, long now)
        {
            TripState trip = train.trip;

            while (trip.reachedStopIndex < trip.LastIndex &&
                   now >= trip.stopTimesUnixMs[trip.reachedStopIndex + 1])
            {
                trip.reachedStopIndex++;
                bool isFinal = trip.reachedStopIndex == trip.LastIndex;
                string cityId = trip.stopIds[trip.reachedStopIndex];
                CityData city = db.GetCity(cityId);

                if (isFinal)
                {
                    train.currentCityId = cityId;
                    train.state = TrainState.Arrived;
                    train.trip = new TripState();
                    Debug.Log($"[Train] {GetDisplayName(train)} прибыл в {CityName(city, cityId)}");
                }
                else
                {
                    Debug.Log($"[Train] {GetDisplayName(train)} проехал остановку {CityName(city, cityId)}");
                }

                events.RaiseTrainReachedStop(train, city, isFinal);
                if (isFinal)
                {
                    // После выгрузки состав легче — проверяем, хватит ли топлива хотя бы на толчок
                    fuel.UpdateOutOfFuel(train);
                    break;
                }
            }
        }

        /// <summary>
        /// Где поезд сейчас: между какими городами и какая доля участка пройдена (0..1).
        /// Если поезд стоит — fromCityId = toCityId = город стоянки.
        /// </summary>
        public void GetPosition(TrainInstance train, out string fromCityId, out string toCityId, out float legProgress)
        {
            if (train == null || !train.IsMoving)
            {
                fromCityId = train != null ? train.currentCityId : null;
                toCityId = fromCityId;
                legProgress = 0f;
                return;
            }

            TripState trip = train.trip;
            int leg = Mathf.Clamp(trip.reachedStopIndex, 0, trip.LastIndex - 1);
            long start = trip.stopTimesUnixMs[leg];
            long end = trip.stopTimesUnixMs[leg + 1];

            fromCityId = trip.stopIds[leg];
            toCityId = trip.stopIds[leg + 1];
            legProgress = end > start ? Mathf.Clamp01((float)(clock.NowUnixMs - start) / (end - start)) : 1f;
        }

        /// <summary>Доля всей поездки (0..1).</summary>
        public float GetTripProgress(TrainInstance train)
        {
            if (train == null || !train.IsMoving) return 0f;

            long start = train.trip.DepartureUnixMs;
            long end = train.trip.FinalArrivalUnixMs;
            return end > start ? Mathf.Clamp01((float)(clock.NowUnixMs - start) / (end - start)) : 1f;
        }

        /// <summary>Сколько секунд осталось до конечной.</summary>
        public float GetSecondsLeft(TrainInstance train)
        {
            if (train == null || !train.IsMoving) return 0f;
            return Mathf.Max(0f, (train.trip.FinalArrivalUnixMs - clock.NowUnixMs) / 1000f);
        }

        private static string CityName(CityData city, string fallbackId) =>
            city != null ? city.displayName : fallbackId;
    }
}
