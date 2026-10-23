using TMPro;
using UnityEngine;

/// <summary>HUD line under the money: the total training hours bought from the trainer NPC ("Giờ tập: 3").</summary>
public class TrainingHoursHUD : MonoBehaviour
{
    [SerializeField] ChickenStats stats;
    [SerializeField] TMP_Text label;
    [SerializeField] string format = "Giờ tập: {0}";

    void OnEnable()
    {
        if (stats != null) stats.Changed += Refresh;
        Refresh();
    }

    void Start() { Refresh(); }

    void OnDisable()
    {
        if (stats != null) stats.Changed -= Refresh;
    }

    void Refresh()
    {
        if (label != null && stats != null) label.text = string.Format(format, stats.TrainingHours);
    }
}
