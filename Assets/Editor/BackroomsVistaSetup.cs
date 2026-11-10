using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Mecanica de los Backrooms: un caramelo con agujero que, mirando a traves (clic derecho), deja ver
// flechas pintadas en las paredes que llevan hasta "Destino Indicaciones".
// - "Caramelo Agujereado": en el suelo de la primera habitacion, a la vista desde la entrada, con un brillo
//   verde que late (se puede mover).
// - "Destino Indicaciones": por defecto el punto mas lejano caminando desde la entrada (se puede mover).
// - "Indicaciones Ocultas": flechas en las paredes siguiendo el camino mas corto del caramelo al destino
//   (calculado con la capsula del jugador, no con el NavMesh).
// Si se mueve el caramelo o el destino: Tools > Backrooms > Rehacer indicaciones.
// Se aplica sola una vez; todo desde cero: Tools > Backrooms > Caramelo e indicaciones.
[InitializeOnLoad]
public static class BackroomsVistaSetup
{
    const string Carpeta = "Assets/Backrooms/Vista";
    const string NombreCaramelo = "Caramelo Agujereado";
    const string NombreDestino = "Destino Indicaciones";
    const string NombreMarcas = "Indicaciones Ocultas";
    const float Escala = 1.4f;               // tamano del caramelo (el modelo mide 12 cm)
    const float IntensidadBrillo = 6f;       // luz del caramelo en el suelo

    const float AltoMarca = 1.45f;       // altura de las flechas en la pared
    const float LargoFlecha = 0.9f;
    static readonly Color ColorPintura = new Color(0.35f, 1.6f, 1.25f, 0.95f);   // turquesa que brilla

