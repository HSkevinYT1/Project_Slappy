using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Lugar para esconderse (ej: el armario). Se usa con su Interactable (On Interact > Escondite > Usar):
// la primera vez el jugador entra y se cierra la puerta; la siguiente sale.
// Dentro suena en bucle el sonido de agitacion (respiracion, latidos...) y los enemigos no lo ven ni lo oyen.
public class Escondite : MonoBehaviour
{
    [Header("Puntos")]
    [Tooltip("Donde queda la camara del jugador dentro (posicion y hacia donde mira).")]
    public Transform puntoDentro;
    [Tooltip("Donde queda el jugador al salir.")]
    public Transform puntoSalida;

    [Header("Puerta")]
    [Tooltip("Bisagra de la puerta: gira en Y al abrir.")]
    public Transform puerta;
    public float anguloAbierta = -105f;
    // Segundos que tarda en abrirse o cerrarse
    public float duracionPuerta = 0.35f;
    // Segundos que tarda el jugador en meterse o salir
    public float duracionMovimiento = 0.6f;

    [Header("Sonido")]
    [Tooltip("Sonido de agitacion que se repite mientras el jugador esta escondido.")]
    public AudioClip sonidoAgitacion;
    [Range(0f, 1f)] public float volumenAgitacion = 0.8f;
    public float fadeAgitacion = 0.8f;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;

    [Header("Textos")]
    public string textoEsconderse = "Esconderse";
    public string textoSalir = "Salir";

    public UnityEvent alEsconderse;
    public UnityEvent alSalir;

    // Escondite en el que esta el jugador ahora (null = no esta escondido)
    public static Escondite Actual { get; private set; }
    public static bool JugadorEscondido => Actual != null;

    public bool Ocupado { get; private set; }

    private Interactable interactable;
    private AudioSource fuente;
    private Quaternion puertaCerrada;
    private Transform cuerpo;
    private Rigidbody rb;
    private Collider[] collidersJugador;
    private Behaviour[] controlesJugador;
    private float alturaCuerpo;
    private int fadeId;

