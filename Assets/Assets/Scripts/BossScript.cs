using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class BossScript : MonoBehaviour
{
    public Transform model;
    public Transform greatSword;

    private Animator anim;
    public Transform player;

    public AudioClip[] takeDamageSound;

    public GameObject bloodPrefab;
    public Transform bloodPos;

    private float rotationSpeed = 6;

    private float lastDamageTakenTime = 0;

    // Hit Counter
    private int hit = 0; // hit vindo das animacoes
    private int currentHit = 0; // combo verdadeiro

    void Start()
    {
        anim = model.GetComponent<Animator>();
    }

    void Update()
    {
        if (anim.GetBool("Dead")) return; // nao faz nada caso esteja morto

        if (anim.GetCurrentAnimatorStateInfo(0).IsName("idle") && anim.GetCurrentAnimatorStateInfo(1).IsName("None") || !anim.GetBool("CanRotate")) // move as pernas ao rotacionar caso esteja parado
        {
            Vector3 rotationOffset = player.transform.position - model.position;
            rotationOffset.y = 0;
            float lookDirection = Vector3.SignedAngle(model.forward, rotationOffset, Vector3.up);
            anim.SetFloat("LookDirection", lookDirection);
        }
        else if (!anim.GetBool("Attacking") && anim.GetBool("CanRotate"))
        {
            //model.transform.LookAt(player.transform.position); // olha para o player caso nao esteja atacando

            var targetRotation = Quaternion.LookRotation(player.transform.position - model.transform.position);

            // Smoothly rotate towards the target point.
            model.transform.rotation = Quaternion.Slerp(model.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        model.transform.eulerAngles = new Vector3(0, model.transform.eulerAngles.y, 0);
        //model.transform.position = new Vector3(model.transform.position.x, 0, model.transform.position.z);
    }

    private void GreatSwordCollider(bool b) // seta a GreatSword com collider on enquanto estiver atacando
    {
        greatSword.GetComponent<BoxCollider>().isTrigger = !b;
    }

    private void OnTriggerEnter(Collider other) // player atacou o boss e o boss nao esta atacando
    {
        if(other.gameObject.tag == "Sword" && other.gameObject.GetComponentInParent<Animator>().GetBool("Attacking") && !anim.GetBool("Attacking") && DamageInterval() && !anim.GetBool("Dead"))
        {
            lastDamageTakenTime = Time.time;
            CreateAndPlay(takeDamageSound[UnityEngine.Random.Range(0, takeDamageSound.Length)], 2); // som de dano
            StopAllCoroutines(); // reinicia o timer de 2seg do texto
            StartCoroutine(ShowHitCounter()); // exibe informacao sobre o dano
            if (!anim.GetBool("TakingDamage") && !anim.GetBool("Attacking") && anim.GetBool("NotAttacking")) // caso ja nao esteja tocando a animacao de dano
                anim.SetTrigger("TakeDamage"); // animacao de dano

            GameObject blood = Instantiate(bloodPrefab, bloodPos.position, Quaternion.identity);
            blood.transform.LookAt(player.position);
            Destroy(blood, 0.2f);
        }
    }

    public void RegisterPlayerSwordFillDamage()
    {
        if (!anim.GetBool("Attacking") && DamageInterval() && !anim.GetBool("Dead")) // ja esta sendo conferido se o jogador esta atacando antes de vir pra ca
        {
            lastDamageTakenTime = Time.time;
            CreateAndPlay(takeDamageSound[UnityEngine.Random.Range(0, takeDamageSound.Length)], 2); // som de dano
            StopAllCoroutines(); // reinicia o timer de 2seg do texto
            StartCoroutine(ShowHitCounter()); // exibe informacao sobre o dano
            if (!anim.GetBool("TakingDamage") && !anim.GetBool("Attacking") && anim.GetBool("NotAttacking")) // caso ja nao esteja tocando a animacao de dano
                anim.SetTrigger("TakeDamage"); // animacao de dano

            GameObject blood = Instantiate(bloodPrefab, bloodPos.position, Quaternion.identity);
            blood.transform.LookAt(player.position);
            Destroy(blood, 0.2f);
        }
    }

    public void SwordHit(int hit) // recebe o hit atual da animacao
    {
        this.hit = hit;
        if (hit == 0)
            ClearCurrentHit();
    }

    public void ClearCurrentHit() // animacao diz que terminou o combo
    {
        this.currentHit = 0;
    }

    public void HitManager() // Gerencia o combo verdadeiro
    {
        
        currentHit++; // incrementa a variavel que guarda quantos hits foram ate agora

        if (hit == 1) currentHit = 1; // o primeiro ataque sempre sera hit 1

        
    }

    private bool DamageInterval() // garante que tenha meio segundo entre um dano e outro
    {
        return (Time.time > lastDamageTakenTime + 0.7f);
    }

    IEnumerator ShowHitCounter()
    {
        HitManager(); // Seta o texto correto
        yield return new WaitForSeconds(2);
    }

    private void CreateAndPlay(AudioClip clip, float destructionTime, float volume = 1f) // toca um som e o destroi deopis de x segundos
    {
        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
        Destroy(audioSource, destructionTime);
    }

}
