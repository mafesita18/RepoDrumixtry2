using UnityEngine;

public class DrumPad : MonoBehaviour
{
    [Header("Sonido del Tambor / Platillo")]
    public AudioClip hitSound;

    private AudioSource audioSource;

    private void Awake()
    {
        // Obtiene o crea el AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Imprime en consola para verificar qué objeto está chocando
        Debug.Log("¡Colisión detectada con: " + other.gameObject.name + "!");

        // Reproduce el sonido
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }
}