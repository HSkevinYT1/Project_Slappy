using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Puzzle del laberinto (estilo "contrasena" de Keep Talking and Nobody Explodes):
// - 4 paneles de pared, uno por sala. Cada panel tiene 4 pantallas y solo funciona la de su posicion
//   (panel 1 -> pantalla 1, panel 2 -> pantalla 2...): ahi muestra un digito, las demas estan apagadas y rotas.
// - En el centro hay un panel con 4 columnas de digitos: con las flechas se sube/baja cada uno y con ENVIAR se prueba.
// - Con el codigo correcto: luz verde, se completa la zona y una cinematica muestra como se abre la reja de la entrada.
// El codigo cambia en cada partida.
public class PuzzleContrasena : MonoBehaviour
{
    [Header("Paneles de pista (en orden: el 1 muestra el primer digito)")]
    public TextMeshPro[] digitosPista;

    [Header("Brillo de los paneles de pista")]
    [Tooltip("Mientras el jugador este en esta zona (la habitacion del panel central) los paneles de pista brillan")]
    public BoxCollider salaCentral;
    public Highlightable[] brilloPaneles;

    [Header("Panel central")]
    public TextMeshPro[] columnas;
    public Renderer luzEstado;
    public Transform[] botones;
    [Tooltip("Sonido al pulsar cualquier boton (vacio = un clic generado)")]
    public AudioClip sonidoBoton;

    [Header("Cinematica de la puerta")]
    public Transform puntoCamara;
    public Transform mirarA;
    public float duracionCinematica = 5f;
    public bool completarZona = true;
    public UnityEvent alResolver;

    public bool Resuelto { get; private set; }
    public int[] Codigo { get; private set; }

    int[] valores;
    AudioSource fuente;
    AudioClip clic, error, correcto;
    bool ocupado;
    MaterialPropertyBlock bloque;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color Apagado = new Color(0.05f, 0.06f, 0.05f);
    static readonly Color Rojo = new Color(1f, 0.08f, 0.05f);
    static readonly Color Verde = new Color(0.45f, 0.2f, 1f); // (acierto: indigo)

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        Codigo = new int[columnas.Length];
        valores = new int[columnas.Length];
        for (int i = 0; i < Codigo.Length; i++) Codigo[i] = Random.Range(0, 10);
        for (int i = 0; i < digitosPista.Length && i < Codigo.Length; i++)
            if (digitosPista[i] != null) digitosPista[i].text = Codigo[i].ToString();
        // Empieza en un codigo que no es el correcto
        for (int i = 0; i < valores.Length; i++) valores[i] = (Codigo[i] + 5) % 10;
        Mostrar();
        Luz(Apagado);

