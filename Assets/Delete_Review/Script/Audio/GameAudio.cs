using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ONE audio manager of the game (there was no audio code before). It owns every AudioSource it needs
/// (2 music, 2 ambience, a small SFX pool), so other scripts never add AudioSources.
/// It does not edit gameplay scripts: it listens to the events that already exist (QuestManager, CurrencyWallet,
/// ChickenStats, InventorySystem, HealthSystem, BattleManager, DialogueUI.LineShown) and polls a few flags
/// (story state, battle state, player movement).
/// Anti-spam: every SFX group has a minimum interval, a clip is never repeated twice in a row, only a limited
/// number of voices play at once, and music/ambience change with a cross-fade at most every few seconds.
/// Only clips that exist in the project are assigned (see PROJECT_STATUS.md for the list of missing sounds).
/// </summary>
public class GameAudio : MonoBehaviour
{
    [System.Serializable]
    public class Bank
    {
        public string name;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("Minimum seconds between two plays of this group (anti-spam).")]
        [Min(0f)] public float minInterval = 0.15f;
        [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
        [Tooltip("0 = play the whole clip. Otherwise the clip is cut after this many seconds (for long clips used as a short cue).")]
        [Min(0f)] public float maxSeconds = 0f;
        [HideInInspector] public float lastTime = -99f;
        [HideInInspector] public int lastIndex = -1;
        [HideInInspector] public int playedCount;   // for tests / diagnostics
        [HideInInspector] public int blockedCount;  // plays refused by the interval

        public AudioClip Pick()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            int i;
            do { i = Random.Range(0, clips.Length); } while (i == lastIndex);
            lastIndex = i;
            return clips[i];
        }
    }

    [Header("Volumes")]
    [Range(0f, 1f)] public float master = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.30f;
    [Range(0f, 1f)] public float ambienceVolume = 0.45f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Music and ambience go down to this factor while a dialogue is open.")]
    [Range(0.1f, 1f)] public float dialogueDuck = 0.5f;
    [Min(0.1f)] public float fadeSeconds = 2.5f;

    [Header("Music (Assets/Sound)")]
    public AudioClip exploreMusic;   // normal exploring
    public AudioClip sadMusic;       // after the first lost fight, until the chicken transforms
    public AudioClip battleMusic;    // any fight
    public AudioClip endingMusic;    // ending / credits

    [Header("Ambience (Assets/Sound)")]
    public AudioClip dayAmbience;
    public AudioClip eveningAmbience; // after the first lost fight, until the chicken transforms
    public AudioClip nightAmbience;   // while sleeping in the bed

    [Header("SFX groups")]
    public Bank footstep = new Bank { name = "footstep", volume = 0.35f, minInterval = 0.25f, pitchJitter = 0.1f };
    public Bank dialogueVoice = new Bank { name = "dialogueVoice", volume = 0.5f, minInterval = 0.4f, pitchJitter = 0.1f };
    public Bank questAccepted = new Bank { name = "questAccepted", volume = 0.6f, minInterval = 0.5f };
    public Bank objectiveDone = new Bank { name = "objectiveDone", volume = 0.6f, minInterval = 0.5f };
    public Bank questDone = new Bank { name = "questDone", volume = 0.7f, minInterval = 1f };
    public Bank moneyGain = new Bank { name = "moneyGain", volume = 0.6f, minInterval = 0.2f };
    public Bank moneySpend = new Bank { name = "moneySpend", volume = 0.6f, minInterval = 0.2f };
    public Bank pickup = new Bank { name = "pickup", volume = 0.6f, minInterval = 0.2f };
    public Bank trainDone = new Bank { name = "trainDone", volume = 0.7f, minInterval = 0.5f };
    public Bank hitLight = new Bank { name = "hitLight", volume = 0.7f, minInterval = 0.12f, pitchJitter = 0.1f };
    public Bank hitHeavy = new Bank { name = "hitHeavy", volume = 0.85f, minInterval = 0.2f, pitchJitter = 0.08f };
    // Layers under the hit sound. They only play when the main hit sound just played (same gate, so the pair stays in sync and never doubles the density).
    public Bank hitSnap = new Bank { name = "hitSnap", volume = 0.45f, minInterval = 0.15f, pitchJitter = 0.1f };    // crisp crack under a light hit
    public Bank hitBurst = new Bank { name = "hitBurst", volume = 0.7f, minInterval = 0.35f, pitchJitter = 0.08f };  // low boom under a heavy hit (there is no real explosion clip in the project)
    public Bank deathKnock = new Bank { name = "deathKnock", volume = 0.8f, minInterval = 1f };
    public Bank battleStart = new Bank { name = "battleStart", volume = 0.7f, minInterval = 2f };
    public Bank battleWon = new Bank { name = "battleWon", volume = 0.8f, minInterval = 3f };
    public Bank battleLost = new Bank { name = "battleLost", volume = 0.7f, minInterval = 3f };
    public Bank transformation = new Bank { name = "transformation", volume = 0.9f, minInterval = 5f };
    public Bank rooster = new Bank { name = "rooster", volume = 0.7f, minInterval = 20f, pitchJitter = 0.08f };

    [Header("Limits")]
    [Range(2, 16)] public int sfxVoices = 8;
    [Tooltip("Random pause between two idle chicken calls (only while exploring).")]
    public Vector2 idleRoosterDelay = new Vector2(45f, 90f);
    public float footstepStride = 1.7f;
    [Tooltip("How 3D the fight hit sounds are (0 = flat, 1 = full distance falloff). Below 1 so a hit stays audible from the gameplay camera.")]
    [Range(0f, 1f)] public float hitSpatialBlend = 0.6f;
    [Tooltip("At most this many voices of the pool may be busy with fight-hit sounds at once, so a flurry of hits can never use up every voice. A hit that would exceed it is skipped.")]
    [Range(2, 8)] public int maxHitVoices = 4;
    /// <summary>Hits skipped because maxHitVoices was reached (diagnostics / tests).</summary>
    public int HitVoiceBlocked { get; private set; }
    readonly List<AudioSource> hitVoices = new List<AudioSource>();
    AudioSource lastVoice;
    /// <summary>Time.frameCount of the last fight-hit sound (compare with GameVfx.LastHitFrame).</summary>
    public int LastHitSfxFrame { get; private set; }

    public static GameAudio Instance { get; private set; }

    // ---- diagnostics (read by tests) ----
    public AudioClip CurrentMusic { get { return music.clip; } }
    public AudioClip CurrentAmbience { get { return ambience.clip; } }
    public int ActiveSfxVoices { get { int n = 0; foreach (AudioSource s in sfxPool) if (s.isPlaying) n++; return n; } }
    public IEnumerable<Bank> AllBanks()
    {
        yield return footstep; yield return dialogueVoice; yield return questAccepted; yield return objectiveDone; yield return questDone;
        yield return moneyGain; yield return moneySpend; yield return pickup; yield return trainDone; yield return hitLight;
        yield return hitHeavy; yield return hitSnap; yield return hitBurst; yield return deathKnock; yield return battleStart; yield return battleWon; yield return battleLost;
        yield return transformation; yield return rooster;
    }

    // ---- cross-faded looping channel (2 sources) ----
    class Channel
    {
        readonly AudioSource[] src = new AudioSource[2];
        readonly float[] vol = new float[2];
        int cur = -1;
        public AudioClip clip;

        public Channel(GameObject host, string label)
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject go = new GameObject(label + i);
                go.transform.SetParent(host.transform, false);
                src[i] = go.AddComponent<AudioSource>();
                src[i].playOnAwake = false; src[i].loop = true; src[i].spatialBlend = 0f; src[i].volume = 0f;
            }
        }

        public void Set(AudioClip c)
        {
            if (c == clip) return;
            clip = c;
            if (cur >= 0 && src[cur].clip == c) return;
            int next = cur < 0 ? 0 : 1 - cur;
            if (c != null) { src[next].clip = c; src[next].time = 0f; src[next].Play(); vol[next] = 0f; src[next].volume = 0f; }
            cur = next;
        }

        public void Tick(float dt, float fade, float target)
        {
            for (int i = 0; i < 2; i++)
            {
                float goal = (i == cur && clip != null) ? target : 0f;
                vol[i] = Mathf.MoveTowards(vol[i], goal, dt / fade * Mathf.Max(target, 0.05f));
                src[i].volume = vol[i];
                if (i != cur && vol[i] <= 0f && src[i].isPlaying) src[i].Stop();
            }
        }
    }

    Channel music, ambience;
    AudioSource[] sfxPool;

    QuestManager questManager;
    CurrencyWallet wallet;
    ChickenStats chickenStats;
    InventorySystem inventory;
    DialogueUI dialogue;
    BattleManager battle;
    StoryBed bed;
    Transform player;

    readonly Dictionary<HealthSystem, float> health = new Dictionary<HealthSystem, float>();
    int lastBalance = int.MinValue;
    float duck = 1f;
    float nextPolicyCheck;
    float nextRooster;
    bool wasBattle, wasTransformed;
    Vector3 lastPlayerPos;
    float stepAccum, lastMoveTime;
    bool playerPosValid;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; } // never a second manager
        Instance = this;

        music = new Channel(gameObject, "Music");
        ambience = new Channel(gameObject, "Ambience");
        sfxPool = new AudioSource[Mathf.Max(2, sfxVoices)];
        for (int i = 0; i < sfxPool.Length; i++)
        {
            GameObject go = new GameObject("Sfx" + i);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = false;
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 3f; s.maxDistance = 40f;
            sfxPool[i] = s;
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        questManager = QuestManager.Instance;
        if (questManager != null)
        {
            questManager.QuestAccepted += s => Play(questAccepted);
            questManager.ObjectiveCompleted += s => Play(objectiveDone);
            questManager.QuestCompleted += s => Play(questDone);
        }

        wallet = FindFirstObjectByType<CurrencyWallet>();
        if (wallet != null) { lastBalance = wallet.Balance; wallet.BalanceChanged += OnBalanceChanged; }

        chickenStats = FindFirstObjectByType<ChickenStats>();
        if (chickenStats != null) chickenStats.Changed += () => Play(trainDone);

        inventory = InventorySystem.Instance;
        if (inventory != null) inventory.Changed += () => Play(pickup);

        dialogue = FindFirstObjectByType<DialogueUI>(FindObjectsInactive.Include);
        if (dialogue != null)
        {
            dialogue.LineShown += OnDialogueLine;
            dialogue.TypingStarted += StartDialogueVoice;
            dialogue.TypingStopped += StopDialogueVoice;
        }

        battle = BattleManager.Instance;
        if (battle != null) battle.PlayerLostBattle += () => Play(battleLost);

        bed = FindFirstObjectByType<StoryBed>(FindObjectsInactive.Include);
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        HookAllHealth();
        nextRooster = Time.time + Random.Range(idleRoosterDelay.x, idleRoosterDelay.y);
        UpdatePolicy(true);
    }

    // ------------------------------------------------------------ playing
    /// <summary>Plays a clip of the group (2D, or 3D at a position). Returns false when it was skipped.</summary>
    public bool Play(Bank bank, Vector3? position = null, float volumeScale = 1f, float pitch = 1f, float spatialBlend = 1f)
    {
        if (bank == null || bank.clips == null || bank.clips.Length == 0) return false;
        if (Time.unscaledTime - bank.lastTime < bank.minInterval) { bank.blockedCount++; return false; }

        AudioClip clip = bank.Pick();
        if (clip == null) return false;
        AudioSource s = FreeVoice();
        if (s == null) { bank.blockedCount++; return false; }   // every voice busy: skip instead of stacking

        bank.lastTime = Time.unscaledTime;
        bank.playedCount++;
        if (position.HasValue) { s.transform.position = position.Value; s.spatialBlend = spatialBlend; }
        else s.spatialBlend = 0f;
        s.clip = clip;
        s.volume = Mathf.Clamp01(bank.volume * sfxVolume * master * volumeScale);
        s.pitch = pitch * (1f + Random.Range(-bank.pitchJitter, bank.pitchJitter));
        s.Play();
        lastVoice = s;
        if (bank.maxSeconds > 0f && bank.maxSeconds < clip.length) s.SetScheduledEndTime(AudioSettings.dspTime + bank.maxSeconds / Mathf.Max(0.1f, s.pitch));
        return true;
    }

    AudioSource FreeVoice()
    {
        foreach (AudioSource s in sfxPool) if (!s.isPlaying && s != dialogueVoiceSrc) return s; // the source that holds the talking voice is never lent out
        return null;
    }

    // ------------------------------------------------------------ event handlers
    void OnBalanceChanged(int balance)
    {
        if (lastBalance != int.MinValue)
        {
            if (balance > lastBalance) Play(moneyGain);
            else if (balance < lastBalance) Play(moneySpend);
        }
        lastBalance = balance;
    }

    void OnDialogueLine(bool byPlayer)
    {
        // With the typewriter the voice loops while the letters appear (StartDialogueVoice / StopDialogueVoice);
        // without it a line has no typing time, so it keeps the old single blip.
        if (dialogue != null && dialogue.TypewriterEnabled) return;
        Play(dialogueVoice, null, 1f, byPlayer ? 1.3f : 1f);   // the player's lines use the same voice, higher
    }

    // ---- talking voice: repeats WHILE the letters are typed, stops at once when the line is finished / skipped / the dialogue closes.
    // One held source from the existing pool and the existing clip; no source or clip is created per letter or per line.
    AudioSource dialogueVoiceSrc;
    bool voiceLooping;
    bool voiceByPlayer;
    float voicePhraseEndsAt;   // repeat is time based (does not depend on isPlaying, which is false without an audio device or while the editor is paused)

    /// <summary>True while the dialogue voice is on (diagnostics / tests).</summary>
    public bool DialogueVoiceActive { get { return voiceLooping && dialogueVoiceSrc != null; } }
    /// <summary>How many times a voice phrase was started (diagnostics / tests).</summary>
    public int DialogueVoicePhrases { get; private set; }
    /// <summary>The volume the dialogue voice is played at (bank volume x sfx x master). Only this group is affected.</summary>
    public float DialogueVoiceVolume { get { return Mathf.Clamp01(dialogueVoice.volume * sfxVolume * master); } }

    void StartDialogueVoice(bool byPlayer)
    {
        StopDialogueVoice(); // a new line (maybe another NPC) never overlaps the previous voice
        voiceByPlayer = byPlayer;
        voiceLooping = true;
        PlayVoicePhrase();
    }

    void StopDialogueVoice()
    {
        voiceLooping = false;
        if (dialogueVoiceSrc != null)
        {
            dialogueVoiceSrc.Stop();
            dialogueVoiceSrc = null;
        }
    }

    void PlayVoicePhrase()
    {
        AudioClip clip = dialogueVoice.Pick();
        if (clip == null) { voiceLooping = false; return; }

        AudioSource s = dialogueVoiceSrc != null ? dialogueVoiceSrc : FreeVoice();
        if (s == null) return; // all voices busy: try again next frame, never stack

        dialogueVoiceSrc = s;
        dialogueVoice.playedCount++;
        DialogueVoicePhrases++;
        s.spatialBlend = 0f;
        s.clip = clip;
        s.volume = DialogueVoiceVolume;
        s.pitch = (voiceByPlayer ? 1.3f : 1f) * (1f + Random.Range(-dialogueVoice.pitchJitter, dialogueVoice.pitchJitter));
        s.Play();
        float phrase = dialogueVoice.maxSeconds > 0f && dialogueVoice.maxSeconds < clip.length ? dialogueVoice.maxSeconds : clip.length;
        if (phrase < clip.length) s.SetScheduledEndTime(AudioSettings.dspTime + phrase / Mathf.Max(0.1f, s.pitch));
        voicePhraseEndsAt = Time.unscaledTime + phrase / Mathf.Max(0.1f, s.pitch) + 0.05f;
    }

    void TickDialogueVoice()
    {
        if (!voiceLooping) return;
        // safety net: no voice without an open, still typing dialogue
        if (dialogue == null || !dialogue.IsOpen || !dialogue.IsTyping) { StopDialogueVoice(); return; }
        if (dialogueVoiceSrc == null || Time.unscaledTime >= voicePhraseEndsAt) PlayVoicePhrase(); // phrase ended: repeat
    }

    void HookAllHealth()
    {
        foreach (HealthSystem h in FindObjectsByType<HealthSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (health.ContainsKey(h)) continue;
            health[h] = h.currentHealth;
            HealthSystem captured = h;
            h.OnHealthChanged.AddListener((cur, max) => OnHealthChanged(captured, cur, max));
            h.OnDeath.AddListener(() => Play(deathKnock, captured.transform.position));
        }
    }

    void OnHealthChanged(HealthSystem h, float current, float max)
    {
        float before;
        if (!health.TryGetValue(h, out before)) before = current;
        health[h] = current;
        float lost = before - current;
        if (lost <= 0.01f) return;
        bool heavy = lost >= 0.15f * max || lost >= 25f;
        // same point as the particle burst (the victim's collider centre)
        Vector3 at = h.transform.position;
        Collider col = h.GetComponentInChildren<Collider>();
        if (col != null) at = col.bounds.center;
        for (int i = hitVoices.Count - 1; i >= 0; i--) if (hitVoices[i] == null || !hitVoices[i].isPlaying) hitVoices.RemoveAt(i);
        if (hitVoices.Count + 2 > maxHitVoices) { HitVoiceBlocked++; return; }   // never let hit sounds take over the whole pool
        if (Play(heavy ? hitHeavy : hitLight, at, 1f, 1f, hitSpatialBlend))
        {
            LastHitSfxFrame = Time.frameCount;
            hitVoices.Add(lastVoice);
            if (Play(heavy ? hitBurst : hitSnap, at, 1f, 1f, hitSpatialBlend)) hitVoices.Add(lastVoice);   // layer, only together with the main hit sound
        }
    }

    // ------------------------------------------------------------ per frame
    void Update()
    {
        TickDialogueVoice();
        float dt = Time.unscaledDeltaTime;

        float duckGoal = (dialogue != null && dialogue.IsOpen) ? dialogueDuck : 1f;
        duck = Mathf.MoveTowards(duck, duckGoal, dt * 2f);
        music.Tick(dt, fadeSeconds, musicVolume * master * duck);
        ambience.Tick(dt, fadeSeconds, ambienceVolume * master * duck);

        if (Time.unscaledTime >= nextPolicyCheck) UpdatePolicy(false);
        TickFootsteps();
    }

    bool InFight()
    {
        if (battle != null && battle.IsBattleActive) return true;
        TournamentManager t = TournamentManager.Instance;
        return t != null && t.IsBusy;
    }

    /// <summary>Every 0.3 s: choose music / ambience from the story state and react to story / battle transitions.</summary>
    void UpdatePolicy(bool first)
    {
        nextPolicyCheck = Time.unscaledTime + 0.3f;

        bool fight = InFight();
        if (fight && !wasBattle) OnFightStarted();
        if (!fight && wasBattle) OnFightEnded();
        wasBattle = fight;

        bool transformed = ChickenTransformationTrigger.Transformed;
        if (transformed && !wasTransformed && !first) Play(transformation);
        wasTransformed = transformed;

        bool afterLoss = BattleManager.firstBattleCompleted && !transformed;
        bool sleeping = bed != null && bed.IsSleeping;

        AudioClip m = exploreMusic;
        if (EndingSequence.Started) m = endingMusic != null ? endingMusic : exploreMusic;
        else if (fight) m = battleMusic != null ? battleMusic : exploreMusic;
        else if (afterLoss && sadMusic != null) m = sadMusic;
        music.Set(m);

        AudioClip a = dayAmbience;
        if (sleeping && nightAmbience != null) a = nightAmbience;
        else if (afterLoss && eveningAmbience != null) a = eveningAmbience;
        ambience.Set(a);

        if (!fight && Time.time >= nextRooster)
        {
            nextRooster = Time.time + Random.Range(idleRoosterDelay.x, idleRoosterDelay.y);
            MainChickenAcquire mc = MainChickenAcquire.Instance;
            bool dialogueOpen = dialogue != null && dialogue.IsOpen;
            if (mc != null && mc.IsAcquired && battle != null && battle.mainChickenHealth != null && !dialogueOpen)
                Play(rooster, battle.mainChickenHealth.transform.position);
        }
    }

    void OnFightStarted()
    {
        HookAllHealth(); // tournament opponents may have been enabled since Start
        Play(battleStart);
        HealthSystem foe = battle != null ? battle.enemyHealth : null;
        if (foe != null) Play(rooster, foe.transform.position);
    }

    void OnFightEnded()
    {
        HealthSystem foe = battle != null ? battle.enemyHealth : null;
        HealthSystem pet = battle != null ? battle.mainChickenHealth : null;
        if (pet != null && pet.IsDead) return; // the loss cue is played by PlayerLostBattle
        if (foe != null && foe.IsDead) Play(battleWon);
    }

    void TickFootsteps()
    {
        if (player == null) return;
        Vector3 pos = player.position;
        if (!playerPosValid) { lastPlayerPos = pos; playerPosValid = true; return; }
        Vector3 delta = pos - lastPlayerPos; delta.y = 0f;
        lastPlayerPos = pos;
        float d = delta.magnitude;
        if (d <= 0.0005f) { if (Time.unscaledTime - lastMoveTime > 0.4f) stepAccum = footstepStride * 0.6f; return; }
        if (d > 2f) return; // teleport, not walking
        lastMoveTime = Time.unscaledTime;
        stepAccum += d;
        if (stepAccum >= footstepStride) { stepAccum = 0f; Play(footstep); }
    }
}
