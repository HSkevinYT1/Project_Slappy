using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Texturas con mapa de normales para la Zona Roja (faro, isla, arboles) y las Cloacas.
// - Cada material se reemplaza por una copia texturizada que conserva su color (los originales no se tocan,
//   asi lo que use esos materiales en otros sitios queda igual).
// - Las piezas reciben una copia de su malla con UV en METROS (proyectadas segun hacia donde mira cada cara),
//   asi la textura mide lo mismo en un bloque chico que en uno enorme y no se estira.
// Se aplica sola cuando encuentra materiales sin texturizar (por ejemplo despues de reconstruir la Zona Roja).
// Rehacer: Tools > Texturas > Zona Roja y Cloacas. Ctrl+Z deshace. Despues guarda la escena.
[InitializeOnLoad]
public static class TexturasZonasSetup
{
    const string Carpeta = "Assets/TexturasZonas";
    const string CarpetaTex = Carpeta + "/Texturas";
    const string CarpetaMat = Carpeta + "/Materiales";
    const string CarpetaMallas = Carpeta + "/Mallas";
    const string Prefijo = "TZ_";

    // material original -> (tipo de textura, metros por repeticion, suavidad, metalico, fuerza normal)
    struct Def { public string tipo; public float metros, suavidad, metal, normal; }

    static readonly Dictionary<string, Def> ZonaRoja = new Dictionary<string, Def>
    {
        { "FaroBlanco",  new Def { tipo = "pintura",  metros = 2.5f, suavidad = 0.25f, normal = 1.2f } },
        { "FaroRojo",    new Def { tipo = "pintura",  metros = 2.5f, suavidad = 0.28f, normal = 1.2f } },
        { "Madera",      new Def { tipo = "tablones", metros = 2f,   suavidad = 0.3f,  normal = 1.4f } },
        { "Metal",       new Def { tipo = "metal",    metros = 1.5f, suavidad = 0.45f, metal = 0.6f, normal = 1.3f } },
        { "Roca",        new Def { tipo = "roca",     metros = 4f,   suavidad = 0.15f, normal = 2f } },
        { "Pasto",       new Def { tipo = "pasto",    metros = 4f,   suavidad = 0.1f,  normal = 0.5f } },
        { "ArbolTronco", new Def { tipo = "corteza",  metros = 1.5f, suavidad = 0.1f,  normal = 1.6f } },
        { "ArbolHojas",  new Def { tipo = "agujas",   metros = 1f,   suavidad = 0.15f, normal = 1.2f } },
    };

    static readonly Dictionary<string, Def> Cloacas = new Dictionary<string, Def>
    {
        { "CloacaConcreto", new Def { tipo = "concreto", metros = 2.5f, suavidad = 0.25f, normal = 1.4f } },
        { "CloacaMetal",    new Def { tipo = "metal",    metros = 1.5f, suavidad = 0.45f, metal = 0.5f, normal = 1.3f } },
        { "CloacaPiso",     new Def { tipo = "losas",    metros = 3f,   suavidad = 0.45f, normal = 1.5f } },
    };

