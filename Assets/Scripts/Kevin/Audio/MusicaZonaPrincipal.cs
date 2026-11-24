using UnityEngine;

// Musica de fondo de la Zona Principal (la estacion): Sounds/SFX/sala central sonido.
// Suena en bucle (2D, igual en toda la estacion) mientras el jugador esta en ella; se va apagando al alejarse por
// los pasillos y no suena en la tienda, el sotano, dentro de las zonas ni en el menu. Al volver sigue donde iba.
// La arma Tools > Sonidos > Asignar sonidos nuevos (SonidosNuevosSetup).
[RequireComponent(typeof(AudioSource))]
public class MusicaZonaPrincipal : MonoBehaviour
{
    public AudioClip musica;
    [Range(0f, 1f)] public float volumen = 0.5f;
    [Tooltip("Metros fuera de la estacion (por los pasillos) en los que se va apagando")]
    public float distanciaFundido = 12f;
    [Tooltip("Metros por encima/debajo del piso de la estacion que siguen contando como estacion")]
    public float alturaEstacion = 3.5f;
    [Tooltip("Segundos del fundido al entrar o salir")]
    public float fundido = 2f;

    AudioSource fuente;
    VolumenZonas zonas;
    Transform jugador;
    Bounds estacion;
    bool hayEstacion;

    void Awake()
    {
        fuente = GetComponent<AudioSource>();
        fuente.clip = musica;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.dopplerLevel = 0f;
        fuente.volume = 0f;
    }

    void Start()
    {
        zonas = FindFirstObjectByType<VolumenZonas>();
        // La estacion: el piso de la Zona Principal (igual que FinalJuego)
        GameObject zp = GameObject.Find("Zona_Principal");
        Transform piso = zp != null ? zp.transform.Find("piso") : null;
        if (piso != null)
            foreach (Collider c in piso.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!hayEstacion) { estacion = c.bounds; hayEstacion = true; } else estacion.Encapsulate(c.bounds);
            }
        if (!hayEstacion) Debug.LogWarning("[Musica Zona Principal] No encontré el piso de la estación (Zona_Principal/piso).", this);
    }

    void Update()
    {
        if (musica == null) return;
        if (fuente.clip != musica) fuente.clip = musica;

        float meta = volumen * Cercania();
        fuente.volume = Mathf.MoveTowards(fuente.volume, meta, Time.unscaledDeltaTime * Mathf.Max(volumen, 0.01f) / Mathf.Max(0.01f, fundido));

        if (meta > 0f && !fuente.isPlaying)
        {
            if (fuente.time > 0f) fuente.UnPause(); else fuente.Play();
        }
        else if (meta <= 0f && fuente.isPlaying && fuente.volume <= 0f) fuente.Pause();
    }

    // 1 dentro de la estacion, baja a 0 al alejarse distanciaFundido metros (pasillos), 0 dentro de una zona
    float Cercania()
    {
        if (!hayEstacion || MenuPrincipal.Abierto) return 0f;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return 0f; }
        if (zonas != null && zonas.ZonaActual != null) return 0f;

        Vector3 p = jugador.position;
        float dx = Mathf.Max(estacion.min.x - p.x, 0f, p.x - estacion.max.x);
        float dz = Mathf.Max(estacion.min.z - p.z, 0f, p.z - estacion.max.z);
        float dy = Mathf.Max(Mathf.Abs(p.y - estacion.max.y) - alturaEstacion, 0f);
        float d = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        return 1f - Mathf.SmoothStep(0f, 1f, d / Mathf.Max(0.01f, distanciaFundido));
    }
}
