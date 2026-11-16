using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Bombillo colgante en el centro de la Tienda: cable desde el techo, portalampara, pantalla de metal,
// bombillo que brilla y una luz calida con sombras suaves.
// Se crea solo si no existe "Tienda/Bombillo"; rehacer: Tools > Tienda > Bombillo.
[InitializeOnLoad]
public static class TiendaBombilloSetup
{
    const string Nombre = "Bombillo";

    static TiendaBombilloSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject t = GameObject.Find("Tienda");
            if (t && !t.transform.Find(Nombre)) Crear();
        };
    }

    [MenuItem("Tools/Tienda/Bombillo")]
    static void Crear()
    {
        GameObject tienda = GameObject.Find("Tienda");
        GameObject paredes = GameObject.Find("Tienda/paredes");
        if (!tienda || !paredes) return;
        Transform viejo = tienda.transform.Find(Nombre);
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);

        // Centro de la tienda (por sus paredes) y altura del techo (rayo hacia arriba)
        Bounds b = default; bool hay = false;
        foreach (Renderer r in paredes.GetComponentsInChildren<Renderer>()) { if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
        Vector3 centro = new Vector3(b.center.x, b.min.y + 1.2f, b.center.z);
        Physics.SyncTransforms();
        float techo = Physics.Raycast(centro, Vector3.up, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : b.max.y - 0.3f;
        float piso = Physics.Raycast(centro, Vector3.down, out RaycastHit h2, 5f, ~0, QueryTriggerInteraction.Ignore) ? h2.point.y : b.min.y;
        float altoBombillo = Mathf.Max(piso + 2.4f, techo - 0.9f); // cuelga ~0.9 m (por encima de la cabeza del jugador)

        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Bombillo");
        raiz.transform.SetParent(tienda.transform, true);
        raiz.transform.SetPositionAndRotation(new Vector3(centro.x, altoBombillo, centro.z), Quaternion.identity);

        Material negro = Mat("BombilloCable", new Color(0.03f, 0.03f, 0.03f), 0.4f, 0f);
        Material laton = Mat("BombilloLaton", new Color(0.5f, 0.38f, 0.2f), 0.6f, 0.9f);
        Material pantalla = Mat("BombilloPantalla", new Color(0.12f, 0.16f, 0.13f), 0.5f, 0.6f);
        Material vidrio = Mat("BombilloVidrio", new Color(1f, 0.85f, 0.6f), 0.9f, 0f, new Color(1f, 0.72f, 0.4f) * 5f);

        float cable = techo - altoBombillo;
        Pieza(raiz.transform, PrimitiveType.Cylinder, "Roseton", new Vector3(0f, cable - 0.02f, 0f), new Vector3(0.12f, 0.02f, 0.12f), laton);
        Pieza(raiz.transform, PrimitiveType.Cylinder, "Cable", new Vector3(0f, cable * 0.5f + 0.12f, 0f), new Vector3(0.012f, cable * 0.5f - 0.12f, 0.012f), negro);
        Pieza(raiz.transform, PrimitiveType.Cylinder, "Portalampara", new Vector3(0f, 0.1f, 0f), new Vector3(0.05f, 0.05f, 0.05f), laton);
        // Pantalla: un cono hecho con cilindros escalonados
        float[] radios = { 0.11f, 0.17f, 0.23f, 0.28f };
        for (int i = 0; i < radios.Length; i++)
            Pieza(raiz.transform, PrimitiveType.Cylinder, "Pantalla " + i, new Vector3(0f, 0.09f - i * 0.04f, 0f), new Vector3(radios[i] * 2f, 0.02f, radios[i] * 2f), pantalla);
        Pieza(raiz.transform, PrimitiveType.Sphere, "Bombillo", new Vector3(0f, -0.05f, 0f), new Vector3(0.09f, 0.12f, 0.09f), vidrio);

        var luzGo = new GameObject("Luz");
        luzGo.transform.SetParent(raiz.transform, false);
        luzGo.transform.localPosition = new Vector3(0f, -0.12f, 0f);
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.76f, 0.48f);
        luz.range = 11f;
        luz.intensity = 5f;
        luz.shadows = LightShadows.Soft;
        luz.shadowStrength = 0.8f;

        EditorSceneManager.MarkSceneDirty(tienda.scene);
        Debug.Log($"[Tienda] Bombillo colgante en el centro de la tienda (techo a {techo:0.00}, bombillo a {altoBombillo:0.00}). Guarda la escena (Ctrl+S).", raiz);
    }

    static void Pieza(Transform padre, PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala, Material m)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        if (nombre == "Bombillo") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material Mat(string nombre, Color c, float suave, float metal, Color emision = default)
    {
        string ruta = "Assets/Tienda/Materiales/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", suave); m.SetFloat("_Metallic", metal);
        if (emision != default) { m.SetColor("_EmissionColor", emision); m.EnableKeyword("_EMISSION"); }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }
}
