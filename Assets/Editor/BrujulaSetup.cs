using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// La brujula de la Zona Roja (Brujula.cs): un pedestal de madera en el piso 3 del faro (donde se entra desde el
// pasillo) con la brujula encima, lista para agarrar. Se pone sola si falta; rehacer:
// Tools > Zona Roja > Colocar brujula (rehacer). Despues guarda la escena (Ctrl+S).
[InitializeOnLoad]
public static class BrujulaSetup
{
    const string Carpeta = "Assets/Guion";
    const string Glow = "Assets/Materials/GlowMaterial.mat";
    const string Nombre = "Brujula";
    const string NombrePedestal = "Pedestal de la brujula";

    static BrujulaSetup()
    {
        // Despues de NovedadesGuionSetup (que crea la Mision Faro)
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!ZonaRoja(out Transform zr) || !zr.Find("Mision Faro")) return;
        if (Object.FindFirstObjectByType<Brujula>(FindObjectsInactive.Include)) return;
        Colocar(zr);
    }

    [MenuItem("Tools/Zona Roja/Colocar brujula (rehacer)")]
    static void Menu()
    {
        if (!ZonaRoja(out Transform zr)) { EditorUtility.DisplayDialog("Brujula", "No encontre la \"Zona Roja\" en la escena.", "OK"); return; }
        foreach (Brujula b in Object.FindObjectsByType<Brujula>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(b.gameObject);
        Transform viejo = zr.Find(NombrePedestal);
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        Colocar(zr);
    }

    static void Colocar(Transform zr)
    {
        Transform faro = zr.Find("Faro");
        if (!faro) { Debug.LogWarning("[Brujula] No encontre \"Zona Roja/Faro\"."); return; }
        Directory.CreateDirectory(Carpeta);
        Physics.SyncTransforms();

        // Piso 3 (a la altura del faro), a un lado de la entrada: hacia el pasillo, girando hasta encontrar suelo
        // firme y libre (no el hueco de la escalera ni la luz del centro)
        Transform entrada = zr.Find("Entrada Pasillo");
        Vector3 dir = entrada ? entrada.position - faro.position : faro.right;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        Vector3 centro = faro.position;
        Vector3 pos = centro + Quaternion.Euler(0f, 40f, 0f) * dir * 4.5f;
        bool hallado = false;
        foreach (float r in new[] { 4.5f, 4f, 5.2f, 3.6f })
        {
            foreach (float a in new[] { 40f, -40f, 55f, -55f, 28f, -28f, 70f, -70f, 90f, -90f })
            {
                Vector3 p = centro + Quaternion.Euler(0f, a, 0f) * dir * r;
                if (!Physics.Raycast(p + Vector3.up * 1.6f, Vector3.down, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (h.normal.y < 0.8f || Mathf.Abs(h.point.y - centro.y) > 0.6f) continue;
                if (Physics.CheckBox(h.point + Vector3.up * 0.65f, new Vector3(0.3f, 0.55f, 0.3f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                pos = h.point;
                hallado = true;
                break;
            }
            if (hallado) break;
        }
        if (!hallado) Debug.LogWarning("[Brujula] No encontre suelo libre en el piso 3: la puse junto a la entrada, muevela si hace falta.");

        Material madera = Mat("Madera", new Color(0.32f, 0.2f, 0.11f), 0.25f, 0f);
        Material laton = Mat("BrujulaLaton", new Color(0.62f, 0.45f, 0.18f), 0.7f, 0.9f);
        Material dial = Mat("BrujulaDial", new Color(0.9f, 0.86f, 0.74f), 0.35f, 0f);
        Material rojo = Mat("BrujulaRojo", new Color(0.75f, 0.06f, 0.04f), 0.5f, 0f, new Color(0.35f, 0.02f, 0.01f));
        Material blanco = Mat("BrujulaBlanco", new Color(0.92f, 0.92f, 0.9f), 0.5f, 0f);
        Material negro = Mat("BrujulaNegro", new Color(0.08f, 0.07f, 0.06f), 0.3f, 0f);

        // ---- Pedestal (mirando hacia el centro del faro)
        Vector3 haciaCentro = centro - pos; haciaCentro.y = 0f;
        Quaternion giro = Quaternion.LookRotation(haciaCentro.sqrMagnitude > 0.01f ? haciaCentro : dir);
        var pedestal = new GameObject(NombrePedestal);
        Undo.RegisterCreatedObjectUndo(pedestal, "Pedestal brujula");
        pedestal.transform.SetParent(zr, true);
        pedestal.transform.SetPositionAndRotation(pos, giro);
        Cubo(pedestal.transform, "Base", new Vector3(0f, 0.04f, 0f), new Vector3(0.42f, 0.08f, 0.42f), madera, true);
        Cubo(pedestal.transform, "Columna", new Vector3(0f, 0.5f, 0f), new Vector3(0.16f, 0.86f, 0.16f), madera, true);
        Cubo(pedestal.transform, "Tablero", new Vector3(0f, 0.95f, 0f), new Vector3(0.36f, 0.05f, 0.36f), madera, true);

        // ---- La brujula (su eje azul = el "norte" del dial, su eje verde = arriba)
        var b = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(b, "Brujula");
        b.transform.SetParent(zr, true);
        b.transform.SetPositionAndRotation(pos + Vector3.up * 0.976f, giro * Quaternion.Euler(0f, 25f, 0f));

        Cubo(b.transform, "Caja", new Vector3(0f, 0.012f, 0f), new Vector3(0.11f, 0.012f, 0.11f), laton, false, PrimitiveType.Cylinder);
        Cubo(b.transform, "Dial", new Vector3(0f, 0.0245f, 0f), new Vector3(0.094f, 0.0012f, 0.094f), dial, false, PrimitiveType.Cylinder);
        Cubo(b.transform, "Anilla", new Vector3(0f, 0.012f, 0.06f), new Vector3(0.022f, 0.008f, 0.022f), laton, false, PrimitiveType.Cylinder);
        // Marcas: N en rojo, las otras tres en negro
        Cubo(b.transform, "Marca N", new Vector3(0f, 0.0262f, 0.04f), new Vector3(0.007f, 0.0012f, 0.012f), rojo, false);
        Cubo(b.transform, "Marca S", new Vector3(0f, 0.0262f, -0.04f), new Vector3(0.005f, 0.0012f, 0.008f), negro, false);
        Cubo(b.transform, "Marca E", new Vector3(0.04f, 0.0262f, 0f), new Vector3(0.008f, 0.0012f, 0.005f), negro, false);
        Cubo(b.transform, "Marca O", new Vector3(-0.04f, 0.0262f, 0f), new Vector3(0.008f, 0.0012f, 0.005f), negro, false);

        // Aguja: lo que gira (Y local). Punta roja hacia el objetivo, cola blanca.
        var aguja = new GameObject("Aguja").transform;
        aguja.SetParent(b.transform, false);
        aguja.localPosition = new Vector3(0f, 0.0285f, 0f);
        Cubo(aguja, "Punta", new Vector3(0f, 0f, 0.018f), new Vector3(0.006f, 0.0025f, 0.036f), rojo, false).transform.localRotation = Quaternion.identity;
        Cubo(aguja, "Cola", new Vector3(0f, 0f, -0.016f), new Vector3(0.006f, 0.0025f, 0.032f), blanco, false);
        Cubo(aguja, "Eje", new Vector3(0f, 0.001f, 0f), new Vector3(0.009f, 0.002f, 0.009f), laton, false, PrimitiveType.Cylinder);

        var col = b.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.016f, 0.004f);
        col.size = new Vector3(0.13f, 0.04f, 0.14f);

        var it = b.AddComponent<Interactable>();
        var grab = b.AddComponent<Grabbable>();
        b.GetComponent<Rigidbody>().mass = 0.25f;
        it.SetPrompt("Agarrar brújula");
        if (it.onInteract == null) it.onInteract = new UnityEngine.Events.UnityEvent<PlayerInteractor>();
        UnityEventTools.AddPersistentListener<PlayerInteractor>(it.onInteract, grab.PickUp);
        EditorUtility.SetDirty(it);

        var brujula = b.AddComponent<Brujula>();
        brujula.aguja = aguja;
        var oi = b.AddComponent<ObjetoInventario>();
        oi.nombre = "Brújula";

        var hl = b.AddComponent<Highlightable>();
        Material glow = AssetDatabase.LoadAssetAtPath<Material>(Glow);
        if (glow)
        {
            var so = new SerializedObject(hl);
            SerializedProperty p = so.FindProperty("outlineMaterial");
            if (p != null) { p.objectReferenceValue = glow; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        EditorSceneManager.MarkSceneDirty(zr.gameObject.scene);
        Debug.Log($"[Brujula] Brujula en el piso 3 del faro ({pos.x:0.#}, {pos.y:0.#}, {pos.z:0.#}). Se puede mover. Guarda la escena (Ctrl+S).", b);
    }

    static bool ZonaRoja(out Transform t)
    {
        t = null;
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == "Zona Roja") { t = g.transform; return true; }
        return false;
    }

    static GameObject Cubo(Transform padre, string nombre, Vector3 pos, Vector3 tam, Material m, bool collider, PrimitiveType tipo = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = tam;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
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
