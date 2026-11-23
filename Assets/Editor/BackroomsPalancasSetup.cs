using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Los Backrooms segun el guion (ver BackroomsGuion y PuzzlePalancas). Todo dentro de "Backrooms/Palancas":
// - "Habitacion Cadaver": el cadaver (con la piedra encima y el gato de sombra al principio), el tablero de focos con
//   el boton debajo, la camara de seguridad que lo mira, la primera palanca y, en otra pared, el MAPA oculto (solo
//   se ve con la piedra) con las palancas marcadas.
// - Las otras 3 palancas repartidas por el mapa, cada una con una marca oculta encima.
// - "Muro Entrada": la pared que aparece donde estaba la puerta al entrar.
// - "Salida Portal": donde deja el portal (fuera de la entrada, mirando a la estacion).
// - Las viejas flechas ocultas ("Indicaciones Ocultas") se apagan. El gato (Enemy 2) recibe un modelo de sombra.
// Se aplica sola (y rehace la version anterior); rehacer: Tools > Backrooms > Palancas. Despues guarda la escena.
[InitializeOnLoad]
public static class BackroomsPalancasSetup
{
    const string Nombre = "Palancas";
    const string Version = "_guion v2";
    const string Carpeta = "Assets/Backrooms/Palancas";
    const string PrefabGato = "Assets/Prefabs/Enemy 2 (gato).prefab";
    const string ModeloGato = "Assets/Modelos De enemigos/monstruos/gatp.obj";
    const string ModeloCadaver = "Assets/Modelos De enemigos/Walking (2).fbx";
    const string ModeloGema = "Assets/Models/Props/Gema especial.fbx";
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
        if (!br || !br.transform.Find("Inicio")) return;
        Transform viejo = br.transform.Find(Nombre);
        if (viejo && viejo.Find(Version)) return;
        // Los Backrooms de pasillos los arma BackroomsPasillosSetup (con los sitios de su plano)
        if (BackroomsPasillosSetup.Hecho(br.transform)) { BackroomsPasillosSetup.RehacerPuzzle(br.transform); return; }
        if (viejo)
        {
            Debug.Log("[Backrooms] Rehaciendo las palancas segun el guion nuevo.", br);
            Undo.DestroyObjectImmediate(viejo.gameObject);
        }
        Aplicar(br.transform);
    }

    [MenuItem("Tools/Backrooms/Palancas (rehacer)")]
    static void Menu()
    {
        GameObject br = GameObject.Find("Backrooms");
        if (!br || !br.transform.Find("Inicio")) { EditorUtility.DisplayDialog("Backrooms", "No se encontro \"Backrooms/Inicio\".", "OK"); return; }
        if (BackroomsPasillosSetup.Hecho(br.transform)) { BackroomsPasillosSetup.RehacerPuzzle(br.transform); return; }
        Transform viejo = br.transform.Find(Nombre);
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        Aplicar(br.transform);
    }

    internal struct Pared { public Vector3 punto, normal; public int celda; public float dist; }

    // Sitios fijos (los da BackroomsPasillosSetup con su plano) en vez de buscarlos solo
    internal class Plan
    {
        public Pared tablero, primera, mapa;
        public List<Pared> otras = new List<Pared>();
        public Bounds cuarto;                 // la habitacion del cadaver (suelo a techo)
        public Vector3 puntoGato; public float giroGato;   // sentado al lado del cadaver
        public Vector3 salidaGato, salidaGatoAlterna;     // por donde se va (la otra puerta si el jugador viene por ahi)
        public Vector3 reaparicionGato;                   // habitacion lejana donde reaparece
    }

    internal static Pared ParedEn(Vector3 punto, Vector3 normal) => new Pared { punto = punto, normal = normal.normalized };

    internal static void AplicarPlan(Transform br, Plan plan)
    {
        Transform viejo = br.Find(Nombre);
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        ModeloSombraGato();
        Physics.SyncTransforms();
        var rejilla = new BackroomsVistaSetup.Rejilla(br);
        Vector3 inicioPos = br.Find("Inicio").position;
        if (!rejilla.Celda(inicioPos, out int inicio)) { Debug.LogWarning("[Backrooms] La entrada no esta en un sitio libre.", br); return; }
        float[] dist = rejilla.Distancias(inicio);
        if (!PoseCamara(plan.tablero, out Vector3 posCam))
            posCam = plan.tablero.punto + plan.tablero.normal * 4.5f + Vector3.up * 2.5f;
        var elegidas = new List<Pared> { plan.primera };
        elegidas.AddRange(plan.otras);
        Construir(br, rejilla, dist, plan.tablero, posCam, plan.primera, true, plan.mapa, elegidas, plan);
    }

    static void Aplicar(Transform br)
    {
        ModeloSombraGato();
        Physics.SyncTransforms();
        var rejilla = new BackroomsVistaSetup.Rejilla(br);
        Vector3 inicioPos = br.Find("Inicio").position;
        if (!rejilla.Celda(inicioPos, out int inicio)) { Debug.LogWarning("[Backrooms] La entrada no esta en un sitio libre.", br); return; }
        float[] dist = rejilla.Distancias(inicio);
        float max = 0f;
        foreach (float d in dist) if (d < float.MaxValue && d > max) max = d;
        if (max < 10f) { Debug.LogWarning("[Backrooms] Los Backrooms son muy chicos para las palancas.", br); return; }

        // Paredes posibles (cada 4 celdas para no probar todas)
        var paredes = new List<Pared>();
        for (int i = 0; i < dist.Length; i += 4)
        {
            if (dist[i] == float.MaxValue || dist[i] < 5f) continue;
            if (BuscarPared(br, rejilla.Centro(i), rejilla.radio, out Vector3 p, out Vector3 n))
                paredes.Add(new Pared { punto = p, normal = n, celda = i, dist = dist[i] });
        }
        if (paredes.Count < Cantidad + 2) { Debug.LogWarning("[Backrooms] No encontre paredes para las palancas.", br); return; }

        // ---- La habitacion del cadaver: el tablero (con sitio para la camara) a media distancia
        Pared tablero = default;
        Vector3 posCam = default;
        bool hay = false;
        paredes.Sort((a, b) => Mathf.Abs(a.dist - max * 0.45f).CompareTo(Mathf.Abs(b.dist - max * 0.45f)));
        foreach (Pared w in paredes)
            if (PoseCamara(w, out posCam) && Libre(w.punto + w.normal * 2.4f)) { tablero = w; hay = true; break; }
        if (!hay) { Debug.LogWarning("[Backrooms] No encontre una habitacion con sitio para el tablero y la camara.", br); return; }
        Vector3 centro = tablero.punto + tablero.normal * 2.4f;
        Vector3 ojos = centro + Vector3.up * 1.4f;

        // Primera palanca: en la misma habitacion (se ve desde el centro), en otra pared
        Pared primera = default; hay = false; float mejor = float.MaxValue;
        foreach (Pared w in paredes)
        {
            float d = Plano(w.punto - tablero.punto);
            if (d < 2.2f || d > 8f) continue;
            if (Vector3.Dot(w.normal, tablero.normal) > 0.9f && d < 2f) continue;
            if (Physics.Linecast(ojos, w.punto + Vector3.up * AltoPalanca + w.normal * 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            float puntos = Mathf.Abs(d - 4f);
            if (puntos < mejor) { mejor = puntos; primera = w; hay = true; }
        }
        if (!hay) { Debug.LogWarning("[Backrooms] No hay pared para la primera palanca en la habitacion del tablero.", br); return; }

        // Mapa: otra pared de la habitacion, lo mas lejos del tablero
        Pared mapa = default; bool hayMapa = false; float lejos = -1f;
        foreach (Pared w in paredes)
        {
            float d = Plano(w.punto - centro);
            if (d < 1.2f || d > 7f || Plano(w.punto - primera.punto) < 1.6f || Plano(w.punto - tablero.punto) < 1.8f) continue;
            if (Physics.Linecast(ojos, w.punto + Vector3.up * 1.55f + w.normal * 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            float dt = Plano(w.punto - tablero.punto);
            if (dt > lejos) { lejos = dt; mapa = w; hayMapa = true; }
        }

        // Las otras palancas: repartidas lejos de la habitacion y entre si
        var elegidas = new List<Pared> { primera };
        var evitar = new List<Vector3> { inicioPos, centro };
        var rnd = new System.Random(1234);
        for (int k = 1; k < Cantidad; k++)
        {
            Pared mejorP = default; float mejorPuntos = float.MinValue;
            foreach (Pared w in paredes)
            {
                if (w.dist < max * 0.15f || Plano(w.punto - centro) < 9f) continue;
                float cerca = float.MaxValue;
                foreach (Vector3 e in evitar) cerca = Mathf.Min(cerca, Plano(w.punto - e));
                foreach (Pared e in elegidas) cerca = Mathf.Min(cerca, Plano(w.punto - e.punto));
                if (cerca < 6f) continue;
                float puntos = cerca + (float)rnd.NextDouble() * 2f;
                if (puntos > mejorPuntos) { mejorPuntos = puntos; mejorP = w; }
            }
            if (mejorPuntos == float.MinValue) break;
            elegidas.Add(mejorP);
        }
        if (elegidas.Count < Cantidad) Debug.LogWarning("[Backrooms] Solo hubo sitio para " + elegidas.Count + " palancas.", br);
        Construir(br, rejilla, dist, tablero, posCam, primera, hayMapa, mapa, elegidas, null);
    }

    static void Construir(Transform br, BackroomsVistaSetup.Rejilla rejilla, float[] dist, Pared tablero, Vector3 posCam,
                          Pared primera, bool hayMapa, Pared mapa, List<Pared> elegidas, Plan plan)
    {
        Vector3 centro = tablero.punto + tablero.normal * 2.4f;

        // Las flechas viejas (llevaban a la manija de la palanca rota, que ya no existe): se apagan
        Material pintura = AssetDatabase.LoadAssetAtPath<Material>("Assets/Backrooms/Vista/PinturaFlecha.mat");
        Transform flechas = br.Find("Indicaciones Ocultas");
        if (flechas)
        {
            Renderer rf = flechas.GetComponentInChildren<Renderer>(true);
            if (rf != null && rf.sharedMaterial != null) pintura = rf.sharedMaterial;
            Undo.RecordObject(flechas.gameObject, "Indicaciones");
            flechas.gameObject.SetActive(false);
        }

        // ---- Materiales
        Directory.CreateDirectory(Carpeta);
        // Poco brillo especular: con la linterna (luz muy fuerte de cerca) lo liso/metalico se veia quemado
        Material metal = Mat("Metal", new Color(0.23f, 0.23f, 0.22f), 0.3f, 0.2f);
        Material oscuro = Mat("Panel", new Color(0.07f, 0.07f, 0.075f), 0.25f, 0.1f);
        Material rojo = Mat("Pomo", new Color(0.6f, 0.06f, 0.05f), 0.35f, 0f);
        Material foco = Mat("Foco", new Color(0.18f, 0.05f, 0.04f), 0.35f, 0f, true);
        Material led = Mat("Led", new Color(1f, 0.1f, 0.08f), 0.3f, 0f, true, new Color(3f, 0.2f, 0.15f));
        Material boton = Mat("Boton", new Color(0.15f, 0.05f, 0.05f), 0.35f, 0f, true);
        Material sangre = Mat("Sangre", new Color(0.18f, 0.01f, 0.01f), 0.45f, 0f);
        Material portal = Mat("Portal", new Color(0.25f, 0.15f, 0.6f), 0.4f, 0f, true, new Color(1.4f, 0.9f, 4f));
        Material filtro = MaterialFiltro();

        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Palancas");
        raiz.transform.SetParent(br, true);
        raiz.transform.position = centro;
        new GameObject(Version).transform.SetParent(raiz.transform, false);
        var puzzle = raiz.AddComponent<PuzzlePalancas>();
        var guion = raiz.AddComponent<BackroomsGuion>();

        // ---- Palancas (ninguna rota en el guion nuevo)
        var palancas = new List<Palanca>();
        for (int i = 0; i < elegidas.Count; i++)
            palancas.Add(CrearPalanca(raiz.transform, "Palanca " + (i + 1), elegidas[i], metal, oscuro, rojo));

        CrearTablero(raiz.transform, tablero, metal, oscuro, foco, boton, puzzle, palancas.Count);
        Transform vista = CrearCamara(raiz.transform, posCam, tablero, metal, oscuro, led);

        // ---- Habitacion: cadaver, piedra, gato, mapa
        Transform cuarto = new GameObject("Habitacion Cadaver").transform;
        cuarto.SetParent(raiz.transform, false);
        var caja = cuarto.gameObject.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        cuarto.gameObject.layer = 2;
        caja.center = Vector3.up * 1.5f;
        if (plan != null)
        {
            cuarto.position = new Vector3(plan.cuarto.center.x, centro.y, plan.cuarto.center.z);
            caja.size = new Vector3(plan.cuarto.size.x, 3f, plan.cuarto.size.z);
        }
        else
        {
            cuarto.position = centro;
            caja.size = new Vector3(Medida(centro, Vector3.right), 3f, Medida(centro, Vector3.forward));
        }

        Vector3 lado = Vector3.Cross(Vector3.up, tablero.normal).normalized;
        Bounds cuerpo = CrearCadaver(cuarto, centro, lado, sangre);
        MoverPiedra(br, cuerpo);

        // El gato: sentado AL LADO del cadaver (lamiendose la pata: su Idle) hasta la cinematica
        var puntoGato = new GameObject("Punto Gato").transform;
        puntoGato.SetParent(cuarto, false);
        if (plan != null) puntoGato.SetPositionAndRotation(plan.puntoGato, Quaternion.Euler(0f, plan.giroGato, 0f));
        else puntoGato.SetPositionAndRotation(new Vector3(cuerpo.center.x, centro.y, cuerpo.center.z) - tablero.normal * 0.8f, Quaternion.LookRotation(-tablero.normal));

        // Por donde se va de la habitacion (y la otra puerta, por si el jugador viene de ese lado)
        Transform huida = new GameObject("Huida Gato").transform;
        huida.SetParent(raiz.transform, false);
        huida.position = centro;
        Transform alterna = null;
        Transform reaparicion = new GameObject("Reaparicion Gato").transform;
        reaparicion.SetParent(raiz.transform, false);
        if (plan != null)
        {
            huida.position = plan.salidaGato;
            alterna = new GameObject("Huida Gato (otra puerta)").transform;
            alterna.SetParent(raiz.transform, false);
            alterna.position = plan.salidaGatoAlterna;
            reaparicion.position = plan.reaparicionGato;
        }
        else if (rejilla.Celda(centro, out int celdaCuarto))
        {
            // Sin plan: lo mas lejos caminando de la habitacion
            float[] dc = rejilla.Distancias(celdaCuarto);
            int lejana = celdaCuarto; float m = 0f;
            for (int i = 0; i < dc.Length; i++) if (dc[i] < float.MaxValue && dc[i] > m) { m = dc[i]; lejana = i; }
            huida.position = rejilla.Centro(lejana);
            reaparicion.position = huida.position;
        }

        // Mapa oculto con las palancas marcadas, y marcas ocultas sobre las demas palancas
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        if (pintura != null)
        {
            if (hayMapa)
            {
                Texture2D tex = TexturaMapa(rejilla, dist, elegidas, centro);
                Material mm = MaterialPintura("Mapa", pintura, tex);
                Inscripcion(cuarto, quad, mm, "Mapa Oculto", mapa.punto + Vector3.up * 1.55f, mapa.normal, 1.4f);
            }
            else Debug.LogWarning("[Backrooms] No encontre pared libre para el mapa en la habitacion.", raiz);
            Material mx = MaterialPintura("Marca X", pintura, TexturaX());
            for (int i = 1; i < elegidas.Count; i++)
                Inscripcion(raiz.transform, quad, mx, "Marca Palanca " + (i + 1), elegidas[i].punto + Vector3.up * (AltoPalanca + 0.6f), elegidas[i].normal, 0.45f);
        }

        // ---- Entrada (pared que aparece) y salida del portal
        Transform entrada = br.Find("Entrada Pasillo");
        if (!entrada) entrada = br.Find("Inicio");
        GameObject muro = CrearMuroEntrada(raiz.transform, br, entrada);
        Vector3 fuera = entrada.forward; fuera.y = 0f; fuera.Normalize();
        var salida = new GameObject("Salida Portal").transform;
        salida.SetParent(raiz.transform, false);
        Vector3 ps = entrada.position + fuera * 2.5f;
        if (Physics.Raycast(ps + Vector3.up * 1.5f, Vector3.down, out RaycastHit hs, 4f, ~0, QueryTriggerInteraction.Ignore)) ps.y = hs.point.y + 0.05f;
        salida.SetPositionAndRotation(ps, Quaternion.LookRotation(fuera, Vector3.up));

        // ---- Referencias
        puzzle.palancas = palancas.ToArray();
        puzzle.camaraSeguridad = vista;
        puzzle.filtroCamara = filtro;
        puzzle.salida = salida;
        puzzle.materialPortal = portal;
        guion.entrada = entrada;
        guion.muroEntrada = muro;
        guion.habitacion = caja;
        guion.puntoGato = puntoGato;
        guion.huidaGato = huida;
        guion.huidaGatoAlterna = alterna;
        guion.reaparicionGato = reaparicion;
        guion.origenGrito = reaparicion;
        guion.gato = BuscarGato();
        if (!guion.gato) Debug.LogWarning("[Backrooms] No encontre el gato (Enemy 2) en la escena.", raiz);

        EditorSceneManager.MarkSceneDirty(br.gameObject.scene);
        Debug.Log("[Backrooms] Listo segun el guion: habitacion del cadaver (gato, piedra, tablero, camara, mapa oculto), " +
                  palancas.Count + " palancas, boton y portal, y la pared de la entrada. Se pueden mover. Guarda la escena (Ctrl+S).", raiz);
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    static bool Libre(Vector3 suelo) => !Physics.CheckCapsule(suelo + Vector3.up * 0.4f, suelo + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore);

    // Ancho de la habitacion en una direccion (de pared a pared, a la altura del pecho), entre 4 y 14 m
    static float Medida(Vector3 centro, Vector3 dir)
    {
        Vector3 o = centro + Vector3.up * 1.2f;
        float a = Physics.Raycast(o, dir, out RaycastHit h1, 8f, ~0, QueryTriggerInteraction.Ignore) ? h1.distance : 6f;
        float b = Physics.Raycast(o, -dir, out RaycastHit h2, 8f, ~0, QueryTriggerInteraction.Ignore) ? h2.distance : 6f;
        return Mathf.Clamp(a + b, 4f, 14f);
    }

    static JefeIndigoAI BuscarGato()
    {
        foreach (JefeIndigoAI e in Object.FindObjectsByType<JefeIndigoAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e.name.ToLowerInvariant().Contains("gato")) return e;
        return null;
    }

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

    static Palanca CrearPalanca(Transform padre, string nombre, Pared w, Material metal, Material oscuro, Material rojo)
    {
        var go = new GameObject(nombre);
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
        inter.SetPrompt("Bajar palanca");
        var p = go.AddComponent<Palanca>();
        p.pivote = pivote;
        p.manija = manija;
        p.rota = false;
        p.anguloArriba = 40f;
        p.anguloAbajo = 140f;
        pivote.localRotation = Quaternion.Euler(40f, 0f, 0f);
        return p;
    }

    static void CrearTablero(Transform padre, Pared w, Material metal, Material oscuro, Material foco, Material matBoton, PuzzlePalancas puzzle, int n)
    {
        var go = new GameObject("Tablero");
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(w.punto + Vector3.up * AltoTablero + w.normal * 0.01f, Quaternion.LookRotation(w.normal));
        Cubo(go.transform, "Marco", new Vector3(0f, -0.06f, 0.02f), new Vector3(1.42f, 0.98f, 0.04f), metal);
        Cubo(go.transform, "Panel", new Vector3(0f, -0.06f, 0.045f), new Vector3(1.3f, 0.86f, 0.02f), oscuro);
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, -0.06f, 0.04f);
        col.size = new Vector3(1.42f, 0.98f, 0.08f);

        var focos = new List<Renderer>();
        var luces = new List<Light>();
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(-0.45f, 0.45f, n > 1 ? i / (float)(n - 1) : 0.5f);
            Cubo(go.transform, "Aro " + (i + 1), new Vector3(x, 0.05f, 0.06f), new Vector3(0.2f, 0.012f, 0.2f), metal, PrimitiveType.Cylinder)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject f = Cubo(go.transform, "Foco " + (i + 1), new Vector3(x, 0.05f, 0.08f), Vector3.one * 0.15f, foco, PrimitiveType.Sphere);
            focos.Add(f.GetComponent<Renderer>());
            var lg = new GameObject("Luz " + (i + 1));
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(x, 0.05f, 0.3f);
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

        Texto(go.transform, "Titulo", new Vector3(0f, 0.28f, 0.058f), "ENERGÍA", 0.85f, new Color(0.85f, 0.82f, 0.7f));
        puzzle.contador = Texto(go.transform, "Contador", new Vector3(0f, -0.13f, 0.058f), "0 / " + n, 0.55f, new Color(1f, 0.85f, 0.4f));

        // Boton debajo de los focos: bloqueado hasta bajar todas (entonces se ilumina y brilla)
        var b = new GameObject("Boton");
        b.transform.SetParent(go.transform, false);
        b.transform.localPosition = new Vector3(0f, -0.36f, 0.05f);
        Cubo(b.transform, "Base", new Vector3(0f, 0f, 0.01f), new Vector3(0.2f, 0.012f, 0.2f), metal, PrimitiveType.Cylinder)
            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var pulsador = Cubo(b.transform, "Pulsador", new Vector3(0f, 0f, 0.035f), new Vector3(0.13f, 0.03f, 0.13f), matBoton, PrimitiveType.Cylinder);
        pulsador.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var bc = b.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0f, 0.04f);
        bc.size = new Vector3(0.22f, 0.22f, 0.1f);
        var inter = b.AddComponent<Interactable>();
        inter.SetPrompt("Presionar botón");
        var brillo = b.AddComponent<Highlightable>();
        var so = new SerializedObject(brillo);
        so.FindProperty("outlineMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GlowMaterial.mat");
        so.ApplyModifiedPropertiesWithoutUndo();
        puzzle.boton = inter;
        puzzle.botonPulsador = pulsador.transform;
        puzzle.luzBoton = pulsador.GetComponent<Renderer>();
        puzzle.brilloBoton = brillo;
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
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mira - pos));
        Cubo(go.transform, "Cuerpo", new Vector3(0f, 0f, -0.05f), new Vector3(0.16f, 0.13f, 0.3f), oscuro);
        Cubo(go.transform, "Visera", new Vector3(0f, 0.075f, 0f), new Vector3(0.19f, 0.015f, 0.36f), metal);
        Cubo(go.transform, "Lente", new Vector3(0f, 0f, 0.11f), new Vector3(0.08f, 0.025f, 0.08f), metal, PrimitiveType.Cylinder)
            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Cubo(go.transform, "Led", new Vector3(0.05f, 0.04f, 0.1f), Vector3.one * 0.015f, led, PrimitiveType.Sphere);
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

    // Cuerpo tirado boca arriba en medio de la habitacion, con un charco de sangre. Devuelve sus limites
    static Bounds CrearCadaver(Transform padre, Vector3 suelo, Vector3 lado, Material sangre)
    {
        Bounds b = new Bounds(suelo + Vector3.up * 0.15f, new Vector3(0.5f, 0.3f, 1.7f));
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloCadaver);
        if (modelo != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
            go.name = "Cadaver";
            foreach (Animator a in go.GetComponentsInChildren<Animator>()) a.enabled = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            go.transform.SetPositionAndRotation(suelo, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            // De pie mide ~1.75 m
            if (Limites(go.transform, out Bounds parado) && parado.size.y > 0.01f) go.transform.localScale = Vector3.one * (1.75f / parado.size.y);
            // Boca arriba, a lo largo de la pared del tablero
            go.transform.rotation = Quaternion.LookRotation(-lado, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
            if (Limites(go.transform, out Bounds echado))
            {
                go.transform.position += new Vector3(suelo.x - echado.center.x, suelo.y + 0.02f - echado.min.y, suelo.z - echado.center.z);
                Limites(go.transform, out b);
            }
        }
        else Debug.LogWarning("[Backrooms] No encuentro " + ModeloCadaver + " para el cadaver.");

        var charco = Cubo(padre, "Sangre", new Vector3(0f, 0f, 0f), new Vector3(1.3f, 0.004f, 0.9f), sangre, PrimitiveType.Cylinder);
        charco.transform.position = new Vector3(b.center.x, suelo.y + 0.006f, b.center.z);
        charco.transform.rotation = Quaternion.LookRotation(lado, Vector3.up);
        return b;
    }

    // La piedra (antes caramelo) pasa a estar encima del cadaver, con el modelo de la gema
    static void MoverPiedra(Transform br, Bounds cuerpo)
    {
        Transform piedra = br.Find("Caramelo Agujereado");
        if (!piedra) { Debug.LogWarning("[Backrooms] No encontre la piedra (\"Caramelo Agujereado\").", br); return; }
        Undo.RecordObject(piedra, "Piedra");
        piedra.position = new Vector3(cuerpo.center.x, cuerpo.max.y + 0.03f, cuerpo.center.z);
        if (piedra.TryGetComponent(out Interactable inter)) { Undo.RecordObject(inter, "Piedra"); inter.SetPrompt("Recoger piedra"); }

        // Su modelo: una piedra con agujero (la "piedra bruja"; la gema es el modelo de las coleccionables)
        NovedadesGuionSetup.PonerPiedraBruja(piedra);
    }

    // Quad pintado en una pared, que solo se ve con la piedra (MarcaOculta)
    static void Inscripcion(Transform padre, Mesh quad, Material mat, string nombre, Vector3 punto, Vector3 normal, float tam)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.transform.SetPositionAndRotation(punto + normal * 0.016f, Quaternion.LookRotation(-normal, Vector3.up));   // el Quad se ve desde su -Z
        go.transform.Rotate(0f, 0f, Random.Range(-4f, 4f), Space.Self);
        go.transform.localScale = new Vector3(tam, tam, 1f);
        go.AddComponent<MarcaOculta>();
    }

    // Pared que tapa la puerta de la entrada (escondida hasta la cinematica). Se busca el hueco en el muro con rayos
    static GameObject CrearMuroEntrada(Transform padre, Transform br, Transform entrada)
    {
        Vector3 fuera = entrada.forward; fuera.y = 0f; fuera.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, fuera);
        Vector3 baseR = entrada.position + Vector3.up * 1f;
        const float paso = 0.05f, rango = 8f, largo = 12f;
        int n = Mathf.RoundToInt(rango * 2f / paso) + 1;
        var dist = new float[n];
        var conocidas = new List<float>();
        Material matPared = null;
        for (int i = 0; i < n; i++)
        {
            dist[i] = largo;
            if (Physics.Raycast(baseR + lado * (-rango + i * paso), -fuera, out RaycastHit h, largo, ~0, QueryTriggerInteraction.Ignore) && h.collider.transform.IsChildOf(br))
            {
                dist[i] = h.distance;
                conocidas.Add(h.distance);
                if (matPared == null && h.collider.TryGetComponent(out Renderer rr)) matPared = rr.sharedMaterial;
            }
        }
        float ancho = 8.1f, alto = 4.25f, muro = 0.5f, centroLado = 0f;
        if (conocidas.Count > n / 3)
        {
            conocidas.Sort();
            muro = conocidas[conocidas.Count / 2];
            float mejorD = float.MaxValue;
            int ini = -1;
            for (int i = 0; i <= n; i++)
            {
                bool hueco = i < n && dist[i] > muro + 1f;
                if (hueco && ini < 0) ini = i;
                if (hueco || ini < 0) continue;
                float a = (i - ini) * paso, c = -rango + (ini + i - 1) * 0.5f * paso;
                ini = -1;
                if (a < 0.7f || a > 10f) continue;
                if (Mathf.Abs(c) < mejorD) { mejorD = Mathf.Abs(c); centroLado = c; ancho = a + 0.2f; }
            }
            Vector3 p = entrada.position + lado * centroLado - fuera * (muro + 0.05f);
            alto = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.up, out RaycastHit ht, 5f, ~0, QueryTriggerInteraction.Ignore) ? ht.distance + 0.35f : 3f;
        }
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Muro Entrada";
        go.transform.SetParent(padre, false);
        Vector3 pos = entrada.position + lado * centroLado - fuera * (muro + 0.12f) + Vector3.up * (alto * 0.5f);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fuera, Vector3.up));
        go.transform.localScale = new Vector3(ancho, alto, 0.3f);
        if (matPared != null) go.GetComponent<Renderer>().sharedMaterial = matPared;
        go.SetActive(false);
        return go;
    }

    // ------------------------------------------------------------------ Mapa y marcas

    // Plano de los Backrooms visto desde arriba: el contorno de lo que se puede caminar, una X en cada palanca
    // y un circulo en la habitacion donde esta el mapa
    static Texture2D TexturaMapa(BackroomsVistaSetup.Rejilla rejilla, float[] dist, List<Pared> palancas, Vector3 aqui)
    {
        const int N = 256;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < dist.Length; i++)
        {
            if (dist[i] == float.MaxValue) continue;
            Vector3 c = rejilla.Centro(i);
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z);
        }
        float lado = Mathf.Max(maxX - minX, maxZ - minZ) * 1.08f;
        float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
        Vector2Int Px(Vector3 p) => new Vector2Int(Mathf.RoundToInt(((p.x - cx) / lado + 0.5f) * (N - 1)), Mathf.RoundToInt(((p.z - cz) / lado + 0.5f) * (N - 1)));

        var piso = new bool[N * N];
        for (int i = 0; i < dist.Length; i++)
        {
            if (dist[i] == float.MaxValue) continue;
            Vector2Int q = Px(rejilla.Centro(i));
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int x = q.x + dx, z = q.y + dz;
                    if (x >= 0 && z >= 0 && x < N && z < N) piso[z * N + x] = true;
                }
        }
        var px = new Color32[N * N];
        var rnd = new System.Random(7);
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                byte a = 0;
                if (piso[z * N + x])
                {
                    bool borde = false;
                    for (int dx = -1; dx <= 1 && !borde; dx++)
                        for (int dz = -1; dz <= 1 && !borde; dz++)
                        {
                            int xx = x + dx, zz = z + dz;
                            if (xx < 0 || zz < 0 || xx >= N || zz >= N || !piso[zz * N + xx]) borde = true;
                        }
                    // Trazo de tiza: bordes fuertes, el piso apenas, con un poco de ruido
                    a = (byte)(borde ? 200 + rnd.Next(55) : 25 + rnd.Next(15));
                }
                px[z * N + x] = new Color32(255, 255, 255, a);
            }
        void Pintar(int x, int z, int r, byte a)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    int xx = x + dx, zz = z + dz;
                    if (xx < 0 || zz < 0 || xx >= N || zz >= N || dx * dx + dz * dz > r * r) continue;
                    px[zz * N + xx] = new Color32(255, 255, 255, a);
                }
        }
        foreach (Pared p in palancas)
        {
            Vector2Int q = Px(p.punto);
            for (int t = -7; t <= 7; t++) { Pintar(q.x + t, q.y + t, 1, 255); Pintar(q.x + t, q.y - t, 1, 255); }
        }
        Vector2Int h = Px(aqui);
        for (int s = 0; s < 72; s++)
        {
            float ang = s / 72f * Mathf.PI * 2f;
            Pintar(h.x + Mathf.RoundToInt(Mathf.Cos(ang) * 14f), h.y + Mathf.RoundToInt(Mathf.Sin(ang) * 14f), 1, 255);
        }

        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
        tex.SetPixels32(px);
        tex.Apply();
        return GuardarTextura(tex, "MapaOculto");
    }

    static Texture2D TexturaX()
    {
        const int N = 128;
        var px = new Color32[N * N];
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                float d1 = Mathf.Abs(x - z), d2 = Mathf.Abs(x - (N - 1 - z));
                bool dentro = x > 14 && x < N - 14 && z > 14 && z < N - 14;
                byte a = (byte)(dentro && Mathf.Min(d1, d2) < 6f ? 255 : 0);
                px[z * N + x] = new Color32(255, 255, 255, a);
            }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
        tex.SetPixels32(px);
        tex.Apply();
        return GuardarTextura(tex, "MarcaX");
    }

    static Texture2D GuardarTextura(Texture2D tex, string nombre)
    {
        string ruta = Carpeta + "/" + nombre + ".png";
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta);
        if (AssetImporter.GetAtPath(ruta) is TextureImporter ti)
        {
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    // Misma pintura que las indicaciones ocultas, con otra imagen
    static Material MaterialPintura(string nombre, Material baseMat, Texture2D tex)
    {
        string ruta = Carpeta + "/Pintura" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            m = new Material(baseMat);
            AssetDatabase.CreateAsset(m, ruta);
        }
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ Gato de sombra

    // Al prefab del gato (una capsula) le pone el modelo "gatp" todo negro, con ojos que brillan (tamano: NovedadesGuionSetup.EscalarGato)
    static void ModeloSombraGato()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabGato)) return;
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloGato);
        if (!modelo) { Debug.LogWarning("[Backrooms] No encuentro " + ModeloGato); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabGato);
        try
        {
            if (root.transform.Find("Modelo")) return;
            Material sombra = Mat("SombraGato", new Color(0.004f, 0.004f, 0.006f), 0.15f, 0f);
            Material ojo = Mat("OjoGato", new Color(1f, 0.9f, 0.4f), 0.9f, 0f, true, new Color(5f, 4f, 1.2f));

            var m = (GameObject)PrefabUtility.InstantiatePrefab(modelo, root.transform);
            m.name = "Modelo";
            foreach (Collider c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = sombra;
                r.sharedMaterials = mats;
            }
            m.transform.localPosition = Vector3.zero;
            m.transform.localScale = Vector3.one;
            // Que la cabeza mire hacia adelante (+Z): es el extremo del eje largo con los vertices mas altos
            m.transform.localRotation = Quaternion.Euler(0f, HaciaAdelante(m), 0f);

            var agent = root.GetComponent<NavMeshAgent>();
            float pies = agent ? -agent.baseOffset : -1f;
            if (Limites(m.transform, out Bounds b) && b.size.z > 0.0001f)
            {
                m.transform.localScale = Vector3.one * (0.75f / Mathf.Max(b.size.x, b.size.z));
                Limites(m.transform, out b);
                Vector3 centro = root.transform.InverseTransformPoint(b.center);
                float abajo = root.transform.InverseTransformPoint(b.min).y;
                m.transform.localPosition = new Vector3(-centro.x, pies - abajo, -centro.z);
                Limites(m.transform, out b);
                // Ojos: adelante y arriba de la cabeza
                Vector3 min = root.transform.InverseTransformPoint(b.min), max = root.transform.InverseTransformPoint(b.max);
                float tam = max.y - min.y;
                foreach (float s in new[] { -1f, 1f })
                {
                    var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    e.name = "Ojo";
                    Object.DestroyImmediate(e.GetComponent<Collider>());
                    e.transform.SetParent(root.transform, false);
                    e.transform.localPosition = new Vector3(s * tam * 0.07f, min.y + tam * 0.78f, max.z - tam * 0.1f);
                    e.transform.localScale = Vector3.one * Mathf.Max(0.03f, tam * 0.05f);
                    e.GetComponent<Renderer>().sharedMaterial = ojo;
                }
            }
            if (root.TryGetComponent(out MeshRenderer capsula)) capsula.enabled = false;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabGato);
            Debug.Log("[Backrooms] El gato ahora es una sombra (modelo gatp, negro, con ojos que brillan).");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Giro (grados en Y) para que la cabeza del modelo quede hacia +Z
    static float HaciaAdelante(GameObject m)
    {
        var verts = new List<Vector3>();
        foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>())
            if (f.sharedMesh != null)
                foreach (Vector3 v in f.sharedMesh.vertices) verts.Add(m.transform.InverseTransformPoint(f.transform.TransformPoint(v)));
        if (verts.Count == 0) return 0f;
        Vector3 mn = verts[0], mx = verts[0];
        foreach (Vector3 v in verts) { mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v); }
        bool largoEnX = mx.x - mn.x > mx.z - mn.z;
        float a0 = largoEnX ? mn.x : mn.z, a1 = largoEnX ? mx.x : mx.z, cuarto = (a1 - a0) * 0.25f;
        float yBajo = 0f, yAlto = 0f; int nBajo = 0, nAlto = 0;
        foreach (Vector3 v in verts)
        {
            float a = largoEnX ? v.x : v.z;
            if (a < a0 + cuarto) { yBajo += v.y; nBajo++; }
            else if (a > a1 - cuarto) { yAlto += v.y; nAlto++; }
        }
        bool cabezaEnMax = (nAlto > 0 ? yAlto / nAlto : 0f) > (nBajo > 0 ? yBajo / nBajo : 0f);
        // Euler(0, y) * eje -> +Z
        if (largoEnX) return cabezaEnMax ? -90f : 90f;
        return cabezaEnMax ? 0f : 180f;
    }

    // ------------------------------------------------------------------ Utilidades

    static bool Limites(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return hay;
    }

    static Material Mat(string nombre, Color color, float liso, float metalico, bool emision = false, Color brillo = default)
    {
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m)
        {
            // Los que ya existian se crearon muy lisos/metalicos (con la linterna se veian quemados): solo se bajan
            bool cambio = false;
            if (m.HasProperty("_Smoothness") && m.GetFloat("_Smoothness") > liso + 0.001f) { m.SetFloat("_Smoothness", liso); cambio = true; }
            if (m.HasProperty("_Metallic") && m.GetFloat("_Metallic") > metalico + 0.001f) { m.SetFloat("_Metallic", metalico); cambio = true; }
            if (cambio) EditorUtility.SetDirty(m);
            return m;
        }
        Directory.CreateDirectory(Carpeta);
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