    static BackroomsVistaSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += AutoAplicar;
        };
    }

    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject br = GameObject.Find("Backrooms");
        if (!br || !br.transform.Find("Inicio")) return;
        Transform caramelo = br.transform.Find(NombreCaramelo);
        if (!caramelo) { Aplicar(br.transform); return; }
        // Version anterior: lejos de la entrada y con una luz que casi no se veia. Se rehace
        Light luz = caramelo.GetComponentInChildren<Light>(true);
        if (luz && luz.intensity < IntensidadBrillo - 0.01f)
        {
            foreach (string n in new[] { NombreCaramelo, NombreMarcas })
            {
                Transform t = br.transform.Find(n);
                if (t) Undo.DestroyObjectImmediate(t.gameObject);
            }
            Transform destino = br.transform.Find(NombreDestino);
            Aplicar(br.transform, destino ? destino.position : (Vector3?)null);
        }
    }

    [MenuItem("Tools/Backrooms/Caramelo e indicaciones")]
    static void Menu()
    {
        GameObject br = GameObject.Find("Backrooms");
        if (!br) { EditorUtility.DisplayDialog("Backrooms", "No se encontro \"Backrooms\".", "OK"); return; }
        foreach (string n in new[] { NombreCaramelo, NombreDestino, NombreMarcas })
        {
            Transform t = br.transform.Find(n);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }
        Aplicar(br.transform);
    }

    [MenuItem("Tools/Backrooms/Rehacer indicaciones")]
    static void MenuIndicaciones()
    {
        GameObject br = GameObject.Find("Backrooms");
        Transform caramelo = br ? br.transform.Find(NombreCaramelo) : null;
        Transform destino = br ? br.transform.Find(NombreDestino) : null;
        if (!caramelo || !destino) { EditorUtility.DisplayDialog("Backrooms", "Primero: Tools > Backrooms > Caramelo e indicaciones.", "OK"); return; }
        Transform viejas = br.transform.Find(NombreMarcas);
        if (viejas) Undo.DestroyObjectImmediate(viejas.gameObject);
        int n = Indicaciones(br.transform, caramelo.position, destino.position, new Rejilla(br.transform));
        EditorSceneManager.MarkSceneDirty(br.scene);
        Debug.Log("[Backrooms] Indicaciones rehechas: " + n + " marcas. Guarda la escena.", br);
    }

    static void Aplicar(Transform br, Vector3? destinoFijo = null)
    {
        var rejilla = new Rejilla(br);
        if (!rejilla.Celda(br.Find("Inicio").position, out int inicio)) { Debug.LogWarning("[Backrooms] La entrada de los Backrooms no esta en un sitio por donde quepa el jugador."); return; }
        float[] dist = rejilla.Distancias(inicio);

        // Destino: lo mas lejos caminando desde la entrada
        int destino = inicio; float max = 0f;
        for (int i = 0; i < dist.Length; i++) if (dist[i] < float.MaxValue && dist[i] > max) { max = dist[i]; destino = i; }

        // Caramelo: en el suelo de la primera habitacion, junto a una pared y a la vista desde la entrada
        Vector3 ojos = br.Find("Inicio").position + Vector3.up * 1.6f;
        int sitio = inicio; float mejor = float.MaxValue;
        for (int i = 0; i < dist.Length; i++)
        {
            if (dist[i] < 4f || dist[i] > 14f) continue;
            Vector3 p = rejilla.Centro(i);
            bool pared = false;
            foreach (Vector3 d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                pared |= Physics.Raycast(p + Vector3.up * 0.3f, d, rejilla.radio + 0.35f, ~0, QueryTriggerInteraction.Ignore);
            bool aLaVista = !Physics.Linecast(ojos, p + Vector3.up * 0.08f, ~0, QueryTriggerInteraction.Ignore);
            float puntos = Mathf.Abs(dist[i] - 8f) + (pared ? 0f : 6f) + (aLaVista ? 0f : 20f);
            if (puntos < mejor) { mejor = puntos; sitio = i; }
        }

        Directory.CreateDirectory(Carpeta);
        GameObject caramelo = CrearCaramelo(br, rejilla.Centro(sitio));

        Transform dViejo = br.Find(NombreDestino);
        GameObject dGo = dViejo ? dViejo.gameObject : new GameObject(NombreDestino);
        if (!dViejo) Undo.RegisterCreatedObjectUndo(dGo, "Destino");
        else Undo.RecordObject(dGo.transform, "Destino");
        dGo.transform.SetParent(br, true);
        dGo.transform.position = destinoFijo ?? rejilla.Centro(destino);

        int n = Indicaciones(br, caramelo.transform.position, dGo.transform.position, rejilla);
        EditorSceneManager.MarkSceneDirty(br.gameObject.scene);
        Debug.Log($"[Backrooms] Caramelo agujereado a {dist[sitio]:0} m caminando de la entrada y {n} indicaciones ocultas hasta \"{NombreDestino}\" ({max:0} m caminando desde la entrada). Guarda la escena.", caramelo);
    }

    // ------------------------------------------------------------------ Caramelo

    static GameObject CrearCaramelo(Transform br, Vector3 suelo)
    {
        Mesh malla = Asset(Carpeta + "/CarameloAgujereado.asset", () => Toroide(0.042f, 0.017f, 32, 14));
        Material mat = Asset(Carpeta + "/Caramelo.mat", () =>
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.45f, 0.8f, 0.5f));
            m.SetFloat("_Smoothness", 0.85f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.05f, 0.18f, 0.08f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        });

        // Brilla por si mismo (con el bloom se ve desde lejos)
        if (mat.GetColor("_EmissionColor").maxColorComponent < 1f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.35f, 1.5f, 0.55f));
            EditorUtility.SetDirty(mat);
        }

        var go = new GameObject(NombreCaramelo);
        Undo.RegisterCreatedObjectUndo(go, "Caramelo");
        go.transform.SetParent(br, true);
        // Tumbado en el suelo (el agujero mira hacia arriba)
        go.transform.SetPositionAndRotation(suelo + Vector3.up * 0.018f * Escala, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
        go.transform.localScale = Vector3.one * Escala;
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(0.16f, 0.16f, 0.08f);    // mas grande que el caramelo, para apuntarle facil

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;          // quieto en el suelo hasta que lo recojan
        rb.mass = 0.05f;
        var inter = go.AddComponent<Interactable>();
        inter.SetPrompt("Recoger caramelo");
        var grab = go.AddComponent<Grabbable>();
        if (inter.onInteract == null) inter.onInteract = new UnityEngine.Events.UnityEvent<PlayerInteractor>();
        UnityEventTools.AddPersistentListener(inter.onInteract, grab.PickUp);
        // En la mano: de canto, con el agujero hacia delante
        var so = new SerializedObject(grab);
        so.FindProperty("holdRotation").vector3Value = new Vector3(0f, 0f, 0f);
        so.ApplyModifiedProperties();

        // Brillo verde que late, para que se vea desde la entrada
        var luzGo = new GameObject("Brillo");
        luzGo.transform.SetParent(go.transform, false);
        luzGo.transform.position = suelo + Vector3.up * 0.3f;
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(0.45f, 1f, 0.55f);
        luz.range = 3.5f;
        luz.intensity = IntensidadBrillo;
        luz.shadows = LightShadows.None;

        var vidente = go.AddComponent<DulceVidente>();
        vidente.brillo = luz;
        return go;
    }

    // Toroide con el agujero en el eje Z local
    static Mesh Toroide(float radio, float grosor, int segs, int lados)
    {
        var v = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i <= segs; i++)
        {
            float a = i * Mathf.PI * 2f / segs;
            Vector3 c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= lados; j++)
            {
                float b = j * Mathf.PI * 2f / lados;
                // Un poco aplastado: mas ancho que hondo, como una pastilla
                Vector3 n = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                Vector3 p = c * (radio + grosor * Mathf.Cos(b)) + Vector3.forward * (grosor * 0.75f * Mathf.Sin(b));
                v.Add(p); nrm.Add(n.normalized); uv.Add(new Vector2((float)i / segs, (float)j / lados));
            }
        }
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < lados; j++)
            {
                int a = i * (lados + 1) + j, b = a + lados + 1;
                tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        var m = new Mesh { name = "CarameloAgujereado" };
        m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------------ Indicaciones

    static int Indicaciones(Transform br, Vector3 desde, Vector3 hasta, Rejilla rejilla)
    {
        if (!rejilla.Celda(desde, out int a) || !rejilla.Celda(hasta, out int b)) { Debug.LogWarning("[Backrooms] El caramelo o el destino estan donde no cabe el jugador."); return 0; }
        List<Vector3> ruta = rejilla.Ruta(a, b);
        if (ruta == null) { Debug.LogWarning("[Backrooms] No hay camino del caramelo al destino."); return 0; }

        Material flecha = MaterialPintura("Flecha", () => TexturaFlecha());
        Material circulo = MaterialPintura("Circulo", () => TexturaCirculo());
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        var raiz = new GameObject(NombreMarcas);
        Undo.RegisterCreatedObjectUndo(raiz, "Indicaciones");
        raiz.transform.SetParent(br, true);
        raiz.transform.position = br.position;

        var puestas = new List<Vector3>();
        Vector3[] c = ruta.ToArray();
        int n = 0;

        // Recorrer el camino a pasos cortos y encontrar los huecos de las paredes por los que pasa
        var muestras = new List<(Vector3 p, Vector3 dir)>();
        for (int i = 0; i < c.Length - 1; i++)
        {
            Vector3 dir = Plano(c[i + 1] - c[i]);
            float largo = Vector3.Distance(Plano3(c[i]), Plano3(c[i + 1]));
            for (float d = 0f; d < largo; d += 0.05f) muestras.Add((c[i] + dir * d, dir));
        }
        var huecos = new List<(Vector3 centro, Vector3 dir)>();
        int inicioHueco = -1;
        for (int i = 0; i <= muestras.Count; i++)
        {
            bool estrecho = i < muestras.Count && Estrecho(br, muestras[i].p, muestras[i].dir, out _);
            if (estrecho && inicioHueco < 0) inicioHueco = i;
            if (!estrecho && inicioHueco >= 0)
            {
                int medio = (inicioHueco + i - 1) / 2;
                Estrecho(br, muestras[medio].p, muestras[medio].dir, out Vector3 centro);
                // Direccion de paso: perpendicular a la pared del hueco, hacia donde va el camino
                Vector3 paso = muestras[Mathf.Min(i, muestras.Count - 1)].p - muestras[inicioHueco].p;
                paso = Plano(paso);
                if (paso == Vector3.zero) paso = muestras[medio].dir;
                huecos.Add((centro, paso));
                inicioHueco = -1;
            }
        }

        // Junto al caramelo: hacia el primer hueco (o el siguiente tramo)
        Vector3 primero = huecos.Count > 0 ? huecos[0].centro : c[Mathf.Min(1, c.Length - 1)];
        Vector3 haciaPrimero = Plano(primero - c[0]);
        RaycastHit cerca = default; bool hayCerca = false;
        foreach (Vector3 d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            if (Pared(br, c[0], d, 2.5f, out RaycastHit h) && (!hayCerca || h.distance < cerca.distance)) { cerca = h; hayCerca = true; }
        if (hayCerca && Marca(raiz.transform, quad, flecha, cerca, haciaPrimero, LargoFlecha, puestas)) n++;

        // En cada hueco: flecha en la pared de al lado (del lado por donde se llega) apuntando al hueco
        foreach (var (centro, paso) in huecos)
        {
            Vector3 lado = Vector3.Cross(Vector3.up, paso);
            bool puesta = false;
            foreach (float s in new[] { 1f, -1f })
            {
                for (float sep = 0.9f; sep <= 1.8f && !puesta; sep += 0.3f)
                {
                    Vector3 desdeAqui = centro - paso * 1.2f + lado * s * sep;
                    if (Pared(br, desdeAqui, paso, 1.6f, out RaycastHit h) && Vector3.Dot(h.normal, -paso) > 0.7f
                        && Marca(raiz.transform, quad, flecha, h, -lado * s, LargoFlecha * 0.8f, puestas)) { n++; puesta = true; }
                }
                if (puesta) break;
            }
        }

        // Meta: circulo grande en el suelo
        var meta = Pieza(raiz.transform, quad, circulo, "Meta");
        meta.transform.SetPositionAndRotation(c[c.Length - 1] + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f));
        meta.transform.localScale = Vector3.one * 1.4f;
        n++;
        return n;
    }

    // El camino pasa por un hueco de puerta (paredes a los dos lados, a menos de 2.4 m en total). Devuelve su centro
    static bool Estrecho(Transform br, Vector3 p, Vector3 dir, out Vector3 centro)
    {
        centro = p;
        if (dir == Vector3.zero) return false;
        Vector3 lado = Vector3.Cross(Vector3.up, dir);
        bool a = Pared(br, p, lado, 2f, out RaycastHit ha), b = Pared(br, p, -lado, 2f, out RaycastHit hb);
        if (!a || !b || ha.distance + hb.distance > 2.4f) return false;
        centro = p + lado * (ha.distance - hb.distance) * 0.5f;
        return true;
    }

    static bool Pared(Transform br, Vector3 p, Vector3 dir, float max, out RaycastHit h)
    {
        Vector3 o = new Vector3(p.x, p.y + AltoMarca, p.z);
        if (Physics.Raycast(o, dir, out h, max, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(h.normal.y) < 0.3f && h.collider.transform.IsChildOf(br)) return true;
        return false;
    }

    // Flecha pintada en la pared del impacto, apuntando hacia "hacia" (en el plano de la pared)
    static bool Marca(Transform raiz, Mesh quad, Material mat, RaycastHit h, Vector3 hacia, float largo, List<Vector3> puestas)
    {
        Vector3 dir = Vector3.ProjectOnPlane(hacia, h.normal); dir.y = 0f;
        if (dir.sqrMagnitude < 0.25f) return false;   // apuntaria hacia dentro de la pared
        dir.Normalize();
        foreach (Vector3 p in puestas) if ((p - h.point).sqrMagnitude < 1.5f * 1.5f) return false;
        puestas.Add(h.point);

        var go = Pieza(raiz, quad, mat, "Flecha " + puestas.Count);
        Vector3 frente = -h.normal;                          // el Quad se ve desde su -Z
        go.transform.SetPositionAndRotation(h.point + h.normal * 0.015f, Quaternion.LookRotation(frente, Vector3.Cross(frente, dir)));
        // Un poco torcida, como pintada a mano
        go.transform.Rotate(0f, 0f, Random.Range(-6f, 6f), Space.Self);
        go.transform.localScale = new Vector3(largo, largo * 0.5f, 1f);
        return true;
    }

    static GameObject Pieza(Transform padre, Mesh quad, Material mat, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.AddComponent<MarcaOculta>();
        return go;
    }

    static Vector3 Plano(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero; }
    static Vector3 Plano3(Vector3 v) { v.y = 0f; return v; }

    // Material de pintura que brilla (sin luz, transparente)
    static Material MaterialPintura(string nombre, System.Func<Texture2D> textura)
    {
        string ruta = Carpeta + "/" + nombre + ".png";
        if (!File.Exists(ruta))
        {
            File.WriteAllBytes(ruta, textura().EncodeToPNG());
            AssetDatabase.ImportAsset(ruta);
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        return Asset(Carpeta + "/Pintura" + nombre + ".mat", () =>
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", ColorPintura);
            return m;
        });
    }

    // Flecha hacia la derecha (+X), con bordes irregulares de pintura
    static Texture2D TexturaFlecha()
    {
        const int w = 256, h = 128;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var rnd = new System.Random(5);
        float[] ruido = new float[w];
        for (int i = 0; i < w; i++) ruido[i] = (float)rnd.NextDouble();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h - 0.5f;
                float borde = (ruido[(x + y * 7) % w] - 0.5f) * 0.03f;
                bool cuerpo = u > 0.06f && u < 0.62f && Mathf.Abs(v) < 0.11f + borde;
                bool punta = u >= 0.55f && u < 0.95f && Mathf.Abs(v) < (0.95f - u) * 1.15f + borde;
                float a = cuerpo || punta ? 1f : 0f;
                // Pintura mas fina en algunas zonas
                a *= 0.75f + 0.25f * ruido[(x * 3 + y) % w];
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    static Texture2D TexturaCirculo()
    {
        const int n = 256;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var rnd = new System.Random(9);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x / (float)n - 0.5f, dy = y / (float)n - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float r = (float)rnd.NextDouble() * 0.01f;
                bool anillo = d > 0.36f + r && d < 0.46f + r;
                bool centro = d < 0.08f + r;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, anillo || centro ? 0.85f + (float)rnd.NextDouble() * 0.15f : 0f));
            }
        t.Apply();
        return t;
    }

    // ------------------------------------------------------------------ Camino

    // Rejilla de celdas por donde cabe el jugador (su capsula real). No usa el NavMesh: el de los Backrooms
    // no pasa por los huecos entre habitaciones.
    internal class Rejilla
    {
        const float Paso = 0.25f;
        public readonly float radio;
        readonly float alto, suelo;
        readonly Vector3 origen;
        readonly int nx, nz;
        readonly bool[] libre;

        public Rejilla(Transform br)
        {
            PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            var cap = pm ? pm.GetComponent<CapsuleCollider>() : null;
            Vector3 esc = pm ? pm.transform.lossyScale : Vector3.one;
            radio = cap ? cap.radius * Mathf.Max(esc.x, esc.z) - 0.03f : 0.3f;
            alto = cap ? cap.height * esc.y : 1.8f;

            Bounds b = Limites(br);
            Transform ini = br.Find("Inicio");
            Vector3 c0 = ini ? ini.position : b.center;
            suelo = c0.y;
            if (Physics.Raycast(new Vector3(c0.x, c0.y + 1f, c0.z), Vector3.down, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore)) suelo = h.point.y;
            origen = new Vector3(b.min.x, suelo, b.min.z);
            nx = Mathf.CeilToInt(b.size.x / Paso); nz = Mathf.CeilToInt(b.size.z / Paso);
            libre = new bool[nx * nz];
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    Vector3 p = origen + new Vector3((x + 0.5f) * Paso, 0f, (z + 0.5f) * Paso);
                    bool hayPiso = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore);
                    libre[x * nz + z] = hayPiso && !Physics.CheckCapsule(p + Vector3.up * (radio + 0.06f), p + Vector3.up * Mathf.Max(radio + 0.07f, alto - radio), radio, ~0, QueryTriggerInteraction.Ignore);
                }
        }

        public Vector3 Centro(int i) => origen + new Vector3((i / nz + 0.5f) * Paso, 0f, (i % nz + 0.5f) * Paso);

        // La celda libre mas cercana al punto
        public bool Celda(Vector3 p, out int celda)
        {
            celda = -1;
            int cx = Mathf.FloorToInt((p.x - origen.x) / Paso), cz = Mathf.FloorToInt((p.z - origen.z) / Paso);
            for (int r = 0; r < 8; r++)
                for (int x = cx - r; x <= cx + r; x++)
                    for (int z = cz - r; z <= cz + r; z++)
                        if (x >= 0 && z >= 0 && x < nx && z < nz && libre[x * nz + z]) { celda = x * nz + z; return true; }
            return false;
        }

        IEnumerable<(int, float)> Vecinas(int i)
        {
            int x = i / nz, z = i % nz;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int vx = x + dx, vz = z + dz;
                    if (vx < 0 || vz < 0 || vx >= nx || vz >= nz || !libre[vx * nz + vz]) continue;
                    // En diagonal, sin cortar esquinas
                    if (dx != 0 && dz != 0 && (!libre[vx * nz + z] || !libre[x * nz + vz])) continue;
                    yield return (vx * nz + vz, dx != 0 && dz != 0 ? Paso * 1.4142f : Paso);
                }
        }

        // Distancia caminando a todas las celdas
        public float[] Distancias(int desde) => Dijkstra(desde, -1, out _);

        float[] Dijkstra(int desde, int hasta, out int[] previa)
        {
            var dist = new float[libre.Length];
            previa = new int[libre.Length];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; previa[i] = -1; }
            var abiertas = new SortedSet<(float, int)>();
            dist[desde] = 0f;
            abiertas.Add((0f, desde));
            while (abiertas.Count > 0)
            {
                var actual = abiertas.Min;
                abiertas.Remove(actual);
                int i = actual.Item2;
                if (i == hasta) break;
                foreach (var (v, coste) in Vecinas(i))
                {
                    float nd = actual.Item1 + coste;
                    if (nd >= dist[v]) continue;
                    if (dist[v] < float.MaxValue) abiertas.Remove((dist[v], v));
                    dist[v] = nd;
                    previa[v] = i;
                    abiertas.Add((nd, v));
                }
            }
            return dist;
        }

        // Camino como lista de esquinas (se quitan los puntos que se ven en linea recta)
        public List<Vector3> Ruta(int desde, int hasta)
        {
            float[] dist = Dijkstra(desde, hasta, out int[] previa);
            if (dist[hasta] == float.MaxValue) return null;
            var celdas = new List<int>();
            for (int i = hasta; i >= 0; i = previa[i]) celdas.Add(i);
            celdas.Reverse();

            var ruta = new List<Vector3> { Centro(celdas[0]) };
            int ancla = 0;
            for (int i = 2; i < celdas.Count; i++)
                if (!Recto(celdas[ancla], celdas[i]))
                {
                    ancla = i - 1;
                    ruta.Add(Centro(celdas[ancla]));
                }
            ruta.Add(Centro(celdas[celdas.Count - 1]));
            return ruta;
        }

        // Se puede ir en linea recta de una celda a otra (todas las celdas del medio libres)
        bool Recto(int a, int b)
        {
            Vector3 pa = Centro(a), pb = Centro(b);
            int pasos = Mathf.CeilToInt(Vector3.Distance(pa, pb) / (Paso * 0.5f));
            for (int s = 1; s < pasos; s++)
            {
                Vector3 p = Vector3.Lerp(pa, pb, s / (float)pasos);
                int x = Mathf.FloorToInt((p.x - origen.x) / Paso), z = Mathf.FloorToInt((p.z - origen.z) / Paso);
                if (x < 0 || z < 0 || x >= nx || z >= nz || !libre[x * nz + z]) return false;
            }
            return true;
        }
    }

    // ------------------------------------------------------------------ Utilidades

    static T Asset<T>(string ruta, System.Func<T> crear) where T : Object
    {
        T a = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (a) return a;
        a = crear();
        AssetDatabase.CreateAsset(a, ruta);
        AssetDatabase.SaveAssets();
        return a;
    }

    static Bounds Limites(Transform t)
    {
        Collider[] cs = t.GetComponentsInChildren<Collider>();
        Bounds b = cs.Length > 0 ? cs[0].bounds : new Bounds(t.position, Vector3.one * 50f);
        foreach (Collider c in cs) b.Encapsulate(c.bounds);
        return b;
    }
}
