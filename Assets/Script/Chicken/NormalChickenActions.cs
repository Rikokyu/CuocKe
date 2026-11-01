using System.Collections;
using UnityEngine;

/// <summary>
/// Small API for the extra animations of the "normal chicken" Animator (Normal_Chicken.controller):
/// Walk, Eat, Attack, Hit, Die. It only writes the Animator parameters "isWalking" (bool) and "action" (int):
///   0 = none, 2 = eat (loops until StopEat), 3 = attack, 4 = hit, 5 = die (stays down until Revive).
/// One-shot actions go back to 0 by themselves, and the controller then returns to idle.
///
/// Passive: nothing happens unless another script calls it, so adding it never changes existing gameplay.
/// </summary>
[DisallowMultipleComponent]
public class NormalChickenActions : MonoBehaviour
{
    public const int ActionNone = 0, ActionEat = 2, ActionAttack = 3, ActionHit = 4, ActionDie = 5;

    public Animator animator;
    public float attackSeconds = 0.75f;
    public float hitSeconds = 0.55f;

    Coroutine routine;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public bool IsDead { get; private set; }

    public void SetWalking(bool walking)
    {
        if (CanDrive()) animator.SetBool("isWalking", walking);
    }

    public void Attack() { PlayOneShot(ActionAttack, attackSeconds); }
    public void Hit() { PlayOneShot(ActionHit, hitSeconds); }

    public void Eat() { if (IsDead) return; StopRoutine(); Set(ActionEat); }
    public void StopEat() { if (IsDead) return; StopRoutine(); Set(ActionNone); }

    public void Die()
    {
        IsDead = true;
        StopRoutine();
        Set(ActionDie);
    }

    public void Revive()
    {
        IsDead = false;
        StopRoutine();
        Set(ActionNone);
    }

    void PlayOneShot(int action, float seconds)
    {
        if (IsDead || !CanDrive()) return;
        StopRoutine();
        Set(action);
        routine = StartCoroutine(ResetAfter(seconds));
    }

    IEnumerator ResetAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        routine = null;
        if (!IsDead) Set(ActionNone);
    }

    void StopRoutine()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
    }

    void Set(int action)
    {
        if (CanDrive()) animator.SetInteger("action", action);
    }

    bool CanDrive()
    {
        return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
    }
}
