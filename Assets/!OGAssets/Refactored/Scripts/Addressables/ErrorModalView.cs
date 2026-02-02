// ErrorModalView.cs
using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ErrorModalView : MonoBehaviour
{
    [Header("Root (enable/disable to show/hide)")]
    [SerializeField] private GameObject root;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text detailsText;

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button toggleDetailsButton;

    void Awake()
    {
        if (root != null) root.SetActive(false);
        if (toggleDetailsButton != null)
            toggleDetailsButton.onClick.AddListener(() =>
            {
                if (detailsText == null) return;
                var show = !detailsText.gameObject.activeSelf;
                detailsText.gameObject.SetActive(show);
                var lbl = toggleDetailsButton.GetComponentInChildren<TMP_Text>();
                if (lbl) lbl.text = show ? "Hide Details" : "Show Details";
            });
    }

    /// Shows the modal on this Canvas and returns true if user pressed Retry.
    public async UniTask<bool> ShowAsync(string title, string message, string details, 
                                         string retryText = "Retry", string cancelText = "Cancel")
    {
        if (root == null) root = gameObject; // fallback

        titleText.text   = string.IsNullOrEmpty(title) ? "Error" : title;
        messageText.text = string.IsNullOrEmpty(message) ? "An error occurred." : message;

        var hasDetails = !string.IsNullOrEmpty(details);
        if (detailsText != null) { detailsText.text = details ?? ""; detailsText.gameObject.SetActive(false); }
        if (toggleDetailsButton != null)
        {
            toggleDetailsButton.gameObject.SetActive(hasDetails);
            var lbl = toggleDetailsButton.GetComponentInChildren<TMP_Text>();
            if (lbl) lbl.text = "Show Details";
        }

        root.SetActive(true);

        var tcs = new UniTaskCompletionSource<bool>();
        void OnRetry() { Cleanup(); tcs.TrySetResult(true);  }
        void OnCancel(){ Cleanup(); tcs.TrySetResult(false); }

        if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);

        void Cleanup()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(OnRetry);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancel);
            root.SetActive(false);
        }

        return await tcs.Task;
    }
}
