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
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
    }
    public void TakeDamage(int damage)
    {
        if (isDead == false)
        {
            currentHealth -= damage;

            bloadSplashParticles.Play();
            if (currentHealth <= 0)
            {
                PlayDyingSound();
                animator.SetTrigger("die");
                GetComponent<AI_Movement>().enabled = false;
                bloodPuddle.SetActive(true);
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
