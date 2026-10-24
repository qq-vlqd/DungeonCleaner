using UnityEngine;

public static class Upgrades
{
    public const int MaxLevel = 5;

    public static int Coins
    {
        get => PlayerPrefs.GetInt("coins", 0);
        set { PlayerPrefs.SetInt("coins", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static int GetLevel(string id) => PlayerPrefs.GetInt("up_" + id, 0);
    public static int Cost(string id) => 10 * (GetLevel(id) + 1);

    public static bool TryBuy(string id)
    {
        int lvl = GetLevel(id);
        if (lvl >= MaxLevel || Coins < Cost(id)) return false;
        Coins -= Cost(id);
        PlayerPrefs.SetInt("up_" + id, lvl + 1);
        PlayerPrefs.Save();
        return true;
    }

    public static float SpeedMultiplier => 1f + 0.15f * GetLevel("speed");
    public static float MagnetRadius    => 0.5f + 0.4f * GetLevel("magnet");
    public static float CoinMultiplier  => 1f + 0.25f * GetLevel("coins");
}