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
    public string presionarBoton = "Presiona el botón del tablero";
    public string entrarPortal = "Entra al portal";
    public string salirBackrooms = "Vuelve a la estación";

    [Header("Cloacas (los de la llave salen de Salida Cloacas)")]
    public string explorarCloacas = "Explora las cloacas";

    [Header("Zona Roja")]
    public string explorarZonaRoja = "Explora el faro";
    public string leerDiario = "Lee el diario del faro";
    public string buscarNotas = "Encuentra las notas ({0}/{1})";
    public string buscarBidon = "Busca el bidón de gasolina";
    public string rellenarFaro = "Rellena el faro con la gasolina";
    public string salirFaro = "Vuelve a la estación";

    [Header("Final")]
    public string huirMuros = "Busca una salida";
    public string tirarse = "Tírate al agujero";

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
        // Si se pisan (la caja de la Zona Roja es enorme: abarca la isla y todo el alto del faro), la mas pequena,
        // como hace VolumenZonas con el Volume
        float menor = float.MaxValue;
        foreach (var kv in zonas)
        {
            if (kv.Value == null || !kv.Value.JugadorDentro) continue;
            Vector3 s = kv.Value.TryGetComponent(out BoxCollider caja) ? caja.bounds.size : Vector3.one * 1e6f;
            float tam = s.x * s.y * s.z;
            if (tam < menor) { menor = tam; dentro = kv.Key; }
        }

        if (dentro != null)
        {
            bool hecha = ZonaHecha(dentro);
            switch (dentro)
            {
                case "Laberinto": Laberinto(hecha, out texto, out sub); return;
                case "Backrooms": Backrooms(hecha, out texto, out sub); return;
                case "Cloacas": Cloacas(hecha, out texto, out sub); return;
                case "Zona Roja": ZonaRoja(hecha, out texto, out sub); return;
            }
        }

        // En la Zona Principal o en un pasillo: a donde ir
        ProgresoCaminos.Tramo t = progreso.TramoActual;
        if (t == null)
        {
            // Todas completadas: el final
            if (final == null) final = FindFirstObjectByType<FinalJuego>();
            if (final != null && final.Empezado) texto = final.AgujeroAbierto ? tirarse : huirMuros;
            else texto = salirFaro;
            return;
        }
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
        if (hecha) { texto = salirBackrooms; return; }
        if (pp == null) { texto = explorarBackrooms; return; }
        // Hasta encontrar la habitacion del cadaver (y que el gato se vaya): explorar
        BackroomsGuion guion = BackroomsGuion.Instance;
        if (guion != null && !guion.GatoSuelto && pp.Activadas == 0) { texto = explorarBackrooms; return; }
        if (pp.Completo)
        {
            if (pp.boton == null) texto = salirBackrooms;
            else texto = pp.PortalAbierto ? entrarPortal : presionarBoton;
            return;
        }
        texto = F(palancas, pp.Activadas, pp.Total);
        // Ya vio la palanca rota: primero conseguir la manija y ponersela
        if (pp.FaltaManija)
        {
            bool tieneManija = pp.pieza != null && Inventario.Instance != null && Inventario.Instance.IndiceDe(pp.pieza) >= 0;
            texto = tieneManija ? ponerManija : buscarManija;
        }
    }

    FinalJuego final;

    void ZonaRoja(bool hecha, out string texto, out string sub)
    {
        sub = null;
        MisionFaro m = MisionFaro.Instance;
        if (hecha || (m != null && m.Relleno)) { texto = salirFaro; return; }
        if (m == null) { texto = explorarZonaRoja; return; }
        if (!m.DiarioLeido) { texto = Pensamiento.YaDicho("faro.diario") ? leerDiario : explorarZonaRoja; return; }
        if (m.NotasLeidas < m.TotalNotas) { texto = F(buscarNotas, m.NotasLeidas, m.TotalNotas); return; }
        texto = m.TieneBidon ? rellenarFaro : buscarBidon;
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