        fuente = gameObject.AddComponent<AudioSource>();
        fuente.spatialBlend = 1f;
        fuente.minDistance = 2f;
        fuente.maxDistance = 25f;
        clic = HUD.Clic("ClicPanel", 2600f, 0.03f, 0.3f);
        error = Tono("ErrorPanel", new[] { 196f, 147f }, 0.25f, true);
        correcto = Tono("CorrectoPanel", new[] { 523f, 659f, 784f, 1047f }, 0.13f, false);
    }

    Transform jugador;

    void Update()
    {
        if (salaCentral == null || brilloPaneles == null) return;
        if (jugador == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm == null) return;
            jugador = pm.transform;
        }
        bool dentro = !Resuelto && salaCentral.bounds.Contains(jugador.position);
        foreach (Highlightable h in brilloPaneles)
            if (h != null && h.IsHighlighted != dentro) h.SetHighlighted(dentro);
    }

    // Llamado por los botones
    public void Subir(int col) { if (Puede(col)) { valores[col] = (valores[col] + 1) % 10; Clic(); } }
    public void Bajar(int col) { if (Puede(col)) { valores[col] = (valores[col] + 9) % 10; Clic(); } }

    public void Enviar()
    {
        if (Resuelto || ocupado) return;
        fuente.PlayOneShot(sonidoBoton != null ? sonidoBoton : clic, 0.9f);
        bool bien = true;
        for (int i = 0; i < valores.Length; i++) if (valores[i] != Codigo[i]) bien = false;
        if (bien) Resolver();
        else StartCoroutine(Error());
    }

    // Anima el boton hundiendose un poco
    public void Hundir(Transform boton)
    {
        if (boton != null) StartCoroutine(AnimarBoton(boton));
    }

    bool Puede(int col) => !Resuelto && !ocupado && col >= 0 && col < valores.Length;

    void Clic()
    {
        Mostrar();
        fuente.PlayOneShot(sonidoBoton != null ? sonidoBoton : clic, 0.9f);
    }

    void Mostrar()
    {
        for (int i = 0; i < columnas.Length; i++) if (columnas[i] != null) columnas[i].text = valores[i].ToString();
    }

    void Luz(Color c)
    {
        if (luzEstado == null) return;
        luzEstado.GetPropertyBlock(bloque);
        bloque.SetColor(BaseColorId, c);
        bloque.SetColor(EmissionId, c == Apagado ? Color.black : c * 3f);
        luzEstado.SetPropertyBlock(bloque);
    }

    IEnumerator AnimarBoton(Transform b)
    {
        Vector3 inicio = b.localPosition, adentro = inicio + Vector3.forward * 0.015f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.06f) { b.localPosition = Vector3.Lerp(inicio, adentro, t); yield return null; }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.1f) { b.localPosition = Vector3.Lerp(adentro, inicio, t); yield return null; }
        b.localPosition = inicio;
    }

    IEnumerator Error()
    {
        ocupado = true;
        fuente.PlayOneShot(error, 1f);
        for (int k = 0; k < 6; k++) { Luz(k % 2 == 0 ? Rojo : Apagado); yield return new WaitForSeconds(0.15f); }
        Luz(Apagado);
        ocupado = false;
    }

    void Resolver()
    {
        Resuelto = true;
        Luz(Verde);
        fuente.PlayOneShot(correcto, 1f);
        foreach (Transform b in botones)
            if (b != null && b.TryGetComponent(out Interactable i)) i.enabled = false;
        if (completarZona && ProgresoCaminos.Instance != null)
        {
            ProgresoCaminos.Tramo t = ProgresoCaminos.Instance.TramoActual;
            if (t != null && t.nombre == "Laberinto") ProgresoCaminos.Instance.CompletarZonaActual();
        }
        StartCoroutine(Cinematica());
    }

    // ------------------------------------------------------------------ Cinematica: la reja de la entrada se abre

    IEnumerator Cinematica()
    {
        yield return new WaitForSeconds(0.8f);
        Camera camJugador = Camera.main;
        RejasEntrada reja = FindFirstObjectByType<RejasEntrada>();
        if (camJugador == null || puntoCamara == null)
        {
            if (reja != null) reja.Abrir();
            alResolver?.Invoke();
            yield break;
        }

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
        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null && pm.TryGetComponent(out Rigidbody rb)) rb.linearVelocity = Vector3.zero;

        Image negro = Fundido(out GameObject canvas);
        yield return Fundir(negro, 0f, 1f, 0.45f);

        // Camara frente a la reja
        var camGo = new GameObject("Camara Puerta Laberinto");
        Camera cam = camGo.AddComponent<Camera>();
        cam.CopyFrom(camJugador);
        var datos = camJugador.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var datosNuevos = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var brazos = camJugador.GetComponent<BrazosPrimeraPersona>();
        if (datos != null) { datosNuevos.renderPostProcessing = brazos != null ? brazos.PostOriginal : datos.renderPostProcessing; datosNuevos.antialiasing = datos.antialiasing; }
        Vector3 objetivo = mirarA != null ? mirarA.position : puntoCamara.position + puntoCamara.forward;
        camGo.transform.SetPositionAndRotation(puntoCamara.position, Quaternion.LookRotation(objetivo - puntoCamara.position));
        cam.fieldOfView = 55f;
        camJugador.enabled = false;
        // La niebla limita la vision alrededor de la camara que sigue: durante la escena sigue a esta camara
        var nieblas = new List<(FogVolume f, Transform antes)>();
        foreach (FogVolume f in FindObjectsByType<FogVolume>(FindObjectsSortMode.None))
            if (f.followTarget != null) { nieblas.Add((f, f.followTarget)); f.followTarget = camGo.transform; }
        // Luz sobre la reja para que se vea bien
        var luzGo = new GameObject("Luz Reja");
        luzGo.transform.position = objetivo + (puntoCamara.position - objetivo).normalized * 2.5f + Vector3.up * 1.2f;
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point; luz.color = new Color(0.75f, 0.8f, 1f); luz.range = 12f; luz.intensity = 30f; luz.shadows = LightShadows.Soft;
        // Relleno suave cerca de la camara (como una linterna)
        var relleno = new GameObject("Relleno");
        relleno.transform.SetParent(luzGo.transform, true);
        relleno.transform.SetPositionAndRotation(camGo.transform.position + Vector3.down * 0.2f, camGo.transform.rotation);
        var lr = relleno.AddComponent<Light>();
        lr.type = LightType.Spot; lr.spotAngle = 60f; lr.range = 14f; lr.intensity = 20f; lr.color = new Color(1f, 0.95f, 0.85f); lr.shadows = LightShadows.None;

        yield return Fundir(negro, 1f, 0f, 0.45f);
        yield return new WaitForSeconds(0.6f);
        if (reja != null) reja.Abrir();
        // Un leve acercamiento mientras sube
        Vector3 desde = camGo.transform.position, hasta = Vector3.Lerp(desde, objetivo, 0.25f);
        for (float t = 0f; t < duracionCinematica; t += Time.deltaTime)
        {
            camGo.transform.position = Vector3.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / duracionCinematica));
            yield return null;
        }

        yield return Fundir(negro, 0f, 1f, 0.45f);
        foreach (var (f, antes) in nieblas) if (f != null) f.followTarget = antes;
        Destroy(luzGo);
        camJugador.enabled = true;
        Destroy(camGo);
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        yield return Fundir(negro, 1f, 0f, 0.45f);
        Destroy(canvas);
        HUD.Aviso("La puerta del laberinto se abrió", 3.5f);
        alResolver?.Invoke();
    }

    static Image Fundido(out GameObject go)
    {
        go = new GameObject("Fundido Puerta", typeof(RectTransform));
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 95;
        var img = new GameObject("Negro", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        var r = (RectTransform)img.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero;
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        return img;
    }

    static IEnumerator Fundir(Image img, float de, float a, float seg)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seg) { img.color = new Color(0f, 0f, 0f, Mathf.Lerp(de, a, t)); yield return null; }
        img.color = new Color(0f, 0f, 0f, a);
    }

    static AudioClip Tono(string nombre, float[] notas, float dur, bool cuadrada)
    {
        int porNota = Mathf.RoundToInt(44100 * dur);
        var d = new float[porNota * notas.Length + 8000];
        for (int k = 0; k < notas.Length; k++)
            for (int i = 0; i < porNota + 8000 && k * porNota + i < d.Length; i++)
            {
                float t = i / 44100f, s = Mathf.Sin(2f * Mathf.PI * notas[k] * t);
                if (cuadrada) s = Mathf.Sign(s) * 0.6f;
                d[k * porNota + i] += s * Mathf.Exp(-t * 6f) * 0.3f;
            }
        AudioClip c = AudioClip.Create(nombre, d.Length, 1, 44100, false);
        c.SetData(d, 0);
        return c;
    }
}
