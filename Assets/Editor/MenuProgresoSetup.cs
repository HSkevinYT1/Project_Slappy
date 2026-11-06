using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Prepara en la escena de la tienda:
// - Progreso de la Zona Principal: solo brilla el camino de la zona que toca y solo su puerta esta abierta
//   (ProgresoCaminos en "Marcas Caminos" + una PuertaDeslizante al inicio de cada pasillo).
// - Pilas para la linterna repartidas por las zonas (prefab Assets/Prefabs/Bateria.prefab).
// - Linterna visible con los Volumes: mas potente, Volume propio (LinternaVolume) y el negro del laberinto
//   deja de ser metalico (antes no podia reflejar ninguna luz).
// - Menu principal: camara frente a la tienda, Jugar / Salir y cinematica hasta la cabeza del jugador.
// - Quita el jugador de VR de la escena.
//
// Se aplica SOLO una vez (si no existe "Menu y Progreso") y se puede rehacer desde Tools > Menu y Progreso.
// Despues hay que guardar la escena.
public static class MenuProgresoSetup
{
    const string Raiz = "Menu y Progreso";
    const string PrefabBateria = "Assets/Prefabs/Bateria.prefab";
    const string PrefabLinterna = "Assets/Prefabs/Linterna.prefab";
    const string PerfilLinterna = "Assets/Volumenes/LinternaVolume.asset";
    const string MaterialNegro = "Assets/Shaders/NegroLVL1.mat";
    const string Fuente = "Assets/Fonts/Frisky SDF.asset";
    const float IntensidadLinterna = 100f;
    const float RangoLinterna = 22f;
    const int BateriasPorZona = 3;

