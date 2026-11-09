using UnityEngine;

// Musica (OST) que sale de una radio. Ponerlo en el objeto de la radio y asignar la cancion en "Musica".
// - En el menu principal suena igual de fuerte en toda la pantalla (la camara del menu esta fuera de la tienda).
// - Al empezar el juego pasa poco a poco a sonar desde la radio (3D).
// - Al bajar la escalera se va apagando segun la altura del jugador, y en el sotano ya no suena.
//   Una vez llega al sotano se queda apagada para siempre (aunque el teletransporte lo lleve a otra altura).
// - El jugador la enciende y apaga interactuando (Interactable > On Interact > MusicaRadio.Alternar).
// Desde un UnityEvent: MusicaRadio > Alternar / Apagar / Encender / CambiarMusica.
[RequireComponent(typeof(AudioSource))]
public class MusicaRadio : MonoBehaviour
{
    public AudioClip musica;
    [Range(0f, 1f)] public float volumen = 0.6f;
    public bool encendidaAlIniciar = true;
    [Tooltip("Sonido al pulsar el boton de la radio (opcional).")]
    public AudioClip sonidoBoton;
    public string textoApagar = "Apagar radio";
    public string textoEncender = "Encender radio";

    public bool Encendida => objetivo > 0.5f;

    [Header("Sonido desde la radio")]
    [Tooltip("0 = se oye igual en todos lados, 1 = solo sale de la radio.")]
    [Range(0f, 1f)] public float sonido3D = 0.75f;
    [Tooltip("Hasta esta distancia se oye a todo volumen.")]
    public float distanciaMin = 3f;
    [Tooltip("A partir de esta distancia ya casi no se oye.")]
    public float distanciaMax = 25f;
    [Tooltip("Segundos que tarda en pasar del sonido del menu al de la radio al darle a Jugar.")]
    public float transicionMenu = 3f;

    [Header("Sotano")]
    [Tooltip("Altura (Y) del piso de la tienda (pies del jugador): aqui arriba suena a todo volumen.")]
    public float alturaTienda = 0f;
    [Tooltip("Altura (Y) del piso del sotano: aqui ya no suena. Si es igual a Altura Tienda no se usa.")]
    public float alturaSotano = -3f;

    [Header("Fundidos")]
    public float fundido = 1.5f;

    private AudioSource fuente;
    private Transform jugador;
    private Collider cuerpo;
    private float mezcla3D;     // 0 en el menu, sube a 1 al jugar
    private float factor = 1f;  // Apagar/Encender
    private float objetivo = 1f;
    private bool enSotano;

    void Awake()
    {
        fuente = GetComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = true;
        fuente.dopplerLevel = 0f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = distanciaMin;
        fuente.maxDistance = distanciaMax;
        if (musica != null) fuente.clip = musica;
        mezcla3D = MenuPrincipal.Abierto ? 0f : 1f;
        objetivo = factor = encendidaAlIniciar ? 1f : 0f;
        ActualizarTexto();
        Aplicar(0f);
        if (fuente.clip != null) fuente.Play();
    }

    void Update()
    {
        if (jugador == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) { jugador = pm.transform; cuerpo = pm.GetComponent<Collider>(); }
        }

        // Menu: suena "en la pantalla"; jugando: desde la radio
        float meta3D = MenuPrincipal.Abierto ? 0f : 1f;
        mezcla3D = Mathf.MoveTowards(mezcla3D, meta3D, Time.deltaTime / Mathf.Max(0.01f, transicionMenu));
        factor = Mathf.MoveTowards(factor, objetivo, Time.deltaTime / Mathf.Max(0.01f, fundido));

        Aplicar(Altura());

        if (enSotano && fuente.isPlaying && fuente.volume <= 0.0001f) fuente.Stop();
        // Apagada: se pausa (al encenderla sigue la cancion donde iba)
        else if (!enSotano && objetivo <= 0f && factor <= 0f && fuente.isPlaying) fuente.Pause();
    }

    // 1 en la tienda, baja por la escalera y 0 en el sotano
    float Altura()
    {
        if (enSotano) return 0f;
        if (jugador == null || MenuPrincipal.Abierto || Mathf.Approximately(alturaTienda, alturaSotano)) return 1f;
        // Altura de los pies del jugador
        float pies = cuerpo != null && cuerpo.enabled ? cuerpo.bounds.min.y : jugador.position.y;
        float k = Mathf.InverseLerp(alturaSotano, alturaTienda, pies);
        if (k <= 0f) enSotano = true;
        return Mathf.SmoothStep(0f, 1f, k);
    }

    void Aplicar(float porAltura)
    {
        fuente.spatialBlend = sonido3D * Mathf.SmoothStep(0f, 1f, mezcla3D);
        fuente.volume = volumen * factor * porAltura;
    }

    public void Alternar()
    {
        if (sonidoBoton != null) AudioSource.PlayClipAtPoint(sonidoBoton, transform.position);
        if (Encendida) Apagar(); else Encender();
    }

    public void Apagar()
    {
        objetivo = 0f;
        ActualizarTexto();
    }

    public void Encender()
    {
        objetivo = 1f;
        ActualizarTexto();
        if (enSotano || fuente == null || fuente.clip == null || fuente.isPlaying) return;
        if (fuente.time > 0f) fuente.UnPause(); else fuente.Play();
    }

    void ActualizarTexto()
    {
        if (TryGetComponent(out Interactable i)) i.SetPrompt(Encendida ? textoApagar : textoEncender);
    }

    public void CambiarMusica(AudioClip nueva)
    {
        musica = nueva;
        fuente.clip = nueva;
        if (nueva != null && !enSotano && Encendida) fuente.Play();
    }

    // Cajita verde en la escena para ver donde esta la radio aunque todavia no tenga modelo
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(0.3f, 0.2f, 0.12f));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, distanciaMin);
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, distanciaMax);
    }
}
