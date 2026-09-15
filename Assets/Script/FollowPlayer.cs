using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class FollowPlayer : MonoBehaviour
{
    public Transform player;
    NavMeshAgent nav;
    private Animator anim;

    void Start()
    {
        nav = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (player != null && nav.isOnNavMesh)
        {
            // Bắt pet đuổi theo Player
            nav.SetDestination(player.position);

            // Kiểm tra vận tốc thực tế của NavMeshAgent để chuyển Animation
            // agent.velocity.remainingDistance là khoảng cách còn lại tới mục tiêu
            // Ở đây dùng agent.velocity.magnitude (độ lớn vận tốc) là chuẩn nhất
            if (nav.velocity.magnitude > 0.1f)
            {
                // Nếu đang di chuyển, đặt isRunning trong Animator thành true
                anim.SetBool("isRunning", true);
            }
            else
            {
                // Nếu đứng yên, đặt isRunning thành false
                anim.SetBool("isRunning", false);
            }
        }
    }
}
