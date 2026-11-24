using UnityEngine;

// Pisadas del gato de los Backrooms: la misma tira de pasos del jugador (Sounds/SFX/Pasos.mp3), en 3D y mas aguda
// (pasos chicos y rapidos). Suena en bucle mientras el gato se mueve (segun su velocidad real: caminando, corriendo,
// en la cinematica) y se apaga con un fundido corto al quedarse quieto. Corriendo suena mas rapido.
// Lo agrega BackroomsGuion al gato (con el clip que le asigna BackroomsPasillosSetup).
public class PasosGato : MonoBehaviour
{
    [Tooltip("Tira de pasos en bucle (la del jugador: Sounds/SFX/Pasos.mp3)")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volumen = 0.55f;
    [Tooltip("Cuanto mas agudo que los pasos del jugador (1 = igual)")]
    public float agudo = 1.7f;
    [Tooltip("Velocidad (m/s) a la que suena a su ritmo normal (caminando)")]
    public float velocidadNormal = 1.8f;
    [Tooltip("Limites del ritmo: lento / corriendo")]
    public Vector2 ritmo = new Vector2(0.85f, 1.35f);
    public float distanciaMinima = 1.2f, distanciaMaxima = 18f;
    [Tooltip("Segundos del fundido al empezar y al parar")]
    public float fundido = 0.1f;

    AudioSource fuente;
    Vector3 ultimaPos;
    float velocidad;

    void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = distanciaMinima;
        fuente.maxDistance = distanciaMaxima;
        fuente.dopplerLevel = 0f;
        fuente.volume = 0f;
        ultimaPos = transform.position;
    }

    void OnDisable()
    {
        if (fuente) { fuente.volume = 0f; fuente.Pause(); }
    }

    void Update()
    {
        if (clip == null || fuente == null) return;
        if (fuente.clip != clip) { fuente.clip = clip; clip.LoadAudioData(); }

        float dt = Time.deltaTime;
        Vector3 d = transform.position - ultimaPos; d.y = 0f;
        ultimaPos = transform.position;
        if (dt > 0f)
        {
            float v = d.magnitude / dt;
            if (v > 15f) v = 0f;   // teletransporte (reaparecer, reintento): no cuenta como pasos
            velocidad = Mathf.Lerp(velocidad, v, 1f - Mathf.Exp(-10f * dt));
        }

        bool andando = velocidad > 0.15f && !PantallaMuerte.Muerto && !MenuPrincipal.Abierto && Time.timeScale > 0f;
        fuente.volume = Mathf.MoveTowards(fuente.volume, andando ? volumen : 0f, dt * volumen / Mathf.Max(0.01f, fundido));
        if (andando) fuente.pitch = agudo * Mathf.Clamp(velocidad / Mathf.Max(0.1f, velocidadNormal), ritmo.x, ritmo.y);

        if (andando && !fuente.isPlaying)
        {
            // Empieza en un punto al azar para que no suene siempre igual
            if (clip.loadState == AudioDataLoadState.Loaded) fuente.time = Random.Range(0f, clip.length * 0.9f);
            fuente.Play();
        }
        else if (!andando && fuente.isPlaying && fuente.volume <= 0f) fuente.Pause();
    }
}
