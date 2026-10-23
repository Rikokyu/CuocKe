using TMPro;
using UnityEngine;

/// <summary>
/// On-screen "[E] ..." hint shared by several interactors (NPC, scrap collector...).
/// It is owner-aware: Show() makes the caller the owner, and Hide() only works for the current owner,
/// so a component with a stale 'I am showing it' flag can never hide someone else's prompt.
/// This component must live on an object that stays active; only the child 'panel' is toggled.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text label;

    object owner;

    public bool IsVisible => panel != null && panel.activeSelf;

    void Awake()
    {
        owner = null;
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Shows the prompt for 'requester' (it becomes the owner, replacing any previous owner).</summary>
    public void Show(object requester, string text)
    {
        owner = requester;
        if (label != null) label.text = text;
        if (panel != null && !panel.activeSelf) panel.SetActive(true);
    }

    /// <summary>Hides the prompt only if 'requester' is the one that currently owns it.</summary>
    public void Hide(object requester)
    {
        if (owner != requester) return;
        owner = null;
        if (panel != null && panel.activeSelf) panel.SetActive(false);
    }
}
