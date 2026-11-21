using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Cabina del sotano: cuando el jugador entra, la puerta se cierra, parpadean 4 luces (indigo, amarillo,
// negro y rojo, los colores de los caminos) mientras se va desmayando y FaintTeleport lo lleva a la otra cabina.
public class CabinaTeletransporte : MonoBehaviour
{
    [Tooltip("Zona dentro de la cabina: al entrar el jugador empieza la secuencia.")]
    public BoxCollider zona;
    public FaintTeleport desmayo;
    [Tooltip("Opcional: la cabina no funciona hasta que se hayan revisado todas estas cajas.")]
    public RevisionCajas cajasRequeridas;

    [Header("Puerta")]
    public Transform puerta;
    [Tooltip("Punto de la bisagra (la puerta gira alrededor de el, en vertical).")]
    public Transform bisagra;
    [Tooltip("Grados que gira la puerta para cerrarse.")]
    public float anguloCerrar = 90f;
    public float duracionPuerta = 0.7f;
    public AudioClip sonidoPuerta;

    [Header("Luces")]
    public Light luz;
    public Color[] colores =
    {
        new Color(0.35f, 0.05f, 1f),   // indigo
        new Color(1f, 0.85f, 0f),      // amarillo
        Color.black,                   // negro (apagon)
        new Color(1f, 0.02f, 0.05f),   // rojo
    };
    public float intensidad = 20f;
    [Tooltip("Segundos de cada color.")]
    public float duracionColor = 0.6f;
    public AudioClip sonidoLuz;

    public UnityEvent alEntrar;

    public bool Usada { get; private set; }

    private Transform jugador;

    void Awake()
    {
        if (zona != null) zona.isTrigger = true;
        if (luz != null) luz.enabled = false;
    }

    void Update()
    {
        if (Usada || zona == null) return;
        if (cajasRequeridas != null && !cajasRequeridas.Completa) return;
        if (jugador == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm == null) return;
            jugador = pm.transform;
        }
        if (zona.bounds.Contains(jugador.position)) StartCoroutine(Secuencia());
    }

    // Tambien se puede llamar desde un UnityEvent
    public void Empezar()
    {
        if (!Usada) StartCoroutine(Secuencia());
    }

    // Debug (PanelDebug): la cabina se da por usada y el jugador aparece en la otra, sin puerta, luces ni desmayo.
    // "dentro" = donde estaria el cuerpo del jugador dentro de esta cabina (se pasa al sitio equivalente de la otra)
    public void Saltar(Vector3 dentro)
    {
        if (Usada) return;
        Usada = true;
        alEntrar?.Invoke();
        if (desmayo != null) desmayo.TeleportInstant(dentro);
    }

    IEnumerator Secuencia()
    {
        Usada = true;
        alEntrar?.Invoke();

        // El jugador se queda quieto mientras se cierra la puerta
        var quietos = new List<Behaviour>();
        PlayerMovement pm = jugador != null ? jugador.GetComponent<PlayerMovement>() : FindFirstObjectByType<PlayerMovement>();
        if (pm != null && pm.enabled) { pm.enabled = false; quietos.Add(pm); }
        if (pm != null && pm.TryGetComponent(out Rigidbody rb)) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        // Cerrar la puerta girando sobre la bisagra
        if (puerta != null && bisagra != null)
        {
            if (sonidoPuerta != null) AudioSource.PlayClipAtPoint(sonidoPuerta, puerta.position);
            float girado = 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duracionPuerta))
            {
                float objetivo = anguloCerrar * Mathf.SmoothStep(0f, 1f, t);
                puerta.RotateAround(bisagra.position, Vector3.up, objetivo - girado);
                girado = objetivo;
                yield return null;
            }
            puerta.RotateAround(bisagra.position, Vector3.up, anguloCerrar - girado);
        }

        foreach (Behaviour b in quietos) if (b != null) b.enabled = true;

        // Desmayo y teletransporte (FaintTeleport bloquea al jugador mientras dura)
        if (desmayo != null) desmayo.Teleport();
        yield return Luces();
    }

    IEnumerator Luces()
    {
        if (luz == null) yield break;
        foreach (Color c in colores)
        {
            bool apagon = c.maxColorComponent < 0.05f;
            if (sonidoLuz != null) AudioSource.PlayClipAtPoint(sonidoLuz, luz.transform.position);
            for (float t = 0f; t < duracionColor; t += Time.deltaTime)
            {
                float k = t / duracionColor;
                // Destello rapido que se apaga, con un pequeno parpadeo; el negro es un apagon total
                float brillo = apagon ? 0f : Mathf.Clamp01(k * 8f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, k))) * (Mathf.PerlinNoise(t * 25f, 0f) > 0.2f ? 1f : 0.3f);
                luz.enabled = brillo > 0.001f;
                luz.color = c;
                luz.intensity = intensidad * brillo;
                yield return null;
            }
        }
        luz.enabled = false;
    }

    void OnDrawGizmosSelected()
    {
        if (bisagra != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(bisagra.position + Vector3.down, bisagra.position + Vector3.up * 2f);
        }
    }
}
