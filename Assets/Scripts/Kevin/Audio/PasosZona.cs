using System.Collections.Generic;
using UnityEngine;

// Pasos del jugador. El clip es una tira de varios pasos: suena en bucle mientras el jugador camina por el suelo
// y se apaga con un fundido corto al quedarse quieto, saltar/caer o esconderse. Corriendo suena mas rapido.
// - Con "Area" (ej: el agua de las Cloacas): solo suenan dentro de esa area.
// - Sin "Area" (pasos normales): suenan en todos lados MENOS donde otro PasosZona con area tenga al jugador
//   (asi en las Cloacas suena el agua y no los dos a la vez).
// Lo arman Tools > Cloacas > Pasos en el agua (CloacasPasosSetup) y Tools > Jugador > Pasos normales (PasosNormalesSetup).
public class PasosZona : MonoBehaviour
{
    [Tooltip("Tira de pasos en bucle (ej: Pasos, Pisadas_Agua)")]
    public AudioClip clip;
    [Tooltip("Area donde suenan estos pasos. Vacio = pasos normales (en todos lados menos en las areas con pasos propios)")]
    public ZonaVolumen area;
    [Range(0f, 1f)] public float volumen = 0.6f;
    [Tooltip("Velocidad (m/s) a la que el clip suena a su ritmo normal (caminando)")]
    public float velocidadNormal = 5f;
    [Tooltip("Limites del ritmo: caminando lento / corriendo")]
    public Vector2 ritmo = new Vector2(0.85f, 1.35f);
    [Tooltip("Segundos del fundido al empezar y al parar")]
    public float fundido = 0.12f;

    static readonly List<PasosZona> todas = new List<PasosZona>();

    AudioSource fuente;
    Transform jugador;
    Rigidbody rb;
    Collider cuerpo;

    void OnEnable() => todas.Add(this);
    void OnDisable()
    {
        todas.Remove(this);
        if (fuente) { fuente.volume = 0f; fuente.Pause(); }
    }

    void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;     // son los pasos del propio jugador
        fuente.volume = 0f;
        fuente.dopplerLevel = 0f;
        // El clip no tiene "Preload Audio Data": se carga ya para que el primer paso no llegue tarde
        if (clip != null) clip.LoadAudioData();
    }

    void Update()
    {
        if (clip == null) return;
        if (jugador == null)
        {
            jugador = HUD.Jugador();
            if (jugador == null) return;
            rb = jugador.GetComponent<Rigidbody>();
            foreach (Collider c in jugador.GetComponents<Collider>()) if (!c.isTrigger) { cuerpo = c; break; }
        }

        Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
        float horizontal = new Vector2(v.x, v.z).magnitude;
        bool enArea = area != null ? area.Contiene(jugador.position) : !OtraAreaTieneAlJugador();
        bool caminando = enArea && horizontal > 0.5f && EnElSuelo() && !Escondite.JugadorEscondido &&
                         !MenuPrincipal.Abierto && Time.timeScale > 0f;

        float objetivo = caminando ? volumen : 0f;
        fuente.volume = Mathf.MoveTowards(fuente.volume, objetivo, Time.deltaTime * volumen / Mathf.Max(0.01f, fundido));
        if (caminando) fuente.pitch = Mathf.Clamp(horizontal / Mathf.Max(0.1f, velocidadNormal), ritmo.x, ritmo.y);

        if (caminando && !fuente.isPlaying)
        {
            // Empieza en un punto al azar para que no suene siempre igual
            if (clip.loadState == AudioDataLoadState.Loaded) fuente.time = Random.Range(0f, clip.length * 0.9f);
            fuente.Play();
        }
        else if (!caminando && fuente.isPlaying && fuente.volume <= 0f) fuente.Pause();
    }

    // Para los pasos normales: el jugador esta en un area que tiene sus propios pasos (ej: el agua de las Cloacas)
    bool OtraAreaTieneAlJugador()
    {
        foreach (PasosZona p in todas)
            if (p != this && p.area != null && p.clip != null && p.area.Contiene(jugador.position)) return true;
        return false;
    }

    // Pisa algo (no esta saltando ni cayendo). Sirve tambien en escaleras, donde la velocidad vertical no es 0.
    static readonly RaycastHit[] hits = new RaycastHit[8];
    bool EnElSuelo()
    {
        if (cuerpo == null) return rb == null || Mathf.Abs(rb.linearVelocity.y) < 1.5f;
        Bounds b = cuerpo.bounds;
        int n = Physics.RaycastNonAlloc(b.center, Vector3.down, hits, b.extents.y + 0.35f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (!hits[i].collider.transform.IsChildOf(jugador)) return true;
        return false;
    }
}
