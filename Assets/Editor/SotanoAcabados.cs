using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Acabados del sotano: techo nuevo y materiales con textura + MAPA DE NORMALES para paredes, piso y techo.
// - Paredes: bloques de concreto con junta de mortero (en relieve).
// - Piso: losa de concreto con juntas, poros, manchas y grietas.
// - Techo: tablones de madera oscura con vetas.
// Las texturas se generan una vez en Assets/Sotano/Texturas (si se reemplazan por otras, se respetan).
// Cada superficie usa un material con la repeticion ajustada a su tamano (para que no se estire).
public static class SotanoAcabados
{
    const string CarpetaTex = "Assets/Sotano/Texturas";
    const string CarpetaMat = "Assets/Sotano/Materiales";
    const int Tam = 512;

    // Metros que cubre una repeticion de cada textura.
    const float MetrosPared = 1.2f, MetrosPiso = 2.0f, MetrosTecho = 1.6f;

    internal struct Acabado { public Texture2D color, normal; public float metros, suavidad, relieve; }

    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    internal static string Aplicar(Transform zotano, Transform raiz)
    {
        cache.Clear();
        Directory.CreateDirectory(CarpetaTex);
        Directory.CreateDirectory(CarpetaMat);

        var pared = Generar("Pared", Bloques, 0.9f, MetrosPared, 0.12f);
        var piso = Generar("Piso", Losa, 1.0f, MetrosPiso, 0.25f);
        var techoA = Generar("Techo", Tablones, 1.2f, MetrosTecho, 0.1f);

        Transform zona = zotano.Find("zona");
        if (!zona) return "no se encontro zotano/zona";

        int paredes = 0;
        Bounds cuarto = default; bool hay = false;
        foreach (Renderer r in zona.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLowerInvariant();
            Vector3 s = r.bounds.size;
            if (n.StartsWith("pared"))
            {
                // Largo = lado horizontal mas grande; alto = Y.
                float largo = Mathf.Max(s.x, s.z);
                Asignar(r, Material("Pared", pared, largo, s.y));
                paredes++;
                if (!hay) { cuarto = r.bounds; hay = true; } else cuarto.Encapsulate(r.bounds);
            }
            else if (n.StartsWith("piso"))
            {
                Asignar(r, Material("Piso", piso, s.x, s.z));
            }
        }
        if (!hay) return "no se encontraron las paredes";

        // ---- Techo: una losa que cubre el cuarto apoyada sobre las paredes ----
        Transform viejo = raiz.Find("Techo");
        if (viejo) Object.DestroyImmediate(viejo.gameObject);
        float grosor = 0.2f;
        var techo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        techo.name = "Techo";
        techo.transform.SetParent(raiz, false);
        techo.transform.position = new Vector3(cuarto.center.x, cuarto.max.y + grosor * 0.5f, cuarto.center.z);
        techo.transform.localScale = new Vector3(cuarto.size.x, grosor, cuarto.size.z);
        Asignar(techo.GetComponent<Renderer>(), Material("Techo", techoA, cuarto.size.x, cuarto.size.z));
        GameObjectUtility.SetStaticEditorFlags(techo, StaticEditorFlags.BatchingStatic);

        // Vigas de madera bajo el techo (dan profundidad), cada 1.8 m a lo ancho.
        var vigas = new GameObject("Vigas").transform;
        vigas.SetParent(raiz, false);
        Material matViga = Material("Viga", techoA, cuarto.size.z, 0.3f);
        int n2 = Mathf.FloorToInt(cuarto.size.x / 1.8f);
        for (int i = 1; i <= n2; i++)
        {
            float x = cuarto.min.x + i * cuarto.size.x / (n2 + 1);
            var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
            v.name = "Viga";
            Object.DestroyImmediate(v.GetComponent<Collider>());
            v.transform.SetParent(vigas, false);
            v.transform.position = new Vector3(x, cuarto.max.y - 0.11f, cuarto.center.z);
            v.transform.localScale = new Vector3(0.16f, 0.22f, cuarto.size.z - 0.02f);
            Asignar(v.GetComponent<Renderer>(), matViga);
        }
        return $"{paredes} paredes, piso y techo con textura y mapa de normales";
    }

