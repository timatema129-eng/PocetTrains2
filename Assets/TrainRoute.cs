using System.Collections.Generic;

/// <summary>
/// Данные маршрута поезда
/// </summary>
[System.Serializable]
public class TrainRoute
{
    public string trainId;

    public List<string> waypoints = new List<string>();

    // cityId → сколько вагонов выгрузить
    public Dictionary<string, int> wagonsToDeliver = new Dictionary<string, int>();
}