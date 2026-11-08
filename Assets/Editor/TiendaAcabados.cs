using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Acabados de la Tienda:
// 1. Techo de tablas de madera con vigas (y lamparas colgantes si la tienda no tiene luces adentro).
// 2. Nada de colores solidos: paredes, piso, muebles, mesas y props con textura + mapa de normales.
//    - Los props usan COPIAS de sus materiales (mismo color + textura de detalle), los originales no se tocan.
//    - Se respetan vidrios, cuadros (ya tienen textura) y shaders especiales (pantalla CRT, contornos).
// 3. Paredes: tablas verticales de madera pintada de amarillo con un mapa de normales que coincide
//    con las tablas (el anterior era una imagen estirada una sola vez por pared y muy exagerada).
//
// Se aplica sola una vez. Rehacer: Tools > Tienda > Acabados (techo y texturas).
[InitializeOnLoad]
public static class TiendaAcabados
{
    const string Version = "_acabados tienda v2";
    const string CarpetaTex = "Assets/Tienda/Texturas";
    const string CarpetaMat = "Assets/Tienda/Materiales";
    const string CarpetaDetalle = "Assets/Tienda/Materiales/Detalle";
    static readonly Color ColorParedes = new Color(0.66f, 0.5f, 0.3f); // ocre

    static TiendaAcabados()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject t = GameObject.Find("Tienda");
        if (!t) return;
        Transform a = t.transform.Find("Acabados Tienda");
        if (a && a.Find(Version)) return;
        Aplicar();
    }

    [MenuItem("Tools/Tienda/Acabados (techo y texturas)")]
    static void Menu() => Aplicar();

    static readonly Dictionary<Material, Material> copias = new Dictionary<Material, Material>();

    static void Aplicar()
    {
        GameObject tGO = GameObject.Find("Tienda");
        if (!tGO) return;
        Transform tienda = tGO.transform;

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Acabados de la tienda");
        copias.Clear();

        Transform vieja = tienda.Find("Acabados Tienda");
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

        // ---- Texturas (se generan una vez) ----
        var pared = SotanoAcabados.Generar("ParedPintadaV2", ParedPintada, 1.0f, 1.2f, 0.25f, CarpetaTex, "Tienda");
        var mueble = SotanoAcabados.Generar("MaderaBarniz", MaderaBarniz, 0.7f, 1.0f, 0.45f, CarpetaTex, "Tienda");
        var mesa = SotanoAcabados.Generar("MaderaMesa", MaderaMesa, 0.7f, 1.0f, 0.35f, CarpetaTex, "Tienda");
        var piso = SotanoAcabados.Generar("PisoTablas", PisoTablas, 1.0f, 1.6f, 0.3f, CarpetaTex, "Tienda");
        var techo = SotanoAcabados.Generar("TechoTablas", TechoTablas, 1.0f, 1.6f, 0.15f, CarpetaTex, "Tienda");
        var dMadera = SotanoAcabados.Generar("DetalleMadera", DetalleMadera, 0.6f, 1f, 0.3f, CarpetaTex, "Tienda");
        var dMetal = SotanoAcabados.Generar("DetalleMetal", DetalleMetal, 0.5f, 1f, 0.5f, CarpetaTex, "Tienda");
        var dGrano = SotanoAcabados.Generar("DetalleGrano", DetalleGrano, 0.5f, 1f, 0.3f, CarpetaTex, "Tienda");

        // Color de las paredes: ocre (elegido entre varias opciones; antes era amarillo).
        Color amarillo = ColorParedes;

        int paredes = 0, muebles = 0, mesas = 0, pisos = 0, props = 0, omitidos = 0;
        foreach (Renderer r in tienda.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Material[] mats = r.sharedMaterials;
            bool cambio = false;
            Vector3 s = r.bounds.size;
            float largo = Mathf.Max(s.x, s.z), alto = s.y;
            float mayor = Mathf.Max(s.x, s.y, s.z), segundo = Segundo(s);
            string ruta = Ruta(r.transform, tienda);

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (!m) continue;
                string n = m.name;

                if (n == "material paredes" || n.StartsWith("TiendaPared"))
                {
                    // Cubo con coordenadas de textura en metros (tablas siempre del mismo ancho y verticales).
                    bool uv = UsarCuboMetros(r, true, pared.metros);
                    var nm = uv ? SotanoAcabados.Material("TiendaPared", pared, pared.metros, pared.metros, CarpetaMat, "")
                                : SotanoAcabados.Material("TiendaPared", pared, largo, alto, CarpetaMat, "");
                    nm.SetColor("_BaseColor", amarillo);
                    nm.SetFloat("_BumpScale", 1.2f);
                    mats[i] = nm; cambio = true; paredes++;
                }
                else if (n == "DoorMaterial" || n.StartsWith("TiendaMueble"))
                {
                    bool uv = UsarCuboMetros(r, false, mueble.metros);
                    mats[i] = uv ? SotanoAcabados.Material("TiendaMueble", mueble, mueble.metros, mueble.metros, CarpetaMat, "")
                                 : SotanoAcabados.Material("TiendaMueble", mueble, mayor, segundo, CarpetaMat, "");
                    cambio = true; muebles++;
                }
                else if (ruta.Contains("estantes/mesa") && (n == "New Material" || n == "New Material 1"))
                {
                    mats[i] = SotanoAcabados.Material("TiendaMesa", mesa, mayor, segundo, CarpetaMat, "");
                    cambio = true; mesas++;
                }
                else if (ruta.StartsWith("rt") && r.transform.parent == tienda)
                {
                    mats[i] = SotanoAcabados.Material("TiendaPiso", piso, s.x, s.z, CarpetaMat, "");
                    cambio = true; pisos++;
                }
                else
                {
                    Material copia = CopiaConDetalle(m, dMadera, dMetal, dGrano);
                    if (copia) { mats[i] = copia; cambio = true; props++; }
                    else omitidos++;
                }
            }
            if (cambio)
            {
                Undo.RecordObject(r, "Textura");
                r.sharedMaterials = mats;
            }
        }

        // ---- Techo con vigas ----
        var raiz = new GameObject("Acabados Tienda");
        Undo.RegisterCreatedObjectUndo(raiz, "Acabados tienda");
        new GameObject(Version).transform.SetParent(raiz.transform, false);
        Transform contParedes = tienda.Find("paredes");
        Bounds bp = default; bool hay = false;
        if (contParedes)
            foreach (Renderer r in contParedes.GetComponentsInChildren<Renderer>())
            { if (!hay) { bp = r.bounds; hay = true; } else bp.Encapsulate(r.bounds); }
        int luces = 0;
        if (hay)
        {
            float grosor = 0.18f;
            var losa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            losa.name = "Techo";
            losa.transform.SetParent(raiz.transform, false);
            losa.transform.position = new Vector3(bp.center.x, bp.max.y + grosor * 0.5f, bp.center.z);
            losa.transform.localScale = new Vector3(bp.size.x, grosor, bp.size.z);
            bool uvTecho = UsarCuboMetros(losa.GetComponent<Renderer>(), false, techo.metros);
            losa.GetComponent<Renderer>().sharedMaterial = uvTecho
                ? SotanoAcabados.Material("TiendaTecho", techo, techo.metros, techo.metros, CarpetaMat, "")
                : SotanoAcabados.Material("TiendaTecho", techo, bp.size.x, bp.size.z, CarpetaMat, "");

            // Vigas a lo largo del lado corto, repartidas a lo largo del lado largo.
            bool largoX = bp.size.x >= bp.size.z;
            float L = largoX ? bp.size.x : bp.size.z, C = largoX ? bp.size.z : bp.size.x;
            int nv = Mathf.FloorToInt(L / 2.2f);
            Material matViga = SotanoAcabados.Material("TiendaMueble", mueble, mueble.metros, mueble.metros, CarpetaMat, "");
            for (int i = 1; i <= nv; i++)
            {
                float f = (largoX ? bp.min.x : bp.min.z) + i * L / (nv + 1);
                var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
                v.name = "Viga";
                Object.DestroyImmediate(v.GetComponent<Collider>());
                v.transform.SetParent(raiz.transform, false);
                v.transform.position = largoX ? new Vector3(f, bp.max.y - 0.12f, bp.center.z) : new Vector3(bp.center.x, bp.max.y - 0.12f, f);
                v.transform.localScale = largoX ? new Vector3(0.18f, 0.24f, C - 0.02f) : new Vector3(C - 0.02f, 0.24f, 0.18f);
                UsarCuboMetros(v.GetComponent<Renderer>(), false, mueble.metros);
                v.GetComponent<Renderer>().sharedMaterial = matViga;
            }

            // Si no hay luces adentro, dos lamparas colgantes calidas (el techo tapa la luz de afuera).
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (bp.Contains(l.transform.position) && l.type != LightType.Directional) luces++;
            if (luces == 0)
            {
                for (int i = 0; i < 2; i++)
                {
                    float f = (i + 1f) / 3f;
                    Vector3 p = largoX ? new Vector3(Mathf.Lerp(bp.min.x, bp.max.x, f), bp.max.y, bp.center.z)
                                       : new Vector3(bp.center.x, bp.max.y, Mathf.Lerp(bp.min.z, bp.max.z, f));
                    Lampara(raiz.transform, p, dMetal);
                }
            }
        }

        raiz.transform.SetParent(tienda, true);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Tienda] Acabados: {paredes} paredes, {pisos} pisos, {muebles} piezas de muebles, {mesas} piezas de mesas y {props} materiales de props con textura " +
                  $"({omitidos} se dejaron igual: vidrios, cuadros, pantallas). Techo con vigas" +
                  (luces == 0 ? " y 2 lamparas colgantes (no habia luces adentro)." : $" ({luces} luces adentro, no se agregaron lamparas).") + " Guarda la escena (Ctrl+S).", raiz);
    }

    // Cambia la malla de un cubo por una igual pero con coordenadas de textura en METROS por cada cara
    // (dividido por "metros" = lo que mide una repeticion de la textura). Asi la textura no se estira
    // ni se aplasta en piezas delgadas. uHorizontal: en las caras verticales U va siempre en horizontal
    // (paredes: tablas verticales); si no, U va a lo largo del lado mas largo de cada cara (veta a lo largo).
    static bool UsarCuboMetros(Renderer r, bool uHorizontal, float metros)
    {
        if (!r || !r.TryGetComponent(out MeshFilter mf) || !mf.sharedMesh) return false;
        string nm = mf.sharedMesh.name;
        if (nm != "Cube" && !nm.StartsWith("CuboMetros")) return false;

        Vector3 s = r.transform.lossyScale;
        s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        string clave = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}x{1:0.00}x{2:0.00}_{3:0.0}_{4}",
                                     s.x, s.y, s.z, metros, uHorizontal ? "h" : "l");
        string carpeta = "Assets/Tienda/Mallas";
        Directory.CreateDirectory(carpeta);
        string ruta = $"{carpeta}/CuboMetros_{clave}.asset";
        Mesh malla = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (!malla)
        {
            var v = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            // Para cada cara: normal y dos ejes A, B con Cross(A, B) = normal (orden correcto de los triangulos).
            Vector3[,] caras =
            {
                { Vector3.right, Vector3.up, Vector3.forward }, { Vector3.left, Vector3.forward, Vector3.up },
                { Vector3.up, Vector3.forward, Vector3.right }, { Vector3.down, Vector3.right, Vector3.forward },
                { Vector3.forward, Vector3.right, Vector3.up }, { Vector3.back, Vector3.up, Vector3.right },
            };
            float Tam(Vector3 eje) => Mathf.Abs(Vector3.Dot(eje, s));
            for (int c = 0; c < 6; c++)
            {
                Vector3 n = caras[c, 0], A = caras[c, 1], B = caras[c, 2];
                // Que eje de la cara lleva la U.
                Vector3 ejeU;
                bool vertical = Mathf.Abs(n.y) < 0.5f;
                if (uHorizontal && vertical) ejeU = Mathf.Abs(A.y) > 0.5f ? B : A;
                else ejeU = Tam(A) >= Tam(B) ? A : B;
                Vector3 ejeV = ejeU == A ? B : A;
                int b0 = v.Count;
                Vector3[] esquinas = { n * 0.5f - A * 0.5f - B * 0.5f, n * 0.5f + A * 0.5f - B * 0.5f, n * 0.5f + A * 0.5f + B * 0.5f, n * 0.5f - A * 0.5f + B * 0.5f };
                foreach (Vector3 p in esquinas)
                {
                    v.Add(p);
                    nrm.Add(n);
                    float cu = Vector3.Dot(p, Abs(ejeU)) + 0.5f, cv = Vector3.Dot(p, Abs(ejeV)) + 0.5f;
                    uv.Add(new Vector2(cu * Tam(ejeU) / metros, cv * Tam(ejeV) / metros));
                }
                t.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 });
            }
            malla = new Mesh { name = "CuboMetros_" + clave };
            malla.SetVertices(v); malla.SetNormals(nrm); malla.SetUVs(0, uv); malla.SetTriangles(t, 0);
            malla.RecalculateTangents();
            malla.RecalculateBounds();
            AssetDatabase.CreateAsset(malla, ruta);
        }
        Undo.RecordObject(mf, "Malla con UV en metros");
        mf.sharedMesh = malla;
        return true;
    }

    static Vector3 Abs(Vector3 a) => new Vector3(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z));

    // Copia del material con su mismo color + textura de detalle y normales. Null = no se toca.
    static Material CopiaConDetalle(Material m, SotanoAcabados.Acabado madera, SotanoAcabados.Acabado metal, SotanoAcabados.Acabado grano)
    {
        if (copias.TryGetValue(m, out Material ya)) return ya;
        Material res = null;
        string n = m.name.ToLowerInvariant();
        bool lit = m.shader && m.shader.name == "Universal Render Pipeline/Lit";
        bool transparente = m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f;
        bool vidrio = n.Contains("glass") || n.Contains("vidrio") || n.Contains("glow");
        bool yaTiene = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap");
        bool esCopia = AssetDatabase.GetAssetPath(m).StartsWith(CarpetaDetalle) || AssetDatabase.GetAssetPath(m).StartsWith(CarpetaMat);
        if (lit && !transparente && !vidrio && !yaTiene && !esCopia)
        {
            SotanoAcabados.Acabado a = grano;
            if (n.Contains("metal") || n.Contains("cobre") || n.Contains("negro")) a = metal;
            else if (n.Contains("madera") || n.Contains("bronw") || n.Contains("marron") || n.Contains("wood") || n.Contains("mesa")) a = madera;

            Directory.CreateDirectory(CarpetaDetalle);
            string ruta = $"{CarpetaDetalle}/{Limpio(m.name)}_Detalle.mat";
            res = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            bool nuevo = !res;
            if (nuevo) res = new Material(m);
            res.SetTexture("_BaseMap", a.color);
            res.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
            res.SetTexture("_BumpMap", a.normal);
            res.SetTextureScale("_BumpMap", new Vector2(2f, 2f));
            res.SetFloat("_BumpScale", 0.7f);
            res.EnableKeyword("_NORMALMAP");
            if (nuevo) AssetDatabase.CreateAsset(res, ruta); else EditorUtility.SetDirty(res);
        }
        copias[m] = res;
        return res;
    }

    static void Lampara(Transform padre, Vector3 techo, SotanoAcabados.Acabado metal)
    {
        var l = new GameObject("Lampara colgante").transform;
        l.SetParent(padre, false);
        float cable = 0.7f;
        Material matMetal = SotanoAcabados.Material("TiendaLampara", metal, 0.5f, 0.5f, CarpetaMat, "");
        matMetal.SetColor("_BaseColor", new Color(0.18f, 0.2f, 0.17f));
        Pieza(l, PrimitiveType.Cylinder, "Cable", techo - Vector3.up * cable * 0.5f, new Vector3(0.012f, cable * 0.5f, 0.012f), matMetal);
        Pieza(l, PrimitiveType.Sphere, "Pantalla", techo - Vector3.up * (cable + 0.08f), new Vector3(0.42f, 0.2f, 0.42f), matMetal);
        var foco = Pieza(l, PrimitiveType.Sphere, "Foco", techo - Vector3.up * (cable + 0.16f), Vector3.one * 0.1f, null);
        var mf = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FocoTienda" };
        mf.SetColor("_BaseColor", new Color(1f, 0.92f, 0.75f));
        mf.SetColor("_EmissionColor", new Color(2f, 1.6f, 1f));
        mf.EnableKeyword("_EMISSION");
        string rutaF = CarpetaMat + "/FocoTienda.mat";
        var mfAsset = AssetDatabase.LoadAssetAtPath<Material>(rutaF);
        if (!mfAsset) { AssetDatabase.CreateAsset(mf, rutaF); mfAsset = mf; }
        foco.GetComponent<Renderer>().sharedMaterial = mfAsset;

        var luzGO = new GameObject("Luz");
        luzGO.transform.SetParent(l, false);
        luzGO.transform.position = techo - Vector3.up * (cable + 0.25f);
        var luz = luzGO.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.85f, 0.65f);
        luz.range = 10f;
        luz.intensity = 7f;
        luz.shadows = LightShadows.None; // la escena ya tiene muchas luces con sombra
    }

    static GameObject Pieza(Transform padre, PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala, Material mat)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        go.transform.localScale = escala;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static float Segundo(Vector3 s)
    {
        float a = s.x, b = s.y, c = s.z;
        float max = Mathf.Max(a, b, c), min = Mathf.Min(a, b, c);
        return a + b + c - max - min;
    }

    static string Ruta(Transform t, Transform raiz) => t == raiz || !t.parent ? "" : (t.parent == raiz ? t.name : Ruta(t.parent, raiz) + "/" + t.name);
    static string Limpio(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }

    // ------------------------------------------------------------------ Texturas

    static float Ruido(float u, float v, float esc, float sem)
    {
        float a = Mathf.PerlinNoise(u * esc + sem, v * esc + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * esc + sem, v * esc + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * esc + sem, (v - 1f) * esc + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * esc + sem, (v - 1f) * esc + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Suave(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
    static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f) * 43758.5453f, 1f);

    // Veta de madera a lo largo de U (lineas que varian con V).
    static float Veta(float u, float v, float sem, out float fibra)
    {
        float deform = Ruido(u, v, 3f, sem) * 3f + Ruido(u, v, 9f, sem + 5f) * 0.6f;
        float a = Mathf.Sin((v * 22f + deform) * Mathf.PI);
        fibra = Ruido(u, v, 60f, sem + 8f) * 0.5f + Ruido(u, v, 150f, sem + 3f) * 0.5f;
        return a * a;
    }

    // Paredes: 6 tablas verticales por repeticion, pintura clara (se tine con el amarillo del material),
    // juntas hundidas, pinceladas verticales y zonas donde la pintura se gasto y se ve la madera.
    static void ParedPintada(float u, float v, out Color c, out float h)
    {
        const int tablas = 6;
        float fx = u * tablas;
        int t = Mathf.FloorToInt(fx);
        float tx = Mathf.Repeat(fx, 1f);
        float junta = Suave(0.01f, 0.05f, Mathf.Min(tx, 1f - tx));
        float pincel = Ruido(u * 4f, v * 0.3f, 40f, t * 3.1f);                 // vetas de pincel verticales
        float gastado = Ruido(u, v, 2.5f, 17f) * 0.75f + Ruido(u, v, 11f, 4f) * 0.25f;
        float sinPintura = Suave(0.76f, 0.84f, gastado + Hash(t) * 0.04f) * 0.75f;
        float veta = Veta(v, u * tablas, t * 7f, out float fibra);              // veta vertical bajo la pintura

        Color pintura = new Color(0.97f, 0.96f, 0.93f) * (0.93f + pincel * 0.07f + Hash(t) * 0.04f);
        Color madera = new Color(0.55f, 0.4f, 0.26f) * (0.8f + veta * 0.25f);
        c = Color.Lerp(pintura, madera, sinPintura);
        c = Color.Lerp(c * 0.35f, c, junta);
        h = junta * (0.72f + pincel * 0.08f - sinPintura * 0.12f + veta * sinPintura * 0.1f);
    }

    static void MaderaBarniz(float u, float v, out Color c, out float h)
    {
        float veta = Veta(u, v, 2f, out float fibra);
        float nudo = Ruido(u, v, 4f, 13f);
        c = new Color(0.3f, 0.18f, 0.1f) * (0.8f + veta * 0.28f + fibra * 0.06f);
        if (nudo > 0.78f) c *= Mathf.Lerp(1f, 0.65f, (nudo - 0.78f) / 0.22f);
        h = 0.6f + veta * 0.2f + fibra * 0.08f;
    }

    static void MaderaMesa(float u, float v, out Color c, out float h)
    {
        float veta = Veta(u, v, 31f, out float fibra);
        float desgaste = Ruido(u, v, 2f, 21f);
        c = new Color(0.45f, 0.29f, 0.17f) * (0.82f + veta * 0.24f + fibra * 0.08f);
        if (desgaste > 0.6f) c *= Mathf.Lerp(1f, 1.15f, (desgaste - 0.6f) / 0.4f);
        h = 0.6f + veta * 0.22f + fibra * 0.1f;
    }

    // Piso: tablas largas (8 por repeticion) con uniones escalonadas, oscuras y gastadas.
    static void PisoTablas(float u, float v, out Color c, out float h)
    {
        const int tablas = 8;
        float fy = v * tablas;
        int fila = Mathf.FloorToInt(fy);
        float ty = Mathf.Repeat(fy, 1f);
        float fx = u + Hash(fila) * 0.7f;                                       // union de punta a otra altura en cada fila
        float tx = Mathf.Repeat(fx, 1f);
        float junta = Suave(0.01f, 0.06f, Mathf.Min(ty, 1f - ty)) * Suave(0.002f, 0.01f, Mathf.Min(tx, 1f - tx));
        float veta = Veta(u, v * tablas, fila * 4.7f, out float fibra);
        float tono = 0.85f + Hash(fila * 13) * 0.25f;
        float desgaste = Ruido(u, v, 3f, 40f);
        c = new Color(0.3f, 0.2f, 0.13f) * tono * (0.85f + veta * 0.2f + fibra * 0.08f);
        if (desgaste > 0.58f) c = Color.Lerp(c, c * 1.25f, (desgaste - 0.58f) / 0.42f);
        c = Color.Lerp(c * 0.3f, c, junta);
        h = junta * (0.7f + veta * 0.2f + fibra * 0.1f);
    }

    // Techo: tablas mas claras (6 por repeticion) con ranura.
    static void TechoTablas(float u, float v, out Color c, out float h)
    {
        const int tablas = 6;
        float fy = v * tablas;
        int fila = Mathf.FloorToInt(fy);
        float ty = Mathf.Repeat(fy, 1f);
        float junta = Suave(0.02f, 0.07f, Mathf.Min(ty, 1f - ty));
        float veta = Veta(u, v * tablas, fila * 2.3f, out float fibra);
        c = new Color(0.58f, 0.43f, 0.28f) * (0.85f + Hash(fila * 7) * 0.18f) * (0.86f + veta * 0.18f + fibra * 0.06f);
        c = Color.Lerp(c * 0.35f, c, junta);
        h = junta * (0.7f + veta * 0.2f);
    }

    // Texturas de detalle CASI BLANCAS (se multiplican por el color del prop).
    static void DetalleMadera(float u, float v, out Color c, out float h)
    {
        float veta = Veta(u, v, 51f, out float fibra);
        float k = 0.82f + veta * 0.15f + fibra * 0.05f;
        c = new Color(k, k, k);
        h = 0.5f + veta * 0.3f + fibra * 0.1f;
    }

    static void DetalleMetal(float u, float v, out Color c, out float h)
    {
        float cepillado = Ruido(u * 0.05f, v, 220f, 3f) * 0.6f + Ruido(u * 0.1f, v, 90f, 9f) * 0.4f; // rayas finas a lo largo
        float rayones = Mathf.Clamp01(1f - Mathf.Abs(Ruido(u, v, 7f, 19f) - 0.5f) * 80f) * Suave(0.6f, 0.7f, Ruido(u, v, 3f, 2f));
        float manchas = Ruido(u, v, 4f, 27f);
        float k = 0.86f + cepillado * 0.12f - rayones * 0.1f - Mathf.Clamp01(manchas - 0.65f) * 0.4f;
        c = new Color(k, k, k);
        h = 0.5f + cepillado * 0.25f - rayones * 0.3f;
    }

    static void DetalleGrano(float u, float v, out Color c, out float h)
    {
        float g = Ruido(u, v, 70f, 5f) * 0.6f + Ruido(u, v, 20f, 11f) * 0.4f;
        float m = Ruido(u, v, 3f, 33f);
        float k = 0.86f + g * 0.12f - Mathf.Clamp01(m - 0.68f) * 0.35f;
        c = new Color(k, k, k);
        h = 0.5f + g * 0.3f;
    }
}
