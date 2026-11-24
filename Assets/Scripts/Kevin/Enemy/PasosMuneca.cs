using UnityEngine;
using UnityEngine.AI;

// Pisadas en el agua de las muñecas de las Cloacas: sonido 3D que suena (en bucle) mientras la muñeca avanza, o sea,
// mientras el jugador NO la esta mirando. Al congelarse (la ven), destruirse o con el menu abierto se apaga con un
// fundido corto. Corriendo (cuando oyen al jugador correr) suena mas rapido.
// Va en la raiz del prefab "Enemigo 3" (lo pone Tools > Cloacas > Modelo de la muñeca, MunecaModeloSetup).
public class PasosMuneca : MonoBehaviour
{
    [Tooltip("Tira de pasos en bucle (Sounds/SFX/Pisadas_Agua)")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volumen = 0.9f;
    [Tooltip("Metros a los que suena a todo volumen")]
    public float distanciaMinima = 1.5f;
    [Tooltip("Metros a partir de los que ya no se oye")]
    public float distanciaMaxima = 24f;
    [Tooltip("Velocidad (m/s) a la que el clip suena a su ritmo normal")]
    public float velocidadNormal = 3.5f;
    [Tooltip("Limites del ritmo: lento / rapido")]
    public Vector2 ritmo = new Vector2(0.85f, 1.4f);
    [Tooltip("Segundos del fundido al empezar y al parar")]
    public float fundido = 0.15f;

    AudioSource fuente;
    EstatuaAI ai;
    NavMeshAgent agente;
    MunecaLuz luz;
    Vector3 ultimaPos;
    float ultimoPaso = -10f;
    float variacion;

    void Awake()
    {
        ai = GetComponent<EstatuaAI>();
        agente = GetComponent<NavMeshAgent>();
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = distanciaMinima;
        fuente.maxDistance = distanciaMaxima;
        fuente.dopplerLevel = 0f;
        fuente.volume = 0f;
        // Cada muñeca con un ritmo un poco distinto: varias a la vez no suenan como una sola
        variacion = Random.Range(0.92f, 1.08f);
        if (clip != null) clip.LoadAudioData();
        ultimaPos = transform.position;
    }

    void OnDisable()
    {
        if (fuente) { fuente.volume = 0f; fuente.Pause(); }
    }

    void Update()
    {
        if (clip == null) return;
        if (luz == null) luz = GetComponent<MunecaLuz>();   // AparicionMunecas lo agrega despues de crearla

        // EstatuaAI la mueve a saltos (cada pocos frames): cuenta como andando si se movio hace poco
        Vector3 p = transform.position;
        Vector3 d = p - ultimaPos; d.y = 0f;
        float m = d.sqrMagnitude;
        if (m > 0.000025f && m < 9f) ultimoPaso = Time.time;   // un salto grande es un teletransporte (reintento)
        ultimaPos = p;

        bool moviendose = ai == null || (ai.enabled && ai.CurrentState == EstatuaAI.State.Moving);
        bool andando = moviendose && Time.time - ultimoPaso < 0.25f && (luz == null || !luz.Destruida) &&
                       !PantallaMuerte.Muerto && !MenuPrincipal.Abierto && Time.timeScale > 0f;

        float objetivo = andando ? volumen : 0f;
        fuente.volume = Mathf.MoveTowards(fuente.volume, objetivo, Time.deltaTime * volumen / Mathf.Max(0.01f, fundido));
        if (andando)
        {
            float v = agente != null ? agente.speed : velocidadNormal;
            fuente.pitch = Mathf.Clamp(v / Mathf.Max(0.1f, velocidadNormal), ritmo.x, ritmo.y) * variacion;
        }

        if (andando && !fuente.isPlaying)
        {
            // Empieza en un punto al azar para que no suene siempre igual
            if (clip.loadState == AudioDataLoadState.Loaded) fuente.time = Random.Range(0f, clip.length * 0.9f);
            fuente.Play();
        }
        else if (!andando && fuente.isPlaying && fuente.volume <= 0f) fuente.Pause();
    }
}
