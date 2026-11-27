using System.Collections.Generic;
using UnityEngine;

namespace TrainManager.Map
{
    /// <summary>
    /// Карта: знает, где на экране стоит каждая станция (по cityId).
    /// Станции (StationView) и поезда (TrainView) должны быть дочерними объектами карты.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        private readonly Dictionary<string, StationView> stations = new Dictionary<string, StationView>();

        private void Awake()
        {
            foreach (StationView station in GetComponentsInChildren<StationView>(true))
            {
                if (station.city == null || string.IsNullOrEmpty(station.city.cityId))
                {
                    Debug.LogWarning($"[Map] У станции {station.name} не назначен City или пустой cityId", station);
                    continue;
                }
                stations[station.city.cityId] = station;
            }
        }

        public IEnumerable<StationView> Stations => stations.Values;

        /// <summary>Позиция станции города на карте. false — если такой станции нет.</summary>
        public bool TryGetStationPosition(string cityId, out Vector2 position)
        {
            if (!string.IsNullOrEmpty(cityId) && stations.TryGetValue(cityId, out StationView station))
            {
                position = station.city.mapPosition;
                return true;
            }

            position = Vector2.zero;
            return false;
        }
    }
}