    static void Asignar(Renderer r, Material m)
    {
        if (!r || !m) return;
        Undo.RecordObject(r, "Material del sotano");
        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) mats[i] = m;
        r.sharedMaterials = mats;
    }

    // Material con la repeticion para una superficie de (ancho x alto) metros (cacheado por tamano).
    internal static Material Material(string nombre, Acabado a, float ancho, float alto,
                                       string carpeta = CarpetaMat, string prefijo = "Sotano_")
    {
        Vector2 rep = new Vector2(Mathf.Max(1f, Mathf.Round(ancho / a.metros)), Mathf.Max(1f, Mathf.Round(alto / a.metros)));
        string clave = $"{nombre}_{rep.x:0}x{rep.y:0}";
        if (cache.TryGetValue(clave, out Material m)) return m;

        string ruta = $"{carpeta}/{prefijo}{clave}.mat";
        Directory.CreateDirectory(carpeta);
        m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", a.color);
        m.SetColor("_BaseColor", Color.white);
        m.SetTextureScale("_BaseMap", rep);
        m.SetTexture("_BumpMap", a.normal);
        m.SetTextureScale("_BumpMap", rep);
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", a.suavidad);
        m.SetFloat("_Metallic", 0f);
        if (nuevo) AssetDatabase.CreateAsset(m, ruta);
        else EditorUtility.SetDirty(m);
        cache[clave] = m;
        return m;
    }

    // ------------------------------------------------------------------ Generacion de texturas

    // Devuelve color y altura (0-1) de cada pixel.
    internal delegate void Muestra(float u, float v, out Color color, out float altura);

    internal static Acabado Generar(string nombre, Muestra f, float fuerzaNormal, float metros, float suavidad,
                                    string carpeta = CarpetaTex, string prefijo = "Sotano")
    {
        Directory.CreateDirectory(carpeta);
        string rutaC = $"{carpeta}/{prefijo}{nombre}.png", rutaN = $"{carpeta}/{prefijo}{nombre}_Normal.png";
        var color = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaC);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaN);
        // Se regeneran si no existen o si son de una version anterior del generador.
        // Para usar una textura propia: reemplazar el PNG y escribir "propia" en el campo User Data del importador.
        bool viejas = color && normal && EsVersionAnterior(rutaC);
        if (!color || !normal || viejas)
        {
            var cols = new Color[Tam * Tam];
            var alt = new float[Tam * Tam];
            for (int y = 0; y < Tam; y++)
            for (int x = 0; x < Tam; x++)
            {
                f((x + 0.5f) / Tam, (y + 0.5f) / Tam, out Color c, out float h);
                cols[y * Tam + x] = c;
                alt[y * Tam + x] = h;
            }
            // Normal en espacio tangente a partir de la altura (diferencias con vecinos, repetible).
            var nor = new Color[Tam * Tam];
            float k = fuerzaNormal * 6f;
            for (int y = 0; y < Tam; y++)
            for (int x = 0; x < Tam; x++)
            {
                float hl = alt[y * Tam + (x + Tam - 1) % Tam], hr = alt[y * Tam + (x + 1) % Tam];
                float hd = alt[((y + Tam - 1) % Tam) * Tam + x], hu = alt[((y + 1) % Tam) * Tam + x];
                Vector3 n = new Vector3((hl - hr) * k, (hd - hu) * k, 1f).normalized;
                nor[y * Tam + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            Guardar(rutaC, cols, false);
            Guardar(rutaN, nor, true);
            color = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaC);
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaN);
        }
        return new Acabado { color = color, normal = normal, metros = metros, suavidad = suavidad };
    }

    static void Guardar(string ruta, Color[] px, bool esNormal)
    {
        var tex = new Texture2D(Tam, Tam, TextureFormat.RGB24, false, esNormal);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = esNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.userData = MarcaVersion;
        imp.SaveAndReimport();
    }

    const string MarcaVersion = "generado-sotano-v2";

    static bool EsVersionAnterior(string ruta)
    {
        var imp = AssetImporter.GetAtPath(ruta);
        if (imp == null) return false;
        // La primera version no dejaba marca (vacio); "propia" = textura puesta a mano, no se toca.
        return imp.userData != MarcaVersion && (imp.userData.StartsWith("generado-sotano") || string.IsNullOrEmpty(imp.userData));
    }

    // smoothstep de shaders (0 antes de e0, 1 despues de e1). Ojo: Mathf.SmoothStep NO hace esto.
    static float Suave(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static float Ruido(float u, float v, float escala, float semilla) =>
        Mathf.PerlinNoise(u * escala + semilla, v * escala + semilla * 1.7f);

    // Ruido repetible (sin costura) mezclando 4 muestras desplazadas.
    static float RuidoRep(float u, float v, float escala, float semilla)
    {
        float a = Ruido(u, v, escala, semilla), b = Ruido(u - 1f, v, escala, semilla);
        float c = Ruido(u, v - 1f, escala, semilla), d = Ruido(u - 1f, v - 1f, escala, semilla);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // Bloques de concreto (3 x 6 por repeticion, aparejo trabado) con junta de mortero.
    static void Bloques(float u, float v, out Color c, out float h)
    {
        const int filas = 6, cols = 3;
        float fy = v * filas;
        int fila = Mathf.FloorToInt(fy);
        float fx = u * cols + ((fila % 2) * 0.5f);
        float bx = Mathf.Repeat(fx, 1f), by = Mathf.Repeat(fy, 1f);
        float junta = 0.035f;
        float dx = Mathf.Min(bx, 1f - bx) * 1f, dy = Mathf.Min(by, 1f - by) * 0.5f; // dy en proporcion (bloque 2:1)
        float borde = Mathf.Min(dx, dy * 2f);
        float esBloque = Suave(junta * 0.6f, junta * 1.4f, borde);

        int id = Mathf.FloorToInt(fx) * 31 + fila * 17;
        float tono = 0.9f + Mathf.Repeat(Mathf.Sin(id * 12.9898f) * 43758.55f, 1f) * 0.16f;
        float poros = RuidoRep(u, v, 90f, 3f) * 0.5f + RuidoRep(u, v, 30f, 7f) * 0.5f;
        float humedad = RuidoRep(u, v, 4f, 11f);

        Color bloque = new Color(0.36f, 0.355f, 0.34f) * tono * (0.9f + poros * 0.18f);
        if (humedad > 0.6f) bloque *= Mathf.Lerp(1f, 0.78f, (humedad - 0.6f) / 0.4f);
        Color mortero = new Color(0.27f, 0.265f, 0.25f) * (0.95f + poros * 0.1f);
        c = Color.Lerp(mortero, bloque, esBloque);
        h = esBloque * (0.75f + poros * 0.25f);
    }

    // Losa de concreto: juntas en el borde de la repeticion, poros, manchas y grietas.
    static void Losa(float u, float v, out Color c, out float h)
    {
        float borde = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        float junta = Suave(0.004f, 0.012f, borde);
        float grano = RuidoRep(u, v, 120f, 1f), manchas = RuidoRep(u, v, 5f, 9f), suc = RuidoRep(u, v, 14f, 4f);

        // Grietas: lineas finas e irregulares (ruido de varias escalas) solo en algunas zonas.
        float g = RuidoRep(u, v, 6f, 21f) * 0.6f + RuidoRep(u, v, 19f, 5f) * 0.3f + RuidoRep(u, v, 47f, 13f) * 0.1f;
        float grieta = 1f - Mathf.Clamp01(Mathf.Abs(g - 0.5f) * 120f);
        grieta *= Mathf.Clamp01((RuidoRep(u, v, 3f, 33f) - 0.58f) * 6f);

        Color baseC = new Color(0.34f, 0.33f, 0.31f) * (0.88f + grano * 0.14f + suc * 0.1f);
        if (manchas > 0.62f) baseC *= Mathf.Lerp(1f, 0.72f, (manchas - 0.62f) / 0.38f);
        baseC = Color.Lerp(baseC * 0.5f, baseC, junta);
        baseC *= 1f - grieta * 0.55f;
        c = baseC;
        h = junta * (0.8f + grano * 0.2f) - grieta * 0.5f;
    }

    // Tablones de madera oscura (8 por repeticion) con vetas y separacion entre tablas.
    internal static void Tablones(float u, float v, out Color c, out float h)
    {
        const int tablas = 8;
        float fx = u * tablas;
        int tabla = Mathf.FloorToInt(fx);
        float tx = Mathf.Repeat(fx, 1f);
        float sep = Suave(0.0f, 0.06f, Mathf.Min(tx, 1f - tx));
        float sem = tabla * 5.3f;
        float veta = Mathf.Sin((v * 40f + RuidoRep(u, v, 6f, sem) * 6f) * 3.1416f) * 0.5f + 0.5f;
        float nudos = RuidoRep(u, v, 18f, sem + 2f);
        float tono = 0.85f + Mathf.Repeat(Mathf.Sin(tabla * 78.233f) * 12345.6f, 1f) * 0.25f;

        Color madera = new Color(0.27f, 0.19f, 0.12f) * tono * (0.85f + veta * 0.2f);
        if (nudos > 0.75f) madera *= 0.75f;
        c = Color.Lerp(new Color(0.06f, 0.045f, 0.03f), madera, sep);
        h = sep * (0.7f + veta * 0.3f);
    }
}
