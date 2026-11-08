using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Llena la Tienda con los props de Assets/Models/Props:
// - ESTANTES (vitrinas y la estanteria de la pared este): objetos chicos en fila en cada repisa,
//   escalados para que quepan en el fondo y alto de cada repisa.
// - MESAS: composiciones de objetos mas grandes.
// - PAREDES: relojes y katana colgados, plantas grandes y maletas junto a las paredes.
// Los materiales son los mismos que se usaron para estos props en la escena "jesus".
// Se respetan: el cuchillo del tutorial, el camino de los NPCs, el lugar del jugador y la puerta del sotano.
//
// Se crea sola una vez dentro de "Tienda/Decoracion". Rehacer: Tools > Tienda > Decorar con props.
[InitializeOnLoad]
public static class TiendaPropsSetup
{
    const string Version = "_decoracion v3";
    const string Carpeta = "Assets/Models/Props/";
    const int Semilla = 2026;

    // ------------------------------------------------------------------ Catalogo

    enum Forma { Parado, Acostado, Colgado }

    class Prop
    {
        public string archivo;
        public float tam;         // tamano maximo (m) de su lado mas largo
        public Forma forma = Forma.Parado;
        public Prop(string a, float t, Forma f = Forma.Parado) { archivo = a; tam = t; forma = f; }
    }

    static readonly Dictionary<string, Prop> Props = new Dictionary<string, Prop>
    {
        { "taza",     new Prop("taza.obj", 0.11f) },
        { "cenicero", new Prop("ashtray.obj", 0.15f) },
        { "cigarro",  new Prop("cigarette.obj", 0.09f, Forma.Acostado) },
        { "radio",    new Prop("radio.obj", 0.32f) },
        { "reloj",    new Prop("clock.obj", 0.30f) },
        { "lampara",  new Prop("lamp.obj", 0.40f) },
        { "farol",    new Prop("lampara vieja.obj", 0.42f) },
        { "libros",   new Prop("boooks.obj", 0.30f) },
        { "maletin",  new Prop("maleta.obj", 0.40f) },
        { "planta",   new Prop("plant 1.obj", 0.40f) },
        { "auto",     new Prop("car (1).fbx", 0.28f) },
        { "katana",   new Prop("kATANA (1).fbx", 0.95f, Forma.Acostado) },
        { "mesa",     new Prop("mesa.obj", 2.2f) },
    };

    // Que va en cada lugar (se repite la lista al azar).
    static readonly string[] EnEstantes = { "taza", "cenicero", "radio", "reloj", "lampara", "farol", "libros", "maletin", "planta", "auto", "libros", "taza" };
    // Mesas: fila de atras con objetos grandes y fila de adelante con objetos chicos.
    static readonly string[] EnMesasAtras = { "farol", "lampara", "radio", "libros", "maletin", "planta", "reloj", "katana" };
    static readonly string[] EnMesasAdelante = { "taza", "cenicero", "auto", "libros", "reloj", "taza", "cenicero", "radio" };

