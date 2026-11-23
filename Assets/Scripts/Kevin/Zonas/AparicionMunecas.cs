using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Las muñecas de las Cloacas:
// - Entrada: al cerrarse la reja hay una muñeca quieta en medio del camino (la de practica), la camara la mira y
//   pasan los 3 dialogos. Una pared invisible no deja seguir hasta destruirla con la linterna (MunecaLuz).
// - Despues van apareciendo cada cierto tiempo (no hay ninguna puesta), en puntos que el jugador no esta viendo y
//   lejos de el. Una de ellas (la numero "conLlave") trae la llave en la mano; si se destruye, la llave cae al piso.
// - Dejan de aparecer cuando la salida se abre.
// Lo arma Tools > Cloacas > Muñecas y luz (CloacasLuzSetup). Los tiempos se cambian aqui (en el Inspector).
public class AparicionMunecas : MonoBehaviour
{
    public static AparicionMunecas Instance { get; private set; }

    [Header("Aparicion")]
    [Tooltip("La muñeca que aparece (Enemigo 3)")]
    public GameObject prefab;
    [Tooltip("Donde pueden aparecer (se elige uno que el jugador no este viendo)")]
    public Transform[] puntos;
    [Tooltip("Segundos desde que se destruye la muñeca de practica hasta que aparece la primera")]
    public float primeraAparicion = 6f;
    [Tooltip("Segundos entre una aparicion y la siguiente")]
    public float cadaCuanto = 25f;
    [Tooltip("Cuantas puede haber a la vez")]
    public int maximoVivas = 4;
    [Tooltip("Cuantas aparecen en total (0 = sin limite)")]
    public int totalMaximo = 0;
    [Tooltip("Metros minimos al jugador para aparecer")]
    public float distanciaMinima = 10f;
    [Tooltip("Segundos de luz que aguanta cada muñeca")]
    public float segundosParaDestruir = 2.5f;
    [Tooltip("Metros maximos al jugador: aparecen cerca (aunque siempre fuera de la vista). Si no hay ningun punto asi, en cualquiera")]
    public float distanciaMaxima = 30f;

    [Header("Cada vez mas")]
    [Tooltip("Con el tiempo aparecen mas seguido y caben mas: a estos minutos (desde la muñeca de practica) llegan a los valores de abajo")]
    public float minutosHastaMaximo = 4f;
    public float cadaCuantoAlFinal = 14f;
    public int maximoVivasAlFinal = 6;

    [Header("Llave")]
    public LlaveAleatoria llave;
    [Tooltip("Que muñeca trae la llave (1 = la primera que aparece)")]
    public int conLlave = 2;

    [Header("Entrada")]
    [Tooltip("La muñeca de practica (quieta, en medio del camino)")]
    public MunecaLuz practica;
    [Tooltip("Pared invisible que no deja pasar hasta destruirla")]
    public Collider barrera;
    public SalidaConLlave salida;
    public bool mirarMuneca = true;
    [TextArea]
    public string[] dialogos =
    {
        "Dios, aquí huele horrible y no se ve nada. ¿Qué se supone que es este lugar?",
        "¿Qué demonios fue ese sonido?",
        "Qué inquietante muñeca... ¿qué hace algo así por aquí?",
    };
    [TextArea] public string alDestruirPractica = "¿La luz las deshace...? Mejor no apagar la linterna.";
    [Tooltip("Presentacion (como en el guion): cuando el jugador deja de mirarla, la muñeca se le acerca; al volver a verla dice esto")]
    [TextArea] public string alMoverse = "Estoy seguro de que no estabas ahí.";
    public float metrosAcercarse = 3f;
    float desdePractica;

    public bool IntroEmpezada { get; private set; }
    public bool PracticaHecha { get; private set; }
    public bool EsperandoPractica => IntroEmpezada && !PracticaHecha;
    public int Apariciones { get; private set; }

    readonly List<EstatuaAI> vivas = new List<EstatuaAI>();
    Transform jugador;
    ZonaVolumen area;
    float proxima = float.MaxValue;
    bool llaveDada;

    void Awake()
    {
        Instance = this;
        // La llave no esta en ningun lado hasta que aparece la muñeca que la trae
        if (llave != null) llave.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        MunecaLuz.AlDestruirse -= AlDestruirse;
    }

