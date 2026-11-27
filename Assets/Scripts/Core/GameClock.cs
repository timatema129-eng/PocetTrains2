using System;

namespace TrainManager.Core
{
    /// <summary>
    /// Источник времени. Вся логика берёт время только отсюда, а не из Time.deltaTime:
    /// так поездки, таймеры и курс работают и пока игра закрыта.
    /// Интерфейс нужен, чтобы в тестах можно было «перемотать» время.
    /// </summary>
    public interface IGameClock
    {
        /// <summary>Текущее время, Unix-миллисекунды (UTC).</summary>
        long NowUnixMs { get; }
    }

    /// <summary>
    /// Время устройства. Внимание: игрок может перевести часы. Без сервера защита только частичная
    /// (см. kb/13, раздел «Риски»).
    /// </summary>
    public class SystemGameClock : IGameClock
    {
        public long NowUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
