using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Texturas de los Backrooms:
// - Paredes: el papel tapiz (Assets/Materials/texturas/backrooms.jpeg) sin estirarse (UV en metros: cada repeticion
//   mide lo mismo en cualquier pared), con algo de suciedad abajo y un mapa de normales sacado del propio dibujo.
// - Piso: alfombra amarilla gastada (fibras, manchas de humedad, zonas pisadas) con su mapa de normales.
// Los materiales originales (BackroomsPared, BackroomsAlfombra) no se tocan: se usan copias BR_*.
// Se aplica sola si los Backrooms todavia usan los originales. Rehacer: Tools > Texturas > Backrooms.
[InitializeOnLoad]
public static class BackroomsTexturasSetup
{
    const string Raiz = "Backrooms";
    const string Carpeta = "Assets/Backrooms/Texturas";
    const string Papel = "Assets/Materials/texturas/backrooms.jpeg";
    const string Prefijo = "BR_";
    const float MetrosPapel = 2.4f, MetrosAlfombra = 1.6f;

    static BackroomsTexturasSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (Falta()) Aplicar();
                if (File.Exists("Temp/Capturas/backrooms_rehacer.txt")) { File.Delete("Temp/Capturas/backrooms_rehacer.txt"); Rehacer(); }
                if (File.Exists("Temp/Capturas/backrooms_fotos.txt")) { File.Delete("Temp/Capturas/backrooms_fotos.txt"); Fotos(); }
            };
    }

    static bool Falta()
    {
        GameObject g = GameObject.Find(Raiz);
        if (!g) return false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
                if (m && (m.name == "BackroomsPared" || m.name == "BackroomsAlfombra")) return true;
        return false;
    }

    [MenuItem("Tools/Texturas/Backrooms")]
    static void Rehacer()
    {
        // Se regeneran las texturas
        foreach (string n in new[] { "Pared", "Pared_Normal", "Alfombra", "Alfombra_Normal", "Manchas" })
            AssetDatabase.DeleteAsset($"{Carpeta}/{Prefijo}{n}.png");
        Aplicar();
    }

    static void Aplicar()
    {
        GameObject raiz = GameObject.Find(Raiz);
        if (!raiz) { Debug.LogWarning("[Backrooms] No encuentro \"" + Raiz + "\" en la escena."); return; }

        Color[] papel = LeerPapel(out int pw, out int ph);
        var pared = SotanoAcabados.Generar("Pared", (float u, float v, out Color c, out float h) => Pared(papel, pw, ph, u, v, out c, out h),
                                           0.9f, MetrosPapel, 0.12f, Carpeta, Prefijo);
        var alfombra = SotanoAcabados.Generar("Alfombra", Alfombra, 1.6f, MetrosAlfombra, 0.05f, Carpeta, Prefijo);
        Material mPared = Mat("Pared", pared.color, pared.normal, MetrosPapel, 0.12f, new Color(1f, 0.97f, 0.88f));
        Material mAlfombra = Mat("Alfombra", alfombra.color, alfombra.normal, MetrosAlfombra, 0.05f, Color.white);

        int nPared = 0, nPiso = 0;
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool cambia = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!mats[i]) continue;
                string n = mats[i].name;
                if (n == "BackroomsPared" || n == Prefijo + "Pared") { mats[i] = mPared; cambia = true; nPared++; }
                else if (n == "BackroomsAlfombra" || n == Prefijo + "Alfombra") { mats[i] = mAlfombra; cambia = true; nPiso++; }
            }
            if (!cambia) continue;
            Undo.RecordObject(r, "Texturas Backrooms");
            r.sharedMaterials = mats;
            if (r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh)
            {
                Mesh uv = TexturasZonasSetup.MallaUVMetros(mf);
                if (uv && uv != mf.sharedMesh) { Undo.RecordObject(mf, "UV en metros"); mf.sharedMesh = uv; }
            }
        }
        Manchas(raiz, mAlfombra);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"[Backrooms] Texturas: {nPared} paredes con el papel tapiz sin estirar y {nPiso} pisos con alfombra (con mapas de normales). Guarda la escena (Ctrl+S).", raiz);
    }

    static Material Mat(string nombre, Texture2D color, Texture2D normal, float metros, float suavidad, Color tinte)
    {
        string ruta = $"{Carpeta}/{Prefijo}{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = Prefijo + nombre };
        Vector2 rep = Vector2.one / metros; // las UV estan en metros
        m.SetTexture("_BaseMap", color);
        m.SetTextureScale("_BaseMap", rep);
        m.SetColor("_BaseColor", tinte);
        m.SetTexture("_BumpMap", normal);
        m.SetTextureScale("_BumpMap", rep);
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", suavidad);
        m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        if (nuevo) AssetDatabase.CreateAsset(m, ruta); else EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ Texturas (u, v en 0-1, repetibles)

    static Color[] LeerPapel(out int w, out int h)
    {
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(Papel));
        w = t.width; h = t.height;
        Color[] px = t.GetPixels();
        Object.DestroyImmediate(t);
        return px;
    }

    static Color Bilineal(Color[] px, int w, int h, float u, float v)
    {
        float x = Mathf.Repeat(u, 1f) * w - 0.5f, y = Mathf.Repeat(v, 1f) * h - 0.5f;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        int X(int i) => ((i % w) + w) % w;
        int Y(int i) => ((i % h) + h) % h;
        Color a = px[Y(y0) * w + X(x0)], b = px[Y(y0) * w + X(x0 + 1)];
        Color c = px[Y(y0 + 1) * w + X(x0)], d = px[Y(y0 + 1) * w + X(x0 + 1)];
        return Color.Lerp(Color.Lerp(a, b, fx), Color.Lerp(c, d, fx), fy);
    }

    // Papel tapiz: el dibujo original, un poco mas apagado y con manchas de humedad suaves.
    // La altura sale del brillo del dibujo (las franjas en relieve) mas el grano del papel.
    static void Pared(Color[] papel, int w, int h, float u, float v, out Color c, out float alt)
    {
        Color p = Bilineal(papel, w, h, u, v);
        float lum = p.r * 0.3f + p.g * 0.59f + p.b * 0.11f;
        float grano = R(u, v, 160f, 3f);
        float humedad = Suave(0.55f, 0.85f, Fbm(u, v, 3f, 11f));
        c = p * (0.9f + grano * 0.08f);
        c = Color.Lerp(c, c * new Color(0.78f, 0.72f, 0.55f), humedad * 0.45f);
        c.a = 1f;
        alt = lum * 0.8f + grano * 0.12f - humedad * 0.05f;
    }

    // Alfombra: fibras finas (ruido muy denso), un dibujo de pelo apenas visible, zonas aplastadas
    // y manchas de humedad mas oscuras.
    static void Alfombra(float u, float v, out Color c, out float alt)
    {
        Color baseC = new Color(0.72f, 0.6f, 0.3f);
        float fibra = R(u, v, 220f, 1f) * 0.55f + R(u, v, 110f, 4f) * 0.3f + R(u, v, 55f, 6f) * 0.15f;
        float pelo = R2(u, v, 70f, 14f, 9f);
        c = baseC * (0.8f + fibra * 0.36f + pelo * 0.06f);
        c.a = 1f;
        alt = fibra * 0.8f + pelo * 0.1f;
    }

    // ------------------------------------------------------------------ Manchas sin repetir

    // Las manchas de humedad y las zonas pisadas no van en la textura de la alfombra (que se repite cada
    // 1.6 m): van en un mapa de detalle que cubre TODO el piso una sola vez, asi ninguna se repite.
    // Gris 0.5 = sin cambio; mas oscuro = mancha; mas claro = pelo aplastado.
    const int TamManchas = 1024;

    static void Manchas(GameObject raiz, Material m)
    {
        // Rango de UV (en metros) del piso
        bool hay = false; Vector2 min = default, max = default;
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (r.sharedMaterial != m || !r.TryGetComponent(out MeshFilter mf) || !mf.sharedMesh) continue;
            foreach (Vector2 uv in mf.sharedMesh.uv)
            {
                if (!hay) { min = max = uv; hay = true; }
                min = Vector2.Min(min, uv); max = Vector2.Max(max, uv);
            }
        }
        if (!hay) return;
        Vector2 tam = Vector2.Max(max - min, Vector2.one);

        string ruta = $"{Carpeta}/{Prefijo}Manchas.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (!tex)
        {
            // Unos 6 cm por pixel en un piso de 60 m; las manchas miden de 1 a 6 m
            float ex = tam.x / 4f, ey = tam.y / 4f;
            var px = new Color[TamManchas * TamManchas];
            for (int y = 0; y < TamManchas; y++)
            for (int x = 0; x < TamManchas; x++)
            {
                float u = (x + 0.5f) / TamManchas, v = (y + 0.5f) / TamManchas;
                float n1 = Mathf.PerlinNoise(u * ex + 31.7f, v * ey + 12.3f) * 0.6f + Mathf.PerlinNoise(u * ex * 2.3f + 5.1f, v * ey * 2.3f + 77.7f) * 0.3f
                         + Mathf.PerlinNoise(u * ex * 6f + 19f, v * ey * 6f + 3f) * 0.1f;
                float mancha = Suave(0.52f, 0.7f, n1);
                float borde = Suave(0.48f, 0.52f, n1) - Suave(0.52f, 0.56f, n1); // cerco de la mancha seca
                float n2 = Mathf.PerlinNoise(u * ex * 0.7f + 101f, v * ey * 0.7f + 55f);
                float pisado = Suave(0.55f, 0.8f, n2);
                float g = 0.5f - mancha * 0.26f - borde * 0.08f + pisado * 0.07f;
                // Un poco mas marron en la mancha
                px[y * TamManchas + x] = new Color(g, g - mancha * 0.015f, g - mancha * 0.04f, 1f);
            }
            var t = new Texture2D(TamManchas, TamManchas, TextureFormat.RGB24, true);
            t.SetPixels(px); t.Apply();
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(ruta);
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.sRGBTexture = false; // URP: 0.5 lineal = sin cambio
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }

        m.SetTexture("_DetailAlbedoMap", tex);
        m.SetTextureScale("_DetailAlbedoMap", new Vector2(1f / tam.x, 1f / tam.y));
        m.SetTextureOffset("_DetailAlbedoMap", new Vector2(-min.x / tam.x, -min.y / tam.y));
        m.SetFloat("_DetailAlbedoMapScale", 1f);
        m.EnableKeyword("_DETAIL_MULX2");
        EditorUtility.SetDirty(m);
    }

    static float R(float u, float v, float esc, float sem)
    {
        float a = Mathf.PerlinNoise(u * esc + sem, v * esc + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * esc + sem, v * esc + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * esc + sem, (v - 1f) * esc + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * esc + sem, (v - 1f) * esc + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float R2(float u, float v, float escU, float escV, float sem)
    {
        float a = Mathf.PerlinNoise(u * escU + sem, v * escV + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * escU + sem, v * escV + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * escU + sem, (v - 1f) * escV + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * escU + sem, (v - 1f) * escV + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Fbm(float u, float v, float esc, float sem) =>
        R(u, v, esc, sem) * 0.5f + R(u, v, esc * 2f, sem + 3f) * 0.3f + R(u, v, esc * 4f, sem + 7f) * 0.2f;

    static float Suave(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

    // ------------------------------------------------------------------ Revision

    // Fotos desde la entrada de los Backrooms (Temp/Capturas/backrooms_*.png); no guarda nada
    static void Fotos()
    {
        GameObject raiz = GameObject.Find(Raiz);
        if (!raiz) return;
        Transform inicio = raiz.transform.Find("Inicio");
        Vector3 p = inicio ? inicio.position : raiz.transform.position;
        var go = new GameObject("camFotos") { hideFlags = HideFlags.HideAndDontSave };
        var c = go.AddComponent<Camera>();
        c.fieldOfView = 70f; c.nearClipPlane = 0.05f; c.farClipPlane = 200f;
        Directory.CreateDirectory("Temp/Capturas");
        float[] giros = { 0f, 90f, 180f, 270f };
        for (int i = 0; i < giros.Length; i++)
        {
            go.transform.SetPositionAndRotation(p + Vector3.up * 1.6f, Quaternion.Euler(12f, (inicio ? inicio.eulerAngles.y : 0f) + giros[i], 0f));
            var rt = new RenderTexture(960, 540, 24); c.targetTexture = rt; c.Render(); RenderTexture.active = rt;
            var t = new Texture2D(960, 540); t.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); t.Apply();
            File.WriteAllBytes($"Temp/Capturas/backrooms_{i}.png", t.EncodeToPNG());
            RenderTexture.active = null; c.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(t);
        }
        Object.DestroyImmediate(go);
    }
}
