using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Визуал движения поезда (2D)
/// </summary>
public class TrainAnimationView : MonoBehaviour
{
    [Header("UI")]
    public Image trainImage;
    public Transform wagonsContainer;
    public GameObject wagonPrefab;

    [Header("Background")]
    public Image backgroundImage;

    [Header("Movement")]
    public float speed = 300f;
    public float resetX = -1200f;
    public float endX = 1200f;

    [Header("FX")]
    public ParticleSystem smokeFX;

    // ===== DATA =====

    private TrainData currentTrain;
    private List<string> waypoints;

    private int currentCityIndex = 0;

    private RectTransform trainRect;

    // ===== INIT =====

    private void Awake()
    {
        trainRect = trainImage.GetComponent<RectTransform>();
    }

    // ===== ПУБЛИЧНЫЙ СТАРТ =====

    public void Init(TrainData train, List<string> route)
    {
        currentTrain = train;
        waypoints = route;

        SetupTrain();
        SetupWagons();
        UpdateBackground();

        StartCoroutine(MoveLoop());
    }

    // ===== НАСТРОЙКА =====

    private void SetupTrain()
    {
        trainImage.sprite = currentTrain.sprite;

        if (smokeFX != null)
            smokeFX.Play();
    }

    private void SetupWagons()
    {
        // очистка старых
        foreach (Transform child in wagonsContainer)
            Destroy(child.gameObject);

        // создаём вагоны
        int wagonCount = currentTrain.maxWagons;

        for (int i = 0; i < wagonCount; i++)
        {
            Instantiate(wagonPrefab, wagonsContainer);
        }
    }

    private void UpdateBackground()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        string cityId = waypoints[Mathf.Clamp(currentCityIndex, 0, waypoints.Count - 1)];

        CityData city = StationManager.Instance.GetCityById(cityId);

        if (city != null && city.backgroundSprite != null)
        {
            backgroundImage.sprite = city.backgroundSprite;
        }
    }

    // ===== ДВИЖЕНИЕ =====

    private IEnumerator MoveLoop()
    {
        while (true)
        {
            // движение
            trainRect.anchoredPosition += Vector2.right * speed * Time.deltaTime;

            // если уехал за экран
            if (trainRect.anchoredPosition.x > endX)
            {
                ResetTrainPosition();
                OnReachedNextCity();
            }

            yield return null;
        }
    }

    private void ResetTrainPosition()
    {
        trainRect.anchoredPosition = new Vector2(resetX, trainRect.anchoredPosition.y);
    }

    private void OnReachedNextCity()
    {
        currentCityIndex++;

        if (currentCityIndex >= waypoints.Count)
            currentCityIndex = waypoints.Count - 1;

        UpdateBackground();

        Debug.Log("Перешли к следующему городу: " + waypoints[currentCityIndex]);
    }
}