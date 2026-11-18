using UnityEngine;
using UnityEngine.Events;

// Salida de una zona que necesita una llave (ej: las Cloacas, con la llave en la mano de una estatua al azar, ver LlaveAleatoria).
// - Al entrar el jugador a la zona (ya unos metros adentro) la reja de la entrada se cierra detras de el.
// - En la cerradura (un ReceptorObjeto que espera la llave) se entrega la llave: la reja se abre y la zona
//   queda completada en ProgresoCaminos (su puerta queda abierta hasta que el jugador sale, y se abre la siguiente).
// Lo arma Tools > Cloacas > Salida con llave (CloacasSalidaSetup).
public class SalidaConLlave : MonoBehaviour
{
    [Tooltip("Nombre del tramo en ProgresoCaminos que se completa al abrir (ej: Cloacas)")]
    public string zona = "Cloacas";
    [Tooltip("Reja/puerta de la entrada. Empieza abierta y se cierra cuando el jugador ya esta adentro")]
    public PuertaDeslizante reja;
    [Tooltip("Cerradura donde se entrega la llave")]
    public ReceptorObjeto cerradura;
    [Tooltip("Area de la zona. El jugador tiene que estar dentro para que se cierre la reja")]
    public ZonaVolumen area;
    [Tooltip("Metros hacia adentro (contra el eje azul de la reja) que tiene que avanzar el jugador para que se cierre")]
    public float metrosParaCerrar = 6f;
    public AudioClip sonidoCerrar;
    public AudioClip sonidoAbrir;
    [Range(0f, 1f)] public float volumen = 1f;

    [TextArea] public string avisoCerrar = "La reja se cerro detras de ti... una de esas cosas tiene la llave.";
    [TextArea] public string avisoAbrir = "La reja se abre. Ya puedes salir.";

    public UnityEvent alAbrir;

    public bool Cerrada { get; private set; }
    public bool Abierta { get; private set; }

    Transform jugador;

    void Awake()
    {
        if (cerradura != null) cerradura.alRecibir.AddListener(_ => Abrir());
    }

    void Update()
    {
        if (Cerrada || Abierta || reja == null) return;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }
        if (area != null && !area.Contiene(jugador.position)) return;

        // El eje azul de la reja apunta hacia afuera de la zona
        float adentro = -Vector3.Dot(jugador.position - reja.transform.position, reja.transform.forward);
        if (adentro < metrosParaCerrar) return;

        Cerrada = true;
        reja.Cerrar();
        Sonar(sonidoCerrar);
        if (!string.IsNullOrEmpty(avisoCerrar)) HUD.Aviso(avisoCerrar, 5f);
    }

    // La llama la cerradura al recibir la llave (o un UnityEvent)
    public void Abrir()
    {
        if (Abierta) return;
        Abierta = true;
        if (reja != null) reja.Abrir();
        Sonar(sonidoAbrir);
        if (!string.IsNullOrEmpty(avisoAbrir)) HUD.Aviso(avisoAbrir, 4f);

        ProgresoCaminos p = ProgresoCaminos.Instance;
        if (p != null && p.TramoActual != null && p.TramoActual.nombre == zona) p.CompletarZonaActual();
        alAbrir?.Invoke();
    }

    void Sonar(AudioClip clip)
    {
        if (clip == null || reja == null) return;
        var go = new GameObject("Sonido " + name);
        go.transform.position = reja.transform.position + Vector3.up * 1.5f;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = volumen;
        a.spatialBlend = 0.6f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 10f;
        a.maxDistance = 80f;
        a.dopplerLevel = 0f;
        a.Play();
        Destroy(go, clip.length + 0.2f);
    }
}
