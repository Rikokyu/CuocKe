using System;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Plays a sound when a certain dialogue line appears (listens to DialogueUI.LineTextShown; the dialogue itself is not changed).
/// The sound can come from a normal AudioClip or from the SOUND TRACK OF A VIDEO: the video is played in API-Only mode, which means it is
/// decoded but never drawn anywhere, so the player hears it and sees nothing. Video players are prepared in advance, so the sound
/// starts the moment the line appears.
/// </summary>
public class DialogueLineSound : MonoBehaviour
{
    [Serializable]
    public class Rule
    {
        [Tooltip("Speaker name as passed to DialogueUI.Open (e.g. 'Bạn thân'). Empty = any speaker.")]
        public string speaker = "";
        [Tooltip("Plays when the line contains this text (ignores upper/lower case).")]
        public string lineContains = "";
        [Tooltip("Sound taken from this video (audio only, the picture is never shown). Used first when set.")]
        public VideoClip audioFromVideo;
        [Tooltip("Or a plain audio clip.")]
        public AudioClip audioClip;
        [Range(0f, 1f)] public float volume = 1f;
        [HideInInspector] public VideoPlayer player;
        [HideInInspector] public AudioSource source;
        [HideInInspector] public bool playWhenPrepared;
    }

    [SerializeField] DialogueUI dialogue;
    [SerializeField] Rule[] rules;

    void OnEnable()
    {
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueUI>(FindObjectsInactive.Include);
        if (dialogue != null) dialogue.LineTextShown += OnLine;
    }

    void OnDisable()
    {
        if (dialogue != null) dialogue.LineTextShown -= OnLine;
    }

    void Start()
    {
        if (rules == null) return;
        foreach (Rule r in rules)
        {
            if (r == null) continue;
            GameObject go = new GameObject("LineSound_" + (r.audioFromVideo != null ? r.audioFromVideo.name : (r.audioClip != null ? r.audioClip.name : "empty")));
            go.transform.SetParent(transform, false);
            r.source = go.AddComponent<AudioSource>();
            r.source.playOnAwake = false;
            r.source.spatialBlend = 0f;   // 2D: the friend's voice is heard at full volume
            r.source.volume = r.volume;
            if (r.audioFromVideo != null)
            {
                VideoPlayer vp = go.AddComponent<VideoPlayer>();
                vp.playOnAwake = false;
                vp.isLooping = false;
                vp.source = VideoSource.VideoClip;
                vp.clip = r.audioFromVideo;
                vp.renderMode = VideoRenderMode.APIOnly;           // nothing is drawn: no camera plane, no render texture, no UI
                vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
                vp.controlledAudioTrackCount = 1;
                vp.EnableAudioTrack(0, true);
                vp.SetTargetAudioSource(0, r.source);
                Rule rule = r;
                vp.prepareCompleted += p => { if (rule.playWhenPrepared) { rule.playWhenPrepared = false; p.time = 0; p.Play(); } };
                r.player = vp;
                vp.Prepare();
            }
        }
    }

    void OnLine(string speaker, string text)
    {
        if (rules == null || string.IsNullOrEmpty(text)) return;
        foreach (Rule r in rules)
        {
            if (r == null || string.IsNullOrEmpty(r.lineContains)) continue;
            if (!string.IsNullOrEmpty(r.speaker) && !string.Equals(r.speaker, speaker, StringComparison.OrdinalIgnoreCase)) continue;
            if (text.IndexOf(r.lineContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
            Play(r);
        }
    }

    void Play(Rule r)
    {
        if (r.player != null)
        {
            if (r.player.isPrepared) { r.player.time = 0; r.player.Play(); }
            else { r.playWhenPrepared = true; r.player.Prepare(); }
        }
        else if (r.source != null && r.audioClip != null)
        {
            r.source.Stop();
            r.source.clip = r.audioClip;
            r.source.Play();
        }
    }
}
