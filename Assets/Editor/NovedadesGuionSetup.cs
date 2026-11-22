using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Lo nuevo del guion (ver PiedraColeccionable, MisionFaro, Lectura y FinalJuego):
// - Las 4 piedras coleccionables: Laberinto (en el piso de algun pasillo), Backrooms (en una sala lejos del cadaver),
//   Cloacas (junto a un pilar) y Zona Roja (en la orilla del lago).
// - Zona Roja: el diario en el piso 1 del faro, las 3 notas en los arboles (en lugar de las 4 esferas), el bidon de
//   gasolina junto al cubo negro del lago, la pared que tapa la entrada del faro y la luz que recibe el bidon.
//   Las notas van clavadas en el tronco del arbol mas cercano (y se pasan ahi las que estaban en estacas).
// - El gato (Enemy 2): su modelo escalado al tamano de un gato grande (y sus ojos y su collider con el).
// Se aplica solo lo que falte; rehacer: Tools > Guion > Novedades (rehacer). Despues guarda la escena (Ctrl+S).
[InitializeOnLoad]
public static class NovedadesGuionSetup
{
    const string Carpeta = "Assets/Guion";
    const string ModeloGema = "Assets/Models/Props/Gema especial.fbx";
    const string PrefabGato = "Assets/Prefabs/Enemy 2 (gato).prefab";
    const string Glow = "Assets/Materials/GlowMaterial.mat";
    // Largo del gato de punta a punta (cola incluida), en metros
    const float LargoGato = 0.75f;