    // Materiales por pieza, copiados de como estan estos props en la escena "jesus"
    // (clave: archivo|id de la pieza en el modelo|indice de material).
    static readonly Dictionary<string, string> TablaMateriales = new Dictionary<string, string>
    {
            { "ashtray.obj|-4780072770720950627|0", "Assets/Models/Props/metal.mat" },
            { "cigarette.obj|-425565630206218756|0", "Assets/Models/Props/New Material 2.mat" },
            { "cigarette.obj|-4265338650084452101|0", "Assets/Models/Props/blanco.mat" },
            { "cigarette.obj|2586607309129781981|0", "Assets/Models/Props/New Material.mat" },
            { "clock.obj|-2809542737496772586|0", "Assets/Models/Props/negro.mat" },
            { "clock.obj|-3995272465676935165|0", "Assets/Materials/CloacaConcreto.mat" },
            { "clock.obj|-6067185528594298527|0", "Assets/Models/Props/bronw.mat" },
            { "clock.obj|321605248896674044|0", "Assets/Materials/CloacaConcreto.mat" },
            { "clock.obj|3566390031902043909|0", "Assets/Models/Props/glass.mat" },
            { "clock.obj|7942390064308805707|0", "Assets/Materials/CloacaConcreto.mat" },
            { "lamp.obj|-425565630206218756|0", "Assets/Models/Props/New Material 1.mat" },
            { "lamp.obj|3566390031902043909|0", "Assets/Models/Props/madera vieja.mat" },
            { "lampara vieja.obj|-1468662180276524799|0", "Assets/Models/Props/metal.mat" },
            { "lampara vieja.obj|-2417094610102442147|0", "Assets/Models/Props/metal.mat" },
            { "lampara vieja.obj|-2682167134973850427|0", "Assets/Models/Props/metal claro.mat" },
            { "lampara vieja.obj|-425565630206218756|0", "Assets/Models/Props/metal.mat" },
            { "lampara vieja.obj|-4265338650084452101|0", "Assets/Models/Props/cobre.mat" },
            { "lampara vieja.obj|-6067185528594298527|0", "Assets/Models/Props/cobre.mat" },
            { "lampara vieja.obj|1790868808166937211|0", "Assets/Models/Props/metal.mat" },
            { "lampara vieja.obj|2609437277962515516|0", "Assets/Models/Props/New Material 6.mat" },
            { "lampara vieja.obj|3566390031902043909|0", "Assets/Models/Props/metal.mat" },
            { "lampara vieja.obj|3575641881151511511|0", "Assets/Models/Props/New Material 6.mat" },
            { "lampara vieja.obj|4953232840523182856|0", "Assets/Models/Props/metal claro.mat" },
            { "lampara vieja.obj|584855923758527653|0", "Assets/Models/Props/metal.mat" },
            { "lampara vieja.obj|5901893356585517859|0", "Assets/Models/Props/cobre.mat" },
            { "lampara vieja.obj|7216655632450529714|0", "Assets/Models/Props/metal claro.mat" },
            { "lampara vieja.obj|7330095381222774183|0", "Assets/Models/Props/metal claro.mat" },
            { "lampara vieja.obj|7484468418975526891|0", "Assets/Models/Props/metal.mat" },
            { "maleta.obj|-3035608602465568333|0", "Assets/Models/Props/New Material 1.mat" },
            { "maleta.obj|-3316209038747775882|0", "Assets/Models/Props/bronw.mat" },
            { "maleta.obj|-4795165560744122111|0", "Assets/Models/Props/metal.mat" },
            { "maleta.obj|-6067185528594298527|0", "Assets/Models/Props/marron oscuro.mat" },
            { "maleta.obj|-6822343713258076425|0", "Assets/Models/Props/metal.mat" },
            { "maleta.obj|1170970068873931921|0", "Assets/Models/Props/bronw.mat" },
            { "maleta.obj|2869344807088711131|0", "Assets/Models/Props/metal.mat" },
            { "maleta.obj|5901893356585517859|0", "Assets/Models/Props/bronw.mat" },
            { "maleta.obj|7483894711331120201|0", "Assets/Models/Props/metal.mat" },
            { "plant 1.obj|-3035608602465568333|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|-3343327794070940233|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|-4171001361061973571|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|-4440613634464577847|0", "Assets/Models/Props/madera vieja.mat" },
            { "plant 1.obj|-4795165560744122111|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|-6636569964863955296|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|-6822343713258076425|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|-8021837051761670525|0", "Assets/Models/Props/New Material 5.mat" },
            { "plant 1.obj|-9158636819078260051|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|2869344807088711131|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|2929640277652407947|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|569169282116888178|0", "Assets/Models/Props/plant.mat" },
            { "plant 1.obj|5901893356585517859|0", "Assets/Models/Props/New Material 2.mat" },
            { "plant 1.obj|7483894711331120201|0", "Assets/Models/Props/plant.mat" },
            { "radio.obj|-2984341390075865894|0", "Assets/Models/Props/negro.mat" },
            { "radio.obj|-3901992563441801604|0", "Assets/Models/Props/marron oscuro.mat" },
            { "radio.obj|-4529586120300026073|0", "Assets/Models/Props/negro.mat" },
            { "radio.obj|-5727984100303288733|0", "Assets/Models/Props/metal.mat" },
            { "radio.obj|-65689098689643265|0", "Assets/Models/Props/negro.mat" },
            { "radio.obj|-8021837051761670525|0", "Assets/Models/Props/bronw.mat" },
            { "radio.obj|1727654469516573141|0", "Assets/Models/Props/negro.mat" },
            { "radio.obj|4495414801083549842|0", "Assets/Models/Props/negro.mat" },
            { "radio.obj|4653342381608223260|0", "Assets/Models/Props/negro.mat" },
            { "radio.obj|7386544489384849405|0", "Assets/Models/Props/metal.mat" },
            { "taza.obj|3566390031902043909|0", "Assets/Models/Props/glass.mat" },
    };

