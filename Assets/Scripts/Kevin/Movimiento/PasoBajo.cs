using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Pared por debajo de la que el jugador puede pasar al otro lado (ej: las paredes elevadas del laberinto).
// Se usa con su Interactable (On Interact > PasoBajo > Pasar): el jugador se agacha, cruza por debajo
// y se levanta del otro lado. Funciona en los dos sentidos. Los enemigos no caben por debajo.
[RequireComponent(typeof(Collider))]
public class PasoBajo : MonoBehaviour
{
    [Tooltip("Puntos a cada lado de la pared. Vacios = se calculan solos (la pared se cruza por su lado mas delgado).")]
    public Transform ladoA;
    public Transform ladoB;
    [Tooltip("Distancia desde la pared hasta donde queda el jugador a cada lado.")]
    public float separacion = 0.8f;

    [Header("Animacion")]
    [Tooltip("Cuanto baja la camara al agacharse (m).")]
    public float bajada = 0.9f;
    public float duracionAgacharse = 0.35f;
    public float duracionCruzar = 0.9f;
    public float duracionLevantarse = 0.35f;
    public AudioClip sonido;

    public static bool Pasando { get; private set; }

    private Collider pared;

    void Awake() => pared = GetComponent<Collider>();

    // Se elige en el On Interact del Interactable
    public void Pasar(PlayerInteractor interactor)
    {
        if (Pasando || Escondite.JugadorEscondido) return;
        PlayerMovement movimiento = FindFirstObjectByType<PlayerMovement>();
        if (movimiento == null) return;
        StartCoroutine(Cruzar(movimiento));
    }

    // Puntos de los dos lados, a la altura del cuerpo del jugador.
    // Sin puntos asignados, se cruza por el eje (X o Z) que tenga los dos lados despejados.
    void Lados(float alturaCuerpo, Transform jugador, out Vector3 a, out Vector3 b)
    {
        Bounds bounds = pared.bounds;
        if (ladoA != null && ladoB != null)
        {
            a = ladoA.position; b = ladoB.position;
        }
        else
        {
            bool xDelgado = bounds.extents.x < bounds.extents.z;
            Vector3 eje1 = xDelgado ? Vector3.right : Vector3.forward;
            Vector3 eje2 = xDelgado ? Vector3.forward : Vector3.right;
            if (jugador == null || Despejado(bounds, eje1, alturaCuerpo, jugador) || !Despejado(bounds, eje2, alturaCuerpo, jugador))
                PuntosEje(bounds, eje1, out a, out b);
            else
                PuntosEje(bounds, eje2, out a, out b);
        }
        a.y = alturaCuerpo;
        b.y = alturaCuerpo;
    }

    void PuntosEje(Bounds bounds, Vector3 eje, out Vector3 a, out Vector3 b)
    {
        float mitad = Vector3.Scale(bounds.extents, eje).magnitude;
        a = bounds.center - eje * (mitad + separacion);
        b = bounds.center + eje * (mitad + separacion);
    }

    // Los dos lados de ese eje sin paredes (a la altura del cuerpo)
    bool Despejado(Bounds bounds, Vector3 eje, float altura, Transform jugador)
    {
        PuntosEje(bounds, eje, out Vector3 a, out Vector3 b);
        a.y = b.y = altura;
        return Libre(a, jugador) && Libre(b, jugador);
    }

    bool Libre(Vector3 p, Transform jugador)
    {
        foreach (Collider c in Physics.OverlapSphere(p, 0.35f, ~0, QueryTriggerInteraction.Ignore))
            if (c != pared && !c.transform.IsChildOf(jugador)) return false;
        return true;
    }

    IEnumerator Cruzar(PlayerMovement movimiento)
    {
        Pasando = true;
        Transform cuerpo = movimiento.transform;
        Rigidbody rb = movimiento.GetComponent<Rigidbody>();
        Camera cam = Camera.main;
        Transform camara = cam != null && cam.transform.IsChildOf(cuerpo) ? cam.transform : null;

        // Del lado en el que esta hacia el otro
        Lados(cuerpo.position.y, cuerpo, out Vector3 a, out Vector3 b);
        bool desdeA = (cuerpo.position - a).sqrMagnitude < (cuerpo.position - b).sqrMagnitude;
        Vector3 inicio = desdeA ? a : b, fin = desdeA ? b : a;
        Vector3 dir = fin - inicio; dir.y = 0f;
        Quaternion mirar = Quaternion.LookRotation(dir.normalized);

        // Sin controles ni colisiones mientras cruza
        var apagados = new List<Behaviour>();
        void Apagar(Behaviour x) { if (x != null && x.enabled) { x.enabled = false; apagados.Add(x); } }
        Apagar(movimiento);
        foreach (MouseLook m in cuerpo.GetComponentsInChildren<MouseLook>()) Apagar(m);
        foreach (HeadBob h in cuerpo.GetComponentsInChildren<HeadBob>()) Apagar(h);
        foreach (PlayerInteractor p in cuerpo.GetComponentsInChildren<PlayerInteractor>()) Apagar(p);
        var colliders = new List<Collider>();
        foreach (Collider c in cuerpo.GetComponentsInChildren<Collider>())
            if (c.enabled && !c.isTrigger) { c.enabled = false; colliders.Add(c); }
        bool eraKinematic = rb != null && rb.isKinematic;
        if (rb != null) rb.isKinematic = true;

        Vector3 camLocal = camara != null ? camara.localPosition : Vector3.zero;
        Quaternion camRot = camara != null ? camara.localRotation : Quaternion.identity;
        if (sonido != null) AudioSource.PlayClipAtPoint(sonido, transform.position);

        // 1. Se agacha y se pone frente a la pared
        Vector3 desde = cuerpo.position;
        Quaternion rotDesde = cuerpo.rotation;
        yield return Animar(duracionAgacharse, k =>
        {
            cuerpo.SetPositionAndRotation(Vector3.Lerp(desde, inicio, k), Quaternion.Slerp(rotDesde, mirar, k));
            if (camara != null)
            {
                camara.localPosition = camLocal + Vector3.down * bajada * k;
                camara.localRotation = Quaternion.Slerp(camRot, Quaternion.Euler(10f, 0f, 0f), k);
            }
        });

        // 2. Cruza por debajo
        yield return Animar(duracionCruzar, k => cuerpo.position = Vector3.Lerp(inicio, fin, k));

        // 3. Se levanta
        yield return Animar(duracionLevantarse, k =>
        {
            if (camara != null)
            {
                camara.localPosition = camLocal + Vector3.down * bajada * (1f - k);
                camara.localRotation = Quaternion.Slerp(Quaternion.Euler(10f, 0f, 0f), Quaternion.identity, k);
            }
        });

        if (camara != null) camara.localPosition = camLocal;
        if (rb != null)
        {
            rb.position = cuerpo.position;
            rb.rotation = cuerpo.rotation;
            rb.isKinematic = eraKinematic;
            rb.linearVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();
        foreach (Collider c in colliders) if (c != null) c.enabled = true;
        foreach (Behaviour x in apagados) if (x != null) x.enabled = true;
        Pasando = false;
    }

    static IEnumerator Animar(float duracion, System.Action<float> paso)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duracion))
        {
            paso(Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        paso(1f);
    }

    void OnDrawGizmosSelected()
    {
        if (pared == null) pared = GetComponent<Collider>();
        Lados(pared.bounds.min.y, null, out Vector3 a, out Vector3 b);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(a, 0.3f);
        Gizmos.DrawWireSphere(b, 0.3f);
        Gizmos.DrawLine(a, b);
    }
}
