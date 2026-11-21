using System.Collections.Generic;
using UnityEngine;

// Decide lo que toca hacer y lo muestra arriba a la izquierda (MisionHUD), segun lo que ya pasa en el juego:
// la tienda, el sotano, la Zona Principal y cada zona. No cambia nada del juego: solo mira su estado.
// Los textos se editan en el Inspector (si no hay uno en la escena se crea solo al jugar con estos textos).
// Un texto vacio = en ese momento no se muestra nada. {0}/{1} = cuantos lleva / cuantos son.
public class Objetivos : MonoBehaviour
{
    [Header("Tienda")]
    public string atenderCliente = "Atiende al cliente";
    public string buscarCuchillo = "Busca el cuchillo antiguo";
    public string entregarCuchillo = "Entrégale el cuchillo al cliente";
    public string irSotano = "Ve al sótano";

    [Header("Sótano")]
    public string revisarCajas = "Revisa las cajas ({0}/{1})";
    public string buscarMuneco = "Busca el muñeco";

    [Header("Zona Principal")]
    public string irLaberinto = "Ve al Laberinto";
    public string irBackrooms = "Ve a los Backrooms";
    public string irCloacas = "Ve a las Cloacas";
    public string irZonaRoja = "Ve a la Zona Roja";

    [Header("Laberinto")]
    public string explorarLaberinto = "Explora el laberinto";
    public string paneles = "Encuentra los paneles con números ({0}/{1})";
    public string codigo = "Ingresa el código en el panel del centro";
    public string escaparLaberinto = "Escapa del laberinto";
    public float distanciaPanel = 2.6f;

    [Header("Backrooms")]
    public string explorarBackrooms = "Explora los Backrooms";
    public string palancas = "Baja las palancas ({0}/{1})";
    public string buscarManija = "Busca la manija de la palanca rota";
    public string ponerManija = "Ponle la manija a la palanca rota";
    public string salirBackrooms = "Vuelve a la estación";

    [Header("Cloacas (los de la llave salen de Salida Cloacas)")]
    public string explorarCloacas = "Explora las cloacas";

    [Header("Zona Roja")]
    public string explorarZonaRoja = "Explora la isla";

    // ------------------------------------------------------------------ Estado

