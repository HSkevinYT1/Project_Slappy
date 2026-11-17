using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puzzle de contrasena del laberinto (reemplaza al de paneles con numero de "PuzzlePanelesSetup"):
// - 4 paneles de metal en la pared, uno en cada sala de cuadrante. Cada uno tiene 4 pantallas: solo la de su
//   posicion funciona (muestra un digito), las demas estan apagadas y rotas.
//     Panel 1: n x x x   Panel 2: x n x x   Panel 3: x x n x   Panel 4: x x x n
// - Un panel central (en la habitacion central) estilo "contrasena" de Keep Talking: 4 digitos con flechas
//   arriba/abajo y un boton ENVIAR.
// - Al acertar: cinematica con la camara mirando la reja de la entrada mientras se abre.
// Se aplica solo si falta; rehacer: Tools > Laberinto > Puzzle de contrasena. Guarda la escena despues.
[InitializeOnLoad]
public static class PuzzleContrasenaSetup
{
    const string Nombre = "Puzzle Contrasena";
    const string Version = "_contrasena v2";
    const string Carpeta = "Assets/Laberinto";
    // Pared de cada sala donde va el panel (las salas en el orden de "Luz Panel 1..4")
    static readonly Vector3[] Paredes = { Vector3.left, Vector3.forward, Vector3.right, Vector3.back };
    const float MitadSala = 6f - 0.3f; // 3 celdas de 4 m, menos medio grosor de pared

