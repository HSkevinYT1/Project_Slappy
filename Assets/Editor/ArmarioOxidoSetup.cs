using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Armario (Assets/Prefabs/Armario.prefab):
// 1. Material de metal pintado MEDIO OXIDADO: pintura gris verdosa gastada, parches de oxido que chorrean
//    hacia abajo, bordes de pintura descascarada y rayones de metal desnudo. Color + mapa de normales +
//    mapa de metal/brillo (el oxido es opaco, la pintura semimate y los rayones brillan).
// 2. Sonidos de la puerta: chirrido + pestillo al abrir, chirrido + portazo metalico al cerrar
//    (se asignan a sonidoAbrir / sonidoCerrar del Escondite, que ya los reproduce al mover la puerta).
// Si el prefab esta abierto en modo prefab, se modifica ahi (luego guardar el prefab).
// Se aplica solo una vez. Rehacer: Tools > Armario > Material oxidado y sonidos.
[InitializeOnLoad]
public static class ArmarioOxidoSetup
{
    const string Prefab = "Assets/Prefabs/Armario.prefab";
    const string Carpeta = "Assets/Armario";
    const string RutaMaterial = Carpeta + "/ArmarioOxidado.mat";
    const string SonidoAbrir = "Assets/sonidos/SFX/ArmarioAbrir.wav";
    const string SonidoCerrar = "Assets/sonidos/SFX/ArmarioCerrar.wav";

