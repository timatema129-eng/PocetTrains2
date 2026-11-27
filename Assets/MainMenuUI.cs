using UnityEngine;

/// <summary>
/// Управляет главным меню и переключением окон
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]

    public GameObject trainsPanel;
    public GameObject mapPanel;
    public GameObject boxesPanel;
    public GameObject shopPanel;
    public GameObject friendsPanel;

    // ===== INIT =====

    private void Start()
    {
        HideAllPanels();
    }

    // ===== ОСНОВНОЙ МЕТОД =====

    private void ShowPanel(GameObject panel)
    {
        HideAllPanels();

        if (panel != null)
            panel.SetActive(true);
    }

    private void HideAllPanels()
    {
        trainsPanel.SetActive(false);
        mapPanel.SetActive(false);
        boxesPanel.SetActive(false);
        shopPanel.SetActive(false);
        friendsPanel.SetActive(false);
    }

    // ===== КНОПКИ =====

    public void OnTrainsButton()
    {
        ShowPanel(trainsPanel);
        Debug.Log("Открыто окно поездов");
    }

    public void OnMapButton()
    {
        ShowPanel(mapPanel);
        Debug.Log("Открыта карта");
    }

    public void OnBoxesButton()
    {
        ShowPanel(boxesPanel);
        Debug.Log("Открыты боксы");
    }

    public void OnShopButton()
    {
        ShowPanel(shopPanel);
        Debug.Log("Открыт магазин");
    }

    public void OnFriendsButton()
    {
        ShowPanel(friendsPanel);
        Debug.Log("Открыты друзья");
    }

    // ===== ДОПОЛНИТЕЛЬНО =====

    public void CloseAll()
    {
        HideAllPanels();
    }
}