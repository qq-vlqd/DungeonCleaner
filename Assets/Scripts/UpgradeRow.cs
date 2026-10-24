using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeRow : MonoBehaviour
{
    public string id;               // speed, magnet или coins
    public TMP_Text levelText;
    public TMP_Text costText;
    public Button buyButton;
    public UpgradeMenu menu;

    public void Buy()
    {
        if (Upgrades.TryBuy(id)) menu.RefreshAll();
    }

    public void Refresh()
    {
        int lvl = Upgrades.GetLevel(id);
        bool max = lvl >= Upgrades.MaxLevel;
        levelText.text = "Lv " + lvl + "/" + Upgrades.MaxLevel;
        costText.text = max ? "MAX" : Upgrades.Cost(id).ToString();
        buyButton.interactable = !max && Upgrades.Coins >= Upgrades.Cost(id);
    }
}