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

        /// <summary>Сколько монет заплачено при покупке (для цены продажи). 0 — старое сохранение.</summary>
        public long paidCoins;

        public WagonLocation location;

        /// <summary>cityId (склад станции) или instanceId поезда — смотря по location.</summary>
        public string locationId;
    }

    /// <summary>Состояние купленной линии: докупленные пути и цвет, выбранный игроком.</summary>
    [Serializable]
    public class LineState
    {
        public string lineId;

        /// <summary>Сколько путей докуплено. Поездов на линии одновременно = 1 + extraTracks.</summary>
        public int extraTracks;

        /// <summary>Цвет из LinePalette, выбранный игроком. -1 — цвет по умолчанию (из карты).</summary>
        public int colorIndex = -1;
    }

    /// <summary>Состояние станции игрока: докупленные места и уже купленные вагоны из текущего списка.</summary>
    [Serializable]
    public class StationState
    {
        public string cityId;

        /// <summary>Сколько мест докуплено сверх базовых.</summary>
        public int extraSlots;

        /// <summary>Сколько раз расширяли склад (для роста цены).</summary>
        public int expansions;

        /// <summary>Номер периода списка предложений, к которому относится boughtOfferSlots.</summary>
        public long offerPeriod = -1;

        /// <summary>Какие позиции текущего списка уже куплены (исчезают до обновления).</summary>
        public System.Collections.Generic.List<int> boughtOfferSlots = new System.Collections.Generic.List<int>();
    }
}
