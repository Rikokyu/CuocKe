#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Put this file under Assets/Editor/. Select one or more AnimationClip assets in the
/// Project window (e.g. Run, ChickenIdle, and every Kick/HurricaneKick clip used by
/// Chicken.controller) and run the menu command below. It removes any Animation Event
/// whose Function field is blank - that's exactly what "AnimationEvent has no function
/// name specified!" means, and it can't be caught in C# because it fires from the
/// animation data itself, before any method is called.
/// </summary>
public static class AnimationEventCleaner
{
    [MenuItem("Tools/Cockfighting/Strip Empty Animation Events On Selected Clips")]
    static void StripEmptyEvents()
    {
        int cleaned = 0;
        int clipsTouched = 0;

        foreach (var obj in Selection.objects)
        {
            if (obj is AnimationClip clip)
            {
                var events = AnimationUtility.GetAnimationEvents(clip);
                var kept = System.Array.FindAll(events, e => !string.IsNullOrEmpty(e.functionName));

                if (kept.Length != events.Length)
                {
                    AnimationUtility.SetAnimationEvents(clip, kept);
                    EditorUtility.SetDirty(clip);
                    cleaned += events.Length - kept.Length;
                    clipsTouched++;
                    Debug.Log($"[AnimationEventCleaner] Removed {events.Length - kept.Length} empty event(s) from '{clip.name}'.");
                }
            }
        }

        if (clipsTouched > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationEventCleaner] Done - removed {cleaned} empty AnimationEvent(s) across {clipsTouched} clip(s).");
        }
        else
        {
            Debug.Log("[AnimationEventCleaner] No empty AnimationEvents found on the selected clips (or nothing valid was selected).");
        }
    }
}
#endif