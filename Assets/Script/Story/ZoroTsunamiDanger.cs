using UnityEngine;

/// <summary>
/// Zoro Tsunami is dangerous to stand next to. Put this on the Zoro Tsunami NPC (NPC_BacZo/Zoro).
///   - A quiet 3D loop (zoro_tsunami) plays while the player is within hearing range and gets louder when closer.
///   - Close enough: Zoro warns the player once (dialogue) not to come any nearer; a last short banner follows if the player keeps coming.
///   - Closer still: the player dies - black screen, the ending card and the credits (EndingSequence.BeginDeathEnding).
/// Nothing here runs while a dialogue, the bed, a fight, the inventory or the ending is active, so it can never kill the player
/// in the middle of a cutscene. The loop is owned by this component (one AudioSource on Zoro); GameAudio is not touched.
/// </summary>
[DisallowMultipleComponent]
public class ZoroTsunamiDanger : MonoBehaviour
{
    [Header("Loop sound (3D, only while the player is near)")]
    [SerializeField] AudioClip loopClip;                       // Assets/Audio/SFX/zoro_tsunami.mp3
    [Tooltip("The clip is loud, so the volume is kept low on purpose.")]
    [SerializeField, Range(0f, 1f)] float loopVolume = 0.12f;
    [Tooltip("The sound starts at this distance (and fades out to nothing there).")]
    [SerializeField, Min(2f)] float hearDistance = 40f;
    [Tooltip("Inside this distance the loop is at full (low) volume.")]
    [SerializeField, Min(0.5f)] float fullVolumeDistance = 8f;
    [SerializeField, Min(0.1f)] float fadeSeconds = 1.2f;

    [Header("Danger zones (flat distance to Zoro)")]
    [Tooltip("Zoro warns the player once when they come this close.")]
    [SerializeField, Min(2f)] float warnDistance = 14f;
    [Tooltip("A last short banner when the player keeps coming.")]
    [SerializeField, Min(2f)] float finalWarnDistance = 10f;
    [Tooltip("Closer than this the player dies.")]
    [SerializeField, Min(0.5f)] float deathDistance = 6f;
    [Tooltip("The warnings are armed again once the player is this much further than the warn distance.")]
    [SerializeField, Min(0f)] float rearmExtra = 4f;

    [Header("Texts")]
    [SerializeField] string speaker = "Zoro";
    [SerializeField, TextArea(2, 4)] string[] warningLines =
    {
        "Này! Đứng lại đó! Đừng có lại gần tôi!",
        "Cơn lốc Tsunami của tôi không phân biệt bạn bè đâu. Bước thêm nữa là toi mạng đấy!"
    };
    [SerializeField, TextArea(1, 2)] string finalWarning = "Zoro: Tôi cảnh cáo rồi đấy! Lùi lại ngay!";
    [SerializeField] string deathTitle = "Bạn đã ngủm vì cơn lốc Zoro Go";

    Transform player;
    PlayerMovement playerMovement;
    DialogueUI dialogue;
    MessageBannerUI banner;
    StoryBed bed;
    AudioSource loop;
    bool warned, finalWarned, dying;
    float volumeNow;

    void Awake()
    {
        loop = gameObject.AddComponent<AudioSource>();
        loop.playOnAwake = false;
        loop.loop = true;
        loop.clip = loopClip;
        loop.volume = 0f;
        loop.spatialBlend = 1f;
        loop.rolloffMode = AudioRolloffMode.Linear;
        loop.minDistance = fullVolumeDistance;
        loop.maxDistance = hearDistance;
        loop.dopplerLevel = 0f;
    }

    void Start()
    {
        FindRefs();
        if (loopClip == null) Debug.LogWarning("[ZoroTsunamiDanger] No loop clip assigned - the danger zone works, but silently.", this);
    }

    void FindRefs()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { player = p.transform; playerMovement = p.GetComponent<PlayerMovement>(); }
        }
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueUI>(FindObjectsInactive.Include);
        if (banner == null) banner = FindFirstObjectByType<MessageBannerUI>(FindObjectsInactive.Include);
        if (bed == null) bed = FindFirstObjectByType<StoryBed>(FindObjectsInactive.Include);
    }

    void Update()
    {
        FindRefs();
        if (player == null) return;

        float d = FlatDistance(player.position, transform.position);
        TickLoop(d);

        if (dying || EndingSequence.Started) return;

        if (d > warnDistance + rearmExtra) { warned = false; finalWarned = false; }
        if (d > finalWarnDistance + rearmExtra) finalWarned = false;
        if (Blocked()) return;

        if (d <= deathDistance) { Kill(); return; }

        if (d <= warnDistance && !warned)
        {
            warned = true;
            if (dialogue != null && !dialogue.IsOpen) dialogue.Open(speaker, warningLines, null, null);
            return;
        }

        if (d <= finalWarnDistance && warned && !finalWarned)
        {
            finalWarned = true;
            if (banner != null && !string.IsNullOrEmpty(finalWarning)) banner.Show(finalWarning, 3f);
        }
    }

    /// <summary>True while the player is not in free control (dialogue, bed, fight, inventory, a locked player).</summary>
    bool Blocked()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        if (bed != null && (bed.IsBusy || bed.IsSleeping)) return true;
        BattleManager bm = BattleManager.Instance;
        if (bm != null && bm.IsBattleActive) return true;
        if (InventorySystem.Instance != null && InventorySystem.Instance.isOpen) return true;
        if (playerMovement != null && !playerMovement.enabled) return true;
        return false;
    }

    void Kill()
    {
        EndingSequence ending = EndingSequence.Instance;
        if (ending == null) { Debug.LogWarning("[ZoroTsunamiDanger] No EndingSequence in the scene - the player cannot be killed.", this); return; }
        if (!ending.BeginDeathEnding(deathTitle)) return;
        dying = true; // the loop fades out with the black screen
    }

    void TickLoop(float distance)
    {
        if (loop == null || loop.clip == null) return;

        bool wanted = !dying && !EndingSequence.Started && distance <= hearDistance;
        float target = wanted ? loopVolume : 0f;
        volumeNow = Mathf.MoveTowards(volumeNow, target, Time.unscaledDeltaTime / fadeSeconds * Mathf.Max(loopVolume, 0.05f));
        loop.volume = volumeNow;

        if (wanted && !loop.isPlaying) loop.Play();
        else if (!wanted && volumeNow <= 0f && loop.isPlaying) loop.Stop();
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position, hearDistance);
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, warnDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(transform.position, finalWarnDistance);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, deathDistance);
    }
}
