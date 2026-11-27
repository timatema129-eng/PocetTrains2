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
                t.baseSpeed = 50f; t.maxWagons = 8; t.maxFuel = 25; t.maxFuelWagons = 1; t.partsRequiredForAssembly = 3;
            });
            AddUnique(database.trains, standard);

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

            // Картинка поезда на карте (последней — чтобы рисовалась поверх станций)
            Image trainImage = CreateImage("Train_Standard (View)", mapGo.transform, new Color(0.85f, 0.2f, 0.2f));
            trainImage.rectTransform.sizeDelta = new Vector2(30f, 16f);
            trainImage.rectTransform.anchoredPosition = database.startCity.mapPosition;
            trainImage.raycastTarget = false;
            TrainView trainView = trainImage.gameObject.AddComponent<TrainView>();
            trainView.root = root;
            trainView.map = map;
            trainView.trainIndex = 0;

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

            // Составление маршрута: текст + кнопки «Отправить» и «Сбросить»
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
            planRect.offsetMax = new Vector2(-580f, 0f);

            Button sendButton = CreateButton("SendButton", panelGo.transform, "Отправить", new Vector2(-290f, 0f));
            Button clearButton = CreateButton("ClearButton", panelGo.transform, "Сбросить", new Vector2(-10f, 0f));

            RoutePlanner planner = panelGo.AddComponent<RoutePlanner>();
            planner.root = root;
            planner.map = map;
            planner.planText = planText;
            planner.sendButton = sendButton;
            planner.clearButton = clearButton;
            planner.trainIndex = 0;

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

            Vector2 a = data.cityA.mapPosition;
            Vector2 b = data.cityB.mapPosition;
            Image line = CreateImage("Line_" + data.lineId, map, data.color);
            line.raycastTarget = false;
            RectTransform rect = line.rectTransform;
            Vector2 delta = b - a;
            rect.sizeDelta = new Vector2(delta.magnitude, 4f);
            rect.anchoredPosition = (a + b) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
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
