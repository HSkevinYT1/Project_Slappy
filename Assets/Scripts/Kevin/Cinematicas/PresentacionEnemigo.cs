using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;

// Presentacion de un enemigo: al entrar en la zona, el jugador se queda quieto y la camara se acerca y hace
// zoom hacia el enemigo, que esta a lo lejos (al final de un pasillo) SIN ver al jugador: de espaldas,
// mirando hacia donde mira "Punto Aparicion" (su IA esta apagada durante la escena).
// Con "Punto Destino" asignado, en vez de quedarse quieto camina hacia alli.
// Despues vuelve todo a la normal y la IA sigue sola.
// Pasa una sola vez.
public class PresentacionEnemigo : MonoBehaviour
{
    [Tooltip("Al entrar el jugador aqui empieza la cinematica.")]
    public BoxCollider zona;
    public JefeIndigoAI enemigo;

    [Header("Puntos")]
    [Tooltip("Donde aparece el enemigo (visible desde la entrada).")]
    public Transform puntoAparicion;
    [Tooltip("Opcional: hacia donde camina mientras se le mira. Vacio = se queda quieto.")]
    public Transform puntoDestino;
    [Tooltip("Desde donde mira la camara.")]
    public Transform puntoVista;
    [Tooltip("Opcional: la camara mira fijo a este punto (ej: un cruce por el que pasa el enemigo) en vez de seguirlo.")]
    public Transform mirarFijo;

    [Header("Tiempos")]
    public float duracionIr = 1.4f;
    public float duracionMirar = 4.5f;
    public float duracionVolver = 1.2f;

    [Header("Camara")]
    [Tooltip("Campo de vision al hacer zoom sobre el enemigo.")]
    public float fovZoom = 26f;
    public bool franjasCine = false;

    [Header("Enemigo")]
    public float velocidadEnemigo = 1.4f;
    [Tooltip("Luz tenue que sigue al enemigo durante la escena para que se distinga en la oscuridad.")]
    public Light luzEnemigo;
    public AudioClip sonido;

    public UnityEvent alTerminar;

    public bool Usada { get; private set; }

    private Transform jugador;

    void Awake()
    {
        if (zona != null) zona.isTrigger = true;
        if (luzEnemigo != null) luzEnemigo.enabled = false;
    }

