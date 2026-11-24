using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Las muñecas de las Cloacas:
// - Entrada: al cerrarse la reja hay una muñeca quieta en medio del camino (la de practica), la camara la mira y
//   pasan los 3 dialogos. Una pared invisible no deja seguir hasta destruirla con la linterna (MunecaLuz).
// - Al llegar (entrar a "Trigger Cloacas" o cerrarse la reja) ya aparecen "alLlegar" muñecas repartidas, fuera de la
//   vista. Se quedan quietas (sin EstatuaAI) hasta que se destruye la de practica: ahi empiezan a moverse.
// - Despues van apareciendo en tandas de varias a la vez, cada una en un punto distinto que el
//   jugador no esta viendo y lejos de el. Con el tiempo las tandas son mas seguidas y mas grandes.
//   La llave la trae una que aparece mas tarde (nunca una de las del inicio): despues de "munecasAntesDeLlave"
//   de las tandas y de "segundosHastaLlave", en el punto mas lejos del jugador, y aguanta mas la luz. Si se destruye,
//   la llave cae al piso.
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
    // (Los campos de tiempos/cantidades se renombraron para que entren los valores nuevos en vez de los guardados en la escena)
    [Tooltip("Segundos desde que se destruye la muñeca de practica hasta que aparece la primera tanda")]
    public float primeraTanda = 4f;
    [Tooltip("Segundos entre una tanda y la siguiente")]
    public float entreTandas = 12f;
    [Tooltip("Cuantas aparecen juntas en cada tanda (minimo y maximo, al azar)")]
    public Vector2Int porTanda = new Vector2Int(2, 3);
    [Tooltip("Cuantas puede haber a la vez")]
    public int maximoAlInicio = 6;
    [Tooltip("Cuantas aparecen en total (0 = sin limite)")]
    public int totalMaximo = 0;
    [Tooltip("Metros minimos al jugador para aparecer (antes 10: aparecian demasiado cerca)")]
    public float metrosMinimosAlJugador = 18f;
    [Tooltip("Metros minimos a otra muñeca viva (que no aparezcan una encima de otra)")]
    public float metrosMinimosEntreMunecas = 4f;
    [Tooltip("Segundos de luz que aguanta cada muñeca")]
    public float segundosParaDestruir = 2.5f;
    [Tooltip("Metros preferidos al jugador: primero se prueban los puntos mas cerca que esto (siempre fuera de la vista y mas lejos que el minimo). Si no hay ninguno, en cualquiera")]
    public float metrosPreferidos = 35f;
    [Tooltip("Cuantas ya estan al llegar a las Cloacas (quietas hasta destruir la de practica)")]
    public int alLlegar = 3;

    [Header("Cada vez mas")]
    [Tooltip("Con el tiempo aparecen mas seguido, mas por tanda y caben mas: a estos minutos (desde la muñeca de practica) llegan a los valores de abajo")]
    public float minutosHastaMaximo = 4f;
    public float entreTandasAlFinal = 7f;
    public Vector2Int porTandaAlFinal = new Vector2Int(3, 4);
    public int maximoAlFinal = 10;

    [Header("Llave")]
    public LlaveAleatoria llave;
    [Tooltip("Cuantas muñecas de las tandas aparecen antes de la que trae la llave (las del inicio no cuentan)")]
    public int munecasAntesDeLlave = 5;
    [Tooltip("Segundos minimos desde que se destruye la de practica hasta que aparece la de la llave")]
    public float segundosHastaLlave = 60f;
    [Tooltip("La de la llave aguanta la luz esto por el tiempo normal (1.6 = 60% mas)")]
    public float resistenciaLlave = 1.6f;
    int enTandas;   // muñecas aparecidas en tandas (sin las del inicio)

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
    bool llegadaHecha;
    // Las que ya estaban al llegar: quietas hasta la practica
    readonly List<EstatuaAI> esperando = new List<EstatuaAI>();

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

        // Al llegar ya hay algunas repartidas por las Cloacas
        if (!llegadaHecha && (salida == null || !salida.Abierta) && !PantallaMuerte.Muerto &&
            (IntroEmpezada || (area != null && area.Contiene(jugador.position))))
            Llegada();

        if (!PracticaHecha || (salida != null && salida.Abierta) || PantallaMuerte.Muerto) return;
        if (area != null && !area.Contiene(jugador.position)) return;
        vivas.RemoveAll(v => v == null);
        if (Time.time < proxima) return;
        float k = minutosHastaMaximo > 0f ? Mathf.Clamp01((Time.time - desdePractica) / (minutosHastaMaximo * 60f)) : 1f;
        int maximo = Mathf.RoundToInt(Mathf.Lerp(maximoAlInicio, Mathf.Max(maximoAlInicio, maximoAlFinal), k));
        float intervalo = Mathf.Lerp(entreTandas, Mathf.Min(entreTandas, entreTandasAlFinal), k);
        // La de la llave sale aunque ya esten todas las que caben
        bool llaveYa = TocaLlave();
        if ((vivas.Count >= maximo && !llaveYa) || (totalMaximo > 0 && Apariciones >= totalMaximo && !llaveYa)) { proxima = Time.time + 1f; return; }

        // Una tanda: varias a la vez, cada una en un punto distinto
        int min = Mathf.RoundToInt(Mathf.Lerp(porTanda.x, Mathf.Max(porTanda.x, porTandaAlFinal.x), k));
        int max = Mathf.RoundToInt(Mathf.Lerp(porTanda.y, Mathf.Max(porTanda.y, porTandaAlFinal.y), k));
        int n = Mathf.Max(1, Random.Range(Mathf.Max(1, min), Mathf.Max(min, max) + 1));
        n = Mathf.Min(n, maximo - vivas.Count);
        if (totalMaximo > 0) n = Mathf.Min(n, totalMaximo - Apariciones);
        if (llaveYa) n = Mathf.Max(n, 1);
        var usados = new List<Transform>();
        int hechas = 0;
        for (int i = 0; i < n; i++) if (Aparecer(usados) != null) hechas++;
        proxima = hechas > 0 ? Time.time + intervalo : Time.time + 2f;
    }

    void Llegada()
    {
        llegadaHecha = true;
        var usados = new List<Transform>();
        int n = totalMaximo > 0 ? Mathf.Min(alLlegar, totalMaximo) : alLlegar;
        for (int i = 0; i < n; i++)
        {
            GameObject e = Aparecer(usados, true);
            if (e == null) break;
            // Quietas hasta que se destruye la de practica (si no, la alcanzarian durante la presentacion)
            if (!PracticaHecha && e.TryGetComponent(out EstatuaAI ai)) { ai.enabled = false; esperando.Add(ai); }
        }
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
        proxima = Time.time + primeraTanda;
        desdePractica = Time.time;
        // Las que ya estaban empiezan a moverse
        foreach (EstatuaAI ai in esperando) if (ai != null) ai.enabled = true;
        esperando.Clear();
    }

    // Una muñeca nueva en un punto que el jugador no ve (y que no este en "usados": los de la misma tanda)
    // deLlegada: de las que ya estan al llegar (esas nunca traen la llave)
    GameObject Aparecer(List<Transform> usados, bool deLlegada = false)
    {
        if (prefab == null || puntos == null || puntos.Length == 0) return null;
        Camera cam = Camera.main;
        bool traeLlave = !deLlegada && TocaLlave();
        var orden = new List<Transform>();
        foreach (Transform t in puntos) if (t != null) orden.Insert(Random.Range(0, orden.Count + 1), t);
        if (traeLlave)
            // La de la llave: en los puntos mas lejanos del jugador
            orden.Sort((a, b) => Plano(b.position - jugador.position).CompareTo(Plano(a.position - jugador.position)));
        else
            // Primero los puntos cerca (entre la distancia minima y la preferida), en orden al azar; despues el resto
            orden.Sort((a, b) =>
            {
                bool ca = (a.position - jugador.position).sqrMagnitude <= metrosPreferidos * metrosPreferidos;
                bool cb = (b.position - jugador.position).sqrMagnitude <= metrosPreferidos * metrosPreferidos;
                return cb.CompareTo(ca);
            });
        vivas.RemoveAll(v => v == null);
        for (int i = 0; i < orden.Count; i++)
        {
            Transform p = orden[i];
            if (p == null || usados.Contains(p) || Plano(p.position - jugador.position) < metrosMinimosAlJugador) continue;
            if (CercaDeOtra(p.position)) continue;
            // Ni los pies, ni el cuerpo, ni la cabeza a la vista
            if (cam != null && (LoVe(cam, p.position + Vector3.up * 0.15f) || LoVe(cam, p.position + Vector3.up * 0.8f) ||
                                LoVe(cam, p.position + Vector3.up * 1.45f))) continue;
            if (!NavMesh.SamplePosition(p.position, out NavMeshHit h, 2.5f, NavMesh.AllAreas)) continue;

            float alto = prefab.TryGetComponent(out NavMeshAgent ap) ? ap.baseOffset : 0f;
            Vector3 mirar = jugador.position - h.position; mirar.y = 0f;
            var e = Instantiate(prefab, h.position + Vector3.up * alto,
                                mirar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mirar) : p.rotation, transform);
            Apariciones++;
            usados.Add(p);
            e.name = prefab.name + " " + Apariciones;
            var luz = e.GetComponent<MunecaLuz>();
            if (luz == null) luz = e.AddComponent<MunecaLuz>();
            luz.segundosParaDestruir = segundosParaDestruir;
            if (e.TryGetComponent(out EstatuaAI ai)) vivas.Add(ai);
            if (PantallaMuerte.Instance != null) PantallaMuerte.Instance.Suscribir();
            if (!deLlegada) enTandas++;
            if (traeLlave)
            {
                DarLlave(e.transform);
                // Y aguanta mas la luz
                luz.segundosParaDestruir = segundosParaDestruir * resistenciaLlave;
            }
            return e;
        }
        return null;
    }

    // La siguiente que aparezca en una tanda trae la llave: ya pasaron las de antes y el tiempo minimo
    // (Con el doble de tiempo sale igual aunque no hayan aparecido todas las de antes: si no, sin destruir ninguna el
    // maximo de vivas frenaria las tandas y la llave no saldria nunca)
    bool TocaLlave() => !llaveDada && llave != null && PracticaHecha &&
                        (enTandas >= munecasAntesDeLlave || Time.time - desdePractica >= segundosHastaLlave * 2f) &&
                        Time.time - desdePractica >= segundosHastaLlave;

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    bool CercaDeOtra(Vector3 p)
    {
        foreach (EstatuaAI v in vivas)
            if (v != null && Plano(v.transform.position - p) < metrosMinimosEntreMunecas) return true;
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