    // Orden de las zonas (nombreDestino de cada MarcaCamino)
    static readonly string[] Orden = { "Laberinto", "Backrooms", "Cloacas", "Proximamente" };
    static readonly string[] NombresZona = { "Laberinto", "Backrooms", "Cloacas", "Zona Roja" };

    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || GameObject.Find(Raiz)) return;
        if (!GameObject.Find("Zona_Principal/Marcas Caminos") || !Object.FindFirstObjectByType<TutorialTienda>()) return;
        Aplicar();
    }

    [MenuItem("Tools/Menu y Progreso/Rehacer todo")]
    static void RehacerMenu()
    {
        GameObject viejo = GameObject.Find(Raiz);
        if (viejo) Undo.DestroyObjectImmediate(viejo);
        Aplicar();
    }

    public static void Aplicar()
    {
        QuitarJugadorVR();

        var raiz = new GameObject(Raiz);
        Undo.RegisterCreatedObjectUndo(raiz, "Menu y Progreso");

        CrearProgreso();
        CrearBaterias(raiz.transform);
        PrepararLinterna(raiz.transform);
        CrearMenu(raiz.transform);

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log("[Menu y Progreso] Listo: caminos y puertas, pilas, linterna y menu principal. Guarda la escena (Ctrl+S).", raiz);
    }

    // ------------------------------------------------------------------ VR

    static void QuitarJugadorVR()
    {
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!go.name.StartsWith("Player_VR")) continue;
            if (!PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsPrefabAssetMissing(go) && !go.GetComponentInChildren<Camera>(true)) continue;
            Debug.Log("[Menu y Progreso] Quitado " + go.name + " (ya no hay VR).");
            Undo.DestroyObjectImmediate(go);
        }
    }

    // ------------------------------------------------------------- Progreso

    static void CrearProgreso()
    {
        Transform marcas = GameObject.Find("Zona_Principal/Marcas Caminos").transform;
        Transform pasillos = GameObject.Find("Zona_Principal/Pasillos")?.transform;

        var progreso = marcas.GetComponent<ProgresoCaminos>();
        if (!progreso) progreso = Undo.AddComponent<ProgresoCaminos>(marcas.gameObject);
        Undo.RecordObject(progreso, "Progreso caminos");

        // Los caminos apagados quedaban al 12%: ahora quedan sin color
        var matMarca = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/MarcaPiso.mat");
        if (matMarca && matMarca.HasProperty("_AlphaApagado") && matMarca.GetFloat("_AlphaApagado") > 0f)
        {
            Undo.RecordObject(matMarca, "Caminos apagados");
            matMarca.SetFloat("_AlphaApagado", 0f);
            EditorUtility.SetDirty(matMarca);
        }

        Material matPuerta = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gris.mat");
        var tramos = new List<ProgresoCaminos.Tramo>();
        for (int i = 0; i < Orden.Length; i++)
        {
            MarcaCamino marca = null;
            foreach (MarcaCamino m in marcas.GetComponentsInChildren<MarcaCamino>(true))
                if (m.nombreDestino == Orden[i]) marca = m;
            if (!marca) { Debug.LogWarning("[Menu y Progreso] No encuentro el camino de " + Orden[i]); continue; }

            Undo.RecordObject(marca, "Progreso caminos");
            marca.encendidaAlIniciar = i == 0;
            marca.SetEncendida(i == 0);

            PuertaDeslizante puerta = null;
            Transform pasillo = pasillos ? pasillos.Find("Pasillo - " + Orden[i]) : null;
            if (pasillo)
            {
                Transform vieja = pasillo.Find("Puerta");
                if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Puerta";
                Undo.RegisterCreatedObjectUndo(go, "Puerta");
                go.transform.SetParent(pasillo, false);
                // Pasillo: 8 m de ancho interior y paredes de 4.25 m; la puerta tapa la boca del pasillo
                go.transform.localPosition = new Vector3(0f, 2.125f, 0.6f);
                go.transform.localScale = new Vector3(8.1f, 4.25f, 0.4f);
                if (matPuerta) go.GetComponent<Renderer>().sharedMaterial = matPuerta;
                puerta = go.AddComponent<PuertaDeslizante>();
                puerta.abiertaAlIniciar = i == 0;
                puerta.desplazamiento = new Vector3(0f, -4.3f, 0f);
            }
            else Debug.LogWarning("[Menu y Progreso] No encuentro el pasillo de " + Orden[i]);

            tramos.Add(new ProgresoCaminos.Tramo { nombre = NombresZona[i], marca = marca, puerta = puerta });
        }
        progreso.tramos = tramos.ToArray();
        progreso.zonaActual = 0;
        EditorUtility.SetDirty(progreso);
    }

    // -------------------------------------------------------------- Baterias

    static void CrearBaterias(Transform raiz)
    {
        GameObject prefab = PrefabDeBateria();
        var contenedor = new GameObject("Baterias");
        contenedor.transform.SetParent(raiz, false);

        foreach (string zona in NombresZona)
        {
            GameObject area = GameObject.Find(zona);
            if (!area || !Limites(area.transform, out Bounds b)) continue;

            var rnd = new System.Random(("bateria" + zona).GetHashCode() & 0x7fffffff);
            var puestas = new List<Vector3>();
            for (int i = 0; i < 400 && puestas.Count < BateriasPorZona; i++)
            {
                var p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (float)rnd.NextDouble()), b.max.y,
                                    Mathf.Lerp(b.min.z, b.max.z, (float)rnd.NextDouble()));
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, b.size.y + 2f, NavMesh.AllAreas)) continue;
                Vector3 q = hit.position;
                if (!b.Contains(q)) continue;
                bool lejos = true;
                foreach (Vector3 o in puestas) if ((o - q).magnitude < 12f) lejos = false;
                if (!lejos) continue;
                // El NavMesh queda un poco por encima del piso: se apoya con un rayo hacia abajo,
                // y tiene que ser un piso de esta zona (las cajas de las zonas se pisan)
                if (!Physics.Raycast(q + Vector3.up * 0.3f, Vector3.down, out RaycastHit suelo, 2f, ~0, QueryTriggerInteraction.Ignore)
                    || !suelo.collider.transform.IsChildOf(area.transform)) continue;
                puestas.Add(q);

                var bat = (GameObject)PrefabUtility.InstantiatePrefab(prefab, contenedor.transform);
                bat.name = "Bateria " + zona + " " + puestas.Count;
                bat.transform.position = suelo.point + Vector3.up * 0.1f;
                bat.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            }
        }
    }

    static GameObject PrefabDeBateria()
    {
        var existente = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBateria);
        if (existente) return existente;

        Material cuerpo = CrearMaterial("Assets/Materials/Bateria Cuerpo.mat", new Color(0.12f, 0.12f, 0.13f), Color.black, 0.5f);
        Material carga = CrearMaterial("Assets/Materials/Bateria Carga.mat", new Color(0.35f, 1f, 0.35f), new Color(0.2f, 0.9f, 0.25f), 0.3f);
        Material polo = CrearMaterial("Assets/Materials/Bateria Polo.mat", new Color(0.8f, 0.6f, 0.3f), Color.black, 0.7f);

        // Pila de 20 cm de alto: cuerpo oscuro con una franja verde que brilla y el polo arriba
        var raiz = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        raiz.name = "Bateria";
        raiz.transform.localScale = new Vector3(0.09f, 0.1f, 0.09f);
        raiz.GetComponent<Renderer>().sharedMaterial = cuerpo;
        Hijo(raiz.transform, "Franja", new Vector3(0f, 0.35f, 0f), new Vector3(1.03f, 0.25f, 1.03f), carga);
        Hijo(raiz.transform, "Polo", new Vector3(0f, 1.08f, 0f), new Vector3(0.35f, 0.08f, 0.35f), polo);

        var interactable = raiz.AddComponent<Interactable>();
        var so = new SerializedObject(interactable);
        so.FindProperty("prompt").stringValue = "Recoger bateria";
        so.ApplyModifiedPropertiesWithoutUndo();
        var bateria = raiz.AddComponent<Bateria>();
        UnityEventTools.AddPersistentListener(interactable.onInteract, bateria.Recoger);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, PrefabBateria);
        Object.DestroyImmediate(raiz);
        return prefab;
    }

    static void Hijo(Transform padre, string nombre, Vector3 pos, Vector3 escala, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material CrearMaterial(string ruta, Color color, Color emision, float suavidad)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", suavidad);
        if (emision.maxColorComponent > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emision);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    static bool Limites(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Collider c in t.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!hay) { b = c.bounds; hay = true; } else b.Encapsulate(c.bounds);
        }
        return hay;
    }

    // -------------------------------------------------------------- Linterna

    static void PrepararLinterna(Transform raiz)
    {
        // Mas potente: con 22.5 la luz se apagaba a 2-3 m y los Volumes la tapaban del todo
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabLinterna);
        try
        {
            bool cambio = false;
            foreach (Flashlight f in root.GetComponentsInChildren<Flashlight>(true))
                if (f.lightObject && f.lightObject.TryGetComponent(out Light l) && l.intensity < IntensidadLinterna)
                {
                    l.intensity = IntensidadLinterna;
                    l.range = RangoLinterna;
                    cambio = true;
                }
            if (cambio) PrefabUtility.SaveAsPrefabAsset(root, PrefabLinterna);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        foreach (Flashlight f in Object.FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (f.lightObject && f.lightObject.TryGetComponent(out Light l) && l.intensity < IntensidadLinterna)
            {
                Undo.RecordObject(l, "Linterna");
                l.intensity = IntensidadLinterna;
                l.range = RangoLinterna;
            }

        // El negro del laberinto era metalico sin brillo: no reflejaba la linterna (solo el cielo)
        var negro = AssetDatabase.LoadAssetAtPath<Material>(MaterialNegro);
        if (negro && negro.GetFloat("_Metallic") > 0.5f)
        {
            Undo.RecordObject(negro, "Negro laberinto");
            negro.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.06f));
            negro.SetColor("_Color", new Color(0.05f, 0.05f, 0.06f));
            negro.SetFloat("_Metallic", 0f);
            negro.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(negro);
        }

        var go = new GameObject("Volumen Linterna");
        go.transform.SetParent(raiz, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 0f;
        volume.sharedProfile = PerfilDeLinterna();
        go.AddComponent<VolumenLinterna>();
    }

    static VolumeProfile PerfilDeLinterna()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PerfilLinterna);
        if (perfil) return perfil;
        perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(perfil, PerfilLinterna);

        // Sombras y medios tonos neutros (los perfiles de las zonas los hunden hasta el negro)
        var smh = perfil.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.value = new Vector4(1f, 1f, 1f, 0f);
        smh.midtones.value = new Vector4(1f, 1f, 1f, 0f);
        smh.highlights.value = new Vector4(1f, 1f, 1f, 0f);
        var ajustes = perfil.Add<ColorAdjustments>(false);
        ajustes.colorFilter.overrideState = true;
        ajustes.colorFilter.value = Color.white;

        foreach (VolumeComponent c in perfil.components)
        {
            c.name = c.GetType().Name;
            AssetDatabase.AddObjectToAsset(c, perfil);
        }
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        return perfil;
    }

    // ------------------------------------------------------------------ Menu

    static void CrearMenu(Transform raiz)
    {
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>();
        Transform[] entrada = tut ? tut.caminoEntrada : null;
        if (entrada == null || entrada.Length < 2 || !entrada[0] || !entrada[1])
        {
            Debug.LogWarning("[Menu y Progreso] El TutorialTienda no tiene camino de entrada: no se pudo colocar la camara del menu.");
            return;
        }
        Vector3 afuera = entrada[0].position, puerta = entrada[1].position;
        Vector3 haciaAfuera = afuera - puerta; haciaAfuera.y = 0f; haciaAfuera.Normalize();

        var menu = new GameObject("Menu Principal");
        menu.transform.SetParent(raiz, false);
        var script = menu.AddComponent<MenuPrincipal>();

        // Camara frente a la tienda
        var camGo = new GameObject("Camara Menu");
        camGo.transform.SetParent(menu.transform, false);
        camGo.transform.position = afuera + haciaAfuera * 9f + Vector3.up * 2.2f;
        camGo.transform.rotation = Quaternion.LookRotation(puerta + Vector3.up * 2.5f - camGo.transform.position);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        camGo.AddComponent<AudioListener>();
        script.camaraMenu = cam;

        // Camino de la cinematica: hasta la puerta y adentro
        var camino = new GameObject("Camino Cinematica").transform;
        camino.SetParent(menu.transform, false);
        var puntos = new List<Transform>();
        puntos.Add(Punto(camino, "1 Afuera", afuera + Vector3.up * 1.8f));
        puntos.Add(Punto(camino, "2 Puerta", puerta + Vector3.up * 1.8f));
        script.camino = puntos.ToArray();

        script.panel = CrearCanvas(menu.transform, script);
        AsegurarEventSystem(raiz);
    }

    static Transform Punto(Transform padre, string nombre, Vector3 pos)
    {
        var t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        t.position = pos;
        return t;
    }

    static CanvasGroup CrearCanvas(Transform padre, MenuPrincipal script)
    {
        var go = new GameObject("Canvas Menu", typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        var grupo = go.AddComponent<CanvasGroup>();

        TMP_FontAsset fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fuente);

        // Oscurece un poco la parte izquierda para que se lea el texto
        var sombra = Rect(go.transform, "Sombra", new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        var img = sombra.gameObject.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);
        img.raycastTarget = false;

        var titulo = Texto(go.transform, "Titulo", "SLAPPY", 150f, fuente, new Color(0.85f, 0.1f, 0.1f));
        Colocar(titulo.rectTransform, new Vector2(140f, -180f), new Vector2(900f, 200f));

        var jugar = Boton(go.transform, "Boton Jugar", "Jugar", fuente, new Vector2(150f, -480f));
        UnityEventTools.AddPersistentListener(jugar.onClick, script.Jugar);
        var salir = Boton(go.transform, "Boton Salir", "Salir", fuente, new Vector2(150f, -600f));
        UnityEventTools.AddPersistentListener(salir.onClick, script.Salir);
        return grupo;
    }

    static RectTransform Rect(Transform padre, string nombre, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
        return r;
    }

    // Arriba a la izquierda
    static void Colocar(RectTransform r, Vector2 pos, Vector2 tam)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = pos;
        r.sizeDelta = tam;
    }

    static TextMeshProUGUI Texto(Transform padre, string nombre, string texto, float tam, TMP_FontAsset fuente, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.fontSize = tam;
        if (fuente) t.font = fuente;
        t.color = color;
        t.alignment = TextAlignmentOptions.Left;
        t.raycastTarget = false;
        return t;
    }

    static Button Boton(Transform padre, string nombre, string texto, TMP_FontAsset fuente, Vector2 pos)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Colocar((RectTransform)go.transform, pos, new Vector2(420f, 100f));
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        var boton = go.AddComponent<Button>();
        var colores = boton.colors;
        colores.normalColor = new Color(1f, 1f, 1f, 0f);
        colores.highlightedColor = new Color(0.6f, 0.05f, 0.05f, 0.5f);
        colores.pressedColor = new Color(0.4f, 0f, 0f, 0.7f);
        colores.selectedColor = colores.normalColor;
        boton.colors = colores;

        var t = Texto(go.transform, "Texto", texto, 72f, fuente, new Color(0.92f, 0.9f, 0.85f));
        var r = t.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(20f, 0f); r.offsetMax = Vector2.zero;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        return boton;
    }

    static void AsegurarEventSystem(Transform raiz)
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include)) return;
        var go = new GameObject("EventSystem");
        go.transform.SetParent(raiz, false);
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
