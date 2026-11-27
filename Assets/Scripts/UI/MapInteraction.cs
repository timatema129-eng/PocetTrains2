using UnityEngine;
using TrainManager.Map;

namespace TrainManager.UI
{
    /// <summary>
    /// Куда идёт нажатие на станцию: в режиме маршрута — в RoutePlanner, иначе открывается окно станции.
    /// Висит на всегда активном объекте (карте), потому что окно и панель маршрута бывают скрыты.
    /// </summary>
    public class MapInteraction : MonoBehaviour
    {
        public MapView map;
        public RoutePlanner planner;
        public StationWindow stationWindow;

        private void Start()
        {
            if (map == null) return;
            foreach (StationView station in map.Stations)
                station.Clicked += OnStationClicked;
        }

        private void OnStationClicked(StationView station)
        {
            if (planner != null && planner.IsPlanning)
                planner.OnStationClicked(station);
            else if (stationWindow != null)
                stationWindow.Open(station.city);
        }
    }
}