    static TexturasZonasSetup()
    {
        // Despues de las herramientas que construyen las zonas (la Zona Roja se reconstruye con delayCall).
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Falta("Zona Roja", ZonaRoja) || Falta("Cloacas", Cloacas)) Aplicar();
    }

    static bool Falta(string raiz, Dictionary<string, Def> defs)
    {
        GameObject g = GameObject.Find(raiz);
        if (!g) return false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
                if (m && (defs.ContainsKey(m.name) || (m.name.StartsWith(Prefijo) && defs.ContainsKey(m.name.Substring(Prefijo.Length)) && !m.GetTexture("_BaseMap"))))
                    return true;
        return false;
    }

    [MenuItem("Tools/Texturas/Zona Roja y Cloacas")]
    static void Aplicar()
    {
        var cache = new Dictionary<Material, Material>();
        int a = Texturizar("Zona Roja", ZonaRoja, cache), b = Texturizar("Cloacas", Cloacas, cache);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Texturas] Zona Roja: {a} piezas, Cloacas: {b} piezas con textura y mapa de normales. Guarda la escena (Ctrl+S).");
    }

    static int Texturizar(string nombreRaiz, Dictionary<string, Def> defs, Dictionary<Material, Material> cache)
    {
        GameObject raiz = GameObject.Find(nombreRaiz);
        if (!raiz) return 0;
        int n = 0;
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool cambia = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (!m) continue;
                // Ya texturizado: se vuelve a generar desde el material original (por si cambiaron las texturas)
                if (m.name.StartsWith(Prefijo) && defs.ContainsKey(m.name.Substring(Prefijo.Length))) m = Original(m.name.Substring(Prefijo.Length)) ?? m;
                if (!defs.TryGetValue(m.name, out Def d)) continue;
                if (!cache.TryGetValue(m, out Material nuevo)) cache[m] = nuevo = CrearMaterial(m, d);
                mats[i] = nuevo;
                cambia = true;
            }
            if (!cambia) continue;
            Undo.RecordObject(r, "Texturas");
            r.sharedMaterials = mats;
            if (r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh)
            {
                Mesh uv = MallaUVMetros(mf);
                if (uv && uv != mf.sharedMesh) { Undo.RecordObject(mf, "UV en metros"); mf.sharedMesh = uv; }
            }
            n++;
        }
        if (n > 0) EditorSceneManager.MarkSceneDirty(raiz.scene);
        return n;
    }

    static Material Original(string nombre)
    {
        foreach (string g in AssetDatabase.FindAssets(nombre + " t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if (m && m.name == nombre) return m;
        }
        return null;
    }

    // ------------------------------------------------------------------ Mallas con UV en metros

    static readonly Dictionary<string, Mesh> mallas = new Dictionary<string, Mesh>();

    static Mesh MallaUVMetros(MeshFilter mf)
    {
        Mesh orig = mf.sharedMesh;
        if (orig.name.StartsWith("UVM_")) return orig;
        Vector3 s = mf.transform.lossyScale;
        // Redondeada a 5 cm: piezas casi iguales (y los arboles, con escalas al azar) comparten malla
        s = new Vector3(Redondear(s.x), Redondear(s.y), Redondear(s.z));
        string clave = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}_{1:0.00}x{2:0.00}x{3:0.00}",
                                     Limpio(orig.name), s.x, s.y, s.z);
        if (mallas.TryGetValue(clave, out Mesh ya) && ya) return ya;
        string ruta = $"{CarpetaMallas}/UVM_{clave}.asset";
        ya = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (!ya)
        {
            if (!orig.isReadable) return orig;
            Mesh m = Object.Instantiate(orig);
            m.name = "UVM_" + clave;
            Vector3[] v = m.vertices, nr = m.normals;
            if (nr == null || nr.Length != v.Length) { m.RecalculateNormals(); nr = m.normals; }
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = Vector3.Scale(v[i], s);
                Vector3 n = new Vector3(nr[i].x / Mathf.Max(s.x, 1e-4f), nr[i].y / Mathf.Max(s.y, 1e-4f), nr[i].z / Mathf.Max(s.z, 1e-4f));
                Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                if (a.x >= a.y && a.x >= a.z) uv[i] = new Vector2(p.z, p.y);   // caras que miran a los lados: V vertical
                else if (a.z >= a.y) uv[i] = new Vector2(p.x, p.y);
                else uv[i] = new Vector2(p.x, p.z);                           // suelos y techos
            }
            m.uv = uv;
            m.RecalculateTangents();
            Directory.CreateDirectory(CarpetaMallas);
            AssetDatabase.CreateAsset(m, ruta);
            ya = m;
        }
        mallas[clave] = ya;
        return ya;
    }

    static float Redondear(float x) { x = Mathf.Abs(x); return x < 0.2f ? Mathf.Round(x * 200f) / 200f : Mathf.Max(0.05f, Mathf.Round(x * 20f) / 20f); }

    static string Limpio(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace(' ', '_'); }

    // ------------------------------------------------------------------ Materiales

    static Material CrearMaterial(Material orig, Def d)
    {
        Color b = orig.HasProperty("_BaseColor") ? orig.GetColor("_BaseColor") : Color.gray;
        string nombre = orig.name;
        SotanoAcabados.Muestra f = d.tipo switch
        {
            "pintura" => (float u, float v, out Color c, out float h) => Pintura(u, v, b, out c, out h),
            "tablones" => SotanoAcabados.Tablones,
            "metal" => (float u, float v, out Color c, out float h) => MetalOxidado(u, v, b, out c, out h),
            "roca" => (float u, float v, out Color c, out float h) => Roca(u, v, b, out c, out h),
            "pasto" => (float u, float v, out Color c, out float h) => Pasto(u, v, b, out c, out h),
            "corteza" => (float u, float v, out Color c, out float h) => Corteza(u, v, b, out c, out h),
            "agujas" => (float u, float v, out Color c, out float h) => Agujas(u, v, b, out c, out h),
            "concreto" => (float u, float v, out Color c, out float h) => Concreto(u, v, b, out c, out h),
            _ => (float u, float v, out Color c, out float h) => Losas(u, v, b, out c, out h),
        };
        var acabado = SotanoAcabados.Generar(nombre, f, d.normal, d.metros, d.suavidad, CarpetaTex, Prefijo);

        string ruta = $"{CarpetaMat}/{Prefijo}{nombre}.mat";
        Directory.CreateDirectory(CarpetaMat);
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo) m = new Material(orig) { name = Prefijo + nombre };
        Vector2 rep = Vector2.one / d.metros; // las UV estan en metros
        m.SetTexture("_BaseMap", acabado.color);
        m.SetTextureScale("_BaseMap", rep);
        m.SetColor("_BaseColor", Color.white); // el color ya va en la textura
        m.SetTexture("_BumpMap", acabado.normal);
        m.SetTextureScale("_BumpMap", rep);
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", d.suavidad);
        m.SetFloat("_Metallic", d.metal);
        m.enableInstancing = true;
        if (nuevo) AssetDatabase.CreateAsset(m, ruta);
        else EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ Texturas (u, v en 0-1, repetibles)

    static float R(float u, float v, float esc, float sem)
    {
        float a = Mathf.PerlinNoise(u * esc + sem, v * esc + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * esc + sem, v * esc + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * esc + sem, (v - 1f) * esc + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * esc + sem, (v - 1f) * esc + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // Ruido estirado (escalas distintas en U y V), repetible.
    static float R2(float u, float v, float escU, float escV, float sem)
    {
        float a = Mathf.PerlinNoise(u * escU + sem, v * escV + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * escU + sem, v * escV + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * escU + sem, (v - 1f) * escV + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * escU + sem, (v - 1f) * escV + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Fbm(float u, float v, float esc, float sem) =>
        R(u, v, esc, sem) * 0.5f + R(u, v, esc * 2f, sem + 3f) * 0.3f + R(u, v, esc * 4f, sem + 7f) * 0.2f;

    static float Suave(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
    static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f) * 43758.5453f, 1f);

    // Grieta fina e irregular solo en algunas zonas (0-1).
    static float Grieta(float u, float v, float sem, float umbral)
    {
        float g = R(u, v, 6f, sem) * 0.6f + R(u, v, 19f, sem + 5f) * 0.3f + R(u, v, 47f, sem + 13f) * 0.1f;
        float linea = 1f - Mathf.Clamp01(Mathf.Abs(g - 0.5f) * 110f);
        return linea * Mathf.Clamp01((R(u, v, 3f, sem + 33f) - umbral) * 6f);
    }

    // Pintura vieja sobre muro: grano, suciedad que escurre, partes descascaradas con el revoque a la vista.
    static void Pintura(float u, float v, Color b, out Color c, out float h)
    {
        float grano = R(u, v, 110f, 1f), nubes = Fbm(u, v, 4f, 2f);
        float escurre = R2(u, v, 22f, 2f, 5f) * Suave(0.3f, 0.9f, R(u, v, 3f, 8f));
        float pelado = Suave(0.66f, 0.7f, Fbm(u, v, 5f, 9f));
        float bordePelado = Suave(0.62f, 0.66f, Fbm(u, v, 5f, 9f)) - pelado;
        Color pintura = b * (0.88f + grano * 0.1f + nubes * 0.12f) * (1f - escurre * 0.28f);
        Color revoque = new Color(0.52f, 0.5f, 0.46f) * (0.85f + grano * 0.2f);
        c = Color.Lerp(pintura, revoque, pelado);
        c *= 1f - bordePelado * 0.25f;
        c *= 1f - Grieta(u, v, 4f, 0.62f) * 0.5f;
        h = 0.65f + grano * 0.08f + bordePelado * 0.15f - pelado * 0.35f - Grieta(u, v, 4f, 0.62f) * 0.4f;
    }

    // Laminas de metal remachadas con oxido que chorrea.
    static void MetalOxidado(float u, float v, Color b, out Color c, out float h)
    {
        float px = Mathf.Repeat(u * 2f, 1f), py = Mathf.Repeat(v * 2f, 1f);
        float junta = Suave(0f, 0.02f, Mathf.Min(Mathf.Min(px, 1f - px), Mathf.Min(py, 1f - py)));
        // Remaches cerca de las juntas
        float rx = Mathf.Repeat(u * 16f, 1f) - 0.5f, ry = Mathf.Min(py, 1f - py) * 8f - 0.25f;
        float remache = (1f - Suave(0.12f, 0.2f, Mathf.Sqrt(rx * rx + ry * ry))) * (ry < 0.5f ? 1f : 0f);
        float rx2 = Mathf.Min(px, 1f - px) * 8f - 0.25f, ry2 = Mathf.Repeat(v * 16f, 1f) - 0.5f;
        remache = Mathf.Max(remache, 1f - Suave(0.12f, 0.2f, Mathf.Sqrt(rx2 * rx2 + ry2 * ry2)));

        float grano = R(u, v, 140f, 2f), manchas = Fbm(u, v, 5f, 3f);
        float chorro = R2(u, v, 26f, 2.5f, 6f);
        float oxido = Suave(0.5f, 0.62f, manchas * 0.75f + chorro * 0.35f);
        Color metal = b * (0.8f + grano * 0.25f) + new Color(0.03f, 0.03f, 0.03f);
        Color oxi = Color.Lerp(new Color(0.36f, 0.17f, 0.07f), new Color(0.22f, 0.11f, 0.05f), R(u, v, 30f, 9f)) * (0.85f + grano * 0.3f);
        c = Color.Lerp(metal, oxi, oxido);
        c *= Mathf.Lerp(0.55f, 1f, junta);
        c = Color.Lerp(c, c * 1.15f, remache);
        h = 0.55f * junta + remache * 0.4f + oxido * grano * 0.12f;
    }

    // Roca: varias escalas de ruido y grietas.
    static void Roca(float u, float v, Color b, out Color c, out float h)
    {
        float f = Fbm(u, v, 5f, 1f), fino = R(u, v, 60f, 2f);
        float grieta = Mathf.Max(Grieta(u, v, 6f, 0.45f), Grieta(u + 0.37f, v, 11f, 0.5f));
        c = b * (0.65f + f * 0.6f + fino * 0.15f) * (1f - grieta * 0.6f);
        c = Color.Lerp(c, new Color(0.2f, 0.24f, 0.14f), Suave(0.68f, 0.8f, R(u, v, 7f, 9f)) * 0.5f); // musgo
        h = f * 0.8f + fino * 0.15f - grieta * 0.5f;
    }

    // Pasto: hojitas muy finas, variacion de tono y algunos parches de tierra.
    static void Pasto(float u, float v, Color b, out Color c, out float h)
    {
        float hojas = R2(u, v, 220f, 110f, 1f) * 0.5f + R(u, v, 60f, 2f) * 0.5f;
        float tono = Fbm(u, v, 2f, 3f);
        float tierra = Suave(0.66f, 0.74f, Fbm(u, v, 3f, 5f));
        Color pasto = b * (0.85f + hojas * 0.25f) * (0.75f + tono * 0.5f);
        pasto = Color.Lerp(pasto, pasto * new Color(1.15f, 1.1f, 0.7f), Suave(0.55f, 0.75f, tono) * 0.5f); // zonas secas
        Color suelo = new Color(0.2f, 0.15f, 0.09f) * (0.8f + R(u, v, 80f, 7f) * 0.4f);
        c = Color.Lerp(pasto, suelo, tierra * (1f - hojas * 0.5f));
        h = hojas * 0.5f + tono * 0.3f - tierra * 0.2f;
    }

    // Corteza de pino: placas verticales separadas por grietas profundas.
    static void Corteza(float u, float v, Color b, out Color c, out float h)
    {
        float desv = R2(u, v, 4f, 1f, 1f) * 2.2f;
        float x = u * 9f + desv;
        int col = Mathf.FloorToInt(x);
        float fx = Mathf.Repeat(x, 1f);
        // Las grietas no son rectas: el ancho de cada placa cambia a lo alto
        float ancho = 0.12f + R2(u, v, 9f, 3f, col * 1.3f) * 0.22f;
        float placa = Suave(0f, ancho, Mathf.Min(fx, 1f - fx));
        float corte = R2(u, v, 9f, 5f, 11f);                   // cortes horizontales sueltos, no en cuadricula
        placa *= 1f - (1f - Mathf.Clamp01(Mathf.Abs(corte - 0.5f) * 25f)) * 0.7f;
        float fibra = R2(u, v, 60f, 5f, 3f), musgo = Suave(0.62f, 0.78f, R(u, v, 4f, 17f));
        Color tronco = b * (0.7f + fibra * 0.35f) * (0.85f + Hash(col) * 0.25f);
        tronco = Color.Lerp(tronco, new Color(0.13f, 0.17f, 0.08f), musgo * 0.5f);
        c = Color.Lerp(b * 0.28f, tronco, placa);
        h = placa * (0.75f + fibra * 0.25f);
    }

    // Agujas de pino: rayitas finas en dos direcciones con brillos y sombras.
    static void Agujas(float u, float v, Color b, out Color c, out float h)
    {
        float a1 = R2(u + v, v - u, 140f, 20f, 1f), a2 = R2(u - v, u + v, 140f, 20f, 2f);
        float agujas = Mathf.Max(a1, a2);
        float masa = Fbm(u, v, 5f, 3f);
        c = b * (0.6f + agujas * 0.45f) * (0.8f + masa * 0.3f);
        c = Color.Lerp(c, c * new Color(0.85f, 1.1f, 0.9f), Suave(0.6f, 0.8f, masa) * 0.4f);
        h = agujas * 0.7f + masa * 0.3f;
    }

    // Concreto de cloaca: poros, manchas de humedad, suciedad que escurre desde arriba y grietas.
    static void Concreto(float u, float v, Color b, out Color c, out float h)
    {
        float poros = R(u, v, 150f, 1f) * 0.5f + R(u, v, 50f, 2f) * 0.5f;
        float humedad = Suave(0.55f, 0.8f, Fbm(u, v, 3f, 4f));
        float escurre = R2(u, v, 30f, 1.5f, 6f) * Suave(0.35f, 0.85f, R(u, v, 2f, 9f));
        float encofrado = Suave(0f, 0.012f, Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - 0.5f) - 0.488f); // lineas del molde
        float grieta = Grieta(u, v, 12f, 0.55f);
        Color con = b * (0.85f + poros * 0.25f);
        con *= 1f - humedad * 0.32f;
        con = Color.Lerp(con, con * new Color(0.75f, 0.85f, 0.65f), escurre * 0.6f); // verdin
        con *= 1f - grieta * 0.55f;
        con *= 1f - (1f - encofrado) * 0.15f;
        c = con;
        h = 0.6f + poros * 0.25f - grieta * 0.45f - (1f - encofrado) * 0.15f;
    }

    // Piso de cloaca: losas irregulares con juntas sucias y charcos.
    static void Losas(float u, float v, Color b, out Color c, out float h)
    {
        const int n = 3;
        float fy = v * n; int fila = Mathf.FloorToInt(fy);
        float fx = u * n + (fila % 2) * 0.5f; int col = Mathf.FloorToInt(fx);
        float bx = Mathf.Repeat(fx, 1f), by = Mathf.Repeat(fy, 1f);
        float junta = Suave(0.01f, 0.035f, Mathf.Min(Mathf.Min(bx, 1f - bx), Mathf.Min(by, 1f - by)));
        float tono = 0.85f + Hash(col * 13 + fila * 7) * 0.25f;
        float grano = R(u, v, 120f, 1f), charco = Suave(0.6f, 0.7f, Fbm(u, v, 3f, 5f));
        Color losa = b * tono * (0.85f + grano * 0.2f);
        losa *= 1f - charco * 0.35f;
        c = Color.Lerp(b * 0.35f, losa, junta);
        c *= 1f - Grieta(u, v, 8f, 0.6f) * 0.5f;
        h = junta * (0.7f + grano * 0.2f) * (1f - charco * 0.5f) - Grieta(u, v, 8f, 0.6f) * 0.4f;
    }
}
