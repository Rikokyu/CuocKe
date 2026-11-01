using TMPro;
using UnityEngine;

/// <summary>
/// HUD line under the card count: how many medals the player has ("Huy chương: 2"). Display only.
/// Reads TournamentManager and refreshes whenever it changes (a tournament win pays medals, the Reward NPC spends them).
/// </summary>
public class MedalHUD : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] string format = "Huy chương: {0}";
    [Tooltip("Off (default): medals still available to exchange at the Reward NPC - same number the exchange window shows. On: every medal ever earned.")]
    [SerializeField] bool showTotalEarned = false;

    TournamentManager tournament;

    void OnEnable()
    {
        Bind();
        Refresh();
    }

    void Start()
    {
        Bind(); // TournamentManager.Instance is set in its Awake, which may run after our OnEnable
        Refresh();
    }

    void OnDisable()
    {
        if (tournament != null) tournament.Changed -= Refresh;
        tournament = null;
    }

    void Bind()
    {
        TournamentManager current = TournamentManager.Instance;
        if (current == tournament || current == null) return;
        if (tournament != null) tournament.Changed -= Refresh;
        tournament = current;
        tournament.Changed += Refresh;
    }

    void Refresh()
    {
        if (label == null) return;
        int count = tournament == null ? 0 : (showTotalEarned ? tournament.Medals : tournament.AvailableMedals);
        label.text = string.Format(format, count);
    }
}
