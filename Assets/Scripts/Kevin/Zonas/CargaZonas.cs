using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Cada zona (Laberinto, Backrooms, Cloacas, Zona Roja: los tramos de ProgresoCaminos) solo esta activa mientras el
// jugador esta dentro, en su pasillo o cerca de su entrada. El resto del tiempo su objeto raiz se desactiva entero:
// no se dibujan sus mallas, luces ni sombras, y no corren sus enemigos, sonidos ni scripts (su estado se guarda:
// al volver todo sigue como estaba). La tienda y el sotano se apagan cuando ya se uso la cabina.
// Se crea solo al jugar (solo en Play: en el editor las zonas siguen activas para poder editarlas).
public class CargaZonas : MonoBehaviour
{
    [Tooltip("Metros a la entrada de la zona a los que ya se carga")]
    public float radioEntrada = 18f;
    [Tooltip("Metros extra (alrededor del pasillo de la zona) a los que ya se carga")]
    public float margenPasillo = 4f;
    [Tooltip("Metros de mas para descargarla (asi no se carga y descarga en el borde)")]
    public float histeresis = 8f;
    [Tooltip("Apagar la tienda, el sotano y la calle cuando ya se uso la cabina (no se vuelve ahi)")]
    public bool descargarTienda = true;
    public string[] raicesTienda = { "Tienda", "zotano", "escaleras", "Intro Tienda", "Tutorial Tienda" };

    class Zona
    {
        public string nombre;
        public GameObject raiz;
        public ZonaVolumen area;
        public Vector3 entrada;
        public Bounds pasillo;
        public bool hayPasillo;
        public float volumenArea = float.MaxValue;
    }

    readonly List<Zona> zonas = new List<Zona>();
    readonly List<GameObject> tienda = new List<GameObject>();
    Bounds limitesTienda;
    bool hayTienda, tiendaApagada;
    CabinaTeletransporte cabina;
    Transform jugador;
    float proxima;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        Crear();
    }

    static void AlCargarEscena(Scene s, LoadSceneMode m) => Crear();

    static void Crear()
    {
        if (FindFirstObjectByType<CargaZonas>()) return;
        if (!FindFirstObjectByType<ProgresoCaminos>(FindObjectsInactive.Include)) return;
        new GameObject("Carga de zonas").AddComponent<CargaZonas>();
    }

    void Start()
    {
        var raices = new Dictionary<string, GameObject>();
        foreach (GameObject g in SceneManager.GetActiveScene().GetRootGameObjects()) raices[g.name] = g;

        ProgresoCaminos progreso = FindFirstObjectByType<ProgresoCaminos>(FindObjectsInactive.Include);
        var areas = FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (progreso != null && progreso.tramos != null)
            foreach (ProgresoCaminos.Tramo t in progreso.tramos)
            {
                if (t == null || !raices.TryGetValue(t.nombre, out GameObject raiz)) continue;
                var z = new Zona { nombre = t.nombre, raiz = raiz };
                foreach (ZonaVolumen a in areas)
                    if (a.name == "Trigger " + t.nombre && a.TryGetComponent(out BoxCollider caja))
                    {
                        z.area = a;
                        Vector3 s = Vector3.Scale(caja.size, a.transform.lossyScale);
                        z.volumenArea = Mathf.Abs(s.x * s.y * s.z);
                    }
                Transform e = raiz.transform.Find("Entrada Pasillo");
                z.entrada = e != null ? e.position : raiz.transform.position;
                // El pasillo de la zona: el "Pasillo - ..." de la Zona Principal donde esta su puerta
                for (Transform p = t.puerta != null ? t.puerta.transform : null; p != null; p = p.parent)
                    if (p.name.StartsWith("Pasillo")) { z.hayPasillo = Limites(p, out z.pasillo); break; }
                if (z.hayPasillo) z.pasillo.Expand(new Vector3(margenPasillo * 2f, 6f, margenPasillo * 2f));
                zonas.Add(z);
            }

        foreach (string n in raicesTienda)
            if (raices.TryGetValue(n, out GameObject g))
            {
                tienda.Add(g);
                if (Limites(g.transform, out Bounds b)) { if (!hayTienda) { limitesTienda = b; hayTienda = true; } else limitesTienda.Encapsulate(b); }
            }
        cabina = FindFirstObjectByType<CabinaTeletransporte>(FindObjectsInactive.Include);
    }

    void Update()
    {
        if (Time.unscaledTime < proxima) return;
        proxima = Time.unscaledTime + 0.25f;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }
        Vector3 p = jugador.position;

        // La zona en la que esta (si las cajas se pisan, la mas chica, como hace el Volume)
        Zona actual = null;
        foreach (Zona z in zonas)
            if (z.area != null && z.area.Contiene(p) && (actual == null || z.volumenArea < actual.volumenArea)) actual = z;

        foreach (Zona z in zonas)
        {
            if (z.raiz == null) continue;
            bool activa = z.raiz.activeSelf;
            float extra = activa ? histeresis : 0f;
            bool quiere = z == actual
                       || (z.hayPasillo && Dentro(z.pasillo, p, extra))
                       || Vector3.Distance(p, z.entrada) < radioEntrada + extra;
            if (quiere == activa) continue;
            z.raiz.SetActive(quiere);
            if (quiere) StartCoroutine(PegarAgentes(z.raiz));
        }

        // La tienda y el sotano: ya no se vuelve despues de la cabina
        if (descargarTienda && tienda.Count > 0 && cabina != null && cabina.Usada)
        {
            bool ahi = hayTienda && Dentro(limitesTienda, p, 0f);
            if (ahi == tiendaApagada)
            {
                tiendaApagada = !ahi;
                foreach (GameObject g in tienda) if (g != null) g.SetActive(ahi);
            }
        }
    }

    static bool Dentro(Bounds b, Vector3 p, float extra)
    {
        b.Expand(extra * 2f);
        return b.Contains(p);
    }

    // Al volver a activarse, por si algun enemigo se activo antes que el NavMesh de su zona
    static IEnumerator PegarAgentes(GameObject raiz)
    {
        yield return null;
        if (raiz == null) yield break;
        foreach (NavMeshAgent a in raiz.GetComponentsInChildren<NavMeshAgent>())
            if (a.enabled && !a.isOnNavMesh && NavMesh.SamplePosition(a.transform.position - Vector3.up * a.baseOffset, out NavMeshHit h, 4f, NavMesh.AllAreas))
                a.Warp(h.position);
    }

    // Solo lo activo (lo inactivo da limites vacios en el origen del mundo)
    static bool Limites(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        foreach (Collider c in t.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled) continue;
            if (!hay) { b = c.bounds; hay = true; }
            else b.Encapsulate(c.bounds);
        }
        return hay;
    }
}
