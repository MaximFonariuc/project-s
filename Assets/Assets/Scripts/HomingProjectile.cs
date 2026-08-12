using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomingProjectile : MonoBehaviour
{
    public GameObject explosionPrefab;
    public AudioClip explosionSound;
    private Transform player;
    private float speed = 50;
    private float turn = 20;
    private Vector3 offset;

    private float distance;
    private Rigidbody rb;

    private bool chase = true;

    private float lastTime;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        offset = new Vector3(0, 1f, 0);
        rb = this.GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        distance = (player.transform.position - this.transform.position).sqrMagnitude;

        rb.linearVelocity = transform.forward * speed; // aplica velocidade ao projetil

        if(distance > 2 && chase) // caso ainda nao tenha passado pelo player
        {
            Quaternion targetRotation = Quaternion.LookRotation((player.position + offset) - transform.position);
            rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, targetRotation, turn));
        } else
        {
            chase = false; // nao persegue mais o player, apenas segue em frente
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TimeInterval()) return;
        lastTime = Time.time;
        Instantiate(explosionPrefab, this.transform.position, Quaternion.identity); // explosao

        Destroy(this.gameObject, 0.1f); // destroi este objeto apos colidir com algo
    }

    private bool TimeInterval()
    {
        return Time.time > lastTime + 0.5f;
    }

}