    void Start()
    {
        MunecaLuz.AlDestruirse += AlDestruirse;
        foreach (ZonaVolumen z in FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (z.name == "Trigger Cloacas") area = z;
        if (practica != null)
        {
            practica.segundosParaDestruir = segundosParaDestruir;
            if (practica.TryGetComponent(out EstatuaAI ai)) ai.enabled = false;
        }
        if (barrera != null) barrera.enabled = practica != null;
    }

    void Update()
    {
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }

        // Entrada: al cerrarse la reja (o al acercarse a la muñeca de practica)
        if (!IntroEmpezada && ((salida != null && salida.Cerrada) || (practica != null && (practica.transform.position - jugador.position).sqrMagnitude < 9f * 9f)))
            StartCoroutine(Intro());

        if (!PracticaHecha || (salida != null && salida.Abierta) || PantallaMuerte.Muerto) return;
        if (area != null && !area.Contiene(jugador.position)) return;
        vivas.RemoveAll(v => v == null);
        if (Time.time < proxima) return;
        float k = minutosHastaMaximo > 0f ? Mathf.Clamp01((Time.time - desdePractica) / (minutosHastaMaximo * 60f)) : 1f;
        int maximo = Mathf.RoundToInt(Mathf.Lerp(maximoVivas, Mathf.Max(maximoVivas, maximoVivasAlFinal), k));
        float intervalo = Mathf.Lerp(cadaCuanto, Mathf.Min(cadaCuanto, cadaCuantoAlFinal), k);
        if (vivas.Count >= maximo || (totalMaximo > 0 && Apariciones >= totalMaximo)) { proxima = Time.time + 2f; return; }
        proxima = Aparecer() ? Time.time + intervalo : Time.time + 2f;
    }

    IEnumerator Intro()
    {
        IntroEmpezada = true;
        if (practica == null) { Practica(); yield break; }
        if (mirarMuneca) yield return Mirar(practica.transform.position);
        if (dialogos != null && dialogos.Length > 0) Pensamiento.Decir("cloacas.intro", 0.2f, dialogos);
        yield return new WaitForSeconds(2f);
        yield return Acercarse();
    }

    // Como en el guion: el protagonista se da vuelta y, al volver a mirar, la muñeca esta mas cerca
    IEnumerator Acercarse()
    {
        Camera cam = Camera.main;
        float sinVerla = 0f;
        while (practica != null && !PracticaHecha && cam != null)
        {
            sinVerla = VeMuneca(cam) ? 0f : sinVerla + Time.deltaTime;
            if (sinVerla > 0.8f) break;
            yield return null;
        }
        if (practica == null || PracticaHecha || jugador == null) yield break;
        Vector3 hacia = jugador.position - practica.transform.position; hacia.y = 0f;
        float d = hacia.magnitude;
        if (d > 3f)
        {
            Vector3 nueva = practica.transform.position + hacia / d * Mathf.Min(metrosAcercarse, d - 2.5f);
            if (NavMesh.SamplePosition(nueva, out NavMeshHit h, 1.5f, NavMesh.AllAreas))
                nueva.y = h.position.y + (practica.TryGetComponent(out NavMeshAgent ag) ? ag.baseOffset : 0f);
            practica.transform.SetPositionAndRotation(nueva, Quaternion.LookRotation(hacia / d, Vector3.up));
        }
        // Al volver a verla
        while (practica != null && !PracticaHecha && !VeMuneca(cam)) yield return null;
        if (practica == null || PracticaHecha) yield break;
        Jugo.Sacudir(0.25f);
        Jugo.PatadaFOV(-3f);
        if (!string.IsNullOrEmpty(alMoverse)) Pensamiento.Decir("cloacas.moverse", 0.15f, alMoverse);
    }

    void AlDestruirse(MunecaLuz m)
    {
        if (m != null && m == practica) Practica();
    }

    void Practica()
    {
        if (PracticaHecha) return;
        PracticaHecha = true;
        if (barrera != null) barrera.enabled = false;
        if (practica != null && !string.IsNullOrEmpty(alDestruirPractica)) Pensamiento.Decir("cloacas.practica", 0.7f, alDestruirPractica);
        proxima = Time.time + primeraAparicion;
        desdePractica = Time.time;
    }

