using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Map
{
    /// <summary>
    /// Карта: знает, где на экране стоит каждая станция.
    /// Станции (StationView) и поезд (TrainView) должны быть дочерними объектами карты.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        private readonly Dictionary<CityData, StationView> stations = new Dictionary<CityData, StationView>();

        private void Awake()
        {
            foreach (StationView station in GetComponentsInChildren<StationView>(true))
            {
                if (station.city == null)
                {
                    Debug.LogWarning($"[Map] У станции {station.name} не назначен City", station);
                    continue;
                }
                stations[station.city] = station;
            }
        }

        /// <summary>Позиция станции города на карте. false — если такой станции нет.</summary>
        public bool TryGetStationPosition(CityData city, out Vector2 position)
        {
            if (city != null && stations.TryGetValue(city, out StationView station))
            {
                position = city.mapPosition;
                return true;
            }

            position = Vector2.zero;
            return false;
        }
    }
}
