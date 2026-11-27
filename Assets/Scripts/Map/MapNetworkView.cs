using UnityEngine;
using TrainManager.Core;
using TrainManager.UI;

namespace TrainManager.Map
{
    /// <summary>
    /// Раскраска сети на карте: купленные линии яркие (цвет — выбранный игроком или по умолчанию),
    /// некупленные тусклые;
    /// станции закрытых регионов серые. Пока идёт составление маршрута, станции красит RoutePlanner.
    /// </summary>
    public class MapNetworkView : MonoBehaviour
    {
        public GameRoot root;
        public MapView map;
        public RoutePlanner planner;

        public static readonly Color LockedStationColor = new Color(0.45f, 0.45f, 0.5f);

        private LineView[] lines;
        private float nextRefresh;

        private void Start()
        {
            lines = GetComponentsInChildren<LineView>(true);
        }

        private void Update()
        {
            if (root == null || !root.IsReady || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;

            foreach (LineView view in lines)
            {
                root.Network.TryGetCustomColor(view.line, out Color color);
                view.Apply(root.Network.IsLineOwned(view.line), color, root.Network.IsElectrified(view.line));
            }

            if (planner != null && planner.IsPlanning) return;

            foreach (StationView station in map.Stations)
                station.SetHighlight(root.Network.IsCityAccessible(station.city)
                    ? StationView.NormalColor
                    : LockedStationColor);
        }
    }
}
