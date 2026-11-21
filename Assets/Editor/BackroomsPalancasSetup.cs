using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Minijuego de las palancas de los Backrooms (ver PuzzlePalancas): todo dentro de "Backrooms/Palancas".
// - 4 palancas en paredes, repartidas por el mapa (lejos unas de otras). La segunda mas cercana a la entrada esta
//   ROTA: le falta la manija.
// - "Manija de palanca": en el suelo, donde terminan las indicaciones ocultas de la piedra ("Destino Indicaciones").
// - "Tablero": en una pared a media distancia, con 4 focos y un contador.
// - "Camara Seguridad": en lo alto, unos metros enfrente del tablero, mirandolo (su hijo "Punto de vista" es desde
//   donde se ve la cinematica; se puede mover y girar).
// Se aplica sola una vez; rehacer: Tools > Backrooms > Palancas. Ctrl+Z deshace. Despues guarda la escena.
[InitializeOnLoad]
public static class BackroomsPalancasSetup
{
    const string Nombre = "Palancas";
    const string Carpeta = "Assets/Backrooms/Palancas";
    const int Cantidad = 4;
    const float AltoPalanca = 1.15f, AltoTablero = 1.5f;

    static BackroomsPalancasSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject br = GameObject.Find("Backrooms");
        if (!br || !br.transform.Find("Inicio") || br.transform.Find(Nombre)) return;
        Aplicar(br.transform);
    }

    [MenuItem("Tools/Backrooms/Palancas (rehacer)")]
    static void Menu()
    {
        GameObject br = GameObject.Find("Backrooms");
        if (!br || !br.transform.Find("Inicio")) { EditorUtility.DisplayDialog("Backrooms", "No se encontro \"Backrooms/Inicio\".", "OK"); return; }
        Transform viejo = br.transform.Find(Nombre);
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        Aplicar(br.transform);
    }

    struct Pared { public Vector3 punto, normal; public int celda; public float dist; }

    static void Aplicar(Transform br)
    {
        Physics.SyncTransforms();
        var rejilla = new BackroomsVistaSetup.Rejilla(br);
        Vector3 inicioPos = br.Find("Inicio").position;
        if (!rejilla.Celda(inicioPos, out int inicio)) { Debug.LogWarning("[Backrooms] La entrada no esta en un sitio libre.", br); return; }
        float[] dist = rejilla.Distancias(inicio);
        float max = 0f;
        foreach (float d in dist) if (d < float.MaxValue && d > max) max = d;
        if (max < 10f) { Debug.LogWarning("[Backrooms] Los Backrooms son muy chicos para las palancas.", br); return; }

        Transform destino = br.Find("Destino Indicaciones");
        Transform caramelo = br.Find("Caramelo Agujereado");

        // Celdas junto a una pared (cada 4 celdas para no probar todas)
        var paredes = new List<Pared>();
        for (int i = 0; i < dist.Length; i += 4)
        {
            if (dist[i] == float.MaxValue || dist[i] < 5f) continue;
            if (BuscarPared(br, rejilla.Centro(i), rejilla.radio, out Vector3 p, out Vector3 n))
                paredes.Add(new Pared { punto = p, normal = n, celda = i, dist = dist[i] });
        }
        if (paredes.Count < Cantidad + 1) { Debug.LogWarning("[Backrooms] No encontre paredes para las palancas.", br); return; }

        var rnd = new System.Random(1234);
        // Tablero: a media distancia y con sitio enfrente para la camara
        Pared tablero = default;
        Vector3 posCam = default;
        bool hayTablero = false;
        paredes.Sort((a, b) => Mathf.Abs(a.dist - max * 0.45f).CompareTo(Mathf.Abs(b.dist - max * 0.45f)));
        foreach (Pared w in paredes)
            if (PoseCamara(w, out posCam)) { tablero = w; hayTablero = true; break; }
        if (!hayTablero) { Debug.LogWarning("[Backrooms] No encontre una pared con sitio para la camara de seguridad.", br); return; }

        // Palancas: repartidas (cada una lo mas lejos posible de las demas, del tablero y de la manija)
        var elegidas = new List<Pared>();
        var evitar = new List<Vector3> { tablero.punto, inicioPos };
        if (destino) evitar.Add(destino.position);
        if (caramelo) evitar.Add(caramelo.position);
        for (int k = 0; k < Cantidad; k++)
        {
            Pared mejor = default; float mejorPuntos = float.MinValue;
            foreach (Pared w in paredes)
            {
                if (w.dist < max * 0.15f) continue;
                float cerca = float.MaxValue;
                foreach (Vector3 e in evitar) cerca = Mathf.Min(cerca, Plano(w.punto - e));
                foreach (Pared e in elegidas) cerca = Mathf.Min(cerca, Plano(w.punto - e.punto));
                if (cerca < 6f) continue;
                float puntos = cerca + (float)rnd.NextDouble() * 2f;
                if (puntos > mejorPuntos) { mejorPuntos = puntos; mejor = w; }
            }
            if (mejorPuntos == float.MinValue) break;
            elegidas.Add(mejor);
        }
        if (elegidas.Count < Cantidad) Debug.LogWarning("[Backrooms] Solo hubo sitio para " + elegidas.Count + " palancas.", br);
        // De la mas cercana a la entrada a la mas lejana; la segunda es la rota
        elegidas.Sort((a, b) => a.dist.CompareTo(b.dist));
        int rota = Mathf.Min(1, elegidas.Count - 1);

        Directory.CreateDirectory(Carpeta);
        Material metal = Mat("Metal", new Color(0.23f, 0.23f, 0.22f), 0.55f, 0.45f);
        Material oscuro = Mat("Panel", new Color(0.07f, 0.07f, 0.075f), 0.3f, 0.2f);
        Material rojo = Mat("Pomo", new Color(0.7f, 0.06f, 0.05f), 0.6f, 0f);
        Material foco = Mat("Foco", new Color(0.18f, 0.05f, 0.04f), 0.8f, 0f, true);
        Material led = Mat("Led", new Color(1f, 0.1f, 0.08f), 0.5f, 0f, true, new Color(3f, 0.2f, 0.15f));
        Material filtro = MaterialFiltro();

        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Palancas");
        raiz.transform.SetParent(br, true);
        raiz.transform.position = tablero.punto;
        var puzzle = raiz.AddComponent<PuzzlePalancas>();

        // Manija suelta (en el destino de las indicaciones)
        Vector3 suelo = destino ? destino.position : rejilla.Centro(inicio);
        if (Physics.Raycast(suelo + Vector3.up * 1f, Vector3.down, out RaycastHit hs, 3f, ~0, QueryTriggerInteraction.Ignore)) suelo = hs.point;
        Grabbable pieza = CrearPieza(raiz.transform, suelo, metal, rojo);

        var palancas = new List<Palanca>();
        for (int i = 0; i < elegidas.Count; i++)
            palancas.Add(CrearPalanca(raiz.transform, "Palanca " + (i + 1), elegidas[i], i == rota, pieza, metal, oscuro, rojo));

        CrearTablero(raiz.transform, tablero, metal, oscuro, foco, puzzle, palancas.Count);
        Transform vista = CrearCamara(raiz.transform, posCam, tablero, metal, oscuro, led);

        puzzle.palancas = palancas.ToArray();
        puzzle.camaraSeguridad = vista;
        puzzle.filtroCamara = filtro;
        puzzle.pieza = pieza;

        EditorSceneManager.MarkSceneDirty(br.gameObject.scene);
        Debug.Log("[Backrooms] Palancas listas: " + palancas.Count + " palancas (la " + (rota + 1) + " rota, su manija en \"Destino Indicaciones\"), " +
                  "tablero y camara de seguridad. Se pueden mover. Guarda la escena (Ctrl+S).", raiz);
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // Pared a menos de medio metro del borde de la celda (hacia uno de los 4 lados)
    static bool BuscarPared(Transform br, Vector3 celda, float radio, out Vector3 punto, out Vector3 normal)
    {
        punto = normal = default;
        foreach (Vector3 d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            Vector3 o = celda + Vector3.up * AltoPalanca;
            if (!Physics.Raycast(o, d, out RaycastHit h, radio + 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Mathf.Abs(h.normal.y) > 0.2f || !h.collider.transform.IsChildOf(br)) continue;
            // Pared lisa: tambien hay pared un poco a cada lado (no una esquina ni un borde)
            Vector3 lado = Vector3.Cross(Vector3.up, h.normal).normalized;
            bool liso = true;
            foreach (float s in new[] { -0.45f, 0.45f })
                if (!Physics.Raycast(o + lado * s, d, radio + 0.7f, ~0, QueryTriggerInteraction.Ignore)) liso = false;
            if (!liso) continue;
            normal = new Vector3(h.normal.x, 0f, h.normal.z).normalized;
            punto = new Vector3(h.point.x, celda.y, h.point.z);
            return true;
        }
        return false;
    }

    // Un sitio en lo alto, enfrente del tablero (un poco de lado), desde donde se lo ve sin nada en medio
    static bool PoseCamara(Pared w, out Vector3 pos)
    {
        pos = default;
        Vector3 lado = Vector3.Cross(Vector3.up, w.normal).normalized;
        Vector3 centro = w.punto + Vector3.up * AltoTablero + w.normal * 0.15f;
        foreach (float d in new[] { 5.5f, 4.5f, 6.5f, 3.6f })
            foreach (float s in new[] { 1.6f, -1.6f, 0.8f, -0.8f, 0f })
            {
                Vector3 baseP = w.punto + w.normal * d + lado * s;
                float techo = 3f;
                if (Physics.Raycast(baseP + Vector3.up * 0.5f, Vector3.up, out RaycastHit ht, 6f, ~0, QueryTriggerInteraction.Ignore)) techo = ht.point.y - w.punto.y;
                if (techo < 2f) continue;
                Vector3 p = baseP + Vector3.up * Mathf.Min(2.55f, techo - 0.3f);
                if (Physics.CheckSphere(p, 0.25f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(p, centro, ~0, QueryTriggerInteraction.Ignore)) continue;
                pos = p;
                return true;
            }
        return false;
    }

    // ------------------------------------------------------------------ Piezas

    static GameObject Cubo(Transform padre, string nombre, Vector3 pos, Vector3 tam, Material m, PrimitiveType tipo = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = tam;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (m) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Barra con pomo rojo (la parte que se agarra), a lo largo del eje Y local
    static GameObject Manija(Transform padre, string nombre, Material metal, Material rojo)
    {
        var m = new GameObject(nombre);
        m.transform.SetParent(padre, false);
        Cubo(m.transform, "Barra", new Vector3(0f, 0.17f, 0f), new Vector3(0.035f, 0.17f, 0.035f), metal, PrimitiveType.Cylinder);
        Cubo(m.transform, "Pomo", new Vector3(0f, 0.36f, 0f), Vector3.one * 0.075f, rojo, PrimitiveType.Sphere);
        return m;
    }

    static Palanca CrearPalanca(Transform padre, string nombre, Pared w, bool rota, Grabbable pieza, Material metal, Material oscuro, Material rojo)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(w.punto + Vector3.up * AltoPalanca + w.normal * 0.01f, Quaternion.LookRotation(w.normal));

        Cubo(go.transform, "Placa", new Vector3(0f, 0f, 0.015f), new Vector3(0.26f, 0.5f, 0.03f), metal);
        Cubo(go.transform, "Caja", new Vector3(0f, 0f, 0.05f), new Vector3(0.16f, 0.3f, 0.05f), oscuro);
        Cubo(go.transform, "Ranura", new Vector3(0f, 0f, 0.076f), new Vector3(0.03f, 0.24f, 0.005f), metal);
        foreach (float y in new[] { -0.21f, 0.21f })
            foreach (float x in new[] { -0.1f, 0.1f })
                Cubo(go.transform, "Tornillo", new Vector3(x, y, 0.032f), Vector3.one * 0.018f, metal, PrimitiveType.Sphere);

        var pivote = new GameObject("Pivote").transform;
        pivote.SetParent(go.transform, false);
        pivote.localPosition = new Vector3(0f, 0f, 0.08f);
        Cubo(pivote, "Base de la manija", new Vector3(0f, 0.025f, 0f), new Vector3(0.05f, 0.03f, 0.05f), metal, PrimitiveType.Cylinder);
        GameObject manija = Manija(pivote, "Manija", metal, rojo);

        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0f, 0.18f);
        col.size = new Vector3(0.32f, 0.7f, 0.4f);

        var inter = go.AddComponent<Interactable>();
        inter.SetPrompt(rota ? "Revisar palanca" : "Bajar palanca");
        var p = go.AddComponent<Palanca>();
        p.pivote = pivote;
        p.manija = manija;
        p.rota = rota;
        p.anguloArriba = 40f;
        p.anguloAbajo = 140f;
        pivote.localRotation = Quaternion.Euler(40f, 0f, 0f);
        if (rota)
        {
            manija.SetActive(false);
            var r = go.AddComponent<ReceptorObjeto>();
            r.objetoEsperado = pieza;
            r.prompt = "Poner la manija";
            r.puntoMano = pivote;
            r.ocultarAlRecibir = true;
        }
        return p;
    }

    static Grabbable CrearPieza(Transform padre, Vector3 suelo, Material metal, Material rojo)
    {
        var go = new GameObject("Manija de palanca");
        Undo.RegisterCreatedObjectUndo(go, "Manija");
        go.transform.SetParent(padre, true);
        // Tirada en el suelo
        go.transform.SetPositionAndRotation(suelo + Vector3.up * 0.04f, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
        Transform m = Manija(go.transform, "Modelo", metal, rojo).transform;
        m.localPosition = new Vector3(0f, -0.2f, 0f);
        var col = go.AddComponent<BoxCollider>();
        col.center = Vector3.zero;
        col.size = new Vector3(0.14f, 0.5f, 0.14f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.mass = 0.4f;
        var inter = go.AddComponent<Interactable>();
        inter.SetPrompt("Recoger manija");
        var grab = go.AddComponent<Grabbable>();
        if (inter.onInteract == null) inter.onInteract = new UnityEngine.Events.UnityEvent<PlayerInteractor>();
        UnityEventTools.AddPersistentListener(inter.onInteract, grab.PickUp);
        var oi = go.AddComponent<ObjetoInventario>();
        oi.nombre = "Manija";
        oi.inicial = "M";
        return grab;
    }

    static void CrearTablero(Transform padre, Pared w, Material metal, Material oscuro, Material foco, PuzzlePalancas puzzle, int n)
    {
        var go = new GameObject("Tablero");
        Undo.RegisterCreatedObjectUndo(go, "Tablero");
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(w.punto + Vector3.up * AltoTablero + w.normal * 0.01f, Quaternion.LookRotation(w.normal));
        Cubo(go.transform, "Marco", new Vector3(0f, 0f, 0.02f), new Vector3(1.42f, 0.86f, 0.04f), metal);
        Cubo(go.transform, "Panel", new Vector3(0f, 0f, 0.045f), new Vector3(1.3f, 0.74f, 0.02f), oscuro);
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0f, 0.04f);
        col.size = new Vector3(1.42f, 0.86f, 0.08f);

        var focos = new List<Renderer>();
        var luces = new List<Light>();
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(-0.45f, 0.45f, n > 1 ? i / (float)(n - 1) : 0.5f);
            Cubo(go.transform, "Aro " + (i + 1), new Vector3(x, -0.02f, 0.06f), new Vector3(0.2f, 0.012f, 0.2f), metal, PrimitiveType.Cylinder)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject f = Cubo(go.transform, "Foco " + (i + 1), new Vector3(x, -0.02f, 0.08f), Vector3.one * 0.15f, foco, PrimitiveType.Sphere);
            focos.Add(f.GetComponent<Renderer>());
            var lg = new GameObject("Luz " + (i + 1));
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(x, -0.02f, 0.3f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.5f);
            l.range = 1.8f;
            l.intensity = 0f;
            l.enabled = false;
            luces.Add(l);
        }
        puzzle.focos = focos.ToArray();
        puzzle.lucesFocos = luces.ToArray();

        Texto(go.transform, "Titulo", new Vector3(0f, 0.25f, 0.058f), "ENERGÍA", 0.85f, new Color(0.85f, 0.82f, 0.7f));
        puzzle.contador = Texto(go.transform, "Contador", new Vector3(0f, -0.27f, 0.058f), "0 / " + n, 0.7f, new Color(1f, 0.85f, 0.4f));
    }

    static TextMeshPro Texto(Transform padre, string nombre, Vector3 pos, string texto, float tam, Color color)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        // El texto de TMP se lee desde su -Z: se gira para que mire hacia afuera de la pared
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var t = go.AddComponent<TextMeshPro>();
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.rectTransform.sizeDelta = new Vector2(1.2f, 0.2f);
        return t;
    }

    static Transform CrearCamara(Transform padre, Vector3 pos, Pared w, Material metal, Material oscuro, Material led)
    {
        Vector3 mira = w.punto + Vector3.up * AltoTablero;
        var go = new GameObject("Camara Seguridad");
        Undo.RegisterCreatedObjectUndo(go, "Camara Seguridad");
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mira - pos));
        Cubo(go.transform, "Cuerpo", new Vector3(0f, 0f, -0.05f), new Vector3(0.16f, 0.13f, 0.3f), oscuro);
        Cubo(go.transform, "Visera", new Vector3(0f, 0.075f, 0f), new Vector3(0.19f, 0.015f, 0.36f), metal);
        Cubo(go.transform, "Lente", new Vector3(0f, 0f, 0.11f), new Vector3(0.08f, 0.025f, 0.08f), metal, PrimitiveType.Cylinder)
            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Cubo(go.transform, "Led", new Vector3(0.05f, 0.04f, 0.1f), Vector3.one * 0.015f, led, PrimitiveType.Sphere);
        // Soporte hasta el techo
        float techo = Physics.Raycast(pos, Vector3.up, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 0.3f;
        var soporte = Cubo(go.transform, "Soporte", Vector3.zero, new Vector3(0.03f, techo, 0.03f), metal);
        soporte.transform.position = pos + Vector3.up * (techo * 0.5f);
        soporte.transform.rotation = Quaternion.identity;

        // Desde donde se ve la cinematica: delante del lente (asi la camara no se ve a si misma)
        var vista = new GameObject("Punto de vista").transform;
        vista.SetParent(go.transform, false);
        vista.localPosition = new Vector3(0f, 0f, 0.2f);
        vista.localRotation = Quaternion.identity;
        return vista;
    }

    // ------------------------------------------------------------------ Materiales

    static Material Mat(string nombre, Color color, float liso, float metalico, bool emision = false, Color brillo = default)
    {
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", liso);
        m.SetFloat("_Metallic", metalico);
        if (emision)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", brillo);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static Material MaterialFiltro()
    {
        string ruta = Carpeta + "/CamaraSeguridad.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        Shader s = Shader.Find("Slappy/CamaraSeguridad");
        if (s == null) { Debug.LogWarning("[Backrooms] No encuentro el shader Slappy/CamaraSeguridad: la camara se vera sin filtro."); return null; }
        m = new Material(s);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }
}
