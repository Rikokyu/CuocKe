using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// One-line toast shown for a few seconds ("Bạn đã thua trận!...", "Đã nhận nhiệm vụ...").
/// This component must live on an object that stays active; only the child 'panel' is toggled.
/// A new message simply replaces the current one.
/// </summary>
public class MessageBannerUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text label;
    [SerializeField, Min(0.5f)] float defaultSeconds = 3.5f;

    Coroutine hideRoutine;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Shows a message. seconds &lt;= 0 uses the default duration.</summary>
    public void Show(string message, float seconds = 0f)
    {
        if (string.IsNullOrEmpty(message) || panel == null || label == null) return;

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        label.text = message;
        panel.SetActive(true);
        hideRoutine = StartCoroutine(HideAfter(seconds > 0f ? seconds : defaultSeconds));
    }

    IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        panel.SetActive(false);
        hideRoutine = null;
    }

    void OnDisable()
    {
        hideRoutine = null;
        if (panel != null) panel.SetActive(false);
    }
}
