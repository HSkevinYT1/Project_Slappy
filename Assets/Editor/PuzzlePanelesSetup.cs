using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Puzzle de paneles del laberinto:
// - En cada sala de cuadrante (los bloques blancos "Laberinto/Paneles/Panel N") se cuelga un panel con un numero
//   que se puede agarrar (va al inventario).
// - En el centro de la habitacion central (la zona segura) se crea un tablero de piedra con 4 ranuras:
//   hay que colocar los paneles de menor a mayor, de izquierda a derecha (mirando el tablero desde el oeste,
//   la puerta por la que se llega primero).
// - Al resolverlo se sube la reja de la entrada y avanza el progreso de zonas (ver PuzzlePaneles).
// Se aplica solo una vez; rehacer: Tools > Laberinto > Puzzle de paneles. Despues guarda la escena (Ctrl+S).
[InitializeOnLoad]
public static class PuzzlePanelesSetup
{
    const string NombreTablero = "Puzzle Paneles";
    const string NombrePaneles = "Paneles Numero";
    const string Version = "_puzzle v1";
    const string CarpetaMat = "Assets/Laberinto/Materiales";

    static PuzzlePanelesSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject lab = GameObject.Find("Laberinto");
        if (!lab || !lab.transform.Find("Paneles") || !lab.transform.Find("Zona Segura (centro)")) return;
        Transform ya = lab.transform.Find(NombreTablero);
        if (ya && ya.Find(Version)) return;
        Aplicar(lab.transform);
    }

    [MenuItem("Tools/Laberinto/Puzzle de paneles")]
    static void Menu()
    {
        GameObject lab = GameObject.Find("Laberinto");
        if (!lab) { EditorUtility.DisplayDialog("Laberinto", "No se encontro \"Laberinto\".", "OK"); return; }
        Aplicar(lab.transform);
    }

    static void Aplicar(Transform lab)
    {
        Transform contPaneles = lab.Find("Paneles");
        Transform zonaSegura = lab.Find("Zona Segura (centro)");
        if (!contPaneles || !zonaSegura) { Debug.LogWarning("[Puzzle] Faltan \"Laberinto/Paneles\" o \"Zona Segura (centro)\". Rehaz el laberinto primero."); return; }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        foreach (string viejo in new[] { NombreTablero, NombrePaneles })
        {
            Transform t = lab.Find(viejo);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }

        Material piedra = PrimerMaterial(lab.Find("Paredes")) ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gris.mat");
        Material hierro = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RejaHierro.mat") ?? Mat("PuzzleHierro", new Color(0.12f, 0.1f, 0.09f), Color.black, 0.85f, 0.35f);
        Material hueso = Mat("PanelNumero", new Color(0.86f, 0.82f, 0.7f), new Color(0.09f, 0.085f, 0.07f), 0f, 0.25f);
        Material indicador = Mat("PuzzleIndicador", new Color(0.08f, 0.08f, 0.08f), Color.black, 0f, 0.6f);

        // ---------------------------------------------------------------- Paneles con numero
        var raizPaneles = new GameObject(NombrePaneles);
        Undo.RegisterCreatedObjectUndo(raizPaneles, "Paneles");
        raizPaneles.transform.SetParent(lab, false);
        var paneles = new System.Collections.Generic.List<PanelNumero>();
        int k = 0;
        foreach (Transform bloque in contPaneles)
        {
            if (!bloque.TryGetComponent(out Renderer r)) continue;
            Bounds b = r.bounds;
            // Colgado en la cara -Z del bloque blanco, a la altura del pecho
            Vector3 pos = new Vector3(b.center.x, b.min.y + 1.35f, b.min.z - 0.025f);
            paneles.Add(CrearPanel(raizPaneles.transform, "Panel Numero " + (++k), pos, Quaternion.identity, hueso, k));
        }

        // ---------------------------------------------------------------- Tablero en el centro
        Vector3 centro = zonaSegura.position;
        if (Physics.Raycast(centro + Vector3.up, Vector3.down, out RaycastHit suelo, 6f, ~0, QueryTriggerInteraction.Ignore)) centro = suelo.point;
        else centro.y = 0f;
        // Se mira desde el oeste (de donde se llega): el frente del tablero da hacia -X
        Quaternion rot = Quaternion.LookRotation(Vector3.right, Vector3.up);

        var raiz = new GameObject(NombreTablero);
        Undo.RegisterCreatedObjectUndo(raiz, "Puzzle");
        raiz.transform.SetParent(lab, false);
        raiz.transform.SetPositionAndRotation(centro, rot);
        new GameObject(Version).transform.SetParent(raiz.transform, false);
        Transform T = raiz.transform;

        Cubo(T, "Base", new Vector3(0f, 0.075f, 0f), new Vector3(3.8f, 0.15f, 0.9f), piedra, true);
        Cubo(T, "Losa", new Vector3(0f, 1.1f, 0f), new Vector3(3.4f, 1.9f, 0.4f), piedra, true);
        Cubo(T, "Remate", new Vector3(0f, 2.1f, 0f), new Vector3(3.6f, 0.14f, 0.5f), piedra, false);

        var obs = raiz.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Box;
        obs.center = new Vector3(0f, 1.1f, 0f);
        obs.size = new Vector3(3.8f, 2.2f, 0.9f);
        obs.carving = true;

        float frente = -0.2f; // cara del tablero (local -Z mira al jugador)
        Texto(T, "Pista", new Vector3(0f, 1.82f, frente - 0.004f), new Vector2(3.1f, 0.22f), "DE MENOR A MAYOR", new Color(0.75f, 0.68f, 0.95f), 3f);
        TextMeshPro estado = Texto(T, "Estado", new Vector3(0f, 0.55f, frente - 0.004f), new Vector2(1.2f, 0.2f), "0 / 4", new Color(0.9f, 0.85f, 0.75f), 2.5f);

        var ranuras = new ReceptorObjeto[4];
        var luces = new Renderer[4];
        float[] xs = { -1.17f, -0.39f, 0.39f, 1.17f };
        for (int i = 0; i < 4; i++)
        {
            var ranura = new GameObject("Ranura " + (i + 1));
            ranura.transform.SetParent(T, false);
            ranura.transform.localPosition = new Vector3(xs[i], 1.12f, frente - 0.03f);
            ranura.transform.localRotation = Quaternion.identity;
            var col = ranura.AddComponent<BoxCollider>();
            col.size = new Vector3(0.58f, 0.58f, 0.06f);
            Cubo(ranura.transform, "Marco", Vector3.zero, new Vector3(0.58f, 0.58f, 0.06f), hierro, false);
            Cubo(ranura.transform, "Hueco", new Vector3(0f, 0f, -0.031f), new Vector3(0.46f, 0.46f, 0.004f), indicador, false);
            var punto = new GameObject("Punto");
            punto.transform.SetParent(ranura.transform, false);
            punto.transform.localPosition = new Vector3(0f, 0f, -0.055f);

            var rec = ranura.AddComponent<ReceptorObjeto>();
            rec.prompt = "Colocar panel";
            rec.puntoMano = punto.transform;
            rec.alinearConMano = true;
            rec.ocultarAlRecibir = false;
            ranuras[i] = rec;

            GameObject luz = Cubo(T, "Indicador " + (i + 1), new Vector3(xs[i], 1.56f, frente - 0.02f), new Vector3(0.14f, 0.07f, 0.04f), indicador, false);
            luces[i] = luz.GetComponent<Renderer>();
        }

        var luzTablero = new GameObject("Luz Tablero");
        luzTablero.transform.SetParent(T, false);
        luzTablero.transform.localPosition = new Vector3(0f, 2.6f, -1.6f);
        var l = luzTablero.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.75f, 0.65f, 1f);
        l.range = 5f;
        l.intensity = 2.5f;
        l.shadows = LightShadows.None;

        var puzzle = raiz.AddComponent<PuzzlePaneles>();
        puzzle.ranuras = ranuras;
        puzzle.paneles = paneles.ToArray();
        puzzle.indicadores = luces;
        puzzle.textoEstado = estado;

        EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Puzzle] {paneles.Count} paneles con numero en las salas y tablero de 4 ranuras en el centro del laberinto. Guarda la escena (Ctrl+S).", raiz);
    }

    static PanelNumero CrearPanel(Transform padre, string nombre, Vector3 pos, Quaternion rot, Material mat, int numero)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.42f, 0.42f, 0.04f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.mass = 0.6f;
        Cubo(go.transform, "Placa", Vector3.zero, new Vector3(0.42f, 0.42f, 0.04f), mat, false);
        // Marco oscuro fino
        Material borde = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RejaHierro.mat");
        if (borde)
        {
            Cubo(go.transform, "Borde Arriba", new Vector3(0f, 0.2f, 0f), new Vector3(0.42f, 0.02f, 0.045f), borde, false);
            Cubo(go.transform, "Borde Abajo", new Vector3(0f, -0.2f, 0f), new Vector3(0.42f, 0.02f, 0.045f), borde, false);
        }

        var inter = go.AddComponent<Interactable>();
        var so = new SerializedObject(inter);
        so.FindProperty("prompt").stringValue = "Tomar panel";
        so.ApplyModifiedPropertiesWithoutUndo();
        var grab = go.AddComponent<Grabbable>();
        UnityEventTools.AddPersistentListener(inter.onInteract, new UnityAction<PlayerInteractor>(grab.PickUp));
        go.AddComponent<ObjetoInventario>();

        // Numero en las dos caras (la -Z es la que se ve en la mano y colgado)
        Color tinta = new Color(0.45f, 0.05f, 0.04f);
        TextMeshPro frente = Texto(go.transform, "Numero", new Vector3(0f, 0f, -0.0215f), new Vector2(0.34f, 0.34f), numero.ToString(), tinta, 8f);
        TextMeshPro dorso = Texto(go.transform, "Numero Dorso", new Vector3(0f, 0f, 0.0215f), new Vector2(0.34f, 0.34f), numero.ToString(), tinta, 8f);
        dorso.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var pn = go.AddComponent<PanelNumero>();
        pn.numero = numero;
        pn.textos = new[] { frente, dorso };
        return pn;
    }

    static GameObject Cubo(Transform padre, string nombre, Vector3 posLocal, Vector3 tam, Material mat, bool colision)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = tam;
        if (!colision) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static TextMeshPro Texto(Transform padre, string nombre, Vector3 posLocal, Vector2 tam, string texto, Color color, float tamanoMax)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = tam;
        rt.localPosition = posLocal;
        rt.localRotation = Quaternion.identity;
        t.text = texto;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.1f;
        t.fontSizeMax = tamanoMax;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static Material Mat(string nombre, Color color, Color emision, float metal, float liso)
    {
        string ruta = CarpetaMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        System.IO.Directory.CreateDirectory(CarpetaMat);
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Smoothness", liso);
        m.SetColor("_EmissionColor", emision);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static Material PrimerMaterial(Transform t)
    {
        if (!t) return null;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) if (r.sharedMaterial) return r.sharedMaterial;
        return null;
    }
}
