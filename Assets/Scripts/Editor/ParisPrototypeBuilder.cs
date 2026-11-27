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
        private const string ScenePath = "Assets/Scenes/ParisPrototype.unity";

        [MenuItem("Tools/Train Manager/Build Paris Prototype Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Train Manager",
                    "Сцена ParisPrototype уже существует. Пересоздать её?\n(Города и линии обновятся по файлу карты.)",
                    "Пересоздать", "Отмена"))
            {
                return;
            }

            // ---------- 1. Данные ----------
            GameDatabase database = MapDataImporter.Import();
            if (database == null) return;

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

            CityData paris = database.GetCity("paris");
            if (paris == null)
            {
                Debug.LogError("[Train Manager] В карте нет города paris — не с чего начать игру");
                return;
            }
            if (database.startCity == null) database.startCity = paris;
            if (database.startTrain == null) database.startTrain = standard;
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
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform canvasT = canvasGo.transform;

            // Окно карты (viewport): перетаскивание через ScrollRect, масштаб — MapNavigator
            var viewportGo = new GameObject("MapViewport", typeof(RectTransform));
            viewportGo.transform.SetParent(canvasT, false);
            var viewportRect = (RectTransform)viewportGo.transform;
            Stretch(viewportRect);
            Image viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(0.16f, 0.3f, 0.5f); // «море»; картинка нужна, чтобы ловить перетаскивание
            viewportGo.AddComponent<RectMask2D>();

            // Карта (content) — размер по крайним городам + поля
            var mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(viewportGo.transform, false);
            var mapRect = (RectTransform)mapGo.transform;
            mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapRect.pivot = new Vector2(0.5f, 0.5f);
            mapRect.sizeDelta = MapSize(database, 200f);
            MapView map = mapGo.AddComponent<MapView>();

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

            foreach (CityData city in database.cities)
                CreateStation(city, mapGo.transform);

            // Выбранный поезд (один объект на сцене)
            var selectionGo = new GameObject("TrainSelection");
            TrainSelection selection = selectionGo.AddComponent<TrainSelection>();
            selection.root = root;

            // Поезда на карте: шаблон картинки, TrainLayer копирует его для каждого поезда
            // (последними — чтобы рисовались поверх станций)
            Image trainImage = CreateImage("TrainTemplate", mapGo.transform, new Color(0.85f, 0.2f, 0.2f));
            trainImage.rectTransform.sizeDelta = new Vector2(30f, 16f);
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

            MapNavigator navigator = viewportGo.AddComponent<MapNavigator>();
            navigator.scroll = scroll;
            navigator.content = mapRect;
            navigator.root = root;

            // Статус
            TMP_Text status = CreateText("TrainStatus", canvasT, "", 26, TextAlignmentOptions.TopLeft);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(0f, 1f);
            statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(40f, -30f);
            statusRect.sizeDelta = new Vector2(700f, 180f);
            status.outlineWidth = 0.2f;
            status.outlineColor = Color.black;
            TrainStatusText statusText = status.gameObject.AddComponent<TrainStatusText>();
            statusText.root = root;
            statusText.text = status;
            statusText.selection = selection;

            // Баланс (справа сверху)
            TMP_Text walletLabel = CreateText("Wallet", canvasT, "", 30, TextAlignmentOptions.TopRight);
            RectTransform walletRect = walletLabel.rectTransform;
            walletRect.anchorMin = walletRect.anchorMax = new Vector2(1f, 1f);
            walletRect.pivot = new Vector2(1f, 1f);
            walletRect.anchoredPosition = new Vector2(-30f, -24f);
            walletRect.sizeDelta = new Vector2(700f, 50f);
            walletLabel.outlineWidth = 0.2f;
            walletLabel.outlineColor = Color.black;
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
            panelRect.anchoredPosition = new Vector2(0f, 20f);
            panelRect.sizeDelta = new Vector2(1760f, 110f);
            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.6f);

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

            // Список поездов: кнопка «Поезда» под статусом и окно со списком
            CreateTrainList(canvasT, root, selection, navigator, window);

            // Нажатие на станцию: маршрут или окно станции
            MapInteraction interaction = mapGo.AddComponent<MapInteraction>();
            interaction.map = map;
            interaction.planner = planner;
            interaction.stationWindow = window;

            // ---------- 3. Сохранение ----------
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = rootGo;
            Debug.Log("[Train Manager] Сцена ParisPrototype создана: " + ScenePath + ". Нажми Play.");
        }

        // ================= Вспомогательное =================

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

        private static T LoadOrCreate<T>(string path, Action<T> init) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

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

        private static void CreateStation(CityData city, Transform map)
        {
            Image dot = CreateImage("Station_" + city.cityId, map, StationView.NormalColor);
            dot.rectTransform.sizeDelta = new Vector2(18f, 18f);
            dot.rectTransform.anchoredPosition = city.mapPosition;
            Button button = dot.gameObject.AddComponent<Button>();
            button.targetGraphic = dot;
            button.transition = Selectable.Transition.None; // цвет меняет RoutePlanner (подсветка)

            TMP_Text label = CreateText("Label", dot.transform, city.displayName, 16, TextAlignmentOptions.Center);
            label.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            label.rectTransform.sizeDelta = new Vector2(180f, 22f);

            StationView view = dot.gameObject.AddComponent<StationView>();
            view.city = city;
            view.label = label;
            view.dot = dot;
        }

        private static void CreateLine(RailwayLineData data, Transform map)
        {
            if (data == null || !data.IsValid) return;

            const float width = 5f;
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
            Place(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -84f), new Vector2(640f, 860f));
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.1f, 0.92f);

            // Заголовок и «закрыть»
            TMP_Text title = CreateText("Title", go.transform, "Станция", 32, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(-110f, 50f));
            Button close = CreateSmallButton("Close", go.transform, "X", 26);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), new Vector2(60f, 50f));

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
            viewportImage.color = new Color(1f, 1f, 1f, 0.03f);
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

        private static void CreateTrainList(Transform canvasT, GameRoot root, TrainSelection selection,
            MapNavigator navigator, StationWindow stationWindow)
        {
            Button toggle = CreateSmallButton("TrainsButton", canvasT, "Поезда", 24);
            Place((RectTransform)toggle.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -220f), new Vector2(200f, 56f));

            var go = new GameObject("TrainList", typeof(RectTransform));
            go.transform.SetParent(canvasT, false);
            Place((RectTransform)go.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -286f), new Vector2(620f, 0f));
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.1f, 0.92f);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ListRow row = CreateListRowTemplate(go.transform);

            TrainListWindow list = go.AddComponent<TrainListWindow>();
            list.root = root;
            list.selection = selection;
            list.navigator = navigator;
            list.stationWindow = stationWindow;
            list.toggleButton = toggle;
            list.listContainer = (RectTransform)go.transform;
            list.rowTemplate = row;
            // Окно остаётся активным в сцене: TrainListWindow сам скроет его в Awake и повесит кнопку
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
            Image image = CreateImage(name, parent, new Color(0.95f, 0.95f, 0.95f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text label = CreateText("Text", image.transform, text, fontSize, TextAlignmentOptions.Center);
            label.color = new Color(0.1f, 0.1f, 0.1f);
            Stretch(label.rectTransform);
            return button;
        }

        /// <summary>Кнопка справа внизу панели. x — сдвиг от правого края.</summary>
        private static Button CreateButton(string name, Transform parent, string text, Vector2 offsetFromRight)
        {
            Image image = CreateImage(name, parent, new Color(0.95f, 0.95f, 0.95f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 90f);
            rect.anchoredPosition = offsetFromRight;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text label = CreateText("Text", image.transform, text, 30, TextAlignmentOptions.Center);
            label.color = new Color(0.1f, 0.1f, 0.1f);
            Stretch(label.rectTransform);
            return button;
        }
    }
}
