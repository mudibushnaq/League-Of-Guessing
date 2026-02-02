using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple loading UI shown during per-level downloads/loads.
/// Can be shown/hidden and displays progress.
/// Optional - game will work without it, just won't show loading feedback.
/// </summary>
public class LevelLoadingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI statusText;

    private bool _isVisible;

    public void Show(string message = "Loading level...")
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);
        
        if (statusText != null)
            statusText.text = message;
        
        if (progressBar != null)
            progressBar.fillAmount = 0f;
        
        _isVisible = true;
    }

    public void UpdateProgress(float progress, string message = null)
    {
        if (progressBar != null)
            progressBar.fillAmount = Mathf.Clamp01(progress);
        
        if (!string.IsNullOrEmpty(message) && statusText != null)
            statusText.text = message;
    }

    public void Hide()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        
        _isVisible = false;
    }

    public bool IsVisible => _isVisible;
}

