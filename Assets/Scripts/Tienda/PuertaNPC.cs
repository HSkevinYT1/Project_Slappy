using UnityEngine;

// Puerta que solo abren los NPCs que entran desde afuera (el cliente y el cultista del tutorial).
// - Se abre sola cuando alguno de ellos esta cerca y se cierra cuando se alejan.
// - El jugador no la puede mover: no tiene Rigidbody (no se empuja), ni Interactable, ni se puede agarrar.
// Va en el objeto de la bisagra; la hoja es un hijo que gira con el.
public class PuertaNPC : MonoBehaviour
{
    [Tooltip("Los unicos que abren la puerta")]
    public Transform[] npcs;
    [Tooltip("Distancia (horizontal) a la que se abre")]
    public float distancia = 2.2f;
    [Tooltip("Grados que gira al abrirse (negativo = hacia afuera de la tienda)")]
    public float anguloAbierta = -100f;
    [Tooltip("Grados por segundo")]
    public float velocidad = 160f;
    [Tooltip("Centro de la hoja cerrada (para medir la distancia); si se deja vacio se usa la bisagra")]
    public Transform centro;
    public AudioClip sonidoAbrir, sonidoCerrar;

    Quaternion cerrada;
    Vector3 puntoCentro;
    float angulo;
    bool abierta;

    void Start()
    {
        cerrada = transform.localRotation;
        puntoCentro = centro ? centro.position : transform.position;
    }

    void Update()
    {
        bool abrir = false;
        foreach (Transform n in npcs)
        {
            if (!n || !n.gameObject.activeInHierarchy) continue;
            Vector3 d = n.position - puntoCentro;
            d.y = 0f;
            if (d.sqrMagnitude < distancia * distancia) { abrir = true; break; }
        }

        if (abrir != abierta)
        {
            abierta = abrir;
            AudioClip clip = abrir ? sonidoAbrir : sonidoCerrar;
            if (clip) AudioSource.PlayClipAtPoint(clip, puntoCentro, 0.6f);
        }

        float destino = abrir ? anguloAbierta : 0f;
        angulo = Mathf.MoveTowards(angulo, destino, velocidad * Time.deltaTime);
        // Suaviza el final del recorrido
        float t = Mathf.Abs(anguloAbierta) > 0.01f ? angulo / anguloAbierta : 0f;
        float suave = t * t * (3f - 2f * t);
        transform.localRotation = cerrada * Quaternion.Euler(0f, suave * anguloAbierta, 0f);
    }
}