    // Para los props que no estaban en "jesus" (libros, auto, katana): material segun el nombre de la pieza.
    static readonly string[] TapasLibros = { "marron oscuro", "bronw", "negro", "New Material 1", "madera vieja" };

    // ------------------------------------------------------------------

    static TiendaPropsSetup()
    {
        EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject tienda = GameObject.Find("Tienda");
        if (!tienda) return;
        Transform deco = tienda.transform.Find("Decoracion");
        if (deco && deco.Find(Version)) return;
        Crear();
    }

    [MenuItem("Tools/Tienda/Decorar con props")]
    static void Menu()
    {
        Crear();
        GameObject t = GameObject.Find("Tienda");
        if (t) Selection.activeTransform = t.transform.Find("Decoracion");
    }

    static System.Random rnd;

    // ---- Para otras herramientas (ej: SotanoSetup) ----

    // Crea un prop del catalogo (taza, radio, reloj, lampara, farol, libros, maletin, planta, auto, katana,
    // cenicero, cigarro) con los materiales correctos, escalado a "tam" metros y apoyado en "baseCentro".
    internal static GameObject CrearProp(string clave, Transform padre, Vector3 baseCentro, float tam, float yaw,
                                         bool acostado, System.Random azar)
    {
        rnd = azar;
        if (!Props.ContainsKey(clave)) return null;
        GameObject go = Instanciar(clave, padre);
        if (!go) return null;
        Prop p = Props[clave];
        if (acostado) p = new Prop(p.archivo, p.tam, Forma.Acostado);
        Encajar(go, p, Vector3.forward, yaw, tam, 10f, 10f, 10f);
        Apoyar(go, baseCentro);
        return go;
    }

    internal static bool LimitesDe(GameObject go, out Bounds b) => Limites(go, out b);
    static readonly List<Bounds> zonasLibres = new List<Bounds>();
    static readonly Dictionary<string, Material> cacheMat = new Dictionary<string, Material>();
    static int colocados;

