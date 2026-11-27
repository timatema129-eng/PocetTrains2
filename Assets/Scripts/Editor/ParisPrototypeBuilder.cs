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
    /// Одним нажатием создаёт данные (регион, города, линии, маршруты, поезд, GameDatabase)
    /// и сцену ParisPrototype со всеми объектами и уже назначенными ссылками.
    /// Уже существующие файлы данных НЕ перезаписываются — твои правки в них сохранятся.
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
                    "Сцена ParisPrototype уже существует. Пересоздать её?\n(Файлы данных в Assets/Data не трогаются.)",
                    "Пересоздать", "Отмена"))
            {
                return;
            }

            // ---------- 1. Данные ----------
            EnsureFolder(DataRoot + "/Regions");
            EnsureFolder(DataRoot + "/Cities");
            EnsureFolder(DataRoot + "/Lines");
            EnsureFolder(DataRoot + "/Routes");
            EnsureFolder(DataRoot + "/Trains");
            EnsureFolder("Assets/Scenes");

            RegionData europe = LoadOrCreate<RegionData>(DataRoot + "/Regions/Region_Europe.asset", r =>
            {
                r.regionId = "europe"; r.displayName = "Европа";
            });

            CityData paris = CreateCity("City_Paris", "paris", "Париж", new Vector2(0f, -150f), europe);
            CityData london = CreateCity("City_London", "london", "Лондон", new Vector2(-500f, 250f), europe);
            CityData berlin = CreateCity("City_Berlin", "berlin", "Берлин", new Vector2(500f, 250f), europe);

            RailwayLineData lineParisLondon = CreateLineData("Line_Paris_London", "paris_london", europe,
                paris, london, 300f, new Color(0.95f, 0.75f, 0.25f));
            RailwayLineData lineParisBerlin = CreateLineData("Line_Paris_Berlin", "paris_berlin", europe,
                paris, berlin, 600f, new Color(0.35f, 0.85f, 0.55f));

            var routes = new List<RouteData>
            {
                CreateRoute("Route_Paris_London", "paris_london", paris, london),
                CreateRoute("Route_Paris_Berlin", "paris_berlin", paris, berlin),
                CreateRoute("Route_London_Paris", "london_paris", london, paris),
                CreateRoute("Route_Berlin_Paris", "berlin_paris", berlin, paris),
                // Сквозные маршруты через Париж — проверка цепочки городов
                CreateRoute("Route_London_Paris_Berlin", "london_paris_berlin", london, paris, berlin),
                CreateRoute("Route_Berlin_Paris_London", "berlin_paris_london", berlin, paris, london)
            };

            TrainData standard = LoadOrCreate<TrainData>(DataRoot + "/Trains/Train_Standard.asset", t =>
            {
                t.trainId = "train_standard"; t.displayName = "Стандарт"; t.rarity = TrainRarity.Common;
                t.baseSpeed = 50f; t.maxWagons = 8; t.maxFuel = 25; t.maxFuelWagons = 1; t.partsRequiredForAssembly = 3;
            });

            GameDatabase database = LoadOrCreate<GameDatabase>(DataRoot + "/GameDatabase.asset", d =>
            {
                d.secondsPerTimeUnit = 1f;
            });
            AddUnique(database.regions, europe);
            AddUnique(database.cities, paris);
            AddUnique(database.cities, london);
            AddUnique(database.cities, berlin);
            AddUnique(database.lines, lineParisLondon);
            AddUnique(database.lines, lineParisBerlin);
            foreach (RouteData route in routes) AddUnique(database.routes, route);
            AddUnique(database.trains, standard);
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

            Image background = CreateImage("Background", canvasT, new Color(0.18f, 0.24f, 0.2f));
            Stretch(background.rectTransform);

            // Карта
            var mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(canvasT, false);
            Stretch((RectTransform)mapGo.transform);
            MapView map = mapGo.AddComponent<MapView>();

            foreach (RailwayLineData line in database.lines)
                CreateLine(line, mapGo.transform);

            CreateStation(paris, mapGo.transform);
            CreateStation(london, mapGo.transform);
            CreateStation(berlin, mapGo.transform);

            // Картинка поезда на карте (последней — чтобы рисовалась поверх станций)
            Image trainImage = CreateImage("Train_Standard (View)", mapGo.transform, new Color(0.85f, 0.2f, 0.2f));
            trainImage.rectTransform.sizeDelta = new Vector2(70f, 36f);
            trainImage.rectTransform.anchoredPosition = paris.mapPosition;
            trainImage.raycastTarget = false;
            TrainView trainView = trainImage.gameObject.AddComponent<TrainView>();
            trainView.root = root;
            trainView.map = map;
            trainView.trainIndex = 0;

            // Статус
            TMP_Text status = CreateText("TrainStatus", canvasT, "", 32, TextAlignmentOptions.TopLeft);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(0f, 1f);
            statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(40f, -30f);
            statusRect.sizeDelta = new Vector2(1200f, 200f);
            TrainStatusText statusText = status.gameObject.AddComponent<TrainStatusText>();
            statusText.root = root;
            statusText.text = status;

            // Кнопки маршрутов
            var panelGo = new GameObject("RouteButtons", typeof(RectTransform));
            panelGo.transform.SetParent(canvasT, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(1560f, 230f);
            var grid = panelGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(500f, 100f);
            grid.spacing = new Vector2(30f, 30f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.LowerCenter;

            foreach (RouteData route in routes)
                CreateRouteButton(panelGo.transform, root, route);

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

        private static CityData CreateCity(string file, string id, string displayName, Vector2 mapPosition, RegionData region)
        {
            return LoadOrCreate<CityData>(DataRoot + "/Cities/" + file + ".asset", c =>
            {
                c.cityId = id; c.displayName = displayName; c.mapPosition = mapPosition; c.region = region;
            });
        }

        private static RailwayLineData CreateLineData(string file, string id, RegionData region,
            CityData a, CityData b, float distance, Color color)
        {
            return LoadOrCreate<RailwayLineData>(DataRoot + "/Lines/" + file + ".asset", l =>
            {
                l.lineId = id; l.region = region; l.cityA = a; l.cityB = b; l.distance = distance; l.color = color;
            });
        }

        private static RouteData CreateRoute(string file, string id, params CityData[] stops)
        {
            RouteData route = LoadOrCreate<RouteData>(DataRoot + "/Routes/" + file + ".asset", r =>
            {
                r.routeId = id; r.stops = new List<CityData>(stops);
            });

            // Ассет от старой версии (откуда/куда без списка остановок) — заполняем остановки.
            if (route.StopCount == 0)
            {
                route.stops = new List<CityData>(stops);
                EditorUtility.SetDirty(route);
            }
            return route;
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
            Image dot = CreateImage("Station_" + city.cityId, map, new Color(0.25f, 0.55f, 0.95f));
            dot.rectTransform.sizeDelta = new Vector2(60f, 60f);
            dot.rectTransform.anchoredPosition = city.mapPosition;
            dot.raycastTarget = false;

            TMP_Text label = CreateText("Label", dot.transform, city.displayName, 36, TextAlignmentOptions.Center);
            label.rectTransform.anchoredPosition = new Vector2(0f, -65f);
            label.rectTransform.sizeDelta = new Vector2(300f, 50f);

            StationView view = dot.gameObject.AddComponent<StationView>();
            view.city = city;
            view.label = label;
        }

        private static void CreateLine(RailwayLineData data, Transform map)
        {
            if (data == null || !data.IsValid) return;

            Vector2 a = data.cityA.mapPosition;
            Vector2 b = data.cityB.mapPosition;
            Image line = CreateImage("Line_" + data.lineId, map, data.color);
            line.raycastTarget = false;
            RectTransform rect = line.rectTransform;
            Vector2 delta = b - a;
            rect.sizeDelta = new Vector2(delta.magnitude, 8f);
            rect.anchoredPosition = (a + b) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static void CreateRouteButton(Transform parent, GameRoot root, RouteData route)
        {
            Image image = CreateImage("Button_" + route.routeId, parent, new Color(0.95f, 0.95f, 0.95f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text label = CreateText("Text", image.transform, route.DisplayName, 28, TextAlignmentOptions.Center);
            label.color = new Color(0.1f, 0.1f, 0.1f);
            Stretch(label.rectTransform);

            RouteButton routeButton = image.gameObject.AddComponent<RouteButton>();
            routeButton.root = root;
            routeButton.trainIndex = 0;
            routeButton.route = route;
            routeButton.label = label;
        }
    }
}
