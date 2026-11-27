using System;

namespace TrainManager.Core
{
    public enum WagonLocation
    {
        /// <summary>Лежит на складе станции (locationId = cityId).</summary>
        Station,
        /// <summary>Прицеплен к поезду (locationId = instanceId поезда).</summary>
        Train
    }

    /// <summary>
    /// Конкретный вагон игрока. Один объект на всё время жизни: склад → поезд → доставлен (удаляется).
    /// Так вагон не может потеряться или раздвоиться: он всегда ровно в одном месте.
    /// </summary>
    [Serializable]
    public class WagonInstance
    {
        public string instanceId;

        /// <summary>wagonTypeId из WagonData.</summary>
        public string wagonTypeId;

        /// <summary>Город назначения. Вагон выгружается только здесь.</summary>
        public string destinationCityId;

        /// <summary>Где вагон куплен (для статистики и заданий).</summary>
        public string originCityId;

        /// <summary>Доход за доставку, посчитан при покупке — чтобы игрок видел честную цифру заранее.</summary>
        public long incomeCoins;

        public WagonLocation location;

        /// <summary>cityId (склад станции) или instanceId поезда — смотря по location.</summary>
        public string locationId;
    }

    /// <summary>Состояние станции игрока: сколько мест докуплено (Этап 4).</summary>
    [Serializable]
    public class StationState
    {
        public string cityId;
        public int extraSlots;
    }
}
