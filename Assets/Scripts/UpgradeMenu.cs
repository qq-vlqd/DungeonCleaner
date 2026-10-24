using UnityEngine;
using TMPro;

public class UpgradeMenu : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text coinsText;
    public UpgradeRow[] rows;

    void Start() { panel.SetActive(false); }

    public void Open()  { panel.SetActive(true); RefreshAll(); }
    public void Close() { panel.SetActive(false); }

    public void RefreshAll()
    {
        coinsText.text = "Coins: " + Upgrades.Coins;
        foreach (var r in rows) r.Refresh();
    }

    void Update()
    {
        // ТОЛЬКО ДЛЯ ТЕСТА, удалить перед сдачей: клавиша C даёт 50 монет
        if (Input.GetKeyDown(KeyCode.C)) { Upgrades.Coins += 50; RefreshAll(); }
    }
}