    TutorialTienda tutorial;
    RevisionCajas cajas;
    CabinaTeletransporte cabina;
    ProgresoCaminos progreso;
    readonly Dictionary<string, ZonaVolumen> zonas = new Dictionary<string, ZonaVolumen>();
    PuzzleContrasena contrasena;
    readonly HashSet<Transform> panelesVistos = new HashSet<Transform>();
    SalidaConLlave salida;
    FaintTeleport[] desmayos;
    Transform jugador;
    float proxima;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        Crear();
    }

    static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Crear();

    static void Crear()
    {
        if (FindFirstObjectByType<Objetivos>(FindObjectsInactive.Include)) return;
        if (!FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include)) return;
        new GameObject("Objetivos").AddComponent<Objetivos>();
    }

    void Start()
    {
        tutorial = FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        cajas = FindFirstObjectByType<RevisionCajas>(FindObjectsInactive.Include);
        cabina = FindFirstObjectByType<CabinaTeletransporte>(FindObjectsInactive.Include);
        progreso = FindFirstObjectByType<ProgresoCaminos>(FindObjectsInactive.Include);
        contrasena = FindFirstObjectByType<PuzzleContrasena>(FindObjectsInactive.Include);
        salida = FindFirstObjectByType<SalidaConLlave>(FindObjectsInactive.Include);
        foreach (ZonaVolumen z in FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (z.name.StartsWith("Trigger ")) zonas[z.name.Substring(8)] = z;
    }

    void Update()
    {
        if (Time.time < proxima) return;
        proxima = Time.time + 0.25f;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }

        Calcular(out string texto, out string sub);
        if (string.IsNullOrEmpty(texto)) MisionHUD.Ocultar();
        else MisionHUD.Mostrar(texto, sub);
    }

    static string F(string formato, int a, int b) => string.IsNullOrEmpty(formato) ? formato : string.Format(formato, a, b);

    void Calcular(out string texto, out string sub)
    {
        texto = null; sub = null;
        if (desmayos == null) desmayos = FindObjectsByType<FaintTeleport>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (FaintTeleport f in desmayos) if (f != null && f.IsRunning) return;   // desmayandose: nada

        // ---- Tienda y sotano (antes de la cabina)
        bool cabinaUsada = cabina == null || cabina.Usada;
        if (tutorial != null && !cabinaUsada)
        {
            switch (tutorial.Paso)
            {
                case "Atender al cliente": texto = atenderCliente; return;
                case "Buscar el cuchillo":
                case "Entregar el cuchillo":
                    bool loTiene = tutorial.cuchillo != null && Inventario.Instance != null && Inventario.Instance.IndiceDe(tutorial.cuchillo) >= 0;
                    if (loTiene) texto = entregarCuchillo;
                    else texto = buscarCuchillo;
                    return;
                case "Girando hacia el sotano":
                case "Terminado":
                    break;
                default:
                    return;   // el cliente o el cultista llegando / yendose: nada
            }
            if (cajas != null && !cajas.Activa) { texto = irSotano; return; }
        }
        if (!cabinaUsada && cajas != null && cajas.Activa)
        {
            if (!cajas.Completa)
            {
                int hechas = 0, total = 0;
                foreach (CajaRevisable c in cajas.cajas) { if (c == null) continue; total++; if (c.Revisada) hechas++; }
                texto = F(revisarCajas, hechas, total);
                return;
            }
            texto = buscarMuneco;
            return;
        }
        if (!cabinaUsada) return;

        // ---- Zonas
        if (progreso == null || progreso.tramos == null) return;
        string dentro = null;
        foreach (var kv in zonas) if (kv.Value != null && kv.Value.JugadorDentro) { dentro = kv.Key; break; }

        if (dentro != null)
        {
            bool hecha = ZonaHecha(dentro);
            switch (dentro)
            {
                case "Laberinto": Laberinto(hecha, out texto, out sub); return;
                case "Backrooms": Backrooms(hecha, out texto, out sub); return;
                case "Cloacas": Cloacas(hecha, out texto, out sub); return;
                case "Zona Roja": texto = explorarZonaRoja; return;
            }
        }

        // En la Zona Principal o en un pasillo: a donde ir
        ProgresoCaminos.Tramo t = progreso.TramoActual;
        if (t == null) return;
        switch (t.nombre)
        {
            case "Laberinto": texto = irLaberinto; break;
            case "Backrooms": texto = irBackrooms; break;
            case "Cloacas": texto = irCloacas; break;
            case "Zona Roja": texto = irZonaRoja; break;
            default: texto = "Ve a " + t.nombre; break;
        }
    }

    bool ZonaHecha(string nombre)
    {
        for (int i = 0; i < progreso.tramos.Length; i++)
            if (progreso.tramos[i] != null && progreso.tramos[i].nombre == nombre) return i < progreso.zonaActual;
        return false;
    }

    void Laberinto(bool hecha, out string texto, out string sub)
    {
        sub = null;
        if (hecha || (contrasena != null && contrasena.Resuelto)) { texto = escaparLaberinto; return; }
        if (contrasena == null || contrasena.digitosPista == null) { texto = explorarLaberinto; return; }
        foreach (var d in contrasena.digitosPista)
            if (d != null && (jugador.position - d.transform.position).sqrMagnitude < distanciaPanel * distanciaPanel) panelesVistos.Add(d.transform);
        int total = contrasena.digitosPista.Length;
        bool vioElCentro = Pensamiento.YaDicho("lab.zonaSegura") || Pensamiento.YaDicho("lab.panelCentral");
        if (!vioElCentro && panelesVistos.Count == 0) { texto = explorarLaberinto; return; }
        texto = panelesVistos.Count < total ? F(paneles, panelesVistos.Count, total) : codigo;
    }

    void Backrooms(bool hecha, out string texto, out string sub)
    {
        sub = null;
        PuzzlePalancas pp = PuzzlePalancas.Instance;
        if (hecha || (pp != null && pp.Completo)) { texto = salirBackrooms; return; }
        if (pp == null) { texto = explorarBackrooms; return; }
        texto = F(palancas, pp.Activadas, pp.Total);
        // Ya vio la palanca rota: primero conseguir la manija y ponersela
        if (pp.FaltaManija)
        {
            bool tieneManija = pp.pieza != null && Inventario.Instance != null && Inventario.Instance.IndiceDe(pp.pieza) >= 0;
            texto = tieneManija ? ponerManija : buscarManija;
        }
    }

    void Cloacas(bool hecha, out string texto, out string sub)
    {
        sub = null;
        if (salida == null) { texto = explorarCloacas; return; }
        if (hecha || salida.Abierta) { texto = salida.misionSalir; return; }
        if (!salida.Cerrada) { texto = explorarCloacas; return; }
        if (salida.TieneLaLlave()) { texto = salida.misionUsar; return; }
        texto = salida.misionBuscar;
    }
}
