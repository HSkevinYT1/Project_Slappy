using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Pasillos RECTOS desde la Zona Principal hacia cada zona, todos iguales
// (mismo ancho, alto y largo, tomando como modelo el pasillo hecho a mano hacia el Laberinto),
// y reacomodo de los escenarios para que su entrada quede justo al final de su pasillo.
//
// Cada zona tiene un objeto "Entrada Pasillo": su posicion es la entrada y su eje azul (forward)
// apunta hacia AFUERA de la zona. Si una entrada quedo mal, mueve/gira ese objeto y usa
// Tools > Zona Principal > Alinear zonas y crear pasillos.
public static class PasillosSetup
{
    // Medidas (iguales para todos los pasillos).
    const float LargoBase = 25f;        // como el camino rojo
    const float AnchoInterior = 8f;     // como el pasillo del Laberinto (~8 m entre paredes)
    const float AltoPared = 4.25f;
    const float GrosorPared = 1.5f;
    const float GrosorPiso = 0.3f;
    const float PasoAlargar = 5f;       // si una zona choca con otra, su pasillo se alarga de a 5 m
    const int MaxAlargues = 16;

    const string Version = "_pasillos v1";
    const string NombreAncla = "Entrada Pasillo";

    internal static void AutoAplicar(GameObject zp)
    {
        Transform pas = zp.transform.Find("Pasillos");
        if (pas && pas.Find(Version)) return;
        Crear(zp);
        Debug.Log("[Pasillos] Zonas alineadas y pasillos rectos creados. Guarda la escena (Ctrl+S). Ctrl+Z para deshacer.", zp);
    }

    [MenuItem("Tools/Zona Principal/Alinear zonas y crear pasillos")]
    static void Menu()
    {
        GameObject zp = GameObject.Find("Zona_Principal");
        if (!zp || !zp.transform.Find("Marcas Caminos"))
        {
            EditorUtility.DisplayDialog("Pasillos", "Falta Zona_Principal con sus \"Marcas Caminos\".", "OK");
            return;
        }
        Crear(zp);
        Selection.activeTransform = zp.transform.Find("Pasillos");
    }

    // ------------------------------------------------------------------

    static void Crear(GameObject zp)
    {
        Undo.IncrementCurrentGroup();
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Alinear zonas y crear pasillos");

        Transform raiz = zp.transform;
        Transform marcas = raiz.Find("Marcas Caminos");

        // Quitar lo anterior: pasarelas viejas y pasillos previos.
        foreach (string viejo in new[] { "Pasarelas", "Pasillos" })
        {
            Transform t = raiz.Find(viejo);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }

        // Piso de la zona principal.
        Transform piso = raiz.Find("piso") ? raiz.Find("piso") : raiz.Find("Piso");
        Bounds bPiso = piso && piso.TryGetComponent(out Renderer rp) ? rp.bounds : new Bounds(raiz.position, new Vector3(24f, 0f, 24f));
        float yPiso = bPiso.max.y;

        // El pasillo hecho a mano hacia el Laberinto: su piso se reemplaza por el pasillo uniforme.
        Transform pisoManual = raiz.Find("Piso 1 (1)");
        Material matPiso = pisoManual && pisoManual.TryGetComponent(out Renderer rpm) ? rpm.sharedMaterial
                         : piso && piso.TryGetComponent(out Renderer rp2) ? rp2.sharedMaterial : null;
        if (pisoManual && pisoManual.gameObject.activeSelf)
        {
            Undo.RecordObject(pisoManual.gameObject, "Desactivar piso manual");
            pisoManual.gameObject.SetActive(false);
        }

        GameObject lab = GameObject.Find("Laberinto");
        Material matPared = null;
        if (lab)
        {
            Transform p0 = lab.transform.Find("Paredes/Pared 0");
            if (p0 && p0.TryGetComponent(out Renderer rpa)) matPared = rpa.sharedMaterial;
        }
        Material matBorde = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/MarcaPiso.mat");

        var contenedor = new GameObject("Pasillos");
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear pasillos");
        contenedor.transform.SetParent(raiz, false);
        new GameObject(Version).transform.SetParent(contenedor.transform, false);

        // El Laberinto primero (es el modelo), despues el resto.
        var lista = new List<MarcaCamino>(marcas.GetComponentsInChildren<MarcaCamino>(true));
        lista.Sort((a, b) => (a.nombreDestino == "Laberinto" ? 0 : 1).CompareTo(b.nombreDestino == "Laberinto" ? 0 : 1));

        // Zonas ya ubicadas (para no encimarlas).
        var ocupadas = new List<KeyValuePair<string, Bounds>> { new KeyValuePair<string, Bounds>("Zona_Principal", bPiso) };

        foreach (MarcaCamino marca in lista)
        {
            float ang = marca.anguloSalida * Mathf.Deg2Rad;
            Vector3 d = raiz.TransformDirection(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang))).normalized;
            Vector3 centro = marca.transform.position;
            centro.y = yPiso;

