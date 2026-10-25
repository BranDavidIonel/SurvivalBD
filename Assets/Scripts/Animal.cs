using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Animal : MonoBehaviour
{
    public string animalName;
    public bool playerInRange;

    [SerializeField] int currentHealth;
    [SerializeField] int maxHealth;

    //[Header("Sounds")]
    [SerializeField] AudioSource soundChannel;
    [SerializeField] AudioClip rabbitHitAndScreem;
    [SerializeField] AudioClip rabbitHitAndDie;

    private Animator animator;
    public bool isDead;

    [SerializeField] ParticleSystem bloadSplashParticles;
    public GameObject bloodPuddle;

    enum AnimaleType
    {
        Rabbit,
        Lion,
        Snake
    }

    [SerializeField] AnimaleType thisAnimalType;

    private void Start()
    {
        if (!healthSet) currentHealth = maxHealth;
        animator = GetComponent<Animator>();
    }

    bool healthSet;

    // RabbitLife: how many hits it takes (the stone axe does 10, the iron axe 20)
    public void SetMaxHealth(int health)
    {
        maxHealth = health;
        currentHealth = health;
        healthSet = true;
    }
    public void TakeDamage(int damage)
    {
        if (isDead == false)
        {
            currentHealth -= damage;
            var life = GetComponent<RabbitLife>();
            if (life != null) life.OnHit(); // it hops away for a moment

            if (bloadSplashParticles != null) bloadSplashParticles.Play();
            if (currentHealth <= 0)
            {
                PlayDyingSound();
                if (animator != null) { animator.enabled = true; animator.SetTrigger("die"); }
                var ai = GetComponent<AI_Movement>();
                if (ai != null) ai.enabled = false;
                if (bloodPuddle != null) bloodPuddle.SetActive(true);
                isDead = true;
            }
            else
            {
                PlayHitSound();

            }
        }
    }
    private void PlayDyingSound()
    {
        switch (thisAnimalType)
        {
            case AnimaleType.Rabbit:
                soundChannel.PlayOneShot(rabbitHitAndDie);
                break;
            case AnimaleType.Lion:
                //soundChannel.PlayOneShot(lion sound);
                break;
            default:
                break;

        }
        
    }
    private void PlayHitSound()
    {
        switch (thisAnimalType)
        {
            case AnimaleType.Rabbit:
                soundChannel.PlayOneShot(rabbitHitAndScreem);
                break;
            case AnimaleType.Lion:
                //soundChannel.PlayOneShot(lion screem sound);
                break;
            default:
                break;

        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

}
