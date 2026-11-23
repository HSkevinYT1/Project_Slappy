using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Menu principal: la camara del menu mira la tienda de frente. Al darle a Jugar, la camara recorre
// los puntos del camino (entra por la puerta) hasta meterse en la cabeza del jugador y empieza el juego.
// Mientras el menu esta abierto, el jugador, el tutorial, la pausa y los demas Canvas estan apagados.
[DefaultExecutionOrder(-1000)]
public class MenuPrincipal : MonoBehaviour
{
    public Camera camaraMenu;
    // Panel con el titulo y los botones
    public CanvasGroup panel;
    // Puntos por los que pasa la camara (en orden) antes de llegar a la cabeza del jugador
    public Transform[] camino;
    // Segundos que dura la cinematica
    public float duracion = 5f;
    public float fadePanel = 0.6f;

    public static bool Abierto { get; private set; }

    private Camera camaraJugador;
    private AudioListener oidoJugador;
    private Transform cuerpoJugador;
    private readonly List<Behaviour> apagados = new List<Behaviour>();
    private bool empezando;

    void Awake()
    {
        Abierto = true;
        PlayerMovement jugador = FindFirstObjectByType<PlayerMovement>();
        MouseLook mirada = FindFirstObjectByType<MouseLook>();
        if (mirada != null)
        {
            camaraJugador = mirada.GetComponent<Camera>();
            cuerpoJugador = mirada.playerBody;
        }
        if (camaraJugador == null && jugador != null) camaraJugador = jugador.GetComponentInChildren<Camera>();

        // Todo lo que reacciona al teclado o empieza solo se apaga hasta que termine la cinematica
        Apagar<PlayerMovement>();
        Apagar<MouseLook>();
        Apagar<HeadBob>();
        Apagar<PlayerInteractor>();
        Apagar<GraspObject>();
        Apagar<Flashlight>();
        Apagar<PauseMenu>();
        Apagar<TutorialTienda>();
        // Los NPCs del tutorial los esconde el propio tutorial al empezar; con el menu todavia no ha empezado
        foreach (TutorialTienda tut in FindObjectsByType<TutorialTienda>(FindObjectsSortMode.None))
        {
            if (tut.cliente != null) tut.cliente.gameObject.SetActive(false);
            if (tut.cultista != null) tut.cultista.gameObject.SetActive(false);
        }
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.enabled || c.transform.IsChildOf(transform) || !c.isRootCanvas) continue;
            c.enabled = false;
            apagados.Add(c);
        }

        if (camaraJugador != null)
        {
            camaraJugador.enabled = false;
            oidoJugador = camaraJugador.GetComponent<AudioListener>();
            if (oidoJugador != null) oidoJugador.enabled = false;
        }
        if (camaraMenu != null) camaraMenu.enabled = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy() => Abierto = false;

    // Boton Jugar
    public void Jugar()
    {
        if (empezando) return;
        empezando = true;
        IntroTienda intro = FindFirstObjectByType<IntroTienda>();
        if (intro != null && camaraMenu != null) StartCoroutine(ConIntro(intro));
        else StartCoroutine(Cinematica());
    }

    // Cinematica nueva (carro, cliente, tendero y la camara por la ventana derecha): ver IntroTienda
    IEnumerator ConIntro(IntroTienda intro)
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (panel != null) panel.interactable = false;
        yield return intro.Reproducir(camaraMenu, panel, camaraJugador, cuerpoJugador);
        Empezar();
    }

    // Boton Salir
    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator Cinematica()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (panel != null) panel.interactable = false;

        // Puntos: camara del menu -> camino -> cabeza del jugador
        Transform cam = camaraMenu.transform;
        var puntos = new List<Vector3> { cam.position };
        foreach (Transform p in camino) if (p != null) puntos.Add(p.position);

        Vector3 finPos = camaraJugador != null ? camaraJugador.transform.position : cam.position;
        // MouseLook deja la camara mirando al frente del cuerpo al activarse
        Quaternion finRot = cuerpoJugador != null ? Quaternion.Euler(0f, cuerpoJugador.eulerAngles.y, 0f)
                          : camaraJugador != null ? camaraJugador.transform.rotation : cam.rotation;
        puntos.Add(finPos);

        float fovInicio = camaraMenu.fieldOfView;
        float fovFin = camaraJugador != null ? camaraJugador.fieldOfView : fovInicio;
        Quaternion rotInicio = cam.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.1f, duracion);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            if (panel != null) panel.alpha = 1f - Mathf.Clamp01(t * duracion / Mathf.Max(0.01f, fadePanel));

            Vector3 pos = Recorrido(puntos, k);
            cam.position = pos;

            // Mira hacia donde va y al final se alinea con la mirada del jugador
            Vector3 adelante = Recorrido(puntos, Mathf.Min(1f, k + 0.04f)) - pos;
            Quaternion mirarCamino = adelante.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(adelante) : cam.rotation;
            Quaternion rot = Quaternion.Slerp(rotInicio, mirarCamino, Mathf.SmoothStep(0f, 1f, k * 4f));
            cam.rotation = Quaternion.Slerp(rot, finRot, Mathf.SmoothStep(0f, 1f, (k - 0.7f) / 0.3f));
            camaraMenu.fieldOfView = Mathf.Lerp(fovInicio, fovFin, k);
            yield return null;
        }

        Empezar();
    }

    // Empieza el juego (tambien se puede llamar directamente para saltar la cinematica)
    public void Empezar()
    {
        StopAllCoroutines();
        if (camaraJugador != null)
        {
            camaraJugador.enabled = true;
            if (oidoJugador != null) oidoJugador.enabled = true;
        }
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        apagados.Clear();
        // Los brazos del jugador siempre se ven al empezar (en la escena estaban guardados apagados: al saltar la
        // intro nadie los volvia a prender y no salia la mano)
        ActivarBrazos();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Abierto = false;
        gameObject.SetActive(false);
    }

    void ActivarBrazos()
    {
        foreach (Transform raiz in new[] { camaraJugador != null ? camaraJugador.transform : null, cuerpoJugador })
        {
            if (raiz == null) continue;
            foreach (Transform x in raiz.GetComponentsInChildren<Transform>(true))
                if (x.name == "Player_Arms") { x.gameObject.SetActive(true); return; }
        }
    }

    void Apagar<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (!b.enabled) continue;
            b.enabled = false;
            apagados.Add(b);
        }
    }

    // Catmull-Rom por todos los puntos, t de 0 a 1
    static Vector3 Recorrido(List<Vector3> p, float t)
    {
        if (p.Count == 1) return p[0];
        float f = t * (p.Count - 1);
        int i = Mathf.Min(Mathf.FloorToInt(f), p.Count - 2);
        float u = f - i;
        Vector3 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, p.Count - 1)];
        return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
    }
}