    static PuzzleContrasenaSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject lab = GameObject.Find("Laberinto");
        if (!lab || !lab.transform.Find("Zona Segura (centro)") || !lab.transform.Find("Luces Laberinto/Luz Panel 1")) return;
        Transform ya = lab.transform.Find(Nombre);
        bool viejo = lab.transform.Find("Paneles") || lab.transform.Find("Puzzle Paneles") || lab.transform.Find("Paneles Numero");
        if (ya && ya.Find(Version) && !viejo) return;
        Aplicar(lab.transform);
    }

    [MenuItem("Tools/Laberinto/Puzzle de contrasena")]
    static void Menu()
    {
        GameObject lab = GameObject.Find("Laberinto");
        if (lab) Aplicar(lab.transform);
    }

    static void Aplicar(Transform lab)
    {
        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        // Fuera el puzzle anterior (monolitos blancos, paneles para agarrar y tablero de ranuras)
        foreach (string v in new[] { Nombre, "Paneles", "Puzzle Paneles", "Paneles Numero" })
        {
            Transform t = lab.Find(v);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }

        // Contenedor con transformacion de mundo identidad (el Laberinto esta girado y escalado)
        var raizGo = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raizGo, Nombre);
        Transform raiz = raizGo.transform;
        raiz.SetParent(lab, false);
        Vector3 ls = lab.lossyScale;
        raiz.rotation = Quaternion.identity;
        raiz.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
        raiz.position = Vector3.zero;
        new GameObject(Version).transform.SetParent(raiz, false);

        Material metal = MaterialMetal();
        Material oscuro = Mat("Panel_Oscuro", new Color(0.05f, 0.05f, 0.055f), 0.5f, 0.6f, default);
        Material lcd = Mat("Panel_LCD", new Color(0.03f, 0.015f, 0.07f), 0.85f, 0f, new Color(0.05f, 0.02f, 0.14f));
        // (si ya existia de antes, en verde) se pasa a indigo
        lcd.SetColor("_BaseColor", new Color(0.03f, 0.015f, 0.07f));
        lcd.SetColor("_EmissionColor", new Color(0.05f, 0.02f, 0.14f));
        lcd.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(lcd);
        Material roto = Mat("Panel_VidrioRoto", new Color(0.012f, 0.012f, 0.014f), 0.95f, 0f, default);
        Material grieta = Mat("Panel_Grieta", new Color(0.45f, 0.47f, 0.5f), 0.6f, 0f, default);
        Material tornillo = Mat("Panel_Tornillo", new Color(0.35f, 0.33f, 0.3f), 0.55f, 0.9f, default);
        Material led = Mat("Panel_LED", new Color(0.05f, 0.06f, 0.05f), 0.8f, 0f, default);
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var rnd = new System.Random(4321);

        var puzzleGo = new GameObject("Puzzle");
        puzzleGo.transform.SetParent(raiz, false);
        var puzzle = puzzleGo.AddComponent<PuzzleContrasena>();
        var pistas = new TextMeshPro[4];

        // ---------------------------------------------------------------- 4 paneles de pared
        for (int k = 0; k < 4; k++)
        {
            Transform luz = lab.Find("Luces Laberinto/Luz Panel " + (k + 1));
            Vector3 centroSala = new Vector3(luz.position.x, 0f, luz.position.z);
            Vector3 d = Paredes[k];
            var p = new GameObject("Panel Pared " + (k + 1)).transform;
            p.SetParent(raiz, false);
            p.SetPositionAndRotation(centroSala + d * MitadSala + Vector3.up * 1.55f, Quaternion.LookRotation(d)); // +Z hacia la pared
            Carcasa(p, new Vector2(1.3f, 0.75f), metal, oscuro, tornillo, led);
            // Franja de pantallas
            Caja(p, "Marco pantallas", new Vector3(0f, 0f, -0.065f), new Vector3(1.12f, 0.4f, 0.012f), oscuro);
            for (int s = 0; s < 4; s++)
            {
                float x = -0.39f + s * 0.26f;
                if (s == k)
                {
                    Caja(p, "Pantalla " + (s + 1), new Vector3(x, 0f, -0.073f), new Vector3(0.2f, 0.3f, 0.01f), lcd);
                    pistas[k] = Digito(p, "Digito", new Vector3(x, 0f, -0.08f), 2.7f, fuente);
                    var l = new GameObject("Luz pantalla");
                    l.transform.SetParent(p, false);
                    l.transform.localPosition = new Vector3(x, 0f, -0.35f);
                    var lc = l.AddComponent<Light>();
                    lc.type = LightType.Point; lc.color = new Color(0.45f, 0.2f, 1f); lc.range = 1.4f; lc.intensity = 0.7f; lc.shadows = LightShadows.None;
                }
                else
                {
                    // Apagada y rota: vidrio negro con grietas, un poco torcida
                    var v = Caja(p, "Pantalla rota " + (s + 1), new Vector3(x, Rand(rnd, -0.01f, 0.01f), -0.073f), new Vector3(0.2f, 0.3f, 0.01f), roto);
                    v.transform.localRotation = Quaternion.Euler(Rand(rnd, -3f, 3f), Rand(rnd, -3f, 3f), Rand(rnd, -4f, 4f));
                    Vector3 impacto = new Vector3(x + Rand(rnd, -0.05f, 0.05f), Rand(rnd, -0.08f, 0.08f), -0.0795f);
                    int n = rnd.Next(5, 9);
                    for (int g = 0; g < n; g++)
                    {
                        float ang = Rand(rnd, 0f, 360f), largo = Rand(rnd, 0.05f, 0.17f);
                        Vector3 dir = Quaternion.Euler(0f, 0f, ang) * Vector3.up;
                        Vector3 c = impacto + dir * largo * 0.5f;
                        // dentro del vidrio
                        c.x = Mathf.Clamp(c.x, x - 0.09f, x + 0.09f); c.y = Mathf.Clamp(c.y, -0.14f, 0.14f);
                        var r = Caja(p, "Grieta", c, new Vector3(0.004f, largo, 0.002f), grieta);
                        r.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                    }
                    if (rnd.NextDouble() < 0.5) // un pedazo que falta
                    {
                        var hueco = Caja(p, "Hueco", impacto + new Vector3(0f, 0f, 0.003f), new Vector3(0.05f, 0.05f, 0.004f), oscuro);
                        hueco.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    }
                }
            }
        }
        puzzle.digitosPista = pistas;

        // ---------------------------------------------------------------- Panel central
        Transform zona = lab.Find("Zona Segura (centro)");
        Vector3 centro = new Vector3(zona.position.x, 0f, zona.position.z);
        var consola = new GameObject("Panel Central").transform;
        consola.SetParent(raiz, false);
        consola.SetPositionAndRotation(centro, Quaternion.LookRotation(Vector3.right)); // se usa desde el oeste (la entrada)
        Caja(consola, "Pedestal", new Vector3(0f, 0.5f, 0.05f), new Vector3(0.9f, 1.0f, 0.5f), metal, true).GetComponent<MeshFilter>().sharedMesh = Cubo(new Vector3(0.9f, 1.0f, 0.5f));
        Caja(consola, "Base", new Vector3(0f, 0.04f, 0.05f), new Vector3(1.1f, 0.08f, 0.7f), oscuro, true);
        var frente = new GameObject("Frente").transform;
        frente.SetParent(consola, false);
        frente.localPosition = new Vector3(0f, 1.45f, 0f);
        frente.localRotation = Quaternion.Euler(15f, 0f, 0f);
        Carcasa(frente, new Vector2(1.5f, 1.05f), metal, oscuro, tornillo, led, true);
        Caja(frente, "Marco pantallas", new Vector3(0f, 0.05f, -0.065f), new Vector3(1.26f, 0.4f, 0.012f), oscuro);

        var columnas = new TextMeshPro[4];
        var botones = new List<Transform>();
        for (int c = 0; c < 4; c++)
        {
            float x = -0.45f + c * 0.3f;
            Caja(frente, "Pantalla " + (c + 1), new Vector3(x, 0.05f, -0.073f), new Vector3(0.22f, 0.3f, 0.01f), lcd);
            columnas[c] = Digito(frente, "Digito " + (c + 1), new Vector3(x, 0.05f, -0.08f), 2.8f, fuente);
            botones.Add(Boton(frente, puzzle, BotonContrasena.Accion.Subir, c, new Vector3(x, 0.32f, -0.07f), new Vector2(0.2f, 0.1f), oscuro, grieta, "Subir", 1f, fuente));
            botones.Add(Boton(frente, puzzle, BotonContrasena.Accion.Bajar, c, new Vector3(x, -0.22f, -0.07f), new Vector2(0.2f, 0.1f), oscuro, grieta, "Bajar", -1f, fuente));
        }
        botones.Add(Boton(frente, puzzle, BotonContrasena.Accion.Enviar, 0, new Vector3(0f, -0.4f, -0.07f), new Vector2(0.6f, 0.12f), oscuro, grieta, "Enviar codigo", 0f, fuente));
        puzzle.columnas = columnas;
        puzzle.botones = botones.ToArray();
        puzzle.luzEstado = frente.Find("LED").GetComponent<Renderer>();
        var luzConsola = new GameObject("Luz consola");
        luzConsola.transform.SetParent(consola, false);
        luzConsola.transform.localPosition = new Vector3(0f, 2.2f, -0.9f);
        var lco = luzConsola.AddComponent<Light>();
        lco.type = LightType.Point; lco.color = new Color(0.9f, 0.95f, 1f); lco.range = 3.5f; lco.intensity = 1.2f; lco.shadows = LightShadows.None;

        // ---------------------------------------------------------------- Puntos de la cinematica
        RejasEntrada reja = lab.GetComponentInChildren<RejasEntrada>(true);
        Transform inicio = lab.Find("Inicio Laberinto");
        if (reja && inicio)
        {
            Vector3 boca = reja.transform.position;
            var cam = new GameObject("Camara Puerta").transform;
            cam.SetParent(raiz, false);
            Vector3 haciaAdentro = (new Vector3(inicio.position.x, 0f, inicio.position.z) - new Vector3(boca.x, 0f, boca.z)).normalized;
            cam.position = new Vector3(boca.x, 1.7f, boca.z) + haciaAdentro * 7f;
            var mirar = new GameObject("Mirar Puerta").transform;
            mirar.SetParent(raiz, false);
            mirar.position = new Vector3(boca.x, 2.0f, boca.z);
            cam.rotation = Quaternion.LookRotation(mirar.position - cam.position);
            puzzle.puntoCamara = cam;
            puzzle.mirarA = mirar;
        }
        // Que el puzzle viejo no haga nada si aun esta en la escena
        foreach (PuzzlePaneles viejo in Object.FindObjectsByType<PuzzlePaneles>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(viejo);

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (!t.GetComponent<BotonContrasena>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

        EditorUtility.SetDirty(puzzle);
        EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log("[Puzzle] Puzzle de contrasena: 4 paneles de pared con un digito cada uno y panel central con flechas y ENVIAR. Al acertar se abre la reja de la entrada. Guarda la escena (Ctrl+S).", raiz);
    }

    // ------------------------------------------------------------------ Piezas

    static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    // Caja metalica tipo modulo: placa con bisel, 4 tornillos y un LED de estado
    static void Carcasa(Transform p, Vector2 tam, Material metal, Material oscuro, Material tornillo, Material led, bool conLed = false)
    {
        var placa = Caja(p, "Placa", new Vector3(0f, 0f, -0.03f), new Vector3(tam.x, tam.y, 0.06f), metal, true);
        placa.GetComponent<MeshFilter>().sharedMesh = Cubo(new Vector3(tam.x, tam.y, 0.06f));
        float b = 0.035f;
        Caja(p, "Bisel arriba", new Vector3(0f, tam.y * 0.5f - b * 0.5f, -0.068f), new Vector3(tam.x, b, 0.016f), metal);
        Caja(p, "Bisel abajo", new Vector3(0f, -tam.y * 0.5f + b * 0.5f, -0.068f), new Vector3(tam.x, b, 0.016f), metal);
        Caja(p, "Bisel izq", new Vector3(-tam.x * 0.5f + b * 0.5f, 0f, -0.068f), new Vector3(b, tam.y, 0.016f), metal);
        Caja(p, "Bisel der", new Vector3(tam.x * 0.5f - b * 0.5f, 0f, -0.068f), new Vector3(b, tam.y, 0.016f), metal);
        foreach (float sx in new[] { -1f, 1f })
        foreach (float sy in new[] { -1f, 1f })
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Tornillo";
            Object.DestroyImmediate(t.GetComponent<Collider>());
            t.transform.SetParent(p, false);
            t.transform.localPosition = new Vector3(sx * (tam.x * 0.5f - 0.06f), sy * (tam.y * 0.5f - 0.06f), -0.08f);
            t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            t.transform.localScale = new Vector3(0.035f, 0.006f, 0.035f);
            t.GetComponent<Renderer>().sharedMaterial = tornillo;
        }
        var l = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        l.name = "LED";
        Object.DestroyImmediate(l.GetComponent<Collider>());
        l.transform.SetParent(p, false);
        l.transform.localPosition = new Vector3(tam.x * 0.5f - 0.13f, tam.y * 0.5f - 0.11f, -0.075f);
        l.transform.localScale = Vector3.one * 0.055f;
        l.GetComponent<Renderer>().sharedMaterial = led;
    }

    static Transform Boton(Transform padre, PuzzleContrasena puzzle, BotonContrasena.Accion accion, int col, Vector3 pos, Vector2 tam,
                           Material cuerpo, Material marca, string texto, float flecha, TMP_FontAsset fuente)
    {
        var go = Caja(padre, "Boton " + texto + (accion == BotonContrasena.Accion.Enviar ? "" : " " + (col + 1)), pos, new Vector3(tam.x, tam.y, 0.04f), cuerpo, true);
        go.GetComponent<Renderer>().sharedMaterial = cuerpo;
        if (flecha != 0f)
        {
            // Chevron (dos barritas)
            foreach (float lado in new[] { -1f, 1f })
            {
                var c = Caja(go.transform, "Flecha", Vector3.zero, Vector3.one, marca);
                c.transform.localPosition = new Vector3(lado * 0.11f, 0f, -0.55f);
                c.transform.localRotation = Quaternion.Euler(0f, 0f, lado * flecha * -32f);
                c.transform.localScale = new Vector3(0.25f, 0.18f, 0.2f);
            }
        }
        else
        {
            var t = Digito(go.transform, "Texto", new Vector3(0f, 0f, -0.6f), 1f, fuente);
            t.text = "ENVIAR";
            t.color = new Color(0.8f, 0.82f, 0.85f);
            t.fontStyle = FontStyles.Bold;
            t.transform.localScale = new Vector3(1f / tam.x, 1f / tam.y, 1f / 0.04f);
            t.rectTransform.sizeDelta = new Vector2(0.6f, 0.12f);
        }
        var inter = go.AddComponent<Interactable>();
        var so = new SerializedObject(inter);
        so.FindProperty("prompt").stringValue = texto;
        so.ApplyModifiedPropertiesWithoutUndo();
        var b = go.AddComponent<BotonContrasena>();
        b.puzzle = puzzle; b.accion = accion; b.columna = col;
        UnityEventTools.AddPersistentListener(inter.onInteract, b.Pulsar);
        return go.transform;
    }

    static TextMeshPro Digito(Transform padre, string nombre, Vector3 pos, float tam, TMP_FontAsset fuente)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;
        var t = go.AddComponent<TextMeshPro>();
        if (fuente) t.font = fuente;
        t.text = "0";
        t.fontSize = tam;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.62f, 0.42f, 1f); // indigo brillante
        t.rectTransform.sizeDelta = new Vector2(0.2f, 0.3f);
        return t;
    }

    static GameObject Caja(Transform padre, string nombre, Vector3 pos, Vector3 escala, Material m, bool colision = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        if (!colision) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        if (m) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Cubo unitario con UV en metros para ese tamano (la textura de metal no se estira)
    static Mesh Cubo(Vector3 tam)
    {
        var m = typeof(LaberintoNuevoSetup).GetMethod("CuboMetros", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return m != null ? (Mesh)m.Invoke(null, new object[] { tam }) : null;
    }

    // ------------------------------------------------------------------ Materiales

    static Material Mat(string nombre, Color c, float suave, float metal, Color emision)
    {
        string ruta = $"{Carpeta}/Materiales/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        System.IO.Directory.CreateDirectory(Carpeta + "/Materiales");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", suave);
        m.SetFloat("_Metallic", metal);
        if (emision != default) { m.SetColor("_EmissionColor", emision); m.EnableKeyword("_EMISSION"); }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // Metal gris de panel militar: cepillado, rayones, manchas de grasa y bordes gastados (con mapa de normales)
    static Material MaterialMetal()
    {
        var a = SotanoAcabados.Generar("PanelMetal", MuestraMetal, 1.2f, 0.6f, 0.45f, Carpeta + "/Texturas", "Lab_");
        string ruta = $"{Carpeta}/Materiales/Panel_Metal.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Panel_Metal" };
        m.SetTexture("_BaseMap", a.color);
        m.SetTexture("_BumpMap", a.normal);
        m.EnableKeyword("_NORMALMAP");
        m.SetTextureScale("_BaseMap", Vector2.one / 0.6f);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.75f);
        m.SetFloat("_Smoothness", 0.45f);
        if (nuevo) { System.IO.Directory.CreateDirectory(Carpeta + "/Materiales"); AssetDatabase.CreateAsset(m, ruta); }
        else EditorUtility.SetDirty(m);
        return m;
    }

    static float R(float u, float v, float eu, float ev, float sem)
    {
        float a = Mathf.PerlinNoise(u * eu + sem, v * ev + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * eu + sem, v * ev + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * eu + sem, (v - 1f) * ev + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * eu + sem, (v - 1f) * ev + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static void MuestraMetal(float u, float v, out Color c, out float h)
    {
        float cepillado = R(u, v, 4f, 300f, 1f) * 0.6f + R(u, v, 8f, 600f, 2f) * 0.4f;     // lineas finas horizontales
        float manchas = Mathf.Clamp01((R(u, v, 3f, 3f, 5f) - 0.5f) * 3f);
        float g = R(u, v, 9f, 9f, 21f) * 0.7f + R(u, v, 30f, 30f, 6f) * 0.3f;
        float rayon = (1f - Mathf.Clamp01(Mathf.Abs(g - 0.5f) * 150f)) * Mathf.Clamp01((R(u, v, 4f, 4f, 33f) - 0.5f) * 5f);
        Color b = new Color(0.36f, 0.37f, 0.36f) * (0.85f + cepillado * 0.25f);
        b = Color.Lerp(b, new Color(0.17f, 0.15f, 0.12f), manchas * 0.55f);   // grasa
        b = Color.Lerp(b, new Color(0.62f, 0.63f, 0.62f), rayon * 0.7f);      // rayones brillantes
        c = b;
        h = 0.5f + cepillado * 0.08f - rayon * 0.3f;
    }
}
