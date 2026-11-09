using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Version REAL (con geometria y colision) de la calle que muestra el shader Slappy/CalleFalsa en las ventanas de la Tienda:
// anden (acera) con cordon, calzada con lineas, los faroles y los edificios de enfrente. Usa las mismas medidas del material CalleFalsa
// (origen, ancho de acera y calle, separacion de faroles), asi lo real coincide con lo que se ve por las ventanas.
// - Los faroles solo iluminan la calle (rendering layer propia): su luz no se cuela dentro de la Tienda.
// - Se desactiva "Tienda/rt (7)" (el piso plano que habia afuera; quedaria encima de la acera).
// Se crea sola si no existe "Tienda/Calle Exterior"; rehacer: Tools > Tienda > Calle exterior (anden y calzada).
[InitializeOnLoad]
public static class CalleExteriorSetup
{
    const string Nombre = "Calle Exterior";
    const string Version = "_calle v5";
    const string RutaMaterialCalle = "Assets/Tienda/Materiales/CalleFalsa.mat";
    const string PisoViejo = "Tienda/rt (7)";
    const string Carpeta = "Assets/Tienda/Calle";
    const float Largo = 120f;             // metros de calle a lo largo
    const float AltoFarol = 5f;           // igual que el shader
    const int BitCalle = 6;               // rendering layer de la calle (los faroles solo iluminan esto)
    static readonly string[] Obstaculos = { "zotano" };