    void Update()
    {
        if (Usada || zona == null) return;
        if (jugador == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm == null) return;
            jugador = pm.transform;
        }
        if (zona.bounds.Contains(jugador.position)) StartCoroutine(Cinematica());
    }

    public void Empezar()
    {
        if (!Usada) StartCoroutine(Cinematica());
    }

    IEnumerator Cinematica()
    {
        Usada = true;
        Camera camJugador = Camera.main;
        if (camJugador == null || enemigo == null || puntoAparicion == null) yield break;

        // Jugador quieto
        var apagados = new List<Behaviour>();
        void Apagar<T>() where T : Behaviour
        {
            foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (b.enabled) { b.enabled = false; apagados.Add(b); }
        }
        Apagar<PlayerMovement>();
        Apagar<MouseLook>();
        Apagar<HeadBob>();
        Apagar<PlayerInteractor>();
        Apagar<PauseMenu>();
        if (jugador != null && jugador.TryGetComponent(out Rigidbody rb)) rb.linearVelocity = Vector3.zero;

        // Enemigo: sin IA, aparece a lo lejos y camina hacia el destino
        NavMeshAgent agent = enemigo.GetComponent<NavMeshAgent>();
        enemigo.enabled = false;
        float velocidadAntes = agent != null ? agent.speed : 0f;
        bool camina = puntoDestino != null;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(puntoAparicion.position);
            agent.speed = velocidadEnemigo;
            if (camina) { agent.isStopped = false; agent.SetDestination(puntoDestino.position); }
            else { agent.ResetPath(); agent.isStopped = true; agent.velocity = Vector3.zero; }
        }
        else enemigo.transform.position = puntoAparicion.position;
        if (!camina) enemigo.transform.rotation = Quaternion.Euler(0f, puntoAparicion.eulerAngles.y, 0f);
        if (luzEnemigo != null) luzEnemigo.enabled = true;
        if (sonido != null) AudioSource.PlayClipAtPoint(sonido, camJugador.transform.position);

        // Camara de la cinematica (copia de la del jugador)
        var camGo = new GameObject("Camara Presentacion");
        Camera cam = camGo.AddComponent<Camera>();
        cam.CopyFrom(camJugador);
        var datos = camJugador.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var datosNuevos = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (datos != null) { datosNuevos.renderPostProcessing = datos.renderPostProcessing; datosNuevos.antialiasing = datos.antialiasing; }
        camGo.transform.SetPositionAndRotation(camJugador.transform.position, camJugador.transform.rotation);
        camJugador.enabled = false;

        RectTransform barraArriba = null, barraAbajo = null;
        GameObject canvas = franjasCine ? CrearFranjas(out barraArriba, out barraAbajo) : null;

        Vector3 inicioPos = cam.transform.position;
        Quaternion inicioRot = cam.transform.rotation;
        float fovNormal = cam.fieldOfView;
        Vector3 vista = puntoVista != null ? puntoVista.position : inicioPos;
        Vector3 Objetivo() => mirarFijo != null ? mirarFijo.position : enemigo.transform.position + Vector3.up * 0.3f;

        // Acercarse y hacer zoom
        yield return Animar(duracionIr, k =>
        {
            cam.transform.position = Vector3.Lerp(inicioPos, vista, k);
            cam.transform.rotation = Quaternion.Slerp(inicioRot, Quaternion.LookRotation(Objetivo() - cam.transform.position), k);
            cam.fieldOfView = Mathf.Lerp(fovNormal, fovZoom, k);
            Franjas(barraArriba, barraAbajo, k);
        });

        // Mirarlo mientras camina (la camara lo sigue con suavidad)
        for (float t = 0f; t < duracionMirar; t += Time.deltaTime)
        {
            Quaternion mirar = Quaternion.LookRotation(Objetivo() - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, mirar, 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (luzEnemigo != null) luzEnemigo.transform.position = enemigo.transform.position + Vector3.up * 2.2f;
            yield return null;
        }

        // Volver a la vista del jugador
        Vector3 desde = cam.transform.position;
        Quaternion rotDesde = cam.transform.rotation;
        yield return Animar(duracionVolver, k =>
        {
            cam.transform.position = Vector3.Lerp(desde, camJugador.transform.position, k);
            cam.transform.rotation = Quaternion.Slerp(rotDesde, camJugador.transform.rotation, k);
            cam.fieldOfView = Mathf.Lerp(fovZoom, fovNormal, k);
            Franjas(barraArriba, barraAbajo, 1f - k);
        });

        camJugador.enabled = true;
        Destroy(camGo);
        if (canvas != null) Destroy(canvas);
        if (luzEnemigo != null) luzEnemigo.enabled = false;

        // El enemigo sigue con su IA desde donde quedo
        if (agent != null) { agent.speed = velocidadAntes; if (agent.isOnNavMesh) agent.isStopped = false; }
        enemigo.enabled = true;
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        alTerminar?.Invoke();
    }

    GameObject CrearFranjas(out RectTransform arriba, out RectTransform abajo)
    {
        var go = new GameObject("Franjas Cine", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        arriba = Barra(go.transform, "Arriba", new Vector2(0f, 1f));
        abajo = Barra(go.transform, "Abajo", new Vector2(0f, 0f));
        return go;
    }

    static RectTransform Barra(Transform padre, string nombre, Vector2 anclaY)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = new Vector2(0f, anclaY.y);
        r.anchorMax = new Vector2(1f, anclaY.y);
        r.pivot = new Vector2(0.5f, anclaY.y);
        r.sizeDelta = new Vector2(0f, 0f);
        go.AddComponent<Image>().color = Color.black;
        return r;
    }

    static void Franjas(RectTransform a, RectTransform b, float k)
    {
        if (a == null) return;
        float alto = Screen.height * 0.12f * Mathf.Clamp01(k);
        a.sizeDelta = new Vector2(0f, alto);
        b.sizeDelta = new Vector2(0f, alto);
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
}