            // Inicio del pasillo: borde del piso de la zona principal sobre el eje de la marca.
            float alBorde = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? bPiso.extents.x : bPiso.extents.z;
            Vector3 inicio = centro + d * alBorde;

            Transform zona = marca.zonaDestino;
            float largo = LargoBase;

            if (zona)
            {
                if (zona.name == "Laberinto") RecortarParedesEntradaLaberinto(zona, inicio, d);

                Transform ancla = ObtenerAncla(zona, inicio, d);
                if (!ancla)
                {
                    Debug.LogWarning($"[Pasillos] No se encontro la entrada de {zona.name}; se deja donde esta.", zona);
                }
                else
                {
                    Undo.RecordObject(zona, "Mover zona");
                    // Girar la zona para que su entrada mire hacia la zona principal.
                    Vector3 fuera = ancla.forward; fuera.y = 0f;
                    float giro = Vector3.SignedAngle(fuera.normalized, -d, Vector3.up);
                    if (Mathf.Abs(giro) > 0.5f) zona.RotateAround(ancla.position, Vector3.up, giro);

                    // Mover para que la entrada quede al final del pasillo (alargandolo si choca).
                    for (int intento = 0; intento <= MaxAlargues; intento++)
                    {
                        Vector3 fin = inicio + d * largo;
                        zona.position += fin - ancla.position;
                        if (!LimitesZona(zona, out Bounds bz)) break;
                        string choque = Choca(bz, ocupadas, inicio, d, largo);
                        if (choque == null) break;
                        if (intento == MaxAlargues)
                        {
                            Debug.LogWarning($"[Pasillos] {zona.name} sigue chocando con {choque} aun con un pasillo de {largo} m.", zona);
                            break;
                        }
                        largo += PasoAlargar;
                    }
                    if (LimitesZona(zona, out Bounds bFinal)) ocupadas.Add(new KeyValuePair<string, Bounds>(zona.name, bFinal));
                    Debug.Log($"[Pasillos] {zona.name}: girada {giro:0} grados, entrada en ({ancla.position.x:0.#}, {ancla.position.y:0.##}, {ancla.position.z:0.#}), pasillo de {largo} m.", zona);

                    if (zona.TryGetComponent(out LianasLaberinto lianas)) lianas.RegenerarVistaPrevia();
                }
            }