    static CalleExteriorSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject tienda = GameObject.Find("Tienda");
        Transform ya = tienda ? tienda.transform.Find(Nombre) : null;
        if (!tienda || (ya && ya.Find(Version))) return;
        if (!AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialCalle)) return;
        Crear();
    }

    [MenuItem("Tools/Tienda/Calle exterior (anden y calzada)")]
    static void Crear()
    {
        GameObject tienda = GameObject.Find("Tienda");
        var calle = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialCalle);
        if (!tienda || !calle) { Debug.LogWarning("[Calle] Falta la Tienda o el material CalleFalsa."); return; }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Transform vieja = tienda.transform.Find(Nombre);
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

        Vector3 origen = calle.GetVector("_Origen");
        Vector3 afuera = ((Vector3)calle.GetVector("_Afuera")).normalized;
        float A = calle.GetFloat("_Acera"), C = calle.GetFloat("_Calle"), hA = calle.GetFloat("_AlturaAcera"), sep = calle.GetFloat("_SepFaroles");
        float D = A + C + A;

        // Raiz con los mismos ejes del shader: x a lo largo de la calle, y alto sobre la calzada, z hacia afuera.
        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Calle exterior");
        raiz.transform.SetPositionAndRotation(origen, Quaternion.LookRotation(afuera, Vector3.up));

        new GameObject(Version).transform.SetParent(raiz.transform, false);
        var m = Materiales();
        float x0 = -Largo * 0.5f, x1 = Largo * 0.5f;
        // La calle termina antes de lo que ocupe ese espacio (el sotano esta al lado de la Tienda y su techo
        // sobresale del suelo): si no, la acera atravesaria el sotano por dentro.
        foreach (string obst in Obstaculos)
        {
            GameObject g = GameObject.Find(obst);
            if (!g) continue;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
            {
                Bounds b = r.bounds;
                if (b.max.y < origen.y - 0.5f) continue; // muy por debajo de la calle
                Vector3 lmin = Vector3.positiveInfinity, lmax = Vector3.negativeInfinity;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 esq = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 l = raiz.transform.InverseTransformPoint(esq);
                    lmin = Vector3.Min(lmin, l); lmax = Vector3.Max(lmax, l);
                }
                if (lmax.z < 0f || lmin.z > D) continue;
                if (lmax.x <= 0f) x0 = Mathf.Max(x0, lmax.x + 0.3f);
                else if (lmin.x >= 0f) x1 = Mathf.Min(x1, lmin.x - 0.3f);
            }
        }
        const float grosor = 0.4f;

        // Anden cercano (desde el vidrio), calzada y anden de enfrente
        Bloque(raiz.transform, "Anden", new Vector3(x0, hA - grosor, 0f), new Vector3(x1, hA, A - 0.2f), m.acera, true);
        Bloque(raiz.transform, "Cordon", new Vector3(x0, hA - grosor, A - 0.2f), new Vector3(x1, hA, A), m.cordon, true);
        Bloque(raiz.transform, "Calzada", new Vector3(x0, -grosor, A), new Vector3(x1, 0f, A + C), m.asfalto, true);
        Bloque(raiz.transform, "Cordon enfrente", new Vector3(x0, hA - grosor, A + C), new Vector3(x1, hA, A + C + 0.2f), m.cordon, true);
        Bloque(raiz.transform, "Anden enfrente", new Vector3(x0, hA - grosor, A + C + 0.2f), new Vector3(x1, hA, D), m.acera, true);

        // Lineas pintadas (mismas posiciones que el shader: central discontinua cada 6 m, bordes continuos)
        Transform lineas = Hijo(raiz.transform, "Lineas");
        float zc = A + C * 0.5f;
        for (float x = Mathf.Ceil(x0 / 6f) * 6f; x + 3f <= x1; x += 6f)
            Bloque(lineas, "Linea central", new Vector3(x, 0f, zc - 0.07f), new Vector3(x + 3f, 0.006f, zc + 0.07f), m.lineaAmarilla, false);
        Bloque(lineas, "Linea borde", new Vector3(x0, 0f, A + 0.24f), new Vector3(x1, 0.006f, A + 0.36f), m.lineaBlanca, false);
        Bloque(lineas, "Linea borde enfrente", new Vector3(x0, 0f, A + C - 0.36f), new Vector3(x1, 0.006f, A + C - 0.24f), m.lineaBlanca, false);

        // Faroles (mismos lugares que en el shader)
        Transform faroles = Hijo(raiz.transform, "Faroles");
        for (int lado = 0; lado < 2; lado++)
        {
            float zf = lado == 0 ? A - 0.45f : A + C + 0.45f;
            float off = lado == 0 ? 4f : 4f + sep * 0.5f;
            float haciaCalle = lado == 0 ? 1f : -1f;
            for (float x = Mathf.Ceil((x0 - off) / sep) * sep + off; x <= x1; x += sep)
                Farol(faroles, new Vector3(x, 0f, zf), haciaCalle, hA, m);
        }

        // Edificios de enfrente (como los del shader) y siluetas lejanas
        Edificios(raiz.transform, x0, x1, D, hA);

        raiz.transform.SetParent(tienda.transform, true);

        // El piso plano que habia afuera quedaria encima de la acera
        GameObject viejo = GameObject.Find(PisoViejo);
        if (viejo && viejo.activeSelf) { Undo.RecordObject(viejo, "Piso afuera"); viejo.SetActive(false); }

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Calle] Calle exterior creada ({x1 - x0:0} m, de x={x0:0.#} a x={x1:0.#} desde la ventana): anden de {A} m, calzada de {C} m, faroles cada {sep} m. Se desactivo \"{PisoViejo}\". Guarda la escena (Ctrl+S).", raiz);
    }

    static void Farol(Transform padre, Vector3 pie, float haciaCalle, float hA, Mats m)
    {
        Transform f = Hijo(padre, "Farol");
        f.localPosition = pie;
        Bloque(f, "Base", new Vector3(-0.16f, hA, -0.16f), new Vector3(0.16f, hA + 0.5f, 0.16f), m.metal, true);
        Bloque(f, "Poste", new Vector3(-0.07f, hA + 0.5f, -0.07f), new Vector3(0.07f, AltoFarol, 0.07f), m.metal, true);
        float zA = haciaCalle * 0.75f;
        Bloque(f, "Brazo", new Vector3(-0.05f, AltoFarol - 0.08f, Mathf.Min(0f, zA)), new Vector3(0.05f, AltoFarol + 0.02f, Mathf.Max(0f, zA)), m.metal, false);
        Bloque(f, "Lampara", new Vector3(-0.25f, AltoFarol - 0.35f, zA - 0.18f), new Vector3(0.25f, AltoFarol - 0.08f, zA + 0.18f), m.metal, false);
        Bloque(f, "Foco", new Vector3(-0.2f, AltoFarol - 0.37f, zA - 0.13f), new Vector3(0.2f, AltoFarol - 0.34f, zA + 0.13f), m.foco, false);

        var luzGo = new GameObject("Luz");
        luzGo.transform.SetParent(f, false);
        luzGo.transform.localPosition = new Vector3(0f, AltoFarol - 0.4f, zA);
        luzGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var l = luzGo.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = 130f;
        l.innerSpotAngle = 60f;
        l.range = 11f;
        l.intensity = 32f;
        l.color = new Color(1f, 0.72f, 0.42f);
        l.shadows = LightShadows.None;
        var datos = l.GetUniversalAdditionalLightData();
        datos.renderingLayers = 1u << BitCalle; // solo la calle
    }

    // ------------------------------------------------------------------ Bloques con UV en metros

    static readonly Dictionary<string, Mesh> mallas = new Dictionary<string, Mesh>();

    static GameObject Bloque(Transform padre, string nombre, Vector3 min, Vector3 max, Material mat, bool colision)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        Vector3 tam = max - min;
        go.transform.localPosition = (min + max) * 0.5f;
        go.AddComponent<MeshFilter>().sharedMesh = Caja(tam);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.renderingLayerMask = 1u | (1u << BitCalle);
        if (colision) go.AddComponent<BoxCollider>().size = tam;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // Caja de tam metros con UV en metros (la textura no se estira)
    static Mesh Caja(Vector3 s)
    {
        string clave = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.000}x{1:0.000}x{2:0.000}", s.x, s.y, s.z);
        if (mallas.TryGetValue(clave, out Mesh ya) && ya) return ya;
        string ruta = $"{Carpeta}/Mallas/Caja_{clave}.asset";
        ya = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (!ya)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3[,] caras =
            {
                { Vector3.right, Vector3.up, Vector3.forward }, { Vector3.left, Vector3.forward, Vector3.up },
                { Vector3.up, Vector3.forward, Vector3.right }, { Vector3.down, Vector3.right, Vector3.forward },
                { Vector3.forward, Vector3.right, Vector3.up }, { Vector3.back, Vector3.up, Vector3.right },
            };
            for (int c = 0; c < 6; c++)
            {
                Vector3 nn = caras[c, 0], a = caras[c, 1], b = caras[c, 2];
                int b0 = v.Count;
                Vector3[] esq = { nn * 0.5f - a * 0.5f - b * 0.5f, nn * 0.5f + a * 0.5f - b * 0.5f, nn * 0.5f + a * 0.5f + b * 0.5f, nn * 0.5f - a * 0.5f + b * 0.5f };
                foreach (Vector3 p in esq)
                {
                    Vector3 q = Vector3.Scale(p, s);
                    v.Add(q); n.Add(nn);
                    // Proyeccion segun la cara: suelos en XZ, lados con V vertical
                    uv.Add(Mathf.Abs(nn.y) > 0.5f ? new Vector2(q.x, q.z) : Mathf.Abs(nn.x) > 0.5f ? new Vector2(q.z, q.y) : new Vector2(q.x, q.y));
                }
                t.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 });
            }
            ya = new Mesh { name = "Caja_" + clave };
            ya.SetVertices(v); ya.SetNormals(n); ya.SetUVs(0, uv); ya.SetTriangles(t, 0);
            ya.RecalculateTangents(); ya.RecalculateBounds();
            System.IO.Directory.CreateDirectory(Carpeta + "/Mallas");
            AssetDatabase.CreateAsset(ya, ruta);
        }
        mallas[clave] = ya;
        return ya;
    }

    static Transform Hijo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }


    // ------------------------------------------------------------------ Edificios (enfrente y lejanos)

    const float AnchoBloque = 12f;   // igual que el shader
    const float AltoTextura = 24f;   // la textura de fachada cubre 12 x 24 m
    const int Variantes = 6;

    static double Hd(double n) { double s = System.Math.Sin(n * 127.1) * 43758.5453; return s - System.Math.Floor(s); }
    static float H(float n) => (float)Hd(n);

    static void Edificios(Transform raiz, float x0, float x1, float D, float hA)
    {
        Transform cont = Hijo(raiz, "Edificios");
        var mats = new Material[Variantes];
        for (int i = 0; i < Variantes; i++) mats[i] = MaterialFachada(i);
        Material lado = Mat("EdificioLado", SotanoAcabados.Generar("EdificioLado", Ladrillo, 1.2f, 2f, 0.2f, Carpeta + "/Texturas", "Calle"), 2f, 0.2f);
        Material techo = Simple("EdificioTecho", new Color(0.06f, 0.06f, 0.065f), 0.1f);
        Material cornisa = Simple("EdificioCornisa", new Color(0.32f, 0.3f, 0.28f), 0.2f);

        // Enfrente: bloques de 12 m (mismas posiciones que en el shader), alto de 10 a 22 m
        for (float b = Mathf.Floor(x0 / AnchoBloque); b * AnchoBloque < x1; b++)
        {
            float alto = 10f + Mathf.Floor(H(b) * 5f) * 3f;
            int variante = Mathf.Abs((int)(H(b + 3.1f) * 997f)) % Variantes;
            var go = new GameObject("Edificio " + (int)b);
            go.transform.SetParent(cont, false);
            go.transform.localPosition = new Vector3(b * AnchoBloque, hA, D);
            go.AddComponent<MeshFilter>().sharedMesh = MallaEdificio(new Vector3(AnchoBloque, alto, 12f));
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { mats[variante], lado, techo };
            r.renderingLayerMask = 1u | (1u << BitCalle);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(AnchoBloque * 0.5f, alto * 0.5f, 6f);
            col.size = new Vector3(AnchoBloque, alto, 12f);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            // Cornisa del techo (un poco salida)
            Bloque(go.transform, "Cornisa", new Vector3(0f, alto - 0.05f, -0.25f), new Vector3(AnchoBloque, alto + 0.35f, 0.3f), cornisa, false);
        }

        // Panos a los lados de la Tienda (en la misma acera, mirando a la calle): fachadas delgadas
        // para que desde la puerta y desde afuera la Tienda quede entre edificios.
        if (LimitesTienda(raiz, out float tMin, out float tMax, out float tFrente, out float tTecho))
        {
            const float grosor = 0.5f;
            var tramos = new List<Vector2>();
            if (tMin - 0.05f - x0 > 0.5f) tramos.Add(new Vector2(x0, tMin - 0.05f));   // lado oeste (hasta el sotano)
            for (float xa = tMax + 0.05f; xa < x1 - 0.5f; xa += AnchoBloque)             // lado este, en bloques de 12 m
                tramos.Add(new Vector2(xa, Mathf.Min(xa + AnchoBloque, x1)));
            int i = 0;
            foreach (Vector2 t in tramos)
            {
                float ancho = t.y - t.x;
                float alto = 10f + Mathf.Floor(H(t.x * 0.37f + 50f) * 4f) * 3f;
                int variante = Mathf.Abs((int)(H(t.x * 0.11f + 7f) * 997f)) % Variantes;
                var go = new GameObject("Pano " + (++i));
                go.transform.SetParent(cont, false);
                // La malla mira a -z: girada 180 queda mirando a la calle (+z), con el borde derecho en t.y
                go.transform.localPosition = new Vector3(t.y, hA, tFrente);
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = MallaEdificio(new Vector3(ancho, alto, grosor));
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = new[] { mats[variante], lado, techo };
                r.renderingLayerMask = 1u | (1u << BitCalle);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(ancho * 0.5f, alto * 0.5f, grosor * 0.5f);
                col.size = new Vector3(ancho, alto, grosor);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                Bloque(go.transform, "Cornisa", new Vector3(0f, alto - 0.05f, -0.25f), new Vector3(ancho, alto + 0.35f, 0.3f), cornisa, false);
            }
        }

        // Encima de la Tienda: pisos de departamentos (sin local: la textura arranca en el primer piso)
        // y el letrero rojo "SHOP" sobre la entrada.
        if (LimitesTienda(raiz, out float sMin, out float sMax, out float sFrente, out float sTecho))
        {
            const float grosorArriba = 0.5f;
            float anchoArriba = sMax - sMin, altoArriba = 13f - sTecho;
            var goA = new GameObject("Pano encima de la Tienda");
            goA.transform.SetParent(cont, false);
            goA.transform.localPosition = new Vector3(sMax, sTecho, sFrente);
            goA.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            goA.AddComponent<MeshFilter>().sharedMesh = MallaEdificio(new Vector3(anchoArriba, altoArriba, grosorArriba), false, 4f);
            var rA = goA.AddComponent<MeshRenderer>();
            rA.sharedMaterials = new[] { mats[1], lado, techo };
            rA.renderingLayerMask = 1u | (1u << BitCalle);
            var colA = goA.AddComponent<BoxCollider>();
            colA.center = new Vector3(anchoArriba * 0.5f, altoArriba * 0.5f, grosorArriba * 0.5f);
            colA.size = new Vector3(anchoArriba, altoArriba, grosorArriba);
            GameObjectUtility.SetStaticEditorFlags(goA, StaticEditorFlags.BatchingStatic);
            Bloque(goA.transform, "Cornisa", new Vector3(0f, altoArriba - 0.05f, -0.25f), new Vector3(anchoArriba, altoArriba + 0.35f, 0.3f), cornisa, false);
            // Repisa que separa la Tienda de los pisos de arriba (tapa la union)
            Bloque(cont, "Repisa sobre la Tienda", new Vector3(sMin - 0.1f, sTecho - 0.05f, sFrente - 0.1f), new Vector3(sMax + 0.1f, sTecho + 0.25f, sFrente + 0.35f), cornisa, false);

            Letrero(cont, new Vector3((sMin + sMax) * 0.5f, sTecho + 0.3f, sFrente + 0.12f));
        }

        // Lejanos: siluetas oscuras con pocas luces, 30 m detras
        Material lejano = MaterialLejano();
        float D2 = D + 30f;
        for (float b = Mathf.Floor((x0 - 20f) / 17f); b * 17f < x1 + 20f; b++)
        {
            float alto = 22f + H(b * 1.7f) * 40f;
            var go = new GameObject("Edificio lejano " + (int)b);
            go.transform.SetParent(cont, false);
            go.transform.localPosition = new Vector3(b * 17f, 0f, D2);
            go.AddComponent<MeshFilter>().sharedMesh = MallaEdificio(new Vector3(17f, alto, 15f), true);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { lejano, lejano, techo };
            r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            // Luz roja de antena en algunos
            if (H(b) > 0.6f)
                Bloque(go.transform, "Antena", new Vector3(8.4f, alto, 7.4f), new Vector3(8.6f, alto + 0.25f, 7.6f), Simple("AntenaRoja", new Color(0.3f, 0f, 0f), 0.2f, 0f, new Color(1f, 0.08f, 0.08f) * 4f), false);
        }
    }

    // Letrero "SHOP": tablero oscuro con letras rojas luminosas hechas de barras, y una luz roja que tine la acera.
    static void Letrero(Transform padre, Vector3 abajoCentro)
    {
        Transform l = Hijo(padre, "Letrero SHOP");
        l.localPosition = abajoCentro;
        Material tablero = Simple("LetreroTablero", new Color(0.05f, 0.02f, 0.02f), 0.4f, 0.3f);
        Material neon = Simple("LetreroNeon", new Color(0.6f, 0.02f, 0.02f), 0.6f, 0f, new Color(1f, 0.05f, 0.04f) * 6f);
        Material marco = Simple("LetreroMarco", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.8f);

        const float altoLetra = 0.9f, anchoLetra = 0.6f, trazo = 0.12f, sepLetra = 0.3f, margen = 0.3f;
        string texto = "SHOP";
        float anchoTexto = texto.Length * anchoLetra + (texto.Length - 1) * sepLetra;
        float ancho = anchoTexto + margen * 2f, alto = altoLetra + margen * 2f;
        Bloque(l, "Tablero", new Vector3(-ancho * 0.5f, 0f, -0.08f), new Vector3(ancho * 0.5f, alto, 0.08f), tablero, false);
        Bloque(l, "Marco arriba", new Vector3(-ancho * 0.5f - 0.05f, alto, -0.1f), new Vector3(ancho * 0.5f + 0.05f, alto + 0.06f, 0.1f), marco, false);
        Bloque(l, "Marco abajo", new Vector3(-ancho * 0.5f - 0.05f, -0.06f, -0.1f), new Vector3(ancho * 0.5f + 0.05f, 0f, 0.1f), marco, false);

        // Cada letra en una cuadricula de 3 x 5: rectangulos (x0, y0, x1, y1) en celdas
        var formas = new Dictionary<char, float[][]>
        {
            { 'S', new[] { new[] { 0f, 4f, 3f, 5f }, new[] { 0f, 2f, 1f, 5f }, new[] { 0f, 2f, 3f, 3f }, new[] { 2f, 0f, 3f, 3f }, new[] { 0f, 0f, 3f, 1f } } },
            { 'H', new[] { new[] { 0f, 0f, 1f, 5f }, new[] { 2f, 0f, 3f, 5f }, new[] { 0f, 2f, 3f, 3f } } },
            { 'O', new[] { new[] { 0f, 0f, 1f, 5f }, new[] { 2f, 0f, 3f, 5f }, new[] { 0f, 4f, 3f, 5f }, new[] { 0f, 0f, 3f, 1f } } },
            { 'P', new[] { new[] { 0f, 0f, 1f, 5f }, new[] { 0f, 4f, 3f, 5f }, new[] { 0f, 2f, 3f, 3f }, new[] { 2f, 2f, 3f, 5f } } },
        };
        float cx = anchoLetra / 3f, cy = altoLetra / 5f;
        // El letrero mira a la calle (+z): visto desde la calle, +x queda a la IZQUIERDA, asi que se escribe de +x a -x
        float x = anchoTexto * 0.5f;
        foreach (char c in texto)
        {
            Transform letra = Hijo(l, "Letra " + c);
            foreach (float[] rr in formas[c])
            {
                // celdas espejadas en x (por lo mismo) y el tubo un poco mas fino que la celda
                float ax = x - rr[2] * cx, bx = x - rr[0] * cx, ay = margen + rr[1] * cy, by = margen + rr[3] * cy;
                float px = (cx - trazo) * 0.5f, py = (cy - trazo) * 0.5f;
                Bloque(letra, "Tubo", new Vector3(ax + px, ay + py, 0.08f), new Vector3(bx - px, by - py, 0.16f), neon, false);
            }
            x -= anchoLetra + sepLetra;
        }

        // Resplandor rojo sobre la acera y la fachada (solo la calle, no entra a la Tienda)
        var luzGo = new GameObject("Luz letrero");
        luzGo.transform.SetParent(l, false);
        luzGo.transform.localPosition = new Vector3(0f, alto * 0.5f, 0.8f);
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = 6f;
        luz.intensity = 4f;
        luz.color = new Color(1f, 0.08f, 0.06f);
        luz.shadows = LightShadows.None;
        luz.GetUniversalAdditionalLightData().renderingLayers = 1u << BitCalle;
    }

    // Limites de la Tienda (sus paredes) en coordenadas de la calle: x minimo y maximo, y el frente (z)
    static bool LimitesTienda(Transform raiz, out float xMin, out float xMax, out float zFrente, out float yTecho)
    {
        xMin = float.MaxValue; xMax = float.MinValue; zFrente = 0f; yTecho = 0f;
        GameObject paredes = GameObject.Find("Tienda/paredes");
        if (!paredes) return false;
        float zMax = float.MinValue;
        foreach (Renderer r in paredes.GetComponentsInChildren<Renderer>())
        {
            Bounds b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 l = raiz.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                xMin = Mathf.Min(xMin, l.x); xMax = Mathf.Max(xMax, l.x); yTecho = Mathf.Max(yTecho, l.y);
                if (l.z < 1.5f) zMax = Mathf.Max(zMax, l.z); // la cara de afuera de la pared del frente
            }
        }
        zFrente = Mathf.Max(0f, zMax - 0.3f); // el pano queda alineado con el frente de la Tienda
        return xMax > xMin;
    }

    // Caja con la esquina en (0,0,0): submalla 0 = frente (z = 0, UV de la textura de fachada),
    // 1 = lados y fondo (UV en metros), 2 = techo.
    static Mesh MallaEdificio(Vector3 s, bool frenteEnMetros = false, float vDesde = 0f)
    {
        string clave = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Edificio_{0:0.0}x{1:0.0}x{2:0.0}{3}{4}", s.x, s.y, s.z, frenteEnMetros ? "_m" : "", vDesde > 0f ? "_v" + vDesde.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "");
        string ruta = $"{Carpeta}/Mallas/{clave}.asset";
        var ya = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (ya) return ya;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
        var subs = new[] { new List<int>(), new List<int>(), new List<int>() };
        void Cara(Vector3 o, Vector3 a, Vector3 b, Vector3 nn, int sub, bool fachada)
        {
            int b0 = v.Count;
            Vector3[] esq = { o, o + a, o + a + b, o + b };
            foreach (Vector3 p in esq)
            {
                v.Add(p); n.Add(nn);
                if (fachada) uv.Add(new Vector2(p.x / AnchoBloque, (p.y + vDesde) / AltoTextura));
                else if (Mathf.Abs(nn.y) > 0.5f) uv.Add(new Vector2(p.x, p.z));
                else if (Mathf.Abs(nn.x) > 0.5f) uv.Add(new Vector2(p.z, p.y));
                else uv.Add(new Vector2(p.x, p.y));
            }
            // orden segun la normal
            if (Vector3.Dot(Vector3.Cross(a, b), nn) > 0) subs[sub].AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 });
            else subs[sub].AddRange(new[] { b0, b0 + 2, b0 + 1, b0, b0 + 3, b0 + 2 });
        }
        Vector3 X = new Vector3(s.x, 0, 0), Y = new Vector3(0, s.y, 0), Z = new Vector3(0, 0, s.z);
        Cara(Vector3.zero, X, Y, Vector3.back, 0, !frenteEnMetros);   // frente (mira a la calle, -z)
        Cara(Z, X, Y, Vector3.forward, 1, false);                       // fondo
        Cara(Vector3.zero, Z, Y, Vector3.left, 1, false);               // lado izquierdo
        Cara(X, Z, Y, Vector3.right, 1, false);                         // lado derecho
        Cara(Y, X, Z, Vector3.up, 2, false);                            // techo
        var m = new Mesh { name = clave };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
        m.subMeshCount = 3;
        for (int i = 0; i < 3; i++) m.SetTriangles(subs[i], i);
        m.RecalculateTangents(); m.RecalculateBounds();
        System.IO.Directory.CreateDirectory(Carpeta + "/Mallas");
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Texturas de fachada (color, normal y luces)

    static readonly Color[] ColoresPared = { new Color(0.25f, 0.11f, 0.08f), new Color(0.22f, 0.21f, 0.2f), new Color(0.28f, 0.24f, 0.17f), new Color(0.12f, 0.15f, 0.14f) };
    static readonly Color[] ColoresLetrero = { new Color(1f, 0.15f, 0.2f), new Color(0.2f, 0.7f, 1f), new Color(1f, 0.75f, 0.2f), new Color(0.4f, 1f, 0.5f) };

    static Material MaterialFachada(int variante)
    {
        string nombre = "Fachada" + variante;
        string tex = Carpeta + "/Texturas";
        string rc = $"{tex}/Calle{nombre}.png", rn = $"{tex}/Calle{nombre}_Normal.png", re = $"{tex}/Calle{nombre}_Luces.png";
        if (!System.IO.File.Exists(rc) || !System.IO.File.Exists(rn) || !System.IO.File.Exists(re))
            GenerarFachada(variante, rc, rn, re);
        Material m = Base("Calle" + nombre);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rc));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rn));
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(re));
        m.SetColor("_EmissionColor", Color.white * 1.6f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void GenerarFachada(int variante, string rc, string rn, string re)
    {
        const int W = 512, Ht = 1024;
        var col = new Color[W * Ht]; var emi = new Color[W * Ht]; var alt = new float[W * Ht];
        float sem = variante * 17.3f + 1f;
        Color pared = ColoresPared[variante % ColoresPared.Length];
        bool abierto = H(sem + 9.7f) >= 0.35f;
        Color adentro = H(sem + 1.3f) < 0.5f ? new Color(1f, 0.8f, 0.55f) : new Color(0.75f, 0.9f, 1f);
        Color letrero = ColoresLetrero[(int)(H(sem + 5.5f) * 3.99f)];
        bool letreroOn = H(sem + 2.2f) >= 0.3f;
        for (int py = 0; py < Ht; py++)
        for (int px = 0; px < W; px++)
        {
            float u = (px + 0.5f) / W * AnchoBloque, y = (py + 0.5f) / Ht * AltoTextura;
            // Ladrillo
            float fila = Mathf.Floor(y / 0.25f);
            bool junta = Frac(y / 0.25f) < 0.08f || Frac((u + fila * 0.25f) / 0.5f) < 0.04f;
            float ruido = Mathf.PerlinNoise(u * 3f + sem, y * 3f) * 0.3f + Mathf.PerlinNoise(u * 20f, y * 20f + sem) * 0.15f;
            Color c = pared * (0.8f + ruido) * (junta ? 0.75f : 1f) * (0.9f + H(Mathf.Floor((u + fila * 0.25f) / 0.5f) * 7f + fila * 13f + sem) * 0.2f);
            float h = junta ? 0.3f : 0.7f + ruido * 0.3f;
            Color e = Color.black;

            if (y < 4f)
            {
                bool vidriera = u > 1f && u < AnchoBloque - 1f && y > 0.6f && y < 3f && Mathf.Abs(u - AnchoBloque * 0.5f) > 0.7f;
                bool puerta = Mathf.Abs(u - AnchoBloque * 0.5f) <= 0.6f && y < 2.4f;
                bool cartel = u > 1.2f && u < AnchoBloque - 1.2f && y > 3.15f && y < 3.75f;
                if (vidriera)
                {
                    bool marco = Frac((u - 1f) / 2.5f) < 0.03f || y < 0.66f || y > 2.94f;
                    c = marco ? new Color(0.08f, 0.08f, 0.08f) : new Color(0.02f, 0.02f, 0.025f);
                    h = marco ? 0.6f : 0.2f;
                    if (!marco && abierto) e = adentro * (0.45f + 0.35f * Mathf.PerlinNoise(u * 1.5f + sem, y * 1.5f));
                }
                else if (puerta) { c = new Color(0.12f, 0.08f, 0.05f); h = 0.35f; }
                else if (cartel) { c = letrero * 0.12f; h = 0.85f; if (letreroOn) e = letrero * 0.9f; }
            }
            else
            {
                float pf = Mathf.Floor((y - 4f) / 3f), fy = Frac((y - 4f) / 3f);
                float cc = Mathf.Floor((u - 0.6f) / 2.4f), fx = Frac((u - 0.6f) / 2.4f);
                if (u > 0.6f && u < AnchoBloque - 0.6f && fx > 0.22f && fx < 0.78f && fy > 0.25f && fy < 0.82f)
                {
                    float hv = H(cc * 13f + pf * 7.7f + sem);
                    bool marco = fx < 0.25f || fx > 0.75f || fy < 0.28f || fy > 0.79f;
                    if (marco) { c = new Color(0.35f, 0.33f, 0.3f); h = 0.8f; }
                    else
                    {
                        c = new Color(0.015f, 0.018f, 0.025f); h = 0.15f;
                        if (hv > 0.62f)
                        {
                            Color tono = hv > 0.94f ? new Color(0.45f, 0.6f, 1f) : Color.Lerp(new Color(1f, 0.7f, 0.4f), new Color(1f, 0.88f, 0.7f), Frac(hv * 7f));
                            float cortina = Frac(hv * 13f) > 0.5f && fy > 0.58f ? 0.4f : 1f;
                            e = tono * cortina * (0.35f + 0.25f * Frac(hv * 31f));
                        }
                    }
                }
                else if (fy < 0.04f) { c *= 1.3f; h = 0.9f; } // cornisa entre pisos
            }
            col[py * W + px] = c; emi[py * W + px] = e; alt[py * W + px] = h;
        }
        var nor = new Color[W * Ht];
        for (int py = 0; py < Ht; py++)
        for (int px = 0; px < W; px++)
        {
            float hl = alt[py * W + (px + W - 1) % W], hr = alt[py * W + (px + 1) % W];
            float hd = alt[Mathf.Max(0, py - 1) * W + px], hu = alt[Mathf.Min(Ht - 1, py + 1) * W + px];
            Vector3 nn = new Vector3((hl - hr) * 4f, (hd - hu) * 4f, 1f).normalized;
            nor[py * W + px] = new Color(nn.x * 0.5f + 0.5f, nn.y * 0.5f + 0.5f, nn.z * 0.5f + 0.5f, 1f);
        }
        GuardarPng(rc, W, Ht, col, TextureImporterType.Default);
        GuardarPng(rn, W, Ht, nor, TextureImporterType.NormalMap);
        GuardarPng(re, W, Ht, emi, TextureImporterType.Default);
    }

    static Material MaterialLejano()
    {
        string tex = Carpeta + "/Texturas";
        string rc = $"{tex}/CalleLejano.png", re = $"{tex}/CalleLejano_Luces.png";
        if (!System.IO.File.Exists(rc) || !System.IO.File.Exists(re))
        {
            // 16 x 26 m: celdas de ventana de 1.6 x 2.6 m, pocas encendidas
            const int T = 256;
            var c = new Color[T * T]; var e = new Color[T * T];
            for (int py = 0; py < T; py++)
            for (int px = 0; px < T; px++)
            {
                float u = (px + 0.5f) / T * 10f, v = (py + 0.5f) / T * 10f;
                bool vent = Frac(u) > 0.3f && Frac(v) > 0.3f;
                float hv = H(Mathf.Floor(u) * 11f + Mathf.Floor(v) * 37f);
                c[py * T + px] = vent ? new Color(0.01f, 0.012f, 0.018f) : new Color(0.025f, 0.026f, 0.032f);
                e[py * T + px] = vent && hv > 0.86f ? new Color(1f, 0.78f, 0.5f) * 0.6f : Color.black;
            }
            GuardarPng(rc, T, T, c, TextureImporterType.Default);
            GuardarPng(re, T, T, e, TextureImporterType.Default);
        }
        Material m = Base("CalleLejano");
        Vector2 rep = new Vector2(1f / 16f, 1f / 26f); // UV en metros
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rc)); m.SetTextureScale("_BaseMap", rep);
        m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(re));
        m.SetColor("_EmissionColor", Color.white * 1.5f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void GuardarPng(string ruta, int w, int h, Color[] px, TextureImporterType tipo)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ruta));
        var t = new Texture2D(w, h, TextureFormat.RGB24, false, tipo == TextureImporterType.NormalMap);
        t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(ruta, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(ruta);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = tipo;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
    }

    static float Frac(float x) => x - Mathf.Floor(x);

    // Ladrillo para los lados de los edificios (2 m por repeticion)
    static void Ladrillo(float u, float v, out Color c, out float h)
    {
        float y = v * 8f; float fila = Mathf.Floor(y);
        bool junta = Frac(y) < 0.08f || Frac(u * 4f + fila * 0.5f) < 0.04f;
        float ruido = R(u, v, 12f, 3f) * 0.3f + R(u, v, 60f, 5f) * 0.15f;
        c = new Color(0.2f, 0.17f, 0.15f) * (0.8f + ruido) * (junta ? 0.7f : 1f);
        h = junta ? 0.3f : 0.7f + ruido * 0.3f;
    }

    // ------------------------------------------------------------------ Materiales (mismos colores que el shader)

    class Mats { public Material acera, cordon, asfalto, lineaAmarilla, lineaBlanca, metal, foco; }

    static Mats Materiales()
    {
        string tex = Carpeta + "/Texturas";
        return new Mats
        {
            acera = Mat("Anden", SotanoAcabados.Generar("Anden", Anden, 1.3f, 1.5f, 0.25f, tex, "Calle"), 1.5f, 0.25f),
            cordon = Mat("Cordon", SotanoAcabados.Generar("Cordon", Cordon, 1.2f, 1f, 0.2f, tex, "Calle"), 1f, 0.2f),
            asfalto = Mat("Asfalto", SotanoAcabados.Generar("Asfalto", Asfalto, 1.1f, 4f, 0.35f, tex, "Calle"), 4f, 0.35f),
            lineaAmarilla = Simple("LineaAmarilla", new Color(0.75f, 0.62f, 0.25f), 0.3f),
            lineaBlanca = Simple("LineaBlanca", new Color(0.6f, 0.6f, 0.58f), 0.3f),
            metal = Simple("FarolMetal", new Color(0.06f, 0.06f, 0.065f), 0.5f, 0.7f),
            foco = Simple("FarolFoco", new Color(1f, 0.85f, 0.6f), 0.5f, 0f, new Color(1f, 0.72f, 0.42f) * 6f),
        };
    }

    static Material Mat(string nombre, SotanoAcabados.Acabado a, float metros, float suavidad)
    {
        Material m = Base("Calle" + nombre);
        Vector2 rep = Vector2.one / metros;
        m.SetTexture("_BaseMap", a.color); m.SetTextureScale("_BaseMap", rep);
        m.SetTexture("_BumpMap", a.normal); m.SetTextureScale("_BumpMap", rep);
        m.EnableKeyword("_NORMALMAP");
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", suavidad);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Simple(string nombre, Color color, float suavidad, float metal = 0f, Color emision = default)
    {
        Material m = Base("Calle" + nombre);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", suavidad);
        m.SetFloat("_Metallic", metal);
        if (emision != default)
        {
            m.SetColor("_EmissionColor", emision);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Base(string nombre)
    {
        string ruta = $"{Carpeta}/Materiales/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        System.IO.Directory.CreateDirectory(Carpeta + "/Materiales");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre, enableInstancing = true };
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Texturas (una repeticion = "metros")

    static float R(float u, float v, float esc, float sem)
    {
        float a = Mathf.PerlinNoise(u * esc + sem, v * esc + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * esc + sem, v * esc + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * esc + sem, (v - 1f) * esc + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * esc + sem, (v - 1f) * esc + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Suave(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

    // Losa de 1.5 m (como en el shader) con junta en el borde, grano y manchas
    static void Anden(float u, float v, out Color c, out float h)
    {
        float borde = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        float junta = Suave(0.008f, 0.018f, borde);
        float grano = R(u, v, 120f, 1f), manchas = Suave(0.6f, 0.78f, R(u, v, 3f, 9f));
        Color b = new Color(0.22f, 0.215f, 0.21f) * (0.8f + grano * 0.3f) * (1f - manchas * 0.3f);
        c = Color.Lerp(b * 0.6f, b, junta);
        h = junta * (0.75f + grano * 0.25f);
    }

    static void Cordon(float u, float v, out Color c, out float h)
    {
        float grano = R(u, v, 90f, 2f), sucio = R(u, v, 6f, 4f);
        c = new Color(0.4f, 0.4f, 0.38f) * (0.8f + grano * 0.3f) * (0.85f + sucio * 0.2f);
        h = 0.5f + grano * 0.3f;
    }

    static void Asfalto(float u, float v, out Color c, out float h)
    {
        float grava = R(u, v, 260f, 1f) * 0.6f + R(u, v, 90f, 2f) * 0.4f;
        float parches = Suave(0.62f, 0.66f, R(u, v, 2.5f, 5f));                    // remiendos mas oscuros
        float g = R(u, v, 5f, 21f) * 0.6f + R(u, v, 17f, 6f) * 0.3f + R(u, v, 45f, 13f) * 0.1f;
        float grieta = (1f - Mathf.Clamp01(Mathf.Abs(g - 0.5f) * 120f)) * Mathf.Clamp01((R(u, v, 3f, 33f) - 0.55f) * 6f);
        Color b = new Color(0.075f, 0.075f, 0.08f) * (0.75f + grava * 0.5f);
        b = Color.Lerp(b, b * 0.7f, parches);
        c = b * (1f - grieta * 0.5f);
        h = grava * 0.7f - grieta * 0.5f + parches * 0.05f;
    }
}