    void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (interactable != null) interactable.SetPrompt(textoEsconderse);
        if (puerta != null) puertaCerrada = puerta.localRotation;

        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = true;
        fuente.spatialBlend = 0f;
        fuente.volume = 0f;
    }

    void OnDisable()
    {
        if (Actual == this) Actual = null;
    }

    // Se elige en el On Interact del Interactable
    public void Usar(PlayerInteractor interactor)
    {
        if (Ocupado) return;
        if (Actual == this) StartCoroutine(Salir());
        else if (Actual == null) StartCoroutine(Entrar());
    }

    IEnumerator Entrar()
    {
        if (!BuscarJugador()) yield break;
        Ocupado = true;
        Actual = this;

        // El jugador no se mueve ni choca mientras se mete
        alturaCuerpo = cuerpo.position.y;
        SetControles(false);
        rb.isKinematic = true;
        foreach (Collider c in collidersJugador) c.enabled = false;

        yield return MoverPuerta(true, sonidoAbrir);

        Camera cam = Camera.main;
        Vector3 offsetCamara = cam != null ? cam.transform.position - cuerpo.position : Vector3.up * 0.8f;
        Vector3 destino = puntoDentro.position - offsetCamara;
        Quaternion giro = Quaternion.Euler(0f, puntoDentro.eulerAngles.y, 0f);
        yield return MoverCuerpo(destino, giro);
        if (cam != null) cam.transform.localRotation = Quaternion.identity;

        yield return MoverPuerta(false, sonidoCerrar);

        // Dentro se puede mirar, pero no caminar
        foreach (Behaviour b in controlesJugador) if (b is MouseLook) b.enabled = true;
        SetInteraccion(true);
        if (interactable != null) interactable.SetPrompt(textoSalir);
        StartCoroutine(FadeAgitacion(true));
        alEsconderse?.Invoke();
        Ocupado = false;
    }

    IEnumerator Salir()
    {
        Ocupado = true;
        SetControles(false);
        StartCoroutine(FadeAgitacion(false));

        yield return MoverPuerta(true, sonidoAbrir);

        Vector3 destino = puntoSalida.position;
        destino.y = alturaCuerpo;
        yield return MoverCuerpo(destino, Quaternion.Euler(0f, puntoSalida.eulerAngles.y, 0f));

        foreach (Collider c in collidersJugador) c.enabled = true;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        SetControles(true);

        yield return MoverPuerta(false, sonidoCerrar);

        if (interactable != null) interactable.SetPrompt(textoEsconderse);
        Actual = null;
        alSalir?.Invoke();
        Ocupado = false;
    }

    bool BuscarJugador()
    {
        PlayerMovement movimiento = FindFirstObjectByType<PlayerMovement>();
        if (movimiento == null)
        {
            Debug.LogWarning(name + ": no encuentro al jugador (PlayerMovement).", this);
            return false;
        }
        cuerpo = movimiento.transform;
        rb = movimiento.GetComponent<Rigidbody>();
        if (rb == null) rb = cuerpo.gameObject.AddComponent<Rigidbody>();

        var lista = new System.Collections.Generic.List<Collider>();
        foreach (Collider c in cuerpo.GetComponentsInChildren<Collider>())
            if (!c.isTrigger && c.enabled) lista.Add(c);
        collidersJugador = lista.ToArray();

        var controles = new System.Collections.Generic.List<Behaviour>();
        controles.Add(movimiento);
        foreach (MouseLook m in cuerpo.GetComponentsInChildren<MouseLook>()) controles.Add(m);
        foreach (HeadBob h in cuerpo.GetComponentsInChildren<HeadBob>()) controles.Add(h);
        foreach (PlayerInteractor p in cuerpo.GetComponentsInChildren<PlayerInteractor>()) controles.Add(p);
        controlesJugador = controles.ToArray();
        return true;
    }

    void SetControles(bool activos)
    {
        foreach (Behaviour b in controlesJugador) if (b != null) b.enabled = activos;
    }

    void SetInteraccion(bool activa)
    {
        foreach (Behaviour b in controlesJugador) if (b is PlayerInteractor) b.enabled = activa;
    }

    IEnumerator MoverPuerta(bool abrir, AudioClip clip)
    {
        if (puerta == null) yield break;
        if (clip != null) AudioSource.PlayClipAtPoint(clip, puerta.position);
        Quaternion desde = puerta.localRotation;
        Quaternion hasta = abrir ? puertaCerrada * Quaternion.Euler(0f, anguloAbierta, 0f) : puertaCerrada;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duracionPuerta))
        {
            puerta.localRotation = Quaternion.Slerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        puerta.localRotation = hasta;
    }

    IEnumerator MoverCuerpo(Vector3 destino, Quaternion giro)
    {
        Vector3 desde = cuerpo.position;
        Quaternion rotDesde = cuerpo.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duracionMovimiento))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            cuerpo.SetPositionAndRotation(Vector3.Lerp(desde, destino, k), Quaternion.Slerp(rotDesde, giro, k));
            yield return null;
        }
        cuerpo.SetPositionAndRotation(destino, giro);
        rb.position = destino;
        rb.rotation = giro;
        Physics.SyncTransforms();
    }

    IEnumerator FadeAgitacion(bool sonar)
    {
        if (sonidoAgitacion == null) yield break;
        int id = ++fadeId;
        if (sonar)
        {
            fuente.clip = sonidoAgitacion;
            if (!fuente.isPlaying) fuente.Play();
        }
        float objetivo = sonar ? volumenAgitacion : 0f;
        while (!Mathf.Approximately(fuente.volume, objetivo))
        {
            // Si empezo otro fade (entro o salio a mitad), este se detiene
            if (id != fadeId) yield break;
            fuente.volume = Mathf.MoveTowards(fuente.volume, objetivo, Time.deltaTime * volumenAgitacion / Mathf.Max(0.01f, fadeAgitacion));
            yield return null;
        }
        if (!sonar) fuente.Stop();
    }
}