    static ArmarioOxidoSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject raiz = Raiz(out bool enEtapa, out _);
        if (!raiz) return;
        bool falta = false;
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
            if (!r.sharedMaterial || r.sharedMaterial.name != "ArmarioOxidado") falta = true;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh && !mf.sharedMesh.name.StartsWith("UVMetros_")) falta = true;
        var esc = raiz.GetComponent<Escondite>();
        if (esc && (!esc.sonidoAbrir || !esc.sonidoCerrar)) falta = true;
        if (!enEtapa) PrefabUtility.UnloadPrefabContents(raiz);
        if (falta) Aplicar();
    }

    [MenuItem("Tools/Armario/Material oxidado y sonidos")]
    static void Aplicar()
    {
        Material mat = CrearMaterial();
        AssetDatabase.ImportAsset(SonidoAbrir);
        AssetDatabase.ImportAsset(SonidoCerrar);
        var abrir = AssetDatabase.LoadAssetAtPath<AudioClip>(SonidoAbrir);
        var cerrar = AssetDatabase.LoadAssetAtPath<AudioClip>(SonidoCerrar);

        GameObject raiz = Raiz(out bool enEtapa, out var etapa);
        if (!raiz) { Debug.LogWarning("[Armario] No se encontro " + Prefab); return; }

        int piezas = 0;
        // Coordenadas de textura en metros (las del modelo meten cada cara en 1/4 de la textura y se estira).
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh conUV = MallaConUVMetros(mf);
            if (!conUV) continue;
            Undo.RecordObject(mf, "UV en metros");
            mf.sharedMesh = conUV;
        }
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(r, "Material oxidado");
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            piezas++;
        }
        var esc = raiz.GetComponent<Escondite>();
        if (esc)
        {
            Undo.RecordObject(esc, "Sonidos de la puerta");
            if (abrir) esc.sonidoAbrir = abrir;
            if (cerrar) esc.sonidoCerrar = cerrar;
            EditorUtility.SetDirty(esc);
        }

        if (enEtapa)
        {
            EditorSceneManager.MarkSceneDirty(etapa.scene);
            Debug.Log($"[Armario] Material oxidado en {piezas} piezas y sonidos de puerta asignados en el prefab abierto. Guarda el prefab (Ctrl+S).", raiz);
        }
        else
        {
            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            PrefabUtility.UnloadPrefabContents(raiz);
            Debug.Log($"[Armario] Material oxidado en {piezas} piezas y sonidos de puerta asignados (prefab guardado).");
        }
    }

    // El prefab abierto en modo prefab, o su contenido cargado aparte.
    static GameObject Raiz(out bool enEtapa, out UnityEditor.SceneManagement.PrefabStage etapa)
    {
        etapa = PrefabStageUtility.GetCurrentPrefabStage();
        enEtapa = etapa != null && etapa.assetPath == Prefab;
        if (enEtapa) return etapa.prefabContentsRoot;
        if (!File.Exists(Prefab)) return null;
        return PrefabUtility.LoadPrefabContents(Prefab);
    }

    // Copia de la malla con UV proyectadas por cara (segun hacia donde mira cada vertice), en metros.
    static Mesh MallaConUVMetros(MeshFilter mf)
    {
        Mesh orig = mf.sharedMesh;
        if (!orig) return null;
        if (orig.name.StartsWith("UVMetros_")) return orig;
        float escala = mf.transform.lossyScale.x; // escala uniforme: unidades del modelo -> metros
        string ruta = $"{Carpeta}/Mallas/UVMetros_{orig.name}.asset";
        var ya = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (ya) return ya;

        Mesh m = Object.Instantiate(orig);
        m.name = "UVMetros_" + orig.name;
        Vector3[] v = m.vertices, n = m.normals;
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = v[i] * Mathf.Abs(escala);
            Vector3 a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
            if (a.x >= a.y && a.x >= a.z) uv[i] = new Vector2(p.z, p.y);       // caras laterales: U horizontal, V vertical
            else if (a.z >= a.y) uv[i] = new Vector2(p.x, p.y);                // frente y espalda
            else uv[i] = new Vector2(p.x, p.z);                                // arriba y abajo
        }
        m.uv = uv;
        m.RecalculateTangents();
        Directory.CreateDirectory(Carpeta + "/Mallas");
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Material

    static Material CrearMaterial()
    {
        string tex = Carpeta + "/Texturas";
        var acabado = SotanoAcabados.Generar("OxidadoV2", MuestraColor, 1.1f, 1f, 0.4f, tex, "Armario");
        Texture2D metal = TexturaMetal(tex + "/ArmarioOxidadoV2_Metal.png");

        var m = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        bool nuevo = !m;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ArmarioOxidado" };
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BaseMap", acabado.color);
        m.SetTexture("_BumpMap", acabado.normal);
        m.SetFloat("_BumpScale", 1.2f);
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", metal);
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", 1f);   // con mapa: el brillo sale del alfa del mapa
        m.SetFloat("_Metallic", 1f);
        if (nuevo) { Directory.CreateDirectory(Carpeta); AssetDatabase.CreateAsset(m, RutaMaterial); }
        else EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    // Capas: 0 = pintura, 1 = oxido, 2 = metal desnudo (rayones).
    static void Evaluar(float u, float v, out Color color, out float altura, out float metal, out float brillo)
    {
        float n1 = Ruido(u, v, 3f, 1f), n2 = Ruido(u, v, 9f, 4f), n3 = Ruido(u, v, 27f, 7f);
        // Chorreado: ruido estirado en vertical (el oxido "baja" por la lamina).
        float chorro = Ruido(u, v, 1f, 12f, 14f, 1.6f);
        float oxidoMascara = n1 * 0.45f + n2 * 0.25f + n3 * 0.1f + chorro * 0.35f;
        float oxido = Suave(0.5f, 0.57f, oxidoMascara);                         // cerca de la mitad de la superficie
        float borde = Suave(0.44f, 0.5f, oxidoMascara) - oxido;                 // pintura levantada alrededor
        float rayon = Mathf.Clamp01(1f - Mathf.Abs(Ruido(u, v, 1f, 31f, 6f, 20f) - 0.5f) * 70f)
                      * Suave(0.55f, 0.62f, Ruido(u, v, 3f, 41f)) * (1f - oxido);

        Color pintura = new Color(0.33f, 0.38f, 0.36f) * (0.85f + n3 * 0.2f + n2 * 0.08f);
        float sucio = Ruido(u, v, 5f, 51f);
        pintura *= 1f - Mathf.Clamp01(sucio - 0.6f) * 0.5f;
        Color oxidoC = Color.Lerp(new Color(0.42f, 0.2f, 0.08f), new Color(0.24f, 0.12f, 0.06f), n3) * (0.85f + n2 * 0.3f);
        Color desnudo = new Color(0.55f, 0.55f, 0.56f);

        color = Color.Lerp(pintura, oxidoC, oxido);
        color = Color.Lerp(color, pintura * 1.15f, borde * 0.6f);
        color = Color.Lerp(color, desnudo, rayon);

        altura = 0.6f + borde * 0.15f - oxido * 0.1f + oxido * (n3 - 0.5f) * 0.4f - rayon * 0.2f;
        metal = Mathf.Lerp(Mathf.Lerp(0.25f, 0.0f, oxido), 0.9f, rayon);
        brillo = Mathf.Lerp(Mathf.Lerp(0.42f, 0.08f, oxido), 0.55f, rayon);
    }

    static void MuestraColor(float u, float v, out Color c, out float h) => Evaluar(u, v, out c, out h, out _, out _);

    static Texture2D TexturaMetal(string ruta)
    {
        const int tam = 512;
        var px = new Color[tam * tam];
        for (int y = 0; y < tam; y++)
        for (int x = 0; x < tam; x++)
        {
            Evaluar((x + 0.5f) / tam, (y + 0.5f) / tam, out _, out _, out float met, out float bri);
            px[y * tam + x] = new Color(met, met, met, bri);
        }
        var t = new Texture2D(tam, tam, TextureFormat.RGBA32, false, true);
        t.SetPixels(px);
        t.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        File.WriteAllBytes(ruta, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(ruta);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.sRGBTexture = false;           // datos, no color
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    // Ruido repetible sin costuras; escX/escY permiten estirarlo (para el chorreado vertical).
    static float Ruido(float u, float v, float esc, float sem, float escX = -1f, float escY = -1f)
    {
        float ex = escX > 0f ? escX : esc, ey = escY > 0f ? escY : esc;
        float a = Mathf.PerlinNoise(u * ex + sem, v * ey + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * ex + sem, v * ey + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * ex + sem, (v - 1f) * ey + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * ex + sem, (v - 1f) * ey + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Suave(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
}
