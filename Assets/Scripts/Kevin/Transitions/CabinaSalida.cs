using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Cabina a la que llega el jugador despues del desmayo: la puerta empieza cerrada (se cierra sola al iniciar,
// en la escena se ve abierta) y unas paredes invisibles no le dejan salir. Al resolver el puzzle del telefono
// (PhoneMinigame > On Repaired > CabinaSalida.Abrir) la puerta se abre y puede salir.
public class CabinaSalida : MonoBehaviour
{
    public Transform puerta;
    [Tooltip("Punto de la bisagra (la puerta gira alrededor de el, en vertical).")]
    public Transform bisagra;
    [Tooltip("Grados que gira la puerta para cerrarse (desde como esta en la escena).")]
    public float anguloCerrar = 90f;
    public float duracionPuerta = 0.9f;
    public bool cerradaAlIniciar = true;
    public AudioClip sonidoPuerta;

    [Header("Paredes invisibles")]
    [Tooltip("Pared del lado de la puerta: bloquea la salida mientras esta cerrada.")]
    public Collider paredPuerta;

    public UnityEvent alAbrir;

    public bool Cerrada { get; private set; }

    void Awake()
    {
        if (cerradaAlIniciar && puerta != null && bisagra != null)
        {
            puerta.RotateAround(bisagra.position, Vector3.up, anguloCerrar);
            Cerrada = true;
        }
        if (paredPuerta != null) paredPuerta.enabled = Cerrada;
    }

    public void Abrir()
    {
        if (!Cerrada) return;
        Cerrada = false;
        if (paredPuerta != null) paredPuerta.enabled = false;
        StartCoroutine(Girar(-anguloCerrar));
        alAbrir?.Invoke();
    }

    IEnumerator Girar(float angulo)
    {
        if (puerta == null || bisagra == null) yield break;
        if (sonidoPuerta != null) AudioSource.PlayClipAtPoint(sonidoPuerta, puerta.position);
        float girado = 0f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duracionPuerta))
        {
            float objetivo = angulo * Mathf.SmoothStep(0f, 1f, t);
            puerta.RotateAround(bisagra.position, Vector3.up, objetivo - girado);
            girado = objetivo;
            yield return null;
        }
        puerta.RotateAround(bisagra.position, Vector3.up, angulo - girado);
    }

    void OnDrawGizmosSelected()
    {
        if (bisagra == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(bisagra.position + Vector3.down, bisagra.position + Vector3.up * 2f);
    }
}
