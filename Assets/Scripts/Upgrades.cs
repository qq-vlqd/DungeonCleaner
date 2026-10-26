
using UnityEngine;

public static class Upgrades
{
    public const int MaxLevel = 5;

    public static int Coins
    {
        get => PlayerPrefs.GetInt("coins", 0);
        set
        {
            PlayerPrefs.SetInt("coins", Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    public static int GetLevel(string id)
    {
        return PlayerPrefs.GetInt("up_" + id, 0);
    }

    public static int Cost(string id)
    {
        return 10 * (GetLevel(id) + 1);
    }

    public static bool TryBuy(string id)
    {
        int level = GetLevel(id);
        int cost = Cost(id);

        if (level >= MaxLevel || Coins < cost)
            return false;

        Coins -= cost;
        PlayerPrefs.SetInt("up_" + id, level + 1);
        PlayerPrefs.Save();

        return true;
    }

    // Скорость: +15% за каждый уровень
    public static float SpeedMultiplier =>
        1f + 0.15f * GetLevel("speed");

    // Максимальное здоровье: +20 HP за каждый уровень
    public static int BonusHP =>
        20 * GetLevel("hp");

    // Радиус магнита
    public static float MagnetRadius =>
        0.5f + 0.4f * GetLevel("magnet");

    // Дополнительные монеты
    public static float CoinMultiplier =>
        1f + 0.25f * GetLevel("coins");

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey("coins");
        PlayerPrefs.DeleteKey("up_speed");
        PlayerPrefs.DeleteKey("up_hp");
        PlayerPrefs.DeleteKey("up_magnet");
        PlayerPrefs.DeleteKey("up_coins");

        PlayerPrefs.Save();
    }
}
