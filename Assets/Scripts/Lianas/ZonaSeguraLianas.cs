using UnityEngine;
using UnityEngine.Events;

// Zona segura del laberinto: cuando el jugador entra se dispara el evento "alLlegar"
// (sonidos, dialogos, etc). Ya no enciende todas las lianas.
// Necesita un Collider marcado como "Is Trigger" (se marca solo al agregar el componente).
[RequireComponent(typeof(Collider))]
public class ZonaSeguraLianas : MonoBehaviour
{
    [Tooltip("Lianas del laberinto. Si se deja vacio, se busca en los padres o en la escena.")]
    public LianasLaberinto lianas;

    public string tagJugador = "Player";

    [Tooltip("Evento extra al llegar (sonidos, dialogos, etc).")]
    public UnityEvent alLlegar;

    bool activada;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
        lianas = GetComponentInParent<LianasLaberinto>();
    }

    void Awake()
    {
        if (!lianas) lianas = GetComponentInParent<LianasLaberinto>();
        if (!lianas) lianas = FindFirstObjectByType<LianasLaberinto>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (activada) return;

        bool esJugador = other.CompareTag(tagJugador)
            || (other.attachedRigidbody && other.attachedRigidbody.CompareTag(tagJugador));
        if (!esJugador) return;

        activada = true;
        // Llegar ya no enciende todas las lianas (solo se encienden por donde pasa el jugador).
        alLlegar?.Invoke();
    }

    void OnDrawGizmos()
    {
        Collider c = GetComponent<Collider>();
        if (!c) return;
        Gizmos.color = new Color(0.29f, 0f, 0.51f, 0.35f);
        Bounds b = c.bounds;
        Gizmos.DrawCube(b.center, b.size);
    }
}
