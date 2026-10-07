using UnityEngine;

/// <summary>
/// Identifies and gates the player's Main Chicken, including the initial "lying on the
/// ground" discovery beat. ChienKe_Pet starts Down (playing the existing Dead animation,
/// PetCombatAI disabled - no movement, no follow, no combat). Pressing E while in range
/// plays a WakeUp animation (the existing Dead clip reused in reverse - see Chicken.controller),
/// and only once that finishes does the chicken become the MainChicken and start following.
/// </summary>
[RequireComponent(typeof(PetCombatAI))]
[RequireComponent(typeof(Animator))]
public class MainChickenAcquire : MonoBehaviour
{
    public static MainChickenAcquire Instance { get; private set; }

    public enum ChickenState { Down, WakingUp, MainChicken }

    public ChickenState currentState = ChickenState.Down;
    public bool IsAcquired => currentState == ChickenState.MainChicken;
    public KeyCode interactKey = KeyCode.E;

    bool playerInRange;
    PetCombatAI combatAI;
    Animator anim;

    void Awake()
    {
        Instance = this;
        combatAI = GetComponent<PetCombatAI>();
        anim = GetComponent<Animator>();

        combatAI.enabled = false; // no Follow/Combat until acquired
        anim.SetBool("isDead", true); // lie down on the existing Dead animation at scene start
    }

    void Update()
    {
        // E only does anything while the chicken is still down - can't repeat/stack the sequence.
        if (currentState == ChickenState.Down && playerInRange && Input.GetKeyDown(interactKey))
            StartWakeUp();

        // Poll for the Animator finishing its own WakeUp->ChickenIdle transition (driven by
        // exit time in the controller) rather than hardcoding a duration here, so the script
        // always matches however long the animation actually takes.
        if (currentState == ChickenState.WakingUp && anim.GetCurrentAnimatorStateInfo(0).IsName("ChickenIdle"))
            FinishWakeUp();
    }

    void StartWakeUp()
    {
        currentState = ChickenState.WakingUp;
        anim.SetBool("isDead", false);
        anim.SetTrigger("WakeUp");
        Debug.Log("[MainChicken] Waking up...");
    }

    void FinishWakeUp()
    {
        currentState = ChickenState.MainChicken;
        combatAI.enabled = true;
        Debug.Log("[MainChicken] ChienKe_Pet is now the Main Chicken - following the Player.");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }
}