    static void Crear()
    {
        GameObject tiendaGO = GameObject.Find("Tienda");
        if (!tiendaGO) return;
        Transform tienda = tiendaGO.transform;

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Decorar la tienda");

        Transform vieja = tienda.Find("Decoracion");
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

        rnd = new System.Random(Semilla);
        colocados = 0;
        cacheMat.Clear();
        ArmarZonasLibres(tienda);

        // La raiz se arma en el mundo y despues se cuelga de la Tienda (asi se mueve con ella).
        var raiz = new GameObject("Decoracion");
        Undo.RegisterCreatedObjectUndo(raiz, "Decoracion");
        new GameObject(Version).transform.SetParent(raiz.transform, false);
        Transform estantes = Hijo(raiz.transform, "Estantes");
        Transform mesas = Hijo(raiz.transform, "Mesas");
        Transform paredes = Hijo(raiz.transform, "Paredes");

        int enEstantes = 0, enMesas = 0, enParedes = 0;
        Transform contEstantes = tienda.Find("estantes");
        if (contEstantes)
        {
            foreach (Transform g in contEstantes)
            {
                if (g.name.StartsWith("Gabinete")) { int a = colocados; DecorarEstante(g, Hijo(estantes, g.name)); enEstantes += colocados - a; }
                else if (g.name.StartsWith("mesa")) { int a = colocados; DecorarMesa(g, Hijo(mesas, g.name)); enMesas += colocados - a; }
            }
        }
        { int a = colocados; DecorarParedes(tienda, paredes); enParedes = colocados - a; }

        raiz.transform.SetParent(tienda, true);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Tienda] Decoracion: {colocados} props ({enEstantes} en estantes, {enMesas} en mesas, {enParedes} en paredes). Guarda la escena (Ctrl+S).", raiz);
    }

    // Lugares que deben quedar libres (en el mundo).
    static void ArmarZonasLibres(Transform tienda)
    {
        zonasLibres.Clear();
        Transform cuchillo = tienda.Find("cuchillo");
        if (cuchillo && cuchillo.TryGetComponent(out Renderer rc))
        {
            Bounds b = rc.bounds; b.Expand(new Vector3(0.35f, 0.4f, 0.35f));
            zonasLibres.Add(b);
        }
        GameObject tut = GameObject.Find("Tutorial Tienda");
        if (tut)
        {
            Transform puerta = tut.transform.Find("Camino NPCs/1 Puerta");
            Transform mostrador = tut.transform.Find("Camino NPCs/2 Mostrador");
            Transform jugador = tut.transform.Find("Lugar del jugador (detras del mostrador)");
            if (puerta && mostrador)
            {
                var b = new Bounds(puerta.position, Vector3.zero);
                b.Encapsulate(mostrador.position);
                b.Expand(new Vector3(1.6f, 10f, 1.6f));
                zonasLibres.Add(b);
            }
            if (jugador) zonasLibres.Add(new Bounds(jugador.position, new Vector3(2f, 10f, 3f)));
        }
    }

    static bool EnZonaLibre(Bounds b)
    {
        foreach (Bounds z in zonasLibres) if (z.Intersects(b)) return true;
        return false;
    }

    // ------------------------------------------------------------------ Estantes

    static void DecorarEstante(Transform gabinete, Transform padre)
    {
        Renderer base_ = R(gabinete, "Cube (21)"), medio = R(gabinete, "Cube (25)"), tope = R(gabinete, "Cube (24)");
        Renderer ladoA = R(gabinete, "Cube (22)"), ladoB = R(gabinete, "Cube (23)");
        Renderer vidrioA = R(gabinete, "Cube (18)"), vidrioB = R(gabinete, "Cube (19)");
        if (!base_ || !medio || !tope) return;

        Bounds bb = base_.bounds;
        Vector3 u = bb.size.x >= bb.size.z ? Vector3.right : Vector3.forward;
        Vector3 v = u == Vector3.right ? Vector3.forward : Vector3.right;

        // Largo util: entre los laterales.
        float uMin = Min(bb, u), uMax = Max(bb, u);
        if (ladoA && ladoB)
        {
            uMin = Mathf.Min(Max(ladoA.bounds, u), Max(ladoB.bounds, u));
            uMax = Mathf.Max(Min(ladoA.bounds, u), Min(ladoB.bounds, u));
        }
        // Fondo util: entre los vidrios (si los hay) o el fondo de la repisa.
        float vMin = Min(bb, v), vMax = Max(bb, v);
        if (vidrioA && vidrioB)
        {
            vMin = Mathf.Min(Max(vidrioA.bounds, v), Max(vidrioB.bounds, v));
            vMax = Mathf.Max(Min(vidrioA.bounds, v), Min(vidrioB.bounds, v));
        }
        float fondo = (vMax - vMin) * 0.9f;
        float vCentro = (vMin + vMax) * 0.5f;

        Rellenar(padre, u, v, uMin, uMax, vCentro, fondo, bb.max.y, medio.bounds.min.y - bb.max.y, EnEstantes, 0.04f, 0.14f, 0f);
        Rellenar(padre, u, v, uMin, uMax, vCentro, fondo, medio.bounds.max.y, tope.bounds.min.y - medio.bounds.max.y, EnEstantes, 0.04f, 0.14f, 0f);
        // Arriba del mueble: menos objetos y mas separados.
        float fondoArriba = (Max(tope.bounds, v) - Min(tope.bounds, v)) * 0.9f;
        Rellenar(padre, u, v, Min(tope.bounds, u), Max(tope.bounds, u), (Min(tope.bounds, v) + Max(tope.bounds, v)) * 0.5f,
                 fondoArriba, tope.bounds.max.y, 0.45f, EnEstantes, 0.25f, 0.7f, 0f);
    }

    // ------------------------------------------------------------------ Mesas

    static void DecorarMesa(Transform mesa, Transform padre)
    {
        Bounds b = default; bool hay = false; float tapa = float.MinValue;
        foreach (Renderer r in mesa.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            tapa = Mathf.Max(tapa, r.bounds.max.y);
        }
        if (!hay) return;
        Vector3 u = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
        Vector3 v = u == Vector3.right ? Vector3.forward : Vector3.right;
        float vCentro = (Min(b, v) + Max(b, v)) * 0.5f;
        float ancho = Max(b, v) - Min(b, v);
        float fila = ancho * 0.42f;
        // Dos filas: una mitad de la mesa con objetos grandes y la otra con chicos.
        Rellenar(padre, u, v, Min(b, u) + 0.08f, Max(b, u) - 0.08f, vCentro - ancho * 0.22f, fila, tapa, 0.75f, EnMesasAtras, 0.08f, 0.25f, ancho * 0.04f, 1.2f);
        Rellenar(padre, u, v, Min(b, u) + 0.15f, Max(b, u) - 0.08f, vCentro + ancho * 0.22f, fila, tapa, 0.5f, EnMesasAdelante, 0.1f, 0.3f, ancho * 0.05f, 1.15f);
    }

    // ------------------------------------------------------------------ Paredes

    static void DecorarParedes(Transform tienda, Transform padre)
    {
        Renderer oeste = R(tienda, "paredes/Cube (45)"), este = R(tienda, "paredes/Cube (1)");
        Renderer sur = R(tienda, "paredes/Cube (18)"), norte = R(tienda, "paredes/Cube (30)");
        Renderer piso = R(tienda, "rt (6)");
        if (!oeste || !este || !sur) return;
        float ySuelo = piso ? piso.bounds.max.y : oeste.bounds.min.y;
        float xOeste = oeste.bounds.max.x, xEste = este.bounds.min.x, zSur = sur.bounds.max.z;
        float zNorte = norte ? norte.bounds.min.z : este.bounds.max.z;
        float zOesteMin = oeste.bounds.min.z, zOesteMax = oeste.bounds.max.z; // tramo de la pared oeste antes de la puerta del sotano

        // Relojes de pared (el frente del modelo mira hacia su -X local).
        Colgar(padre, "reloj", new Vector3(xOeste + 0.06f, ySuelo + 2.05f, Mathf.Lerp(zOesteMin, zOesteMax, 0.55f)), Vector3.forward, Vector3.right, 0.42f);
        Colgar(padre, "reloj", new Vector3(xEste - 0.06f, ySuelo + 2.45f, Mathf.Lerp(zSur, zNorte, 0.5f)), Vector3.forward, Vector3.left, 0.42f);

        // Katana colgada en la pared oeste, horizontal.
        Colgar(padre, "katana", new Vector3(xOeste + 0.07f, ySuelo + 1.65f, Mathf.Lerp(zOesteMin, zOesteMax, 0.25f)), Vector3.forward, Vector3.right, 1.0f);

        // Plantas grandes en las esquinas libres y junto a la vidriera norte.
        EnSuelo(padre, "planta", new Vector3(xOeste + 0.5f, ySuelo, zSur + 0.5f), 1.15f, 0f);
        EnSuelo(padre, "planta", new Vector3(xEste - 0.5f, ySuelo, zNorte - 0.5f), 1.05f, 40f);
        EnSuelo(padre, "planta", new Vector3(Mathf.Lerp(xOeste, xEste, 0.75f), ySuelo, zNorte - 0.45f), 0.9f, 75f);

        // Maletas apiladas junto a la pared oeste.
        float zMaletas = Mathf.Lerp(zOesteMin, zOesteMax, 0.72f);
        GameObject m1 = EnSuelo(padre, "maletin", new Vector3(xOeste + 0.45f, ySuelo, zMaletas), 0.75f, 0f, Forma.Acostado);
        if (m1 && Limites(m1, out Bounds b1))
        {
            GameObject m2 = EnSuelo(padre, "maletin", new Vector3(b1.center.x + 0.03f, b1.max.y, b1.center.z - 0.04f), 0.62f, 8f, Forma.Acostado, false);
            if (m2 && Limites(m2, out Bounds b2))
                EnSuelo(padre, "farol", new Vector3(b2.center.x, b2.max.y, b2.center.z), 0.42f, 20f, Forma.Parado, false);
        }
        EnSuelo(padre, "maletin", new Vector3(xOeste + 0.35f, ySuelo, zMaletas + 0.85f), 0.65f, -6f);
    }

    // ------------------------------------------------------------------ Colocacion

    // Llena una repisa/superficie en fila a lo largo de "u".
    static void Rellenar(Transform padre, Vector3 u, Vector3 v, float uMin, float uMax, float vCentro, float fondo,
                         float y, float alto, string[] lista, float sepMin, float sepMax, float desvioV, float escalaTam = 1f)
    {
        if (alto < 0.08f || uMax - uMin < 0.15f) return;
        float cursor = uMin + 0.05f;
        string anterior = null;
        int fallos = 0;
        while (cursor < uMax - 0.08f && fallos < 6)
        {
            string clave = lista[rnd.Next(lista.Length)];
            if (clave == anterior) continue;
            Prop p = Props[clave];
            // En las mesas los maletines van acostados (en los estantes, parados).
            if (escalaTam > 1f && clave == "maletin") p = new Prop(p.archivo, p.tam, Forma.Acostado);
            float yaw = p.forma == Forma.Parado ? Rango(-12f, 12f) : Rango(-4f, 4f);

            GameObject go = Instanciar(clave, padre);
            if (!go) { fallos++; continue; }
            Encajar(go, p, u, yaw, Mathf.Min(p.tam * escalaTam, alto * 0.88f), alto * 0.88f, uMax - cursor - 0.03f, fondo);
            if (!Limites(go, out Bounds b) || Largo(b, u) > uMax - cursor - 0.03f || b.size.y > alto * 0.92f)
            {
                Object.DestroyImmediate(go); fallos++; continue;
            }

            float l = Largo(b, u);
            float dv = desvioV > 0f ? Rango(-desvioV, desvioV) : 0f;
            Vector3 destino = u * (cursor + l * 0.5f) + v * (vCentro + dv) + Vector3.up * y;
            Apoyar(go, destino);
            Limites(go, out b);
            if (EnZonaLibre(b))
            {
                Object.DestroyImmediate(go);
                cursor += 0.2f;
                continue;
            }

            colocados++;
            anterior = clave;
            fallos = 0;
            if (clave == "cenicero") PonerCigarro(go, padre);
            cursor += l + Rango(sepMin, sepMax);
        }
    }

    static void PonerCigarro(GameObject cenicero, Transform padre)
    {
        if (!Limites(cenicero, out Bounds b)) return;
        GameObject c = Instanciar("cigarro", padre);
        if (!c) return;
        Encajar(c, Props["cigarro"], Vector3.right, Rango(0f, 360f), 0.09f, 0.05f, 0.12f, 0.12f);
        Apoyar(c, new Vector3(b.center.x, b.max.y - 0.01f, b.center.z));
        colocados++;
    }

    // Objeto colgado en una pared: "largo" = direccion horizontal del objeto, "normal" = hacia el cuarto.
    static GameObject Colgar(Transform padre, string clave, Vector3 centro, Vector3 largo, Vector3 normal, float tam)
    {
        GameObject go = Instanciar(clave, padre);
        if (!go) return null;
        Prop p = Props[clave];
        if (clave == "reloj")
        {
            // El frente del reloj es su -X local: se gira para que mire hacia el cuarto.
            go.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.left, normal, Vector3.up), Vector3.up);
            Escalar(go, tam);
        }
        else
        {
            // Lado mas largo a lo largo de la pared y el mas delgado contra la pared.
            Ejes(go, out Vector3 aLargo, out Vector3 aMedio, out Vector3 aCorto);
            go.transform.rotation = Quaternion.LookRotation(largo, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(aLargo, aMedio));
            Escalar(go, tam);
        }
        if (!Limites(go, out Bounds b)) { Object.DestroyImmediate(go); return null; }
        go.transform.position += centro - b.center;
        Limites(go, out b);
        if (EnZonaLibre(b))
        {
            Debug.Log($"[Tienda] \"{clave}\" no se coloco: cae en una zona que debe quedar libre.");
            Object.DestroyImmediate(go); return null;
        }
        if (Choca(b, go, 0.85f)) { Object.DestroyImmediate(go); return null; }
        Debug.Log($"[Tienda] En pared: \"{clave}\" en ({b.center.x:0.00}, {b.center.y:0.00}, {b.center.z:0.00}).");
        colocados++;
        return go;
    }

    // Objeto apoyado en el suelo (o sobre otro objeto si comprobarChoque = false).
    static GameObject EnSuelo(Transform padre, string clave, Vector3 baseCentro, float tam, float yaw,
                              Forma forma = Forma.Parado, bool comprobarChoque = true)
    {
        GameObject go = Instanciar(clave, padre);
        if (!go) return null;
        var p = new Prop(Props[clave].archivo, tam, forma);
        Encajar(go, p, Vector3.forward, yaw, tam, 5f, 5f, 5f);
        Apoyar(go, baseCentro);
        if (!Limites(go, out Bounds b) || EnZonaLibre(b) || (comprobarChoque && Choca(b, go, 0.8f)))
        {
            Object.DestroyImmediate(go);
            return null;
        }
        // Los objetos grandes del suelo tienen colision (el jugador no los atraviesa).
        if (comprobarChoque && b.size.y > 0.5f)
        {
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.lossyScale;
            col.size = new Vector3(b.size.x / Mathf.Abs(s.x), b.size.y / Mathf.Abs(s.y), b.size.z / Mathf.Abs(s.z));
        }
        colocados++;
        return go;
    }

    // Orienta y escala el objeto: su lado mas largo a lo largo de "u" y que quepa en (alto, largo, fondo).
    static void Encajar(GameObject go, Prop p, Vector3 u, float yawExtra, float tam, float alto, float largo, float fondo)
    {
        go.transform.localScale = Vector3.one;
        Ejes(go, out Vector3 aLargo, out Vector3 aMedio, out Vector3 aCorto);

        Quaternion marco = Quaternion.LookRotation(u, Vector3.up);
        Quaternion orient;
        if (p.forma == Forma.Acostado)
        {
            // Lado largo a lo largo de u, el mas delgado hacia arriba (acostado).
            orient = Quaternion.Inverse(Quaternion.LookRotation(aLargo, aCorto));
        }
        else
        {
            // Parado tal como viene (Y arriba): el lado horizontal mas largo a lo largo de u.
            Bounds lb = LimitesLocales(go);
            orient = lb.size.x > lb.size.z ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        }
        go.transform.rotation = marco * Quaternion.Euler(0f, yawExtra, 0f) * orient;

        if (!Limites(go, out Bounds b)) return;
        Vector3 v = Vector3.Cross(Vector3.up, u);
        float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
        float k = tam / Mathf.Max(maxDim, 1e-4f);
        k = Mathf.Min(k, alto / Mathf.Max(b.size.y, 1e-4f));
        k = Mathf.Min(k, largo / Mathf.Max(Largo(b, u), 1e-4f));
        k = Mathf.Min(k, fondo / Mathf.Max(Largo(b, v), 1e-4f));
        go.transform.localScale = Vector3.one * k;
    }

    static void Escalar(GameObject go, float tam)
    {
        go.transform.localScale = Vector3.one;
        if (!Limites(go, out Bounds b)) return;
        go.transform.localScale = Vector3.one * (tam / Mathf.Max(b.size.x, b.size.y, b.size.z, 1e-4f));
    }

    // Deja la base del objeto (centro de abajo) en el punto dado.
    static void Apoyar(GameObject go, Vector3 baseCentro)
    {
        if (!Limites(go, out Bounds b)) return;
        go.transform.position += baseCentro - new Vector3(b.center.x, b.min.y, b.center.z);
    }

    // Ejes locales del objeto ordenados por tamano (en su espacio local, sin rotar).
    static void Ejes(GameObject go, out Vector3 largo, out Vector3 medio, out Vector3 corto)
    {
        Bounds lb = LimitesLocales(go);
        var ejes = new List<(float t, Vector3 d)> { (lb.size.x, Vector3.right), (lb.size.y, Vector3.up), (lb.size.z, Vector3.forward) };
        ejes.Sort((a, b) => b.t.CompareTo(a.t));
        largo = ejes[0].d; medio = ejes[1].d; corto = ejes[2].d;
    }

    static Bounds LimitesLocales(GameObject go)
    {
        Quaternion r = go.transform.rotation; Vector3 s = go.transform.localScale; Vector3 p = go.transform.position;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        Limites(go, out Bounds b);
        go.transform.SetPositionAndRotation(p, r);
        go.transform.localScale = s;
        return b;
    }

    // Choca con algo de la escena (paredes, muebles...) sin contar el piso ni a si mismo.
    static bool Choca(Bounds b, GameObject propio, float encoger)
    {
        Physics.SyncTransforms();
        Vector3 medio = b.extents * encoger;
        medio.y = Mathf.Max(0.01f, medio.y - 0.03f);
        foreach (Collider c in Physics.OverlapBox(b.center + Vector3.up * 0.03f, medio, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.transform.IsChildOf(propio.transform)) continue;
            if (c.name == "rt (6)") continue; // piso
            Debug.Log($"[Tienda] \"{propio.name}\" no se coloco en ({b.center.x:0.00}, {b.center.y:0.00}, {b.center.z:0.00}): choca con \"{RutaCompleta(c.transform)}\".");
            return true;
        }
        return false;
    }

    static string RutaCompleta(Transform t) => t.parent ? RutaCompleta(t.parent) + "/" + t.name : t.name;

    // ------------------------------------------------------------------ Instancias y materiales

    static GameObject Instanciar(string clave, Transform padre)
    {
        Prop p = Props[clave];
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Carpeta + p.archivo);
        if (!modelo) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        go.name = clave;
        go.transform.SetParent(padre, true);
        AplicarMateriales(go, p.archivo);
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        return go;
    }

    static void AplicarMateriales(GameObject go, string archivo)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        int libro = 0;
        foreach (Renderer r in rs)
        {
            Material[] mats = r.sharedMaterials;
            bool cambio = false;
            Object fuente = PrefabUtility.GetCorrespondingObjectFromSource(r);
            long id = 0;
            if (fuente) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(fuente, out string _, out id);
            for (int i = 0; i < mats.Length; i++)
            {
                string ruta = null;
                if (TablaMateriales.TryGetValue($"{archivo}|{id}|{i}", out string t)) ruta = t;
                else ruta = MaterialPorNombre(archivo, r.name, ref libro);
                Material m = ruta != null ? CargarMat(ruta) : null;
                if (m) { mats[i] = m; cambio = true; }
            }
            if (cambio) r.sharedMaterials = mats;
        }
    }

    static string MaterialPorNombre(string archivo, string pieza, ref int libro)
    {
        string n = pieza.ToLowerInvariant();
        if (archivo.StartsWith("boooks"))
        {
            if (n.Contains("page") || n.Contains("hoja") || n.Contains("paper")) return Carpeta + "crema.mat";
            return Carpeta + TapasLibros[(libro++ + rnd.Next(TapasLibros.Length)) % TapasLibros.Length] + ".mat";
        }
        if (archivo.StartsWith("kATANA"))
        {
            if (n.Contains("blade") || n.Contains("hoja") || n.Contains("filo")) return Carpeta + "metal claro.mat";
            if (n.Contains("guard") || n.Contains("tsuba")) return Carpeta + "cobre.mat";
            if (n.Contains("handle") || n.Contains("mango") || n.Contains("tsuka") || n.Contains("saya") || n.Contains("sheath")) return Carpeta + "negro.mat";
            return Carpeta + "metal.mat";
        }
        if (archivo.StartsWith("car"))
        {
            if (n.Contains("wheel") || n.Contains("rueda") || n.Contains("tire") || n.Contains("llanta")) return Carpeta + "negro.mat";
            if (n.Contains("glass") || n.Contains("window") || n.Contains("vidrio")) return Carpeta + "glass.mat";
            return Carpeta + "cobre.mat";
        }
        return null; // el resto queda con su material original
    }

    static Material CargarMat(string ruta)
    {
        if (!cacheMat.TryGetValue(ruta, out Material m))
        {
            m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            cacheMat[ruta] = m;
        }
        return m;
    }

    // ------------------------------------------------------------------ Utilidades

    static Renderer R(Transform raiz, string ruta)
    {
        Transform t = raiz.Find(ruta);
        return t ? t.GetComponent<Renderer>() : null;
    }

    static bool Limites(GameObject go, out Bounds b)
    {
        b = default; bool hay = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return hay;
    }

    static float Min(Bounds b, Vector3 eje) => Vector3.Dot(b.min, Abs(eje));
    static float Max(Bounds b, Vector3 eje) => Vector3.Dot(b.max, Abs(eje));
    static float Largo(Bounds b, Vector3 eje) => Vector3.Dot(b.size, Abs(eje));
    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static float Rango(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    static Transform Hijo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }
}
