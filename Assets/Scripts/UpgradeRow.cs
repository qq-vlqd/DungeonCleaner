using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeRow : MonoBehaviour
{
    public string id;
    public Image levelImage;
    public Sprite[] levelSprites;      // 6 штук: level_bar_0 ... level_bar_5
    public TMP_Text costText;
    public Button buyButton;
    public Sprite buyDisabled, buyMax;
    public UpgradeMenu menu;

    public void Buy()
    {
        Upgrades.TryBuy(id);
        if (menu != null) menu.RefreshAll(); else Refresh();
    }

    public void Refresh()
    {
        int lvl = Upgrades.GetLevel(id);
        bool max = lvl >= Upgrades.MaxLevel;
        if (levelImage != null && levelSprites != null && lvl < levelSprites.Length)
            levelImage.sprite = levelSprites[lvl];
        costText.text = max ? "MAX" : Upgrades.Cost(id).ToString();
        buyButton.interactable = !max && Upgrades.Coins >= Upgrades.Cost(id);
        var ss = buyButton.spriteState;
        ss.disabledSprite = max ? buyMax : buyDisabled;
        buyButton.spriteState = ss;
    }
}