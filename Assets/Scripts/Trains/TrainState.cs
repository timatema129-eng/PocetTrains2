namespace TrainManager.Trains
{
    /// <summary>
    /// Состояние поезда.
    /// </summary>
    public enum TrainState
    {
        Idle,       // стоит на станции, готов к отправлению
        Moving,     // в пути
        Arrived,    // только что прибыл (тоже можно отправлять дальше)
        OutOfFuel   // зарезервировано для этапа 3 (топливо), пока не используется
    }
}