    static NovedadesGuionSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EscalarGato();
        if (!GameObject.Find("Zona_Principal")) return;
        Aplicar(false);
    }

    [MenuItem("Tools/Guion/Novedades (rehacer)")]
    static void Menu()
    {
        foreach (PiedraColeccionable p in Object.FindObjectsByType<PiedraColeccionable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(p.gameObject);
        GameObject zr = GameObject.Find("Zona Roja");
        if (zr)
            foreach (string n in new[] { "Mision Faro", "Mesa del diario", "Notas del farero", "Bidon de gasolina", "Muro Entrada Faro" })
            {
                Transform t = zr.transform.Find(n);
                if (t) Undo.DestroyObjectImmediate(t.gameObject);
            }
        Aplicar(true);
    }

    static void Aplicar(bool avisar)
    {
        Directory.CreateDirectory(Carpeta);
        Physics.SyncTransforms();
        var hechos = new List<string>();
        if (Piedras(hechos) | ZonaRoja(hechos) | Arreglos(hechos))
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Guion] Novedades: " + string.Join(", ", hechos) + ". Se pueden mover. Guarda la escena (Ctrl+S).");
        }
        else if (avisar) Debug.Log("[Guion] Novedades: no habia nada que poner.");
    }

    // ------------------------------------------------------------------ Piedras coleccionables

    static bool Piedras(List<string> hechos)
    {
        bool algo = false;
        var nav = CentrosNavMesh();
        var ya = new HashSet<string>();
        foreach (PiedraColeccionable p in Object.FindObjectsByType<PiedraColeccionable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ya.Add(p.transform.root.name);

        // Laberinto: en un pasillo lejos del centro, de la entrada y de los paneles
        if (!ya.Contains("Laberinto") && Raiz("Laberinto", out Transform lab))
        {
            ZonaVolumen area = Area("Laberinto");
            var evitar = new List<Vector3>();
            Agregar(evitar, lab.Find("Zona Segura (centro)"), lab.Find("Entrada Pasillo"));
            foreach (PanelNumero pn in Object.FindObjectsByType<PanelNumero>(FindObjectsInactive.Include, FindObjectsSortMode.None)) evitar.Add(pn.transform.position);
            if (Elegir(nav, p => area == null || area.Contiene(p + Vector3.up), evitar, 14f, 11, out Vector3 sitio))
            { Piedra(lab, sitio, "Laberinto"); hechos.Add("piedra del Laberinto"); algo = true; }
            else Debug.LogWarning("[Guion] No encontre donde poner la piedra del Laberinto.");
        }

        // Backrooms: en una sala lejos de la del cadaver, de la entrada y de las palancas
        if (!ya.Contains("Backrooms") && Raiz("Backrooms", out Transform br))
        {
            ZonaVolumen area = Area("Backrooms");
            var evitar = new List<Vector3>();
            Agregar(evitar, br.Find("Palancas/Habitacion Cadaver"), br.Find("Inicio"), br.Find("Entrada Pasillo"));
            foreach (Palanca pa in Object.FindObjectsByType<Palanca>(FindObjectsInactive.Include, FindObjectsSortMode.None)) evitar.Add(pa.transform.position);
            if (Elegir(nav, p => area == null || area.Contiene(p + Vector3.up), evitar, 13f, 22, out Vector3 sitio))
            { Piedra(br, sitio, "Backrooms"); hechos.Add("piedra de los Backrooms"); algo = true; }
            else Debug.LogWarning("[Guion] No encontre donde poner la piedra de los Backrooms.");
        }

        // Cloacas: al pie de un pilar (si no hay pilares con ese nombre, en un punto lejos de la entrada)
        if (!ya.Contains("Cloacas") && Raiz("Cloacas", out Transform cl))
        {
            ZonaVolumen area = Area("Cloacas");
            Vector3 entrada = cl.Find("Entrada Pasillo") ? cl.Find("Entrada Pasillo").position : cl.position;
            bool Dentro(Vector3 p) => (area == null || area.Contiene(p + Vector3.up)) && ZonaCerrada.Dentro(cl, p);
            Vector3 sitio = default;
            bool hay = false;
            Transform mejor = null;
            float lejos = 0f;
            foreach (Transform t in cl.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!(n.Contains("pilar") || n.Contains("columna") || n.Contains("pillar") || n.Contains("column")) || !t.GetComponent<Renderer>()) continue;
                float d = Vector3.Distance(t.position, entrada);
                if (d > lejos && NavMesh.SamplePosition(t.position, out _, 4f, NavMesh.AllAreas)) { lejos = d; mejor = t; }
            }
            if (mejor != null && NavMesh.SamplePosition(mejor.position, out NavMeshHit h, 4f, NavMesh.AllAreas))
            {
                Bounds b = mejor.GetComponent<Renderer>().bounds;
                Vector3 haciaPilar = new Vector3(b.center.x - h.position.x, 0f, b.center.z - h.position.z);
                sitio = h.position + (haciaPilar.sqrMagnitude > 0.01f ? haciaPilar.normalized * 0.3f : Vector3.zero);
                hay = true;
            }
            else hay = Elegir(nav, Dentro, new List<Vector3> { entrada }, 25f, 33, out sitio);
            if (hay) { Piedra(cl, sitio, "Cloacas"); hechos.Add("piedra de las Cloacas" + (mejor != null ? " (junto a " + mejor.name + ")" : "")); algo = true; }
            else Debug.LogWarning("[Guion] No encontre donde poner la piedra de las Cloacas.");
        }

        // Zona Roja: en la orilla del lago, del lado contrario al cubo negro
        if (!ya.Contains("Zona Roja") && Raiz("Zona Roja", out Transform zr))
        {
            if (Orilla(zr, out Vector3 sitio)) { Piedra(zr, sitio, "Zona Roja"); hechos.Add("piedra de la Zona Roja"); algo = true; }
            else Debug.LogWarning("[Guion] No encontre la orilla del lago para la piedra de la Zona Roja.");
        }
        return algo;
    }

    static void Agregar(List<Vector3> l, params Transform[] ts) { foreach (Transform t in ts) if (t) l.Add(t.position); }

    // Centros de los triangulos del NavMesh (puntos caminables de todas las zonas)
    static List<Vector3> CentrosNavMesh()
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        var res = new List<Vector3>();
        for (int i = 0; i + 2 < tri.indices.Length; i += 3)
            res.Add((tri.vertices[tri.indices[i]] + tri.vertices[tri.indices[i + 1]] + tri.vertices[tri.indices[i + 2]]) / 3f);
        return res;
    }

    // El punto valido mas alejado de lo que hay que evitar (hasta "ideal"), con algo de azar fijo
    static bool Elegir(List<Vector3> puntos, System.Func<Vector3, bool> valido, List<Vector3> evitar, float ideal, int semilla, out Vector3 sitio)
    {
        sitio = default;
        var rnd = new System.Random(semilla);
        float mejor = float.MinValue;
        foreach (Vector3 p in puntos)
        {
            if (!valido(p)) continue;
            float cerca = ideal;
            foreach (Vector3 e in evitar) cerca = Mathf.Min(cerca, Plano(p - e));
            float puntos2 = cerca + (float)rnd.NextDouble() * 4f;
            if (puntos2 <= mejor) continue;
            if (!Suelo(p, out Vector3 s) || Physics.CheckCapsule(s + Vector3.up * 0.5f, s + Vector3.up * 1.5f, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;
            mejor = puntos2;
            sitio = s;
        }
        return mejor > float.MinValue;
    }

    static bool Orilla(Transform zr, out Vector3 sitio)
    {
        sitio = default;
        Transform lago = zr.Find("Lago");
        if (!lago) return false;
        Transform agua = lago.Find("Agua del lago");
        float nivel = agua ? agua.position.y : lago.position.y;
        Vector3 c = lago.position;
        Transform cubo = zr.Find("Objeto del lago (cubo negro)");
        Vector3 contra = cubo ? -(cubo.position - c) : Vector3.right;
        contra.y = 0f;
        contra = contra.sqrMagnitude > 0.01f ? contra.normalized : Vector3.right;
        foreach (float giro in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f })
        {
            Vector3 dir = Quaternion.Euler(0f, giro, 0f) * contra;
            for (float r = 6f; r < 60f; r += 0.5f)
            {
                Vector3 p = c + dir * r;
                if (!Physics.Raycast(new Vector3(p.x, nivel + 20f, p.z), Vector3.down, out RaycastHit h, 60f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (h.point.y < nivel + 0.12f) continue;
                // Primer punto seco: un metro mas afuera, para que no quede en el agua
                if (!Suelo(p + dir * 1f, out sitio)) sitio = h.point;
                return true;
            }
        }
        return false;
    }

    static void Piedra(Transform zona, Vector3 suelo, string nombre)
    {
        var go = new GameObject("Piedra Coleccionable (" + nombre + ")");
        Undo.RegisterCreatedObjectUndo(go, "Piedra");
        go.transform.SetParent(zona, true);
        go.transform.position = suelo + Vector3.up * 0.32f;
        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.16f;
        go.AddComponent<Interactable>().SetPrompt("Recoger piedra");
        go.AddComponent<PiedraColeccionable>();

        Material mat = Mat("GemaColeccionable", new Color(0.55f, 0.85f, 1f), 0.92f, 0.1f, new Color(0.35f, 0.9f, 1.6f));
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloGema);
        GameObject g;
        if (modelo != null) g = (GameObject)PrefabUtility.InstantiatePrefab(modelo, go.transform);
        else { g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.transform.SetParent(go.transform, false); }
        g.name = "Gema";
        foreach (Collider c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.Euler(-20f, 0f, 15f);
        g.transform.localScale = Vector3.one;
        if (Limites(g.transform, out Bounds b) && b.size.magnitude > 0.0001f)
        {
            g.transform.localScale = Vector3.one * (0.16f / Mathf.Max(b.size.x, b.size.y, b.size.z));
            Limites(g.transform, out b);
            g.transform.position += go.transform.position - b.center;
        }

        var luz = new GameObject("Destello").AddComponent<Light>();
        luz.transform.SetParent(go.transform, false);
        luz.type = LightType.Point;
        luz.color = new Color(0.55f, 0.9f, 1f);
        luz.range = 2.2f;
        luz.intensity = 0.7f;
        luz.shadows = LightShadows.None;
    }

    // ------------------------------------------------------------------ Zona Roja

    static bool ZonaRoja(List<string> hechos)
    {
        if (!Raiz("Zona Roja", out Transform zr) || zr.Find("Mision Faro")) return false;
        Transform faro = zr.Find("Faro");
        Transform p3 = faro ? faro.Find("Piso 3 - Linterna") : null;
        Transform luz = p3 ? p3.Find("Luz del faro (apagada)") : null;
        if (!faro || !luz) { Debug.LogWarning("[Guion] Zona Roja: no encontre el faro o su luz (Faro/Piso 3 - Linterna/Luz del faro)."); return false; }
        Material glow = AssetDatabase.LoadAssetAtPath<Material>(Glow);

        var mision = new GameObject("Mision Faro").AddComponent<MisionFaro>();
        Undo.RegisterCreatedObjectUndo(mision.gameObject, "Mision Faro");
        mision.transform.SetParent(zr, false);
        mision.centroFaro = faro;

        // ---- Pared de la entrada (piso 3, donde llega el pasillo)
        Transform entrada = zr.Find("Entrada Pasillo");
        Vector3 dir = entrada ? entrada.position - faro.position : faro.right;
        dir.y = 0f;
        dir.Normalize();
        var muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
        muro.name = "Muro Entrada Faro";
        Undo.RegisterCreatedObjectUndo(muro, "Muro");
        muro.transform.SetParent(zr, true);
        muro.transform.SetPositionAndRotation(faro.position + dir * 8.7f + Vector3.up * 5f, Quaternion.LookRotation(dir));
        muro.transform.localScale = new Vector3(4.9f, 10f, 0.9f);
        Transform murete = p3.Find("Murete");
        Renderer rm = murete ? murete.GetComponentInChildren<Renderer>() : null;
        if (rm) muro.GetComponent<Renderer>().sharedMaterial = rm.sharedMaterial;
        muro.SetActive(false);
        mision.muroEntrada = muro;

        // ---- Diario en el piso 1, en una mesa junto al centro (lejos de la puerta)
        Vector3 c1 = faro.TransformPoint(new Vector3(0f, -18f, 0f));
        if (Physics.Raycast(c1, Vector3.down, out RaycastHit hp, 6f, ~0, QueryTriggerInteraction.Ignore)) c1 = hp.point;
        Vector3 puerta1 = -dir;
        Vector3 mesaPos = c1 + dir * 2.2f;
        foreach (float a in new[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f })
        {
            Vector3 p = c1 + Quaternion.Euler(0f, a, 0f) * dir * 2.2f;
            if (Vector3.Angle(p - c1, puerta1) < 45f) continue;
            if (Physics.CheckBox(p + Vector3.up * 0.7f, new Vector3(0.6f, 0.55f, 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
            mesaPos = p;
            break;
        }
        var mesa = new GameObject("Mesa del diario");
        Undo.RegisterCreatedObjectUndo(mesa, "Mesa");
        mesa.transform.SetParent(zr, true);
        Vector3 haciaCentro = c1 - mesaPos; haciaCentro.y = 0f;
        mesa.transform.SetPositionAndRotation(mesaPos, Quaternion.LookRotation(haciaCentro.sqrMagnitude > 0.01f ? haciaCentro : dir));
        Material madera = Mat("Madera", new Color(0.32f, 0.2f, 0.11f), 0.25f, 0f);
        Material cuero = Mat("Cuero", new Color(0.28f, 0.09f, 0.06f), 0.35f, 0f);
        Material papel = Mat("Papel", new Color(0.88f, 0.84f, 0.7f), 0.1f, 0f);
        Cubo(mesa.transform, "Tablero", new Vector3(0f, 0.76f, 0f), new Vector3(1f, 0.06f, 0.65f), madera, true);
        foreach (float x in new[] { -0.44f, 0.44f })
            foreach (float z in new[] { -0.27f, 0.27f })
                Cubo(mesa.transform, "Pata", new Vector3(x, 0.37f, z), new Vector3(0.06f, 0.74f, 0.06f), madera, false);
        var diario = new GameObject("Diario");
        diario.transform.SetParent(mesa.transform, false);
        diario.transform.localPosition = new Vector3(0.05f, 0.81f, 0f);
        diario.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
        Cubo(diario.transform, "Tapa", Vector3.zero, new Vector3(0.24f, 0.045f, 0.32f), cuero, false);
        Cubo(diario.transform, "Hojas", new Vector3(0.008f, 0f, 0f), new Vector3(0.23f, 0.035f, 0.305f), papel, false);
        diario.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.12f, 0.38f);
        diario.AddComponent<Interactable>();
        var ld = diario.AddComponent<Lectura>();
        ld.titulo = "Diario del farero";
        ld.prompt = "Leer diario";
        ld.texto = "La luz se apagó otra vez. Sin gasolina no va a aguantar otra noche.\n\n" +
                   "Escondí el bidón donde nadie pudiera encontrarlo y dejé tres notas en el bosque para no olvidar el camino.\n\n" +
                   "Si alguien lee esto: encuentra las tres notas. El faro no puede quedarse a oscuras.";
        mision.diario = ld;

        // ---- Las 3 notas (en el sitio de las esferas viejas del bosque)
        Transform viejas = zr.Find("Notas");
        var posNotas = new List<Vector3>();
        if (viejas)
        {
            // Las esferas viejas se apagan (no se borran: marcan donde van las notas si se rehace)
            foreach (Transform t in viejas) posNotas.Add(t.position);
            Undo.RecordObject(viejas.gameObject, "Notas viejas");
            viejas.gameObject.SetActive(false);
        }
        string[] pistas =
        {
            "No puse el bidón en el faro: ahí lo habría encontrado cualquiera.\n\nLo llevé hacia el agua quieta, la que no es del mar.",
            "Desde el lago, busca la orilla plana, donde ya no hay árboles.\n\nLo vas a ver antes de llegar al agua.",
            "Lo dejé junto a la roca negra, la que no debería estar ahí.\n\nNo la mires mucho tiempo.",
        };
        var notas = new List<Lectura>();
        Transform grupo = new GameObject("Notas del farero").transform;
        Undo.RegisterCreatedObjectUndo(grupo.gameObject, "Notas");
        grupo.SetParent(zr, false);
        Transform bosque = zr.Find("Bosque");
        for (int i = 0; i < 3 && i < posNotas.Count; i++)
        {
            if (!Suelo(posNotas[i], out Vector3 s, 3f)) s = posNotas[i] - Vector3.up;
            var n = new GameObject("Nota " + (i + 1));
            n.transform.SetParent(grupo, true);
            n.transform.position = s;
            FormaNota(n.transform, papel, madera, i);
            if (!PegarEnArbol(n.transform, bosque, s)) Debug.LogWarning("[Guion] Zona Roja: no encontre un arbol para la nota " + (i + 1) + ".", n);
            n.AddComponent<Interactable>();
            var l = n.AddComponent<Lectura>();
            l.titulo = "Nota " + (i + 1);
            l.prompt = "Leer nota";
            l.texto = pistas[i];
            Brillo(n, glow);
            notas.Add(l);
        }
        mision.notas = notas.ToArray();

        // ---- El bidon de gasolina, al lado del cubo negro del lago
        Transform cubo = zr.Find("Objeto del lago (cubo negro)");
        Transform lago = zr.Find("Lago");
        Vector3 pb = cubo ? cubo.position : faro.position;
        if (cubo && lago)
        {
            Vector3 lado = Vector3.Cross(Vector3.up, cubo.position - lago.position).normalized;
            pb = cubo.position + lado * 2f;
        }
        if (!Suelo(pb, out Vector3 sb, 3f)) sb = pb;
        var bidon = new GameObject("Bidon de gasolina");
        Undo.RegisterCreatedObjectUndo(bidon, "Bidon");
        bidon.transform.SetParent(zr, true);
        bidon.transform.SetPositionAndRotation(sb + Vector3.up * 0.25f, Quaternion.Euler(0f, 35f, 0f));
        Material rojo = Mat("BidonRojo", new Color(0.62f, 0.05f, 0.03f), 0.45f, 0.2f);
        Material negroM = Mat("BidonTapa", new Color(0.06f, 0.06f, 0.06f), 0.4f, 0.3f);
        Cubo(bidon.transform, "Cuerpo", Vector3.zero, new Vector3(0.34f, 0.44f, 0.17f), rojo, false);
        Cubo(bidon.transform, "Asa", new Vector3(-0.04f, 0.25f, 0f), new Vector3(0.2f, 0.05f, 0.05f), rojo, false);
        var tapa = Cubo(bidon.transform, "Tapa", new Vector3(0.12f, 0.25f, 0f), new Vector3(0.06f, 0.05f, 0.06f), negroM, false, PrimitiveType.Cylinder);
        tapa.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
        var bbc = bidon.AddComponent<BoxCollider>();
        bbc.center = new Vector3(0f, 0.02f, 0f);
        bbc.size = new Vector3(0.36f, 0.5f, 0.19f);
        bidon.AddComponent<Grabbable>();
        bidon.GetComponent<Rigidbody>().mass = 4f;

        // ---- El bidon y la luz del faro (que lo recibe y gira) conectados con la mision
        ConectarFaro(mision, luz, bidon, glow);

        hechos.Add("Zona Roja (diario en el piso 1, " + notas.Count + " notas, bidón junto al cubo negro, pared de la entrada y la luz del faro)");
        return true;
    }

    // Se puede llamar otra vez sin duplicar nada (la primera version se cortaba aqui y dejaba la mision sin la luz)
    static void ConectarFaro(MisionFaro mision, Transform luz, GameObject bidon, Material glow)
    {
        Grabbable grab = null;
        if (bidon)
        {
            grab = bidon.GetComponent<Grabbable>();
            if (!grab) grab = Undo.AddComponent<Grabbable>(bidon);
            var ib = bidon.GetComponent<Interactable>();
            ib.SetPrompt("Agarrar bidón");
            // Recien agregado, el evento todavia no existe (Unity lo crea al serializar)
            if (ib.onInteract == null) ib.onInteract = new UnityEngine.Events.UnityEvent<PlayerInteractor>();
            bool ya = false;
            for (int i = 0; i < ib.onInteract.GetPersistentEventCount(); i++) if (ib.onInteract.GetPersistentTarget(i) == grab) ya = true;
            if (!ya) UnityEventTools.AddPersistentListener<PlayerInteractor>(ib.onInteract, grab.PickUp);
            if (!bidon.GetComponent<Highlightable>()) Brillo(bidon, glow);
            EditorUtility.SetDirty(ib);
            mision.bidon = grab;
        }

        foreach (Transform t in luz.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var receptor = luz.GetComponent<ReceptorObjeto>();
        if (!receptor) receptor = Undo.AddComponent<ReceptorObjeto>(luz.gameObject);
        if (receptor.alRecibir == null) receptor.alRecibir = new UnityEngine.Events.UnityEvent<Grabbable>();
        receptor.objetoEsperado = grab;
        receptor.prompt = "Rellenar el faro";
        receptor.ocultarAlRecibir = true;
        EditorUtility.SetDirty(receptor);
        mision.deposito = receptor;
        mision.foco = luz.GetComponentInChildren<Light>(true);
        mision.giratoria = luz;
        Transform lente = luz.Find("Lente (apagada)");
        mision.lente = lente ? lente.GetComponent<Renderer>() : null;
        EditorUtility.SetDirty(mision);
    }

    // La hoja (y el clavo) de una nota pegada a un tronco: el origen de la nota es la superficie del tronco y su eje
    // azul apunta hacia afuera
    static void FormaNota(Transform n, Material papel, Material clavo, int i)
    {
        Cubo(n, "Papel", new Vector3(0f, 0f, 0.006f), new Vector3(0.24f, 0.32f, 0.01f), papel, false).transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 6f);
        Cubo(n, "Clavo", new Vector3(0f, 0.12f, 0.014f), new Vector3(0.018f, 0.012f, 0.018f), clavo, false, PrimitiveType.Cylinder)
            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var bc = n.GetComponent<BoxCollider>();
        if (!bc) bc = n.gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0f, 0.04f);
        bc.size = new Vector3(0.32f, 0.4f, 0.08f);
    }

    // Pega la nota en el tronco del arbol mas cercano a "desde", del lado que da hacia ese punto, a 1.6 m del suelo
    static bool PegarEnArbol(Transform nota, Transform bosque, Vector3 desde)
    {
        if (!bosque) return false;
        Transform mejor = null;
        float d = float.MaxValue;
        foreach (Transform a in bosque)
        {
            if (!a.GetComponent<CapsuleCollider>()) continue;
            float dd = Plano(a.position - desde);
            if (dd < d) { d = dd; mejor = a; }
        }
        if (!mejor) return false;
        var cc = mejor.GetComponent<CapsuleCollider>();
        Vector3 fuera = desde - mejor.position;
        fuera.y = 0f;
        fuera = fuera.sqrMagnitude > 0.01f ? fuera.normalized : mejor.forward;
        float alto = mejor.position.y + 0.15f + 1.6f;
        Vector3 eje = new Vector3(mejor.position.x, alto, mejor.position.z);
        Vector3 punto = eje + fuera * (cc.radius * mejor.lossyScale.x), normal = fuera;
        foreach (RaycastHit h in Physics.RaycastAll(eje + fuera * 3f, -fuera, 3f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider == cc) { punto = h.point; normal = new Vector3(h.normal.x, 0f, h.normal.z).normalized; break; }
        nota.SetPositionAndRotation(punto + normal * 0.004f, Quaternion.LookRotation(normal, Vector3.up));
        return true;
    }

    // Lo que ya estaba puesto de antes: las notas en estacas pasan a los arboles y se quita la luz de la mesa del diario
    static bool Arreglos(List<string> hechos)
    {
        if (!Raiz("Zona Roja", out Transform zr)) return false;
        bool algo = false;
        Transform grupo = zr.Find("Notas del farero");
        if (grupo)
        {
            Material papel = Mat("Papel", new Color(0.88f, 0.84f, 0.7f), 0.1f, 0f);
            Material madera = Mat("Madera", new Color(0.32f, 0.2f, 0.11f), 0.25f, 0f);
            int i = 0, n = 0;
            foreach (Transform nota in grupo)
            {
                Transform estaca = nota.Find("Estaca");
                if (!estaca) { i++; continue; }
                Undo.RecordObject(nota, "Nota en arbol");
                Vector3 desde = nota.position;
                Undo.DestroyObjectImmediate(estaca.gameObject);
                Transform viejo = nota.Find("Papel");
                if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
                FormaNota(nota, papel, madera, i++);
                if (PegarEnArbol(nota, zr.Find("Bosque"), desde)) n++;
            }
            if (n > 0) { hechos.Add(n + " notas pegadas en los árboles"); algo = true; }
        }
        Transform luz = zr.Find("Mesa del diario/Luz de la mesa");
        if (luz) { Undo.DestroyObjectImmediate(luz.gameObject); hechos.Add("sin la luz de la mesa del diario"); algo = true; }

        // La mision que quedo a medias (sin la luz del faro ni el bidon): se termina de conectar
        Transform mt = zr.Find("Mision Faro");
        MisionFaro m = mt ? mt.GetComponent<MisionFaro>() : null;
        if (m && (m.foco == null || m.deposito == null || m.bidon == null || m.giratoria == null))
        {
            Transform luzFaro = zr.Find("Faro/Piso 3 - Linterna/Luz del faro (apagada)");
            Transform bidon = zr.Find("Bidon de gasolina");
            if (luzFaro)
            {
                ConectarFaro(m, luzFaro, bidon ? bidon.gameObject : null, AssetDatabase.LoadAssetAtPath<Material>(Glow));
                hechos.Add("la luz del faro y el bidón conectados a la misión (habían quedado sueltos)");
                algo = true;
            }
            else Debug.LogWarning("[Guion] No encontre Faro/Piso 3 - Linterna/Luz del faro (apagada) para conectar la mision.", m);
        }
        return algo;
    }

    static void Brillo(GameObject go, Material glow)
    {
        var h = go.AddComponent<Highlightable>();
        if (glow == null) return;
        var so = new SerializedObject(h);
        so.FindProperty("outlineMaterial").objectReferenceValue = glow;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Gato

    // El modelo "Modelo" del gato a ~0.75 m de largo (antes 1.4: parecia una pantera), con los pies en el piso,
    // los ojos en la cara y el collider del tamano del cuerpo (antes una capsula de 2 m)
    public static void EscalarGato()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabGato)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabGato);
        try
        {
            Transform m = root.transform.Find("Modelo");
            if (!m) return;
            float antes = m.localScale.x;
            m.localScale = Vector3.one;
            if (!Vertices(root.transform, m, out List<Vector3> vs, out Bounds b) || b.size.magnitude < 0.0001f) { m.localScale = Vector3.one * antes; return; }
            float s = LargoGato / Mathf.Max(b.size.x, b.size.z);
            if (Mathf.Abs(s - antes) < 0.0005f) return;   // ya estaba bien
            m.localScale = Vector3.one * s;
            var agent = root.GetComponent<NavMeshAgent>();
            float pies = agent ? -agent.baseOffset : -1f;
            Vertices(root.transform, m, out vs, out b);
            m.localPosition += new Vector3(-b.center.x, pies - b.min.y, -b.center.z);
            Vertices(root.transform, m, out vs, out b);

            // Cara: los vertices de la punta delantera (+Z)
            float corte = b.max.z - b.size.z * 0.16f;
            float yMin = float.MaxValue, yMax = float.MinValue, xMin = float.MaxValue, xMax = float.MinValue;
            foreach (Vector3 v in vs)
                if (v.z > corte) { yMin = Mathf.Min(yMin, v.y); yMax = Mathf.Max(yMax, v.y); xMin = Mathf.Min(xMin, v.x); xMax = Mathf.Max(xMax, v.x); }
            if (yMin < float.MaxValue)
            {
                float alto = yMax - yMin, ancho = xMax - xMin, cx = (xMin + xMax) * 0.5f;
                int lado = 0;
                foreach (Transform t in root.transform)
                {
                    if (t.name != "Ojo") continue;
                    t.localPosition = new Vector3(cx + (lado++ == 0 ? -1f : 1f) * ancho * 0.2f, yMin + alto * 0.52f, b.max.z - b.size.z * 0.035f);
                    t.localScale = Vector3.one * Mathf.Max(0.012f, alto * 0.085f);
                }
            }
            var col = root.GetComponent<CapsuleCollider>();
            if (col)
            {
                col.direction = 2;
                col.radius = Mathf.Clamp(b.size.x * 0.5f, 0.1f, 0.3f);
                col.height = b.size.z;
                col.center = new Vector3(0f, pies + Mathf.Min(b.size.y * 0.4f, 0.35f), b.center.z);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabGato);
            Debug.Log("[Guion] Gato escalado a " + LargoGato + " m de largo (" + b.size.y.ToString("0.00") + " m de alto con la cola).");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Vertices del modelo en el espacio del prefab (sin depender de Renderer.bounds)
    static bool Vertices(Transform root, Transform modelo, out List<Vector3> vs, out Bounds b)
    {
        vs = new List<Vector3>();
        b = default;
        foreach (MeshFilter f in modelo.GetComponentsInChildren<MeshFilter>())
            if (f.sharedMesh != null)
                foreach (Vector3 v in f.sharedMesh.vertices) vs.Add(root.InverseTransformPoint(f.transform.TransformPoint(v)));
        if (vs.Count == 0) return false;
        b = new Bounds(vs[0], Vector3.zero);
        foreach (Vector3 v in vs) b.Encapsulate(v);
        return true;
    }

    // ------------------------------------------------------------------ Utilidades

    static bool Raiz(string nombre, out Transform t)
    {
        t = null;
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == nombre) { t = g.transform; return true; }
        return false;
    }

    static ZonaVolumen Area(string zona)
    {
        foreach (ZonaVolumen z in Object.FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (z.name == "Trigger " + zona) return z;
        return null;
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    static bool Suelo(Vector3 p, out Vector3 s, float alto = 1.2f)
    {
        s = p;
        if (!Physics.Raycast(p + Vector3.up * alto, Vector3.down, out RaycastHit h, alto + 6f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < 0.6f) return false;
        s = h.point;
        return true;
    }

    static GameObject Cubo(Transform padre, string nombre, Vector3 pos, Vector3 tam, Material m, bool collider, PrimitiveType tipo = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = tam;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (m) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

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

    static Material Mat(string nombre, Color color, float liso, float metalico, Color brillo = default)
    {
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", liso);
        m.SetFloat("_Metallic", metalico);
        if (brillo != default)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", brillo);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }
}
