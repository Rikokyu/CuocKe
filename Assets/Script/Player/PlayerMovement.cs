using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;

    public float speed = 12f;
    public float gravity = -9.81f;
    public float jumpHeight = 3f;

    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Respawn (stuck / fell through the ground)")]
    [Tooltip("Press this key to go back to where the player started in the scene.")]
    [SerializeField] KeyCode respawnKey = KeyCode.M;
    [SerializeField] string respawnMessage = "Đã quay về vị trí ban đầu";
    [Tooltip("World Y the player appears at after pressing the key (same X/Z as the start). Gravity then lets him settle on the ground.")]
    [SerializeField] float respawnHeight = 2f;

    Vector3 startPosition;
    Quaternion startRotation;
    MessageBannerUI banner;

    Vector3 velocity;
    bool isGrounded;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(respawnKey) && !RespawnBlocked())
        {
            RespawnAtStart();
            return; // start moving again from the next frame
        }

        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if(isGrounded && velocity.y < 0)
        {
            velocity.y =- 2f;
        }
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * speed * Time.deltaTime);

        if(Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>Puts the player back where he started (position + facing) and stops any fall. Public so other scripts / tests can call it.</summary>
    public void RespawnAtStart()
    {
        // an enabled CharacterController overwrites transform.position, so it is switched off for the teleport
        bool controllerWasOn = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(new Vector3(startPosition.x, respawnHeight, startPosition.z), startRotation);
        velocity = Vector3.zero;
        Physics.SyncTransforms();
        if (controller != null) controller.enabled = controllerWasOn;

        if (banner == null) banner = FindAnyObjectByType<MessageBannerUI>();
        if (banner != null && !string.IsNullOrEmpty(respawnMessage)) banner.Show(respawnMessage, 2.5f);
    }

    /// <summary>No teleport while a story scene or a UI owns the player (dialogue, inventory, shop, reward, trainer, fight, sleep, camera shot).</summary>
    bool RespawnBlocked()
    {
        DialogueUI dialogue = FindAnyObjectByType<DialogueUI>();
        if (dialogue != null && dialogue.IsOpen) return true;
        if (InventorySystem.Instance != null && InventorySystem.Instance.isOpen) return true;
        ShopUI shop = FindAnyObjectByType<ShopUI>();
        if (shop != null && shop.IsOpen) return true;
        RewardUI reward = FindAnyObjectByType<RewardUI>();
        if (reward != null && reward.IsOpen) return true;
        TrainerNPC trainer = FindAnyObjectByType<TrainerNPC>();
        if (trainer != null && trainer.IsOpen) return true;
        BattleManager battle = BattleManager.Instance;
        if (battle != null && battle.IsBattleActive) return true;
        StoryBed bed = FindAnyObjectByType<StoryBed>();
        if (bed != null && bed.IsSleeping) return true;
        PlayerMemeShot shot = FindAnyObjectByType<PlayerMemeShot>();
        if (shot != null && shot.IsPlaying) return true;
        return false;
    }



    void Awake()
    {
        // the spot the player was placed on in the scene (Awake runs before anything can move him)
        startPosition = transform.position;
        startRotation = transform.rotation;
    }
}