    // Una muñeca nueva en un punto que el jugador no ve
    bool Aparecer()
    {
        if (prefab == null || puntos == null || puntos.Length == 0) return false;
        Camera cam = Camera.main;
        // Primero los puntos cerca (entre la distancia minima y la maxima), en orden al azar; despues el resto
        var orden = new List<Transform>();
        foreach (Transform t in puntos) if (t != null) orden.Insert(Random.Range(0, orden.Count + 1), t);
        orden.Sort((a, b) =>
        {
            bool ca = (a.position - jugador.position).sqrMagnitude <= distanciaMaxima * distanciaMaxima;
            bool cb = (b.position - jugador.position).sqrMagnitude <= distanciaMaxima * distanciaMaxima;
            return cb.CompareTo(ca);
        });
        for (int i = 0; i < orden.Count; i++)
        {
            Transform p = orden[i];
            if (p == null || (p.position - jugador.position).sqrMagnitude < distanciaMinima * distanciaMinima) continue;
            if (cam != null && LoVe(cam, p.position + Vector3.up * 0.8f)) continue;
            if (!NavMesh.SamplePosition(p.position, out NavMeshHit h, 2.5f, NavMesh.AllAreas)) continue;

            float alto = prefab.TryGetComponent(out NavMeshAgent ap) ? ap.baseOffset : 0f;
            Vector3 mirar = jugador.position - h.position; mirar.y = 0f;
            var e = Instantiate(prefab, h.position + Vector3.up * alto,
                                mirar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mirar) : p.rotation, transform);
            Apariciones++;
            e.name = prefab.name + " " + Apariciones;
            var luz = e.GetComponent<MunecaLuz>();
            if (luz == null) luz = e.AddComponent<MunecaLuz>();
            luz.segundosParaDestruir = segundosParaDestruir;
            if (e.TryGetComponent(out EstatuaAI ai)) vivas.Add(ai);
            if (PantallaMuerte.Instance != null) PantallaMuerte.Instance.Suscribir();
            if (!llaveDada && llave != null && Apariciones >= conLlave) DarLlave(e.transform);
            return true;
        }
        return false;
    }

    void DarLlave(Transform muneca)
    {
        Transform mano = null;
        foreach (Transform t in muneca.GetComponentsInChildren<Transform>(true)) if (t.name == "Mano") { mano = t; break; }
        llave.manos = new[] { mano != null ? mano : muneca };
        llave.recinto = null;
        llave.gameObject.SetActive(true);
        llave.ElegirMano();
        llaveDada = true;
    }

    // La muñeca de practica esta a la vista (sin contar sus propios colliders)
    bool VeMuneca(Camera cam)
    {
        Vector3 p = practica.transform.position + Vector3.up * 0.8f;
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
        Vector3 d = p - cam.transform.position;
        foreach (RaycastHit h in Physics.RaycastAll(cam.transform.position, d.normalized, d.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(practica.transform)) continue;
            if (jugador != null && h.collider.transform.IsChildOf(jugador.root)) continue;
            return false;
        }
        return true;
    }

    static bool LoVe(Camera cam, Vector3 p)
    {
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0f || v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) return false;
        return !Physics.Linecast(cam.transform.position, p, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    // Gira al jugador hacia la muñeca (sin mover la camara arriba/abajo) y lo suelta
    IEnumerator Mirar(Vector3 punto)
    {
        var apagados = new List<Behaviour>();
        void Apagar(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; apagados.Add(b); } }
        Apagar(jugador.GetComponent<PlayerMovement>());
        foreach (MouseLook m in jugador.GetComponentsInChildren<MouseLook>()) Apagar(m);
        foreach (HeadBob hb in jugador.GetComponentsInChildren<HeadBob>()) Apagar(hb);
        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        Vector3 dir = punto - jugador.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion desde = jugador.rotation, hasta = Quaternion.LookRotation(dir.normalized, Vector3.up);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                Quaternion r = Quaternion.Slerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t));
                if (rb != null) rb.MoveRotation(r); else jugador.rotation = r;
                yield return null;
            }
            if (rb != null) rb.rotation = hasta;
            jugador.rotation = hasta;
        }
        yield return new WaitForSeconds(0.8f);
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
    }
}
