# Train Manager (рабочее название)

Мобильная 2D-игра про логистику поездов в духе Pocket Trains, расширенная новыми механиками.
Unity **6000.3.12f1 (6.3 LTS)**, URP 2D, C#, Unity UI + TextMeshPro, новый Input System.

## Структура

```
Assets/
├── Scripts/
│   ├── Data/     CityData, RouteData, TrainData — конфиги (ScriptableObject)
│   ├── Trains/   TrainController (логика поезда), TrainState, TrainView (картинка поезда)
│   ├── Map/      MapView, StationView — карта и станции
│   ├── UI/       RouteButton, TrainStatusText
│   └── Editor/   ParisPrototypeBuilder — собирает сцену прототипа
├── Data/         создаётся сборщиком: города, маршруты, поезда
└── Scenes/       создаётся сборщиком: ParisPrototype.unity
```

Правила: все `.cs` в UTF-8 (см. `.editorconfig`); данные — в ScriptableObject, прогресс игрока — не в них;
логика отдельно от картинки; никакой случайности в базовой логике.

## Этап 1: прототип «Париж»

1. Открой проект в Unity Hub и дождись, пока Unity закончит компиляцию (внизу справа пропадёт крутилка).
2. В верхнем меню: **Tools → Train Manager → Build Paris Prototype Scene**.
3. Нажми **Play**.

Что должно быть: поезд (красный прямоугольник) стоит в Париже. Активны кнопки «Париж — Лондон» и «Париж — Берлин».
После нажатия поезд едет (Лондон ~6 с, Берлин ~12 с), все кнопки неактивны, пока он в пути.
По прибытии активна только кнопка обратного маршрута. В Console пишутся сообщения с префиксом `[Train]`.

Старый код (до 27.11.2026) удалён из проекта, он остался в истории Git (коммит a0d81bd).
