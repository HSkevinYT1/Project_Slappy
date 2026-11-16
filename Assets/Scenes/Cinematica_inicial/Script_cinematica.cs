using UnityEngine;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla el paso de la cinemática inicial al gameplay.
///
/// Durante la cinemática: bloquea los controles del jugador.
/// Al terminar (o al saltarla): desactiva las cámaras/objetos de la cinemática,
/// activa los de gameplay y devuelve el control al jugador.
///
/// Importante para el rendimiento: las cámaras de la cinemática DEBEN apagarse al
/// terminar. Si quedan encendidas, la escena se renderiza una vez por cada cámara
/// y además tapan la cámara del jugador.
/// </summary>
public class Script_cinematica : MonoBehaviour
{
    [Header("Cinemática")]
    [Tooltip("Timeline de la cinemática. Si está vacío, se busca en este mismo objeto.")]
    public PlayableDirector director;

    [Tooltip("Objetos que solo sirven para la cinemática (cámaras, actores animados...). Se desactivan al terminar.\n" +
             "Si se deja vacío, se apagan automáticamente todas las cámaras hijas de este objeto que no tengan el tag MainCamera.")]
    public GameObject[] objetosCinematica;

    [Tooltip("Objetos que se activan al empezar el gameplay (UI, enemigos, etc.). Opcional.")]
    public GameObject[] objetosGameplay;

    [Header("Jugador")]
    [Tooltip("Componentes que se bloquean durante la cinemática.\n" +
             "Si se deja vacío, se buscan PlayerMovement, MouseLook y HeadBob en los hijos de este objeto.")]
    public Behaviour[] controlesJugador;

    [Header("Pruebas")]
    [Tooltip("Permite saltar la cinemática con Espacio o Escape.")]
    public bool permitirSaltar = true;

    float _tiempoFin;
    bool _terminada;

    void Awake()
    {
        if (director == null) director = GetComponent<PlayableDirector>();

        if (controlesJugador == null || controlesJugador.Length == 0)
        {
            controlesJugador = new Behaviour[]
            {
                GetComponentInChildren<PlayerMovement>(true),
                GetComponentInChildren<MouseLook>(true),
                GetComponentInChildren<HeadBob>(true),
            };
        }
    }

    void Start()
    {
        BloquearJugador(true);

        if (director == null || director.playableAsset == null)
        {
            Debug.LogWarning("[Cinemática] No hay Timeline asignado: se pasa directo al gameplay.", this);
            Terminar();
            return;
        }

        if (director.state != PlayState.Playing) director.Play();
        director.stopped += AlDetenerse;

        // Respaldo por tiempo, por si el Timeline está en modo Hold y nunca dispara "stopped".
        _tiempoFin = Time.time + (float)(director.duration - director.time);
    }

    void Update()
    {
        if (_terminada) return;

        if (permitirSaltar && SaltoPresionado())
        {
            Terminar();
            return;
        }

        if (Time.time >= _tiempoFin) Terminar();
    }

    void OnDestroy()
    {
        if (director != null) director.stopped -= AlDetenerse;
    }

    void AlDetenerse(PlayableDirector _) => Terminar();

    /// <summary>Termina la cinemática y pasa al gameplay. Se puede llamar desde otros scripts o desde una Signal del Timeline.</summary>
    public void Terminar()
    {
        if (_terminada) return;
        _terminada = true;

        if (director != null)
        {
            director.stopped -= AlDetenerse;
            if (director.state == PlayState.Playing) director.Stop();
        }

        if (objetosCinematica != null && objetosCinematica.Length > 0)
        {
            foreach (var obj in objetosCinematica)
                if (obj != null) obj.SetActive(false);
        }
        else
        {
            // Sin asignación manual: apagar todas las cámaras de la cinemática.
            foreach (var cam in GetComponentsInChildren<Camera>(true))
                if (!cam.CompareTag("MainCamera")) cam.gameObject.SetActive(false);
        }

        if (objetosGameplay != null)
            foreach (var obj in objetosGameplay)
                if (obj != null) obj.SetActive(true);

        BloquearJugador(false);
        enabled = false;
    }

    void BloquearJugador(bool bloquear)
    {
        if (controlesJugador == null) return;
        foreach (var c in controlesJugador)
            if (c != null) c.enabled = !bloquear;
    }

    static bool SaltoPresionado()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.spaceKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