            ConstruirPasillo(contenedor.transform, marca, inicio, d, largo, matPiso, matPared, matBorde, zona ? null : "Entrada Zona Roja (pendiente)");
        }

        EditorSceneManager.MarkSceneDirty(zp.scene);
        Undo.CollapseUndoOperations(grupoUndo);
    }

    // Devuelve el nombre de lo que choca con la zona (o null).
    static string Choca(Bounds b, List<KeyValuePair<string, Bounds>> ocupadas, Vector3 inicio, Vector3 d, float largo)
    {
        Bounds b2 = b;
        b2.Expand(new Vector3(-1f, 0f, -1f)); // tolerancia
        foreach (var o in ocupadas)
        {
            Bounds ob = o.Value;
            if (b2.min.x < ob.max.x && b2.max.x > ob.min.x && b2.min.z < ob.max.z && b2.max.z > ob.min.z)
                return o.Key;
        }
        // Que la zona no tape el propio pasillo.
        Vector3 medio = inicio + d * (largo * 0.5f);
        Vector3 lado = Vector3.Cross(Vector3.up, d) * (AnchoInterior * 0.5f);
        var pasillo = new Bounds(medio, Vector3.zero);
        pasillo.Encapsulate(inicio + lado + d * 0.5f);
        pasillo.Encapsulate(inicio - lado + d * 0.5f);
        pasillo.Encapsulate(inicio + d * (largo - 1f) + lado);
        pasillo.Encapsulate(inicio + d * (largo - 1f) - lado);
        if (b2.min.x < pasillo.max.x && b2.max.x > pasillo.min.x && b2.min.z < pasillo.max.z && b2.max.z > pasillo.min.z)
            return "su propio pasillo";
        return null;
    }

    // ------------------------------------------------------------------ Entradas

    static Transform ObtenerAncla(Transform zona, Vector3 inicio, Vector3 d)
    {
        Transform ancla = zona.Find(NombreAncla);
        if (ancla) return ancla;

        Vector3 pos, fuera;
        if (zona.name == "Laberinto")
        {
            if (!EntradaLaberinto(zona, inicio, d, out pos)) return null;
            fuera = -d; // el Laberinto ya esta orientado hacia la zona principal
        }
        else
        {
            Transform puerta = zona.Find("Puerta Entrada");
            if (!puerta || !LimitesZona(zona, out Bounds bz)) return null;
            Bounds bp = puerta.TryGetComponent(out Renderer r) ? r.bounds : new Bounds(puerta.position, Vector3.one);
            Vector3 c = bp.center;

            // La entrada da hacia el borde del escenario mas cercano a la puerta,
            // y el pasillo termina justo en ese borde, frente a la puerta.
            var opciones = new[]
            {
                (dir: Vector3.right,   dist: bz.max.x - c.x),
                (dir: Vector3.left,    dist: c.x - bz.min.x),
                (dir: Vector3.forward, dist: bz.max.z - c.z),
                (dir: Vector3.back,    dist: c.z - bz.min.z),
            };
            var mejor = opciones[0];
            foreach (var o in opciones) if (o.dist < mejor.dist) mejor = o;
            fuera = mejor.dir;
            pos = c + fuera * Mathf.Max(0f, mejor.dist);
            pos.y = bp.min.y;
            Debug.Log($"[Pasillos] Entrada de {zona.name}: puerta a {mejor.dist:0.#} m del borde, mira hacia ({fuera.x:0}, {fuera.z:0}).", zona);
        }

        var go = new GameObject(NombreAncla);
        Undo.RegisterCreatedObjectUndo(go, "Crear entrada");
        go.transform.SetParent(zona, true);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fuera, Vector3.up));
        return go.transform;
    }

    // Centro de la abertura entre "Pared 0" y "Pared 1", en el borde del piso del Laberinto.
    static bool EntradaLaberinto(Transform lab, Vector3 inicio, Vector3 d, out Vector3 pos)
    {
        pos = default;
        Transform piso = lab.Find("Piso");
        if (!piso || !piso.TryGetComponent(out Renderer rpiso)) return false;
        Bounds bp = rpiso.bounds;
        float borde = DistanciaMinima(bp, inicio, d);

        Vector3 lado = Vector3.Cross(Vector3.up, d);
        Transform p0 = lab.Find("Paredes/Pared 0"), p1 = lab.Find("Paredes/Pared 1");
        float lateral;
        if (p0 && p1 && p0.TryGetComponent(out Renderer r0) && p1.TryGetComponent(out Renderer r1))
        {
            // Caras interiores de las dos paredes que forman la entrada.
            float c0 = Vector3.Dot(r0.bounds.center - inicio, lado), c1 = Vector3.Dot(r1.bounds.center - inicio, lado);
            float e0 = Mathf.Abs(Vector3.Dot(r0.bounds.extents, Abs(lado))), e1 = Mathf.Abs(Vector3.Dot(r1.bounds.extents, Abs(lado)));
            float interior0 = c0 < c1 ? c0 + e0 : c0 - e0;
            float interior1 = c1 < c0 ? c1 + e1 : c1 - e1;
            lateral = (interior0 + interior1) * 0.5f;
        }
        else lateral = Vector3.Dot(bp.center - inicio, lado);

        pos = inicio + d * borde + lado * lateral;
        pos.y = bp.max.y;
        return true;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static float DistanciaMinima(Bounds b, Vector3 desde, Vector3 d)
    {
        float min = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, b.center.y, (i & 2) == 0 ? b.min.z : b.max.z);
            min = Mathf.Min(min, Vector3.Dot(c - desde, d));
        }
        return min;
    }

    // Las paredes del Laberinto que se alargaron a mano hasta la zona principal se recortan
    // al borde del piso del Laberinto (el pasillo uniforme las reemplaza afuera).
    static void RecortarParedesEntradaLaberinto(Transform lab, Vector3 inicio, Vector3 d)
    {
        Transform piso = lab.Find("Piso");
        if (!piso || !piso.TryGetComponent(out Renderer rpiso)) return;
        float borde = DistanciaMinima(rpiso.bounds, inicio, d);

        foreach (string nombre in new[] { "Paredes/Pared 0", "Paredes/Pared 1" })
        {
            Transform p = lab.Find(nombre);
            if (!p || !p.TryGetComponent(out Renderer r)) continue;
            Bounds b = r.bounds;
            float cerca = DistanciaMinima(b, inicio, d);
            float lejos = Vector3.Dot(b.center - inicio, d) + Mathf.Abs(Vector3.Dot(b.extents, Abs(d)));
            if (cerca >= borde - 0.05f || lejos <= borde) continue; // ya esta recortada

            // Eje local de la pared que va en la direccion del pasillo.
            Vector3 ejeLocal = Eje(p, d, out int indice);
            float largoActual = lejos - cerca;
            float largoNuevo = lejos - borde;
            Undo.RecordObject(p, "Recortar pared del Laberinto");
            Vector3 s = p.localScale;
            s[indice] *= largoNuevo / largoActual;
            p.localScale = s;
            p.position += d * ((largoActual - largoNuevo) * 0.5f);
            Debug.Log($"[Pasillos] {nombre} recortada de {largoActual:0.#} m a {largoNuevo:0.#} m (empieza en el borde del Laberinto).", p);
        }
    }

    static Vector3 Eje(Transform t, Vector3 dir, out int indice)
    {
        float x = Mathf.Abs(Vector3.Dot(t.right, dir)), y = Mathf.Abs(Vector3.Dot(t.up, dir)), z = Mathf.Abs(Vector3.Dot(t.forward, dir));
        if (x >= y && x >= z) { indice = 0; return t.right; }
        if (y >= z) { indice = 1; return t.up; }
        indice = 2; return t.forward;
    }

    internal static bool LimitesZona(Transform zona, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in zona.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.gameObject.hideFlags != HideFlags.None) continue; // vistas previas generadas
            if (r.gameObject.name == "Mar") continue;                // el mar de la zona roja no cuenta como "zona"
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return hay;
    }

    // ------------------------------------------------------------------ Geometria

    static void ConstruirPasillo(Transform contenedor, MarcaCamino marca, Vector3 inicio, Vector3 d, float largo,
                                 Material matPiso, Material matPared, Material matBorde, string nombreFinal)
    {
        var grupo = new GameObject("Pasillo - " + marca.nombreDestino);
        Undo.RegisterCreatedObjectUndo(grupo, "Crear pasillo");
        grupo.transform.SetParent(contenedor, false);
        grupo.transform.SetPositionAndRotation(inicio, Quaternion.LookRotation(d, Vector3.up));
        Transform g = grupo.transform;

        // En el espacio local del pasillo: z = a lo largo, x = a lo ancho, y = arriba.
        float anchoTotal = AnchoInterior + GrosorPared * 2f;
        Cubo(g, "Piso", new Vector3(0f, -GrosorPiso * 0.5f, largo * 0.5f), new Vector3(anchoTotal, GrosorPiso, largo), matPiso, true);
        for (int lado = -1; lado <= 1; lado += 2)
        {
            float x = lado * (AnchoInterior * 0.5f + GrosorPared * 0.5f);
            Cubo(g, lado < 0 ? "Pared Izq" : "Pared Der", new Vector3(x, AltoPared * 0.5f, largo * 0.5f),
                 new Vector3(GrosorPared, AltoPared, largo), matPared, true);
        }

        // Lineas brillantes del color de la marca (continuan las lineas del piso de la zona principal).
        var bordes = new List<Renderer>();
        for (int lado = -1; lado <= 1; lado += 2)
        {
            var linea = Cubo(g, lado < 0 ? "Linea Izq" : "Linea Der", new Vector3(lado * marca.mitadPasillo, 0.012f, largo * 0.5f),
                             new Vector3(marca.anchoLinea, 0.02f, largo), matBorde, false);
            var r = linea.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bordes.Add(r);
        }

        if (nombreFinal != null)
        {
            var fin = new GameObject(nombreFinal);
            Undo.RegisterCreatedObjectUndo(fin, "Crear fin de pasillo");
            fin.transform.SetParent(g, false);
            fin.transform.localPosition = new Vector3(0f, 0f, largo);
        }

        Undo.RecordObject(marca, "Asignar lineas");
        marca.extras.RemoveAll(r => !r);
        marca.extras.AddRange(bordes);
        EditorUtility.SetDirty(marca);
        marca.SetEncendida(marca.Encendida);
    }

    static GameObject Cubo(Transform padre, string nombre, Vector3 posLocal, Vector3 escala, Material mat, bool colision)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = escala;
        if (!colision) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        if (colision) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }
}
