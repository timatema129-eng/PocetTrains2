using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;
using TrainManager.Map;
using TrainManager.Trains;
using TrainManager.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TrainManager.EditorTools
{
    /// <summary>
    /// Меню Tools → Train Manager → Build Paris Prototype Scene.
    /// Одним нажатием импортирует карту (Assets/Data/MapSource/*.txt → города и линии),
    /// создаёт поезд и GameDatabase и собирает сцену ParisPrototype со всеми ссылками.
    /// Города и линии обновляются по текстовому файлу карты (правь файл, а не ассеты).
    /// Ассет поезда, если уже есть, не перезаписывается.
    /// Tools → Train Manager → Delete Save удаляет сохранение (игра начнётся заново).
    /// </summary>
    public static class ParisPrototypeBuilder
    {
        private const string DataRoot = "Assets/Data";
        public const string ScenePath = "Assets/Scenes/ParisPrototype.unity";

        /// <summary>
        /// Номер версии сборщика сцены. Увеличивать, когда меняется вид сцены: перед Play редактор сравнит его
        /// с номером в сцене и сам пересоберёт её (SceneVersionCheck). Текст LayoutVersion виден в игре сверху.
        /// </summary>
        public const int LayoutBuild = 4;
        public const string LayoutVersion = "UI 4 (27.11)";

        [MenuItem("Tools/Train Manager/Build Paris Prototype Scene")]
        public static void Build() => Build(true);

        /// <summary>
        /// Собрать сцену заново. interactive = false — без вопросов (автосборка перед Play и сборка APK).
        /// Возвращает false, если собрать не удалось (подробности в Console).
        /// </summary>
        public static bool Build(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            // ---------- 1. Данные ----------
            GameDatabase database = MapDataImporter.Import();
            if (database == null) return false;

            EnsureFolder(DataRoot + "/Trains");
            EnsureFolder("Assets/Scenes");

            TrainData standard = LoadOrCreate<TrainData>(DataRoot + "/Trains/Train_Standard.asset", t =>
            {
                t.trainId = "train_standard"; t.displayName = "Стандарт"; t.rarity = TrainRarity.Common;
                t.baseSpeed = 50f; t.maxWagons = 8; t.maxFuel = 25; t.partsRequiredForAssembly = 3;
                // Двигатели и топливные вагоны: структура утверждена, числа — рабочие (Артем уточнит)
                t.mainEngines = 1; t.maxExtraEngines = 1; t.wagonsPerExtraEngine = 2;
                t.maxFuelWagons = 1; t.fuelPerFuelWagon = 15;
            });
            AddUnique(database.trains, standard);

            // Редкий и эпический поезда. Крутой (эпический) — числа Артема: 200 / 16 / 400 / двигатель 1 / топливных 4,
            // 15 деталей. Средний (редкий) — 100 / 12 / 150 из прежней модели TrainData, 8 деталей (уточнить)
            TrainData rare = LoadOrCreate<TrainData>(DataRoot + "/Trains/Train_Express.asset", t =>
            {
                t.trainId = "train_express"; t.displayName = "Экспресс"; t.rarity = TrainRarity.Rare;
                t.baseSpeed = 100f; t.maxWagons = 12; t.maxFuel = 150; t.partsRequiredForAssembly = 8;
                t.mainEngines = 1; t.maxExtraEngines = 1; t.wagonsPerExtraEngine = 3;
                t.maxFuelWagons = 2; t.fuelPerFuelWagon = 40;
                t.serviceCostMultiplier = 2f; // ТО, ремонт и буксировка дороже — черновик
            });
            AddUnique(database.trains, rare);
            TrainData epic = LoadOrCreate<TrainData>(DataRoot + "/Trains/Train_Maglev.asset", t =>
            {
                t.trainId = "train_maglev"; t.displayName = "Маглев"; t.rarity = TrainRarity.Epic;
                t.baseSpeed = 200f; t.maxWagons = 16; t.maxFuel = 400; t.partsRequiredForAssembly = 15;
                t.mainEngines = 1; t.maxExtraEngines = 1; t.wagonsPerExtraEngine = 4;
                t.maxFuelWagons = 4; t.fuelPerFuelWagon = 100;
                t.serviceCostMultiplier = 4f;
                t.isElectric = true; // маглев — электропоезд (Артем может поменять в ассете)
            });
            AddUnique(database.trains, epic);

            if (database.boxes == null || database.boxes.Count == 0)
            {
                database.boxes = new System.Collections.Generic.List<BoxConfig>
                {
                    new BoxConfig { boxId = "box_common", displayName = "Обычный бокс", rarity = TrainRarity.Common, priceDollars = 5 },
                    new BoxConfig { boxId = "box_rare", displayName = "Редкий бокс", rarity = TrainRarity.Rare, priceDollars = 20 },
                    new BoxConfig { boxId = "box_epic", displayName = "Эпический бокс", rarity = TrainRarity.Epic, priceDollars = 100 },
                };
            }

            EnsureFolder(DataRoot + "/Wagons");
            WagonData cargo = LoadOrCreate<WagonData>(DataRoot + "/Wagons/Wagon_Cargo.asset", w =>
            {
                w.wagonTypeId = "cargo"; w.displayName = "Грузовой"; w.kind = WagonKind.Cargo;
            });
            AddUnique(database.wagons, cargo);
            WagonData fuelWagon = LoadOrCreate<WagonData>(DataRoot + "/Wagons/Wagon_Fuel.asset", w =>
            {
                w.wagonTypeId = "fuel"; w.displayName = "Топливный вагон"; w.kind = WagonKind.Fuel;
                w.fixedPriceCoins = 150; w.color = new Color(0.9f, 0.6f, 0.1f);
            });
            AddUnique(database.wagons, fuelWagon);
            WagonData engineWagon = LoadOrCreate<WagonData>(DataRoot + "/Wagons/Wagon_Engine.asset", w =>
            {
                w.wagonTypeId = "engine"; w.displayName = "Вагон-двигатель"; w.kind = WagonKind.Engine;
                w.fixedPriceCoins = 300; w.color = new Color(0.3f, 0.3f, 0.35f);
            });
            AddUnique(database.wagons, engineWagon);

            // Картинки поездов (из постера Артема). Задаются, только если в ассете спрайта ещё нет
            AssignSprite(standard, "standard_loco");
            AssignSprite(rare, "express_loco");
            AssignSprite(epic, "maglev_loco");

            CityData paris = database.GetCity("paris");
            if (paris == null)
            {
                Debug.LogError("[Train Manager] В карте нет города paris — не с чего начать игру");
                return false;
            }
            if (database.startCity == null) database.startCity = paris;
            if (database.startTrain == null) database.startTrain = standard;
            // Для теста — три стартовых поезда (1 + 2 дополнительных). Работает для новой игры (Tools → Delete Save)
            if (database.extraStartTrainsForTesting < 2) database.extraStartTrainsForTesting = 2;
            database.ClearCache();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            // ---------- 2. Сцена ----------
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            CreateEventSystem();

            // Точка входа игры: загрузка/новая игра, время, сохранение
            var rootGo = new GameObject("GameRoot");
            GameRoot root = rootGo.AddComponent<GameRoot>();
            var rootSo = new SerializedObject(root);
            rootSo.FindProperty("database").objectReferenceValue = database;
            rootSo.FindProperty("sceneLayoutBuild").intValue = LayoutBuild;
            rootSo.FindProperty("mapSourceHash").stringValue = MapDataImporter.SourceHash();
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // телефон в альбомной ориентации: держим высоту
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform canvasT = canvasGo.transform;

            // Окно карты (viewport): перетаскивание через ScrollRect, масштаб — MapNavigator
            var viewportGo = new GameObject("MapViewport", typeof(RectTransform));
            viewportGo.transform.SetParent(canvasT, false);
            var viewportRect = (RectTransform)viewportGo.transform;
            Stretch(viewportRect);
            viewportRect.offsetMin = new Vector2(0f, BottomBarHeight);
            viewportRect.offsetMax = new Vector2(0f, -TopBarHeight);
            Image viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(0.05f, 0.3f, 0.62f); // «море»; картинка нужна, чтобы ловить перетаскивание
            viewportGo.AddComponent<RectMask2D>();

            // Карта (content) — размер по крайним городам + поля
            var mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(viewportGo.transform, false);
            var mapRect = (RectTransform)mapGo.transform;
            mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapRect.pivot = new Vector2(0.5f, 0.5f);
            mapRect.sizeDelta = MapSize(database, 200f);
            MapView map = mapGo.AddComponent<MapView>();

            // Фон — карта мира (первым ребёнком, под линиями и станциями)
            Sprite worldSprite = LoadSprite(WorldMapPath, 4096);
            if (worldSprite != null)
            {
                Image world = CreateImage("WorldMap", mapGo.transform, Color.white);
                world.sprite = worldSprite;
                world.raycastTarget = false;
                world.rectTransform.sizeDelta = WorldMapSize;
                world.rectTransform.anchoredPosition = WorldMapCenter;
                mapRect.sizeDelta = Vector2.Max(mapRect.sizeDelta, WorldMapSize);
            }

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = mapRect;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.1f;
            scroll.scrollSensitivity = 0f; // колесо мыши — масштаб, а не прокрутка

            foreach (RailwayLineData line in database.lines)
                CreateLine(line, mapGo.transform);

            // Станции: круглые точки; названия раскладываются так, чтобы не налезать (часть видна только при приближении)
            Sprite dotSprite = LoadSprite(DotSpritePath, 128);
            Dictionary<CityData, (int side, float minZoom)> labels = LayoutLabels(database.cities);
            foreach (CityData city in database.cities)
                if (city != null) CreateStation(city, mapGo.transform, dotSprite, labels[city]);

            // Выбранный поезд (один объект на сцене)
            var selectionGo = new GameObject("TrainSelection");
            TrainSelection selection = selectionGo.AddComponent<TrainSelection>();
            selection.root = root;

            // Поезда на карте: шаблон картинки, TrainLayer копирует его для каждого поезда
            // (последними — чтобы рисовались поверх станций)
            Sprite locoSprite = LoadSprite(TrainsArt + "standard_loco.png", 512);
            Image trainImage = CreateImage("TrainTemplate", mapGo.transform, locoSprite != null ? Color.white : new Color(0.85f, 0.2f, 0.2f));
            trainImage.sprite = locoSprite;
            trainImage.preserveAspect = true;
            trainImage.rectTransform.sizeDelta = locoSprite != null ? new Vector2(66f, 26f) : new Vector2(30f, 16f);
            trainImage.rectTransform.anchoredPosition = database.startCity.mapPosition;
            trainImage.raycastTarget = false;
            TrainView trainView = trainImage.gameObject.AddComponent<TrainView>();
            trainView.root = root;
            trainView.map = map;
            trainImage.gameObject.SetActive(false);

            TrainLayer trainLayer = mapGo.AddComponent<TrainLayer>();
            trainLayer.root = root;
            trainLayer.map = map;
            trainLayer.selection = selection;
            trainLayer.template = trainView;
            if (locoSprite != null)
            {
                trainLayer.normalColor = Color.white;                  // спрайт без подкраски
                trainLayer.selectedColor = new Color(1f, 0.9f, 0.45f); // выбранный — жёлтый оттенок
            }

            // Значки заданий над городами
            TMP_Text questMarker = CreateText("QuestMarkerTemplate", mapGo.transform, "!", 30, TextAlignmentOptions.Center);
            questMarker.color = new Color(1f, 0.82f, 0.29f);
            questMarker.fontStyle = FontStyles.Bold;
            questMarker.rectTransform.sizeDelta = new Vector2(30f, 36f);
            questMarker.gameObject.SetActive(false);
            QuestMarkerLayer questLayer = mapGo.AddComponent<QuestMarkerLayer>();
            questLayer.root = root;
            questLayer.map = map;
            questLayer.template = questMarker.rectTransform;

            MapNavigator navigator = viewportGo.AddComponent<MapNavigator>();
            navigator.scroll = scroll;
            navigator.content = mapRect;
            navigator.root = root;
            navigator.startScale = 0.9f;

            StationLabelZoom labelZoom = mapGo.AddComponent<StationLabelZoom>();
            labelZoom.map = map;
            labelZoom.content = mapRect;

            // Верхняя панель: статус выбранного поезда слева, монеты и доллары справа
            Image topBar = CreateImage("TopBar", canvasT, PanelColor);
            Place(topBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, TopBarHeight));

            TMP_Text status = CreateText("TrainStatus", topBar.transform, "", 22, TextAlignmentOptions.MidlineLeft);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0f, 0.5f);
            statusRect.offsetMin = new Vector2(24f, 4f);
            statusRect.offsetMax = new Vector2(-720f, -4f);
            status.enableAutoSizing = true;
            status.fontSizeMin = 14f;
            status.fontSizeMax = 24f;
            TrainStatusText statusText = status.gameObject.AddComponent<TrainStatusText>();
            statusText.root = root;
            statusText.text = status;
            statusText.selection = selection;

            TMP_Text walletLabel = CreateText("Wallet", topBar.transform, "Монеты: …    Доллары: …", 36, TextAlignmentOptions.MidlineRight);
            walletLabel.fontStyle = FontStyles.Bold;
            walletLabel.color = new Color(1f, 0.85f, 0.35f);
            RectTransform walletRect = walletLabel.rectTransform;
            walletRect.anchorMin = new Vector2(1f, 0f);
            walletRect.anchorMax = new Vector2(1f, 1f);
            walletRect.pivot = new Vector2(1f, 0.5f);
            walletRect.anchoredPosition = new Vector2(-24f, 0f);
            walletRect.sizeDelta = new Vector2(680f, 0f);

            // Номер версии сцены (мелко, по центру сверху): по нему видно, что сцена пересобрана после обновления
            TMP_Text version = CreateText("LayoutVersion", topBar.transform, LayoutVersion, 16, TextAlignmentOptions.Bottom);
            version.color = new Color(1f, 1f, 1f, 0.45f);
            Place(version.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(300f, 22f));
            WalletText wallet = walletLabel.gameObject.AddComponent<WalletText>();
            wallet.root = root;
            wallet.text = walletLabel;

            // Составление маршрута: текст + кнопки «Отправить», «Сбросить», «Отмена» (скрыто, пока не нажат «Маршрут»)
            var panelGo = new GameObject("RoutePlanner", typeof(RectTransform));
            panelGo.transform.SetParent(canvasT, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, BottomBarHeight + 10f);
            panelRect.sizeDelta = new Vector2(1760f, 110f);
            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.color = PanelColor;
            panelGo.AddComponent<ExclusivePanel>();

            TMP_Text planText = CreateText("PlanText", panelGo.transform, "", 26, TextAlignmentOptions.MidlineLeft);
            RectTransform planRect = planText.rectTransform;
            planRect.anchorMin = new Vector2(0f, 0f);
            planRect.anchorMax = new Vector2(1f, 1f);
            planRect.offsetMin = new Vector2(20f, 0f);
            planRect.offsetMax = new Vector2(-860f, 0f);

            Button sendButton = CreateButton("SendButton", panelGo.transform, "Отправить", new Vector2(-570f, 0f));
            Button clearButton = CreateButton("ClearButton", panelGo.transform, "Сбросить", new Vector2(-290f, 0f));
            Button cancelButton = CreateButton("CancelButton", panelGo.transform, "Отмена", new Vector2(-10f, 0f));

            RoutePlanner planner = panelGo.AddComponent<RoutePlanner>();
            planner.root = root;
            planner.map = map;
            planner.selection = selection;
            planner.planText = planText;
            planner.sendButton = sendButton;
            planner.clearButton = clearButton;
            planner.cancelButton = cancelButton;
            panelGo.SetActive(false);

            // Окно станции (скрыто, открывается нажатием на станцию)
            StationWindow window = CreateStationWindow(canvasT, root, selection, planner);

            // Раскраска сети: купленные/некупленные линии, закрытые регионы
            MapNetworkView network = mapGo.AddComponent<MapNetworkView>();
            network.root = root;
            network.map = map;
            network.planner = planner;

            // Окна справа (одно за раз) и нижняя панель кнопок
            window.gameObject.AddComponent<ExclusivePanel>();
            GameObject trainList = CreateTrainList(canvasT, root, selection, navigator, window);
            GameObject boxes = CreateBoxWindow(canvasT, root);
            GameObject shop = CreateShopWindow(canvasT, root);
            GameObject quests = CreateQuestWindow(canvasT, root, navigator, window);
            GameObject trip = CreateTripView(canvasT, root, selection);
            window.metroWindow = CreateMetroWindow(canvasT, root);
            CreateBottomBar(canvasT, new[]
            {
                ("Поезда", trainList), ("Боксы", boxes), ("Магазин", shop), ("Задания", quests), ("Вид сбоку", trip),
            });

            // Реклама: окно-вопрос и летящий приз (последними — поверх всего)
            AdPrompt prompt = CreateAdPrompt(canvasT);
            window.prompt = prompt;
            Button prizeButton = CreateSmallButton("FlyingPrize", canvasT, "Приз!", 24);
            ((RectTransform)prizeButton.transform).sizeDelta = new Vector2(140f, 64f);
            prizeButton.GetComponent<Image>().color = new Color(1f, 0.82f, 0.29f);
            FlyingPrize prize = canvasT.gameObject.AddComponent<FlyingPrize>();
            prize.root = root;
            prize.button = prizeButton;
            prize.prompt = prompt;
            Place((RectTransform)prizeButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -TopBarHeight - 20f), new Vector2(140f, 64f));
            prizeButton.gameObject.SetActive(false);
            prompt.transform.SetAsLastSibling();

            // Ошибки — красной плашкой на экране (на телефоне Console не видно)
            Image errorPanel = CreateImage("ErrorPanel", canvasT, new Color(0.55f, 0.08f, 0.08f, 0.95f));
            Place(errorPanel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -TopBarHeight - 6f), new Vector2(-40f, 150f));
            errorPanel.gameObject.AddComponent<Button>().targetGraphic = errorPanel;
            TMP_Text errorText = CreateText("Text", errorPanel.transform, "", 20, TextAlignmentOptions.TopLeft);
            Stretch(errorText.rectTransform);
            errorText.rectTransform.offsetMin = new Vector2(16f, 8f);
            errorText.rectTransform.offsetMax = new Vector2(-16f, -8f);
            errorText.overflowMode = TextOverflowModes.Truncate;
            ErrorOverlay errors = canvasGo.AddComponent<ErrorOverlay>();
            errors.panel = errorPanel.gameObject;
            errors.text = errorText;

            // Нажатие на станцию: маршрут или окно станции
            MapInteraction interaction = mapGo.AddComponent<MapInteraction>();
            interaction.map = map;
            interaction.planner = planner;
            interaction.stationWindow = window;

            // ---------- 3. Сохранение ----------
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[Train Manager] Не удалось сохранить сцену " + ScenePath);
                return false;
            }
            if (!CheckSavedScene(database)) return false;
            // Список сцен для сборки APK. Меняем, только если сцены там ещё нет, — чтобы файл настроек не менялся каждый раз
            bool listed = false;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                if (entry != null && entry.path == ScenePath) listed = true;
            if (!listed)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            // Play всегда запускает эту сцену, какая бы ни была открыта
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Selection.activeGameObject = rootGo;
            Debug.Log($"[Train Manager] Сцена ParisPrototype собрана ({LayoutVersion}): городов {database.cities.Count}, линий {database.lines.Count}. Нажми Play.");
            return true;
        }

        // ================= Вспомогательное =================

        /// <summary>
        /// Проверка после сохранения: в файле сцены у GameRoot должна быть ссылка именно на файл базы игры.
        /// Если нет — пишет в Console, что именно не так (это сообщение нужно прислать Claude).
        /// </summary>
        private static bool CheckSavedScene(GameDatabase database)
        {
            string dbPath = AssetDatabase.GetAssetPath(database);
            string dbGuid = AssetDatabase.AssetPathToGUID(dbPath);
            string text = System.IO.File.ReadAllText(ScenePath);
            var m = System.Text.RegularExpressions.Regex.Match(text, @"database: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?");
            string sceneRef = m.Success ? m.Value : "(нет поля database)";
            bool ok = m.Success && m.Groups[2].Value == dbGuid && !string.IsNullOrEmpty(dbGuid);
            if (ok) return true;

            Debug.LogError("[Train Manager] ПРИШЛИ ЭТО CLAUDE: в сцене потерялась ссылка на базу игры. " +
                           $"persistent={EditorUtility.IsPersistent(database)}, path='{dbPath}', guid='{dbGuid}', " +
                           $"в сцене: {sceneRef}, файл есть: {System.IO.File.Exists(MapDataImporter.DatabasePath)}, " +
                           $"тип по пути: {AssetDatabase.GetMainAssetTypeAtPath(MapDataImporter.DatabasePath)}");
            return false;
        }

        [MenuItem("Tools/Train Manager/Delete Save")]
        public static void DeleteSave()
        {
            if (!SaveService.HasSave)
            {
                Debug.Log("[Train Manager] Сохранения нет: " + SaveService.SavePath);
                return;
            }
            if (!EditorUtility.DisplayDialog("Train Manager",
                    "Удалить сохранение? Игра начнётся заново.\n" + SaveService.SavePath, "Удалить", "Отмена"))
                return;

            SaveService.Delete();
            Debug.Log("[Train Manager] Сохранение удалено: " + SaveService.SavePath);
        }

        /// <summary>Размер карты: симметрично от центра до самого дальнего города + поля.</summary>
        private static Vector2 MapSize(GameDatabase database, float margin)
        {
            float halfX = 0f, halfY = 0f;
            foreach (CityData city in database.cities)
            {
                if (city == null) continue;
                halfX = Mathf.Max(halfX, Mathf.Abs(city.mapPosition.x));
                halfY = Mathf.Max(halfY, Mathf.Abs(city.mapPosition.y));
            }
            return new Vector2((halfX + margin) * 2f, (halfY + margin) * 2f);
        }

        private static void AddUnique<T>(List<T> list, T item) where T : UnityEngine.Object
        {
            if (item != null && !list.Contains(item))
                list.Add(item);
        }

        private static T LoadOrCreate<T>(string path, Action<T> init) where T : ScriptableObject =>
            MapDataImporter.LoadOrCreate(path, init);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            // В проекте включён новый Input System, поэтому нужен этот модуль, а не StandaloneInputModule.
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private const string DotSpritePath = "Assets/Art/UI/station_dot.png";
        private const float DotSize = 22f;
        private const float LabelHeight = 24f;
        private const float LabelFont = 18f;
        // Пороги масштаба, начиная с которых видно название (см. StationLabelZoom)
        private static readonly float[] LabelZoomLevels = { 0.25f, 0.35f, 0.5f, 0.7f, 1f, 1.4f, 2f };

        private static float LabelWidth(string text) => (text != null ? text.Length : 0) * 10.5f + 10f;

        /// <summary>
        /// Раскладка названий городов. Точка и надпись держат постоянный размер на экране (StationLabelZoom),
        /// поэтому при отдалении надписи сближаются. Для каждого города ищем наименьший масштаб и сторону
        /// (снизу/сверху/справа/слева), где надпись не задевает точки и уже размещённые надписи.
        /// </summary>
        private static Dictionary<CityData, (int side, float minZoom)> LayoutLabels(List<CityData> cities)
        {
            var result = new Dictionary<CityData, (int side, float minZoom)>();
            var placed = new List<(CityData city, int side, Vector2 size)>();
            var all = cities.FindAll(c => c != null);

            foreach (float level in LabelZoomLevels)
            {
                float k = StationLabelZoom.ScreenFactor(level); // во сколько раз точка меньше обычного
                foreach (CityData city in all)
                {
                    if (result.ContainsKey(city)) continue;
                    var size = new Vector2(LabelWidth(city.displayName), LabelHeight);
                    for (int side = 0; side < 4; side++)
                    {
                        Rect rect = ScreenRect(city.mapPosition * level + StationLabelZoom.LabelOffset(side, size, DotSize, k), size);
                        bool free = true;
                        foreach (CityData other in all)
                        {
                            if (other == city) continue;
                            if (rect.Overlaps(ScreenRect(other.mapPosition * level, Vector2.one * DotSize * k))) { free = false; break; }
                        }
                        if (free)
                        {
                            foreach (var p in placed)
                            {
                                Vector2 center = p.city.mapPosition * level + StationLabelZoom.LabelOffset(p.side, p.size, DotSize, k);
                                if (rect.Overlaps(ScreenRect(center, p.size))) { free = false; break; }
                            }
                        }
                        if (!free) continue;
                        result[city] = (side, level);
                        placed.Add((city, side, size));
                        break;
                    }
                }
            }

            // Совсем тесные места — название видно только при максимальном приближении
            float last = LabelZoomLevels[LabelZoomLevels.Length - 1] + 0.3f;
            foreach (CityData city in all)
                if (!result.ContainsKey(city))
                    result[city] = (0, last);
            return result;
        }

        private static Rect ScreenRect(Vector2 center, Vector2 size) => new Rect(center - size / 2f, size);

        private static void CreateStation(CityData city, Transform map, Sprite dotSprite, (int side, float minZoom) labelPlace)
        {
            Image dot = CreateImage("Station_" + city.cityId, map, StationView.NormalColor);
            dot.sprite = dotSprite; // круг с тёмной обводкой; цвет — подсветка
            dot.rectTransform.sizeDelta = new Vector2(DotSize, DotSize);
            dot.rectTransform.anchoredPosition = city.mapPosition;
            Button button = dot.gameObject.AddComponent<Button>();
            button.targetGraphic = dot;
            button.transition = Selectable.Transition.None; // цвет меняет RoutePlanner (подсветка)

            TMP_Text label = CreateText("Label", dot.transform, city.displayName, LabelFont, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;
            label.enableWordWrapping = false;
            label.rectTransform.anchoredPosition = StationLabelZoom.LabelOffset(labelPlace.side,
                new Vector2(LabelWidth(city.displayName), LabelHeight), DotSize, 1f); // на ходу пересчитывает StationLabelZoom
            label.rectTransform.sizeDelta = new Vector2(LabelWidth(city.displayName), LabelHeight);

            StationView view = dot.gameObject.AddComponent<StationView>();
            view.city = city;
            view.label = label;
            view.dot = dot;
            view.labelMinZoom = labelPlace.minZoom;
            view.labelSide = labelPlace.side;
        }

        private static void CreateLine(RailwayLineData data, Transform map)
        {
            if (data == null || !data.IsValid) return;

            const float width = 10f;
            Vector2 a = data.cityA.mapPosition;
            Vector2 b = data.cityB.mapPosition;
            Vector2 delta = b - a;
            float length = delta.magnitude;

            // Контейнер, повёрнутый вдоль линии; внутри — полосы/штрихи
            var go = new GameObject("Line_" + data.lineId, typeof(RectTransform));
            go.transform.SetParent(map, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(length, width);
            rect.anchoredPosition = (a + b) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            switch (data.pattern)
            {
                case LinePattern.Dashed:
                {
                    const float dash = 10f, gap = 6f;
                    for (float x = -length / 2f; x < length / 2f; x += dash + gap)
                    {
                        float len = Mathf.Min(dash, length / 2f - x);
                        AddStripe(go.transform, data.color, new Vector2(x + len / 2f, 0f), new Vector2(len, width));
                    }
                    break;
                }
                case LinePattern.TwoTone:
                    AddStripe(go.transform, data.color, new Vector2(0f, width / 4f), new Vector2(length, width / 2f));
                    AddStripe(go.transform, data.secondaryColor, new Vector2(0f, -width / 4f), new Vector2(length, width / 2f));
                    break;
                default:
                    AddStripe(go.transform, data.color, Vector2.zero, new Vector2(length, width));
                    break;
            }

            LineView view = go.AddComponent<LineView>();
            view.line = data;
        }

        private static void AddStripe(Transform parent, Color color, Vector2 position, Vector2 size)
        {
            Image stripe = CreateImage("Stripe", parent, color);
            stripe.raycastTarget = false;
            stripe.rectTransform.anchoredPosition = position;
            stripe.rectTransform.sizeDelta = size;
        }

        private static StationWindow CreateStationWindow(Transform canvasT, GameRoot root, TrainSelection selection,
            RoutePlanner planner)
        {
            var go = new GameObject("StationWindow", typeof(RectTransform));
            go.transform.SetParent(canvasT, false);
            var rect = (RectTransform)go.transform;
            PlaceSidePanel(rect);
            Image bg = go.AddComponent<Image>();
            bg.color = PanelColor;

            // Заголовок и «закрыть»
            TMP_Text title = CreateText("Title", go.transform, "Станция", 34, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(-110f, 50f));
            Button close = CreateSmallButton("Close", go.transform, "×", 36);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(70f, 56f));

            // Переключатель поездов
            Button prev = CreateSmallButton("PrevTrain", go.transform, "<", 30);
            Place((RectTransform)prev.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -70f), new Vector2(60f, 56f));
            Button next = CreateSmallButton("NextTrain", go.transform, ">", 30);
            Place((RectTransform)next.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -70f), new Vector2(60f, 56f));
            TMP_Text trainLabel = CreateText("TrainLabel", go.transform, "", 22, TextAlignmentOptions.Center);
            Place(trainLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(-160f, 56f));

            // «Маршрут» и «Расширить склад»
            Button route = CreateSmallButton("RouteButton", go.transform, "Маршрут", 24);
            Place((RectTransform)route.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -138f), new Vector2(240f, 56f));
            Button expand = CreateSmallButton("ExpandButton", go.transform, "", 22);
            Place((RectTransform)expand.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -138f), new Vector2(340f, 56f));
            TMP_Text expandLabel = expand.GetComponentInChildren<TMP_Text>();

            // Заправка
            Button refuel = CreateSmallButton("RefuelButton", go.transform, "", 22);
            Place((RectTransform)refuel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(-24f, 56f));
            TMP_Text refuelLabel = refuel.GetComponentInChildren<TMP_Text>();

            // Прокручиваемый список
            var viewportGo = new GameObject("ListViewport", typeof(RectTransform));
            viewportGo.transform.SetParent(go.transform, false);
            var viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-12f, -274f);
            Image viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.04f);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            // Шаблон строки: текст + две кнопки справа
            ListRow row = CreateListRowTemplate(content);

            StationWindow window = go.AddComponent<StationWindow>();
            window.root = root;
            window.selection = selection;
            window.planner = planner;
            window.titleText = title;
            window.closeButton = close;
            window.prevTrainButton = prev;
            window.nextTrainButton = next;
            window.trainLabel = trainLabel;
            window.routeButton = route;
            window.expandButton = expand;
            window.expandLabel = expandLabel;
            window.refuelButton = refuel;
            window.refuelLabel = refuelLabel;
            window.listContainer = content;
            window.rowTemplate = row;
            go.SetActive(false);
            return window;
        }

        // ================= Общий вид интерфейса =================

        private const float TopBarHeight = 96f;
        private const float BottomBarHeight = 120f;
        private const float SidePanelWidth = 880f;
        private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.13f, 1f);
        private static readonly Color HeaderColor = new Color(0.11f, 0.14f, 0.2f, 1f);

        private const string WorldMapPath = "Assets/Art/Map/world_map.jpg";
        private const string TrainsArt = "Assets/Art/Trains/";
        // Карта мира подогнана под проекцию городов (map; 0; 10; 32; 0; -847 в _World.txt; при смене масштаба размер и сдвиг ×масштаб/24):
        // долгота −170…190, широта −58…77
        private static readonly Vector2 WorldMapSize = new Vector2(11520f, 6272.8f);
        private static readonly Vector2 WorldMapCenter = new Vector2(0f, -0.9f);

        /// <summary>Окно справа между верхней и нижней панелями.</summary>
        private static void PlaceSidePanel(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-SidePanelWidth - 10f, BottomBarHeight + 10f);
            rect.offsetMax = new Vector2(-10f, -TopBarHeight - 10f);
        }

        /// <summary>
        /// Окно справа: заголовок с «×» и прокручиваемый список. Создаётся скрытым, открыто только одно (ExclusivePanel).
        /// </summary>
        private static (GameObject go, RectTransform content, TMP_Text title) CreateSidePanel(Transform canvasT, string name, string titleText)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvasT, false);
            PlaceSidePanel((RectTransform)go.transform);
            go.AddComponent<Image>().color = PanelColor;
            go.AddComponent<ExclusivePanel>();

            Image header = CreateImage("Header", go.transform, HeaderColor);
            Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 70f));
            TMP_Text title = CreateText("Title", header.transform, titleText, 32, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(-110f, 0f));
            Button close = CreateSmallButton("Close", header.transform, "×", 36);
            Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(70f, 56f));
            close.gameObject.AddComponent<ClosePanelButton>().target = go;

            var viewportGo = new GameObject("ListViewport", typeof(RectTransform));
            viewportGo.transform.SetParent(go.transform, false);
            var viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(10f, 10f);
            viewport.offsetMax = new Vector2(-10f, -80f);
            viewportGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            go.SetActive(false);
            return (go, content, title);
        }

        /// <summary>Нижняя панель: большие кнопки, каждая открывает/закрывает своё окно.</summary>
        private static void CreateBottomBar(Transform canvasT, (string label, GameObject panel)[] items)
        {
            Image bar = CreateImage("BottomBar", canvasT, PanelColor);
            Place(bar.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, BottomBarHeight));
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach ((string label, GameObject panel) in items)
            {
                Button b = CreateSmallButton("Nav_" + label, bar.transform, label, 30);
                b.gameObject.AddComponent<PanelToggle>().target = panel;
            }
        }

        /// <summary>Непрозрачные цвета кнопки: выключенная — серая, но не просвечивает.</summary>
        private static void SetOpaqueColors(Button button)
        {
            ColorBlock c = button.colors;
            c.normalColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            c.highlightedColor = new Color(1f, 1f, 1f, 1f);
            c.pressedColor = new Color(0.78f, 0.82f, 0.9f, 1f);
            c.selectedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            c.disabledColor = new Color(0.5f, 0.52f, 0.56f, 1f);
            c.colorMultiplier = 1f;
            button.colors = c;
        }

        /// <summary>Загрузить картинку как спрайт (и выставить настройки импорта). null — файла нет.</summary>
        private static Sprite LoadSprite(string path, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != maxSize ||
                importer.mipmapEnabled || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = maxSize;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Спрайт поезда из Assets/Art/Trains, если в ассете он ещё не задан.</summary>
        private static void AssignSprite(TrainData train, string artName)
        {
            if (train == null || train.sprite != null) return;
            Sprite sprite = LoadSprite(TrainsArt + artName + ".png", 512);
            if (sprite == null) return;
            train.sprite = sprite;
            EditorUtility.SetDirty(train);
        }

        // ================= Окна =================

        private static ListRow CreateRowIn(RectTransform content) => CreateListRowTemplate(content);

        private static GameObject CreateTrainList(Transform canvasT, GameRoot root, TrainSelection selection,
            MapNavigator navigator, StationWindow stationWindow)
        {
            var (go, content, _) = CreateSidePanel(canvasT, "TrainList", "Мои поезда");
            TrainListWindow list = go.AddComponent<TrainListWindow>();
            list.root = root;
            list.selection = selection;
            list.navigator = navigator;
            list.stationWindow = stationWindow;
            list.listContainer = content;
            list.rowTemplate = CreateRowIn(content);
            return go;
        }

        private static GameObject CreateBoxWindow(Transform canvasT, GameRoot root)
        {
            var (go, content, _) = CreateSidePanel(canvasT, "BoxWindow", "Боксы и сборка");
            BoxWindow window = go.AddComponent<BoxWindow>();
            window.root = root;
            window.listContainer = content;
            window.rowTemplate = CreateRowIn(content);
            return go;
        }

        private static GameObject CreateShopWindow(Transform canvasT, GameRoot root)
        {
            var (go, content, _) = CreateSidePanel(canvasT, "ShopWindow", "Магазин");
            ShopWindow window = go.AddComponent<ShopWindow>();
            window.root = root;
            window.listContainer = content;
            window.rowTemplate = CreateRowIn(content);
            return go;
        }

        private static GameObject CreateQuestWindow(Transform canvasT, GameRoot root, MapNavigator navigator, StationWindow stationWindow)
        {
            var (go, content, _) = CreateSidePanel(canvasT, "QuestWindow", "Задания и достижения");
            QuestWindow window = go.AddComponent<QuestWindow>();
            window.root = root;
            window.navigator = navigator;
            window.stationWindow = stationWindow;
            window.listContainer = content;
            window.rowTemplate = CreateRowIn(content);
            return go;
        }

        private static MetroWindow CreateMetroWindow(Transform canvasT, GameRoot root)
        {
            var (go, content, title) = CreateSidePanel(canvasT, "MetroWindow", "Метро");
            MetroWindow window = go.AddComponent<MetroWindow>();
            window.root = root;
            window.titleText = title;
            window.listContainer = content;
            window.rowTemplate = CreateRowIn(content);
            return window;
        }

        /// <summary>Вид поездки сбоку: широкая панель над нижней панелью кнопок.</summary>
        private static GameObject CreateTripView(Transform canvasT, GameRoot root, TrainSelection selection)
        {
            Image bg = CreateImage("TripView", canvasT, PanelColor);
            Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomBarHeight + 10f), new Vector2(-20f, 460f));
            bg.gameObject.AddComponent<ExclusivePanel>();

            TMP_Text caption = CreateText("Caption", bg.transform, "", 26, TextAlignmentOptions.MidlineLeft);
            Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -8f), new Vector2(-130f, 52f));

            Button close = CreateSmallButton("Close", bg.transform, "×", 36);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -6f), new Vector2(70f, 52f));
            close.gameObject.AddComponent<ClosePanelButton>().target = bg.gameObject;

            Image stageImage = CreateImage("Stage", bg.transform, Color.black);
            RectTransform stage = stageImage.rectTransform;
            stage.anchorMin = new Vector2(0f, 0f);
            stage.anchorMax = new Vector2(1f, 1f);
            stage.offsetMin = new Vector2(10f, 10f);
            stage.offsetMax = new Vector2(-10f, -66f);
            stageImage.gameObject.AddComponent<RectMask2D>();

            TripView view = bg.gameObject.AddComponent<TripView>();
            view.root = root;
            view.selection = selection;
            view.stage = stage;
            view.caption = caption;
            bg.gameObject.SetActive(false);
            return bg.gameObject;
        }

        /// <summary>Окно-вопрос по центру поверх всего, фон затемнён и не пропускает нажатия.</summary>
        private static AdPrompt CreateAdPrompt(Transform canvasT)
        {
            Image dim = CreateImage("AdPrompt", canvasT, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);

            Image box = CreateImage("Box", dim.transform, PanelColor);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 300f));

            TMP_Text label = CreateText("Text", box.transform, "", 32, TextAlignmentOptions.Center);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(-48f, 150f));

            Button yes = CreateSmallButton("Yes", box.transform, "Смотреть", 30);
            Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f),
                new Vector2(-12f, 28f), new Vector2(280f, 76f));
            Button no = CreateSmallButton("No", box.transform, "Нет", 30);
            Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f),
                new Vector2(12f, 28f), new Vector2(280f, 76f));

            AdPrompt prompt = dim.gameObject.AddComponent<AdPrompt>();
            prompt.label = label;
            prompt.yesButton = yes;
            prompt.yesLabel = yes.GetComponentInChildren<TMP_Text>();
            prompt.noButton = no;
            dim.gameObject.SetActive(false);
            return prompt;
        }


        /// <summary>Шаблон строки списка: текст + кнопка справа + скрытая вторая кнопка (выключен).</summary>
        private static ListRow CreateListRowTemplate(Transform parent)
        {
            var rowGo = new GameObject("RowTemplate", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowGo.AddComponent<LayoutElement>().minHeight = 44f;

            TMP_Text rowLabel = CreateText("Label", rowGo.transform, "", 21, TextAlignmentOptions.MidlineLeft);
            var labelElement = rowLabel.gameObject.AddComponent<LayoutElement>();
            labelElement.flexibleWidth = 1f;
            labelElement.preferredWidth = 10f;

            Button rowButton = CreateSmallButton("Button", rowGo.transform, "", 20);
            var buttonElement = rowButton.gameObject.AddComponent<LayoutElement>();
            buttonElement.preferredWidth = 170f;
            buttonElement.preferredHeight = 42f;

            // Вторая кнопка (например, «Цвет» у купленной линии). По умолчанию скрыта
            Button rowButton2 = CreateSmallButton("Button2", rowGo.transform, "", 20);
            var button2Element = rowButton2.gameObject.AddComponent<LayoutElement>();
            button2Element.preferredWidth = 110f;
            button2Element.preferredHeight = 42f;
            rowButton2.gameObject.SetActive(false);

            ListRow row = rowGo.AddComponent<ListRow>();
            row.label = rowLabel;
            row.button = rowButton;
            row.buttonLabel = rowButton.GetComponentInChildren<TMP_Text>();
            row.button2 = rowButton2;
            row.button2Label = rowButton2.GetComponentInChildren<TMP_Text>(true);
            rowGo.SetActive(false);
            return row;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Светлая кнопка с тёмным текстом. Размер/позицию задаёт вызывающий.</summary>
        private static Button CreateSmallButton(string name, Transform parent, string text, float fontSize)
        {
            Image image = CreateImage(name, parent, Color.white);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetOpaqueColors(button);

            TMP_Text label = CreateText("Text", image.transform, text, fontSize, TextAlignmentOptions.Center);
            label.color = new Color(0.1f, 0.1f, 0.1f);
            Stretch(label.rectTransform);
            return button;
        }

        /// <summary>Кнопка справа внизу панели. x — сдвиг от правого края.</summary>
        private static Button CreateButton(string name, Transform parent, string text, Vector2 offsetFromRight)
        {
            Image image = CreateImage(name, parent, Color.white);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 90f);
            rect.anchoredPosition = offsetFromRight;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetOpaqueColors(button);

            TMP_Text label = CreateText("Text", image.transform, text, 30, TextAlignmentOptions.Center);
            label.color = new Color(0.1f, 0.1f, 0.1f);
            Stretch(label.rectTransform);
            return button;
        }
    }
}
