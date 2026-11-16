using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puerta de la Tienda entre "paredes/Cube (5)" y "paredes/Cube (9)": un bloque de madera con textura y mapa de normales.
// Solo la abren el cliente y el cultista del tutorial (PuertaNPC). El jugador no la puede mover y tampoco
// puede salir por ahi: un colisionador invisible tapa el hueco (los NPCs caminan sin fisica y pasan igual).
// Se crea sola si no existe "Tienda/Puerta Tienda"; rehacer: Tools > Tienda > Puerta (solo NPCs).
[InitializeOnLoad]
public static class TiendaPuertaSetup
{
    const string Nombre = "Puerta Tienda";
    const string Carpeta = "Assets/Tienda";

    static TiendaPuertaSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject tienda = GameObject.Find("Tienda");
            if (tienda && !tienda.transform.Find(Nombre) && GameObject.Find("Tienda/paredes/Cube (5)")) Crear();
        };
    }

    [MenuItem("Tools/Tienda/Puerta (solo NPCs)")]
    static void Crear()
    {
        GameObject tienda = GameObject.Find("Tienda");
        GameObject izq = GameObject.Find("Tienda/paredes/Cube (5)"), der = GameObject.Find("Tienda/paredes/Cube (9)");
        if (!tienda || !izq || !der) { Debug.LogWarning("[Puerta] No se encontraron Cube (5) y Cube (9) en Tienda/paredes."); return; }
        Transform vieja = tienda.transform.Find(Nombre);
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
        Transform bloqueoViejo = tienda.transform.Find("Bloqueo puerta (jugador)");
        if (bloqueoViejo) Undo.DestroyObjectImmediate(bloqueoViejo.gameObject);

        // Hueco entre las dos paredes
        Bounds a = izq.GetComponent<Renderer>().bounds, b = der.GetComponent<Renderer>().bounds;
        bool aIzq = a.center.x < b.center.x;
        float x0 = aIzq ? a.max.x : b.max.x, x1 = aIzq ? b.min.x : a.min.x;
        float y0 = Mathf.Max(a.min.y, b.min.y) + 0.04f;
        GameObject techo = GameObject.Find("Tienda/paredes/Cube (29)");
        float y1 = techo ? techo.GetComponent<Renderer>().bounds.min.y : Mathf.Min(a.max.y, b.max.y);
        float zc = (a.center.z + b.center.z) * 0.5f;
        const float holgura = 0.012f, grosor = 0.06f;
        float ancho = x1 - x0 - holgura * 2f, alto = y1 - y0 - holgura;

        // Bisagra del lado de Cube (5); la hoja va hacia +x
        var bis = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(bis, "Puerta tienda");
        bis.transform.SetParent(tienda.transform, true);
        bis.transform.position = new Vector3(x0 + holgura, y0, zc);
        bis.transform.rotation = Quaternion.identity;

        Material madera = MaterialPuerta();
        Material laton = Simple("PuertaManija", new Color(0.55f, 0.42f, 0.22f), 0.75f, 0.9f);

        var hoja = new GameObject("Hoja");
        hoja.transform.SetParent(bis.transform, false);
        hoja.transform.localPosition = new Vector3(ancho * 0.5f, alto * 0.5f, 0f);
        hoja.AddComponent<MeshFilter>().sharedMesh = MallaPuerta(new Vector3(ancho, alto, grosor));
        hoja.AddComponent<MeshRenderer>().sharedMaterial = madera;
        hoja.AddComponent<BoxCollider>().size = new Vector3(ancho, alto, grosor);

        // Manijas a ambos lados
        foreach (float lado in new[] { -1f, 1f })
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m.name = lado < 0 ? "Manija adentro" : "Manija afuera";
            Object.DestroyImmediate(m.GetComponent<Collider>());
            m.transform.SetParent(hoja.transform, false);
            m.transform.localPosition = new Vector3(ancho * 0.5f - 0.11f, 1.0f - alto * 0.5f, lado * (grosor * 0.5f + 0.035f));
            m.transform.localScale = new Vector3(0.14f, 0.03f, 0.03f);
            m.GetComponent<Renderer>().sharedMaterial = laton;
            var base_ = GameObject.CreatePrimitive(PrimitiveType.Cube);
            base_.name = "Roseta";
            Object.DestroyImmediate(base_.GetComponent<Collider>());
            base_.transform.SetParent(hoja.transform, false);
            base_.transform.localPosition = new Vector3(ancho * 0.5f - 0.05f, 1.0f - alto * 0.5f, lado * (grosor * 0.5f + 0.012f));
            base_.transform.localScale = new Vector3(0.05f, 0.14f, 0.024f);
            base_.GetComponent<Renderer>().sharedMaterial = laton;
        }

        var centro = new GameObject("Centro");
        centro.transform.SetParent(bis.transform, false);
        centro.transform.localPosition = new Vector3(ancho * 0.5f, 1f, 0f);

        var puerta = bis.AddComponent<PuertaNPC>();
        puerta.centro = centro.transform;
        var tut = Object.FindFirstObjectByType<TutorialTienda>();
        var lista = new List<Transform>();
        if (tut && tut.cliente) lista.Add(tut.cliente);
        if (tut && tut.cultista) lista.Add(tut.cultista);
        puerta.npcs = lista.ToArray();
        // La hoja abre hacia la calle (los NPCs caminan hacia adentro y doblan hacia el mostrador)
        Vector3 haciaAfuera = tut && tut.caminoEntrada != null && tut.caminoEntrada.Length > 0 && tut.caminoEntrada[0]
            ? tut.caminoEntrada[0].position - bis.transform.position : Vector3.forward;
        puerta.anguloAbierta = haciaAfuera.z >= 0f ? -100f : 100f;

        // El jugador no sale por la puerta aunque este abierta (capa Ignore Raycast: no tapa la mira ni las interacciones)
        var bloqueo = new GameObject("Bloqueo puerta (jugador)");
        Undo.RegisterCreatedObjectUndo(bloqueo, "Bloqueo puerta");
        bloqueo.layer = 2;
        bloqueo.transform.SetParent(tienda.transform, true);
        bloqueo.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, zc);
        bloqueo.transform.rotation = Quaternion.identity;
        Vector3 s = bloqueo.transform.lossyScale;
        bloqueo.AddComponent<BoxCollider>().size = new Vector3((x1 - x0) / s.x, (y1 - y0) / s.y, 0.3f / s.z);

        EditorSceneManager.MarkSceneDirty(tienda.scene);
        Debug.Log($"[Puerta] Puerta de la Tienda creada ({ancho:0.00} x {alto:0.00} m). La abren: {lista.Count} NPCs. Guarda la escena (Ctrl+S).", bis);
    }

    // Bloque con la textura de puerta entera en las dos caras grandes y UV en metros en los cantos
    static Mesh MallaPuerta(Vector3 s)
    {
        string ruta = $"{Carpeta}/Mallas/PuertaTienda_{s.x:0.000}x{s.y:0.000}.asset".Replace(',', '.');
        var ya = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (ya) return ya;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        Vector3[,] caras =
        {
            { Vector3.right, Vector3.up, Vector3.forward }, { Vector3.left, Vector3.forward, Vector3.up },
            { Vector3.up, Vector3.forward, Vector3.right }, { Vector3.down, Vector3.right, Vector3.forward },
            { Vector3.forward, Vector3.right, Vector3.up }, { Vector3.back, Vector3.up, Vector3.right },
        };
        for (int c = 0; c < 6; c++)
        {
            Vector3 nn = caras[c, 0], A = caras[c, 1], B = caras[c, 2];
            int b0 = v.Count;
            Vector3[] esq = { nn * 0.5f - A * 0.5f - B * 0.5f, nn * 0.5f + A * 0.5f - B * 0.5f, nn * 0.5f + A * 0.5f + B * 0.5f, nn * 0.5f - A * 0.5f + B * 0.5f };
            foreach (Vector3 p in esq)
            {
                Vector3 q = Vector3.Scale(p, s);
                v.Add(q); n.Add(nn);
                if (Mathf.Abs(nn.z) > 0.5f)
                {
                    // caras grandes: la textura completa (en la de atras, espejada para que la manija coincida)
                    float u = p.x + 0.5f;
                    uv.Add(new Vector2(nn.z > 0 ? 1f - u : u, p.y + 0.5f));
                }
                else uv.Add(Mathf.Abs(nn.y) > 0.5f ? new Vector2(q.x, q.z) : new Vector2(q.z, q.y)); // cantos
            }
            t.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 });
        }
        var m = new Mesh { name = "PuertaTienda" };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateTangents(); m.RecalculateBounds();
        System.IO.Directory.CreateDirectory(Carpeta + "/Mallas");
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static Material MaterialPuerta()
    {
        var a = SotanoAcabados.Generar("Puerta", Puerta, 1.6f, 1f, 0.35f, Carpeta + "/Texturas", "Tienda");
        string ruta = Carpeta + "/Materiales/TiendaPuerta.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "TiendaPuerta" };
        m.SetTexture("_BaseMap", a.color);
        m.SetTexture("_BumpMap", a.normal);
        m.EnableKeyword("_NORMALMAP");
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.35f);
        if (nuevo) { System.IO.Directory.CreateDirectory(Carpeta + "/Materiales"); AssetDatabase.CreateAsset(m, ruta); }
        else EditorUtility.SetDirty(m);
        return m;
    }

    static Material Simple(string nombre, Color c, float suavidad, float metal)
    {
        string ruta = Carpeta + "/Materiales/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", suavidad); m.SetFloat("_Metallic", metal);
        System.IO.Directory.CreateDirectory(Carpeta + "/Materiales");
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Textura: puerta de madera con 2 tableros

    static float R(float u, float v, float eu, float ev, float sem) => Mathf.PerlinNoise(u * eu + sem, v * ev + sem * 1.7f);
    static float Suave(float e0, float e1, float x) { float k = Mathf.Clamp01((x - e0) / (e1 - e0)); return k * k * (3f - 2f * k); }

    static void Puerta(float u, float v, out Color c, out float h)
    {
        // La puerta mide ~1.3 x 2.1 m: los bordes se miden en proporcion para que el marco tenga el mismo ancho
        float ax = 1.3f, ay = 2.1f;
        float x = u * ax, y = v * ay;
        float m = 0.13f; // ancho de largueros y travesanos
        // Dos tableros: abajo y arriba (el de arriba mas alto)
        Rect abajo = new Rect(m, m, ax - m * 2f, 0.78f), arriba = new Rect(m, m + 0.78f + m * 0.9f, ax - m * 2f, ay - (m + 0.78f + m * 0.9f) - m);
        float h0 = 0.75f; // superficie del marco
        float tablero = 0f, bisel = 0f;
        foreach (Rect r in new[] { abajo, arriba })
        {
            float d = Mathf.Min(Mathf.Min(x - r.xMin, r.xMax - x), Mathf.Min(y - r.yMin, r.yMax - y));
            if (d > 0f)
            {
                tablero = 1f;
                bisel = 1f - Suave(0f, 0.05f, d);       // el borde del tablero baja en bisel
            }
        }
        // Veta: vertical en largueros y tableros, horizontal en los travesanos
        bool travesano = tablero < 0.5f && x > m && x < ax - m;
        float veta = travesano
            ? Mathf.Sin((y * 60f + R(u, v, 3f, 30f, 2f) * 5f) * 3.1416f) * 0.5f + 0.5f
            : Mathf.Sin((x * 60f + R(u, v, 30f, 3f, 1f) * 5f) * 3.1416f) * 0.5f + 0.5f;
        float grano = R(u, v, 200f, 200f, 5f);
        float desgaste = Suave(0.55f, 0.85f, R(u, v, 4f, 4f, 9f));
        Color madera = new Color(0.3f, 0.18f, 0.1f) * (0.82f + veta * 0.22f + grano * 0.08f);
        if (tablero > 0.5f) madera *= 0.88f;
        madera *= 1f - bisel * 0.35f;
        madera = Color.Lerp(madera, madera * 1.25f, desgaste * 0.4f); // zonas gastadas mas claras
        // Junta fina entre piezas del marco
        float juntaLarguero = Mathf.Min(Mathf.Abs(x - m), Mathf.Abs(x - (ax - m)));
        if (!travesano && tablero < 0.5f && juntaLarguero < 0.004f && y > m && y < ay - m) madera *= 0.6f;
        c = madera;
        h = (tablero > 0.5f ? h0 - 0.25f + (1f - bisel) * 0.1f : h0) + veta * 0.04f + grano * 0.02f - bisel * 0.15f;
    }
}
