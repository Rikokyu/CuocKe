using UnityEngine;

/// <summary>
/// One collectible scrap item placed by hand in the scene. Deliberately dumb: it knows its type and
/// whether it was already collected. ScrapCollector finds the nearest pickup, handles E, money and quest.
/// MarkCollected() is the single gate that guarantees an item pays and counts only once.
/// No collider: nothing can block or trap the player, and proximity is checked by distance.
/// </summary>
public class ScrapPickup : MonoBehaviour
{
    [SerializeField] ScrapTypeDefinition type;

    public ScrapTypeDefinition Type => type;
    public bool IsCollected { get; private set; }

    /// <summary>Flags the item as collected and hides it. Returns false if it was already collected.</summary>
    public bool MarkCollected()
    {
        if (IsCollected) return false;

        IsCollected = true;
        gameObject.SetActive(false);
        return true;
    }
}
