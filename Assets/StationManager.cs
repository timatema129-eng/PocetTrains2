using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ”Ô‡‚ÎˇÂÚ „ÓÓ‰‡ÏË, ÔÓÂÁ‰‡ÏË Ì‡ ÒÚ‡ÌˆËˇı Ë ÒÍÎ‡‰‡ÏË
/// </summary>
public class StationManager : MonoBehaviour
{
    public static StationManager Instance;

    [Header("All Cities (Configs)")]
    public List<CityData> allCities;

    // ===== RUNTIME ƒ¿ÕÕ€≈ =====

    private HashSet<string> unlockedCities = new HashSet<string>();

    private Dictionary<string, List<string>> trainsAtCity = new Dictionary<string, List<string>>();
    private Dictionary<string, List<string>> storedWagonsAtCity = new Dictionary<string, List<string>>();

    private Dictionary<string, int> cityStorageLevels = new Dictionary<string, int>(); // ‡Ô„ÂÈ‰˚ ÒÎÓÚÓ‚

    private const string SAVE_KEY = "STATION_DATA";

    // ===== UNITY =====

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ===== √Œ–Œƒ¿ =====

    public bool UnlockCity(string cityId)
    {
        CityData city = GetCityById(cityId);
        if (city == null) return false;

        if (unlockedCities.Contains(cityId))
            return false;

        if (!PlayerManager.Instance.SpendCoins(city.unlockCost))
            return false;

        unlockedCities.Add(cityId);
        Save();

        GameEventsHub.OnCityUnlocked?.Invoke(cityId);

        Debug.Log($"√ÓÓ‰ ÓÚÍ˚Ú: {city.cityName}");
        return true;
    }

    // ===== œŒ≈«ƒ¿ =====

    public void ParkTrainAtCity(string trainId, string cityId)
    {
        if (!trainsAtCity.ContainsKey(cityId))
            trainsAtCity[cityId] = new List<string>();

        if (!trainsAtCity[cityId].Contains(trainId))
        {
            trainsAtCity[cityId].Add(trainId);
            Save();
        }
    }

    public void RemoveTrainFromCity(string trainId, string cityId)
    {
        if (!trainsAtCity.ContainsKey(cityId)) return;

        if (trainsAtCity[cityId].Remove(trainId))
        {
            Save();
        }
    }

    public List<string> GetTrainsAtCity(string cityId)
    {
        if (trainsAtCity.ContainsKey(cityId))
            return trainsAtCity[cityId];

        return new List<string>();
    }

    // ===== — À¿ƒ ¬¿√ŒÕŒ¬ =====

    public List<string> GetStoredWagons(string cityId)
    {
        if (storedWagonsAtCity.ContainsKey(cityId))
            return storedWagonsAtCity[cityId];

        return new List<string>();
    }

    // ===== ¿œ√–≈…ƒ √Œ–Œƒ¿ =====

    public bool UpgradeCityStorage(string cityId)
    {
        const int cost = 10;

        if (!PlayerManager.Instance.SpendDollars(cost))
            return false;

        if (!cityStorageLevels.ContainsKey(cityId))
            cityStorageLevels[cityId] = 0;

        cityStorageLevels[cityId]++;

        Save();

        Debug.Log($"Storage upgraded for city: {cityId}");
        return true;
    }

    public int GetCityStorageLevel(string cityId)
    {
        if (cityStorageLevels.ContainsKey(cityId))
            return cityStorageLevels[cityId];

        return 0;
    }

    // ===== ¬—œŒÃŒ√¿“≈À‹ÕŒ≈ =====

    public CityData GetCityById(string cityId)
    {
        return allCities.Find(c => c.cityId == cityId);
    }

    // ===== —Œ’–¿Õ≈Õ»≈ =====

    private void Save()
    {
        SaveWrapper wrapper = new SaveWrapper
        {
            unlockedCities = new List<string>(unlockedCities),
            trains = ConvertDict(trainsAtCity),
            wagons = ConvertDict(storedWagonsAtCity),
            storageLevels = ConvertIntDict(cityStorageLevels)
        };

        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();

        Debug.Log("Station data saved");
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(SAVE_KEY, "");

        if (string.IsNullOrEmpty(json))
        {
            InitDefaultCities();
            return;
        }

        SaveWrapper wrapper = JsonUtility.FromJson<SaveWrapper>(json);

        unlockedCities = new HashSet<string>(wrapper.unlockedCities);
        trainsAtCity = ConvertBack(wrapper.trains);
        storedWagonsAtCity = ConvertBack(wrapper.wagons);
        cityStorageLevels = ConvertBackInt(wrapper.storageLevels);

        Debug.Log("Station data loaded");
    }

    private void InitDefaultCities()
    {
        foreach (var city in allCities)
        {
            if (city.unlocked)
                unlockedCities.Add(city.cityId);
        }
    }

    // ===== —≈–»¿À»«¿÷»ﬂ =====

    [System.Serializable]
    private class SaveWrapper
    {
        public List<string> unlockedCities;
        public List<DictList> trains;
        public List<DictList> wagons;
        public List<IntDict> storageLevels;
    }

    [System.Serializable]
    private class DictList
    {
        public string key;
        public List<string> values;
    }

    [System.Serializable]
    private class IntDict
    {
        public string key;
        public int value;
    }

    private List<DictList> ConvertDict(Dictionary<string, List<string>> dict)
    {
        List<DictList> list = new List<DictList>();

        foreach (var pair in dict)
        {
            list.Add(new DictList
            {
                key = pair.Key,
                values = pair.Value
            });
        }

        return list;
    }

    private Dictionary<string, List<string>> ConvertBack(List<DictList> list)
    {
        Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();

        if (list == null) return dict;

        foreach (var item in list)
        {
            dict[item.key] = item.values;
        }

        return dict;
    }

    private List<IntDict> ConvertIntDict(Dictionary<string, int> dict)
    {
        List<IntDict> list = new List<IntDict>();

        foreach (var pair in dict)
        {
            list.Add(new IntDict
            {
                key = pair.Key,
                value = pair.Value
            });
        }

        return list;
    }

    private Dictionary<string, int> ConvertBackInt(List<IntDict> list)
    {
        Dictionary<string, int> dict = new Dictionary<string, int>();

        if (list == null) return dict;

        foreach (var item in list)
        {
            dict[item.key] = item.value;
        }

        return dict;
    }
}