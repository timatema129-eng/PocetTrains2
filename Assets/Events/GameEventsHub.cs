using System;

public static class GameEventsHub
{
    public static Action<string> OnCityUnlocked;
    public static Action<string> OnTrainAssembled;
    public static Action<float> OnDistanceTraveled;
}