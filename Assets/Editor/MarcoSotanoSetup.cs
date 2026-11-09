using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Marco de la puerta del sotano (la entrada a la escalera desde la tienda era solo un hueco en la pared):
// - Crea "Marco Puerta Sotano" (dos laterales y el dintel, de madera) ajustado al hueco, con Highlightable
//   (GlowMaterial). Brilla al final del tutorial si el jugador no esta en el mostrador.
// - TutorialTienda: Zona Mostrador (solo gira 180 desde ahi) y Marco Sotano.
// Se aplica sola una vez; rehacer: Tools > Tienda > Marco de la puerta del sotano.
[InitializeOnLoad]
public static class MarcoSotanoSetup
{
    const string Nombre = "Marco Puerta Sotano";
    const string Brillo = "Assets/Materials/GlowMaterial.mat";
    const float Grosor = 0.09f;      // ancho de cada pieza del marco
    const float Sobresale = 0.04f;   // cuanto sale de la pared por cada lado

    static MarcoSotanoSetup()
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
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        if (!tut || tut.marcoSotano || !GameObject.Find("escaleras/Barreras Escalera/Barrera Arriba")) return;
        Aplicar(tut);
    }

    [MenuItem("Tools/Tienda/Marco de la puerta del sotano")]
    static void Menu()
    {
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        if (!tut) { EditorUtility.DisplayDialog("Tienda", "No hay TutorialTienda en la escena.", "OK"); return; }
        GameObject viejo = GameObject.Find("escaleras/" + Nombre);
        if (viejo) Undo.DestroyObjectImmediate(viejo);
        Aplicar(tut);
    }

    static void Aplicar(TutorialTienda tut)
    {
        GameObject barrera = GameObject.Find("escaleras/Barreras Escalera/Barrera Arriba");
        if (!barrera) { Debug.LogWarning("[Tienda] No encuentro \"Barrera Arriba\" de la escalera."); return; }
        Transform escaleras = GameObject.Find("escaleras").transform;

        // Hacia la tienda desde lo alto de la escalera (la barrera mira hacia la bajada)
        Vector3 haciaTienda = -barrera.transform.forward; haciaTienda.y = 0f; haciaTienda.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, haciaTienda);
        Vector3 inicio = barrera.transform.position + Vector3.up * 1.2f;

        // El dintel: lo primero que hay encima avanzando hacia la tienda (la pared sobre el hueco)
        Collider dintel = null; Vector3 punto = default;
        for (float d = 0f; d < 2.5f && dintel == null; d += 0.05f)
        {
            Vector3 p = inicio + haciaTienda * d;
            if (Physics.Raycast(p, Vector3.up, out RaycastHit h, 2.5f, ~0, QueryTriggerInteraction.Ignore)
                && h.collider.transform.IsChildOf(escaleras) == false && h.collider.bounds.size.y < 1.5f)
            { dintel = h.collider; punto = p; }
        }
        if (dintel == null) { Debug.LogWarning("[Tienda] No encontre la pared sobre la puerta del sotano."); return; }

        // Grosor de la pared (eje hacia la tienda) segun el dintel
        Bounds bd = dintel.bounds;
        float a0 = Vector3.Dot(bd.min, haciaTienda), a1 = Vector3.Dot(bd.max, haciaTienda);
        float paredMin = Mathf.Min(a0, a1), paredMax = Mathf.Max(a0, a1);
        float centroPared = (paredMin + paredMax) * 0.5f;
        punto += haciaTienda * (centroPared - Vector3.Dot(punto, haciaTienda));

        // Ancho del hueco (a los lados) y alto (suelo -> dintel)
        float izq = Distancia(punto, -lado, 3f), der = Distancia(punto, lado, 3f);
        float arriba = Distancia(punto, Vector3.up, 3f), abajo = Distancia(punto, Vector3.down, 3f);
        if (izq > 2.9f || der > 2.9f || abajo > 2.9f) { Debug.LogWarning("[Tienda] No pude medir el hueco de la puerta del sotano."); return; }
        Vector3 suelo = punto + Vector3.down * abajo;
        float alto = abajo + arriba;
        float fondo = (paredMax - paredMin) + Sobresale * 2f;

        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Marco sotano");
        raiz.transform.SetParent(escaleras, true);
        raiz.transform.SetPositionAndRotation(suelo + lado * ((der - izq) * 0.5f), Quaternion.LookRotation(haciaTienda));
        raiz.transform.localScale = Vector3.one;
        float ancho = izq + der;

        Material madera = MaterialMadera();
        // Laterales (dentro del hueco, pegados a la pared) y dintel
        Pieza(raiz.transform, "Lateral izquierdo", new Vector3(-(ancho * 0.5f - Grosor * 0.5f), alto * 0.5f, 0f), new Vector3(Grosor, alto, fondo), madera);
        Pieza(raiz.transform, "Lateral derecho", new Vector3(ancho * 0.5f - Grosor * 0.5f, alto * 0.5f, 0f), new Vector3(Grosor, alto, fondo), madera);
        Pieza(raiz.transform, "Dintel", new Vector3(0f, alto - Grosor * 0.5f, 0f), new Vector3(ancho, Grosor, fondo), madera);

        var marca = raiz.AddComponent<Highlightable>();
        var so = new SerializedObject(marca);
        so.FindProperty("outlineMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Brillo);
        so.ApplyModifiedProperties();

        Undo.RecordObject(tut, "Marco sotano");
        tut.marcoSotano = marca;
        if (!tut.zonaMostrador)
        {
            ReceptorObjeto rec = tut.cliente ? tut.cliente.GetComponentInChildren<ReceptorObjeto>(true) : null;
            Transform z = tut.transform.Find("Zona Mostrador");
            tut.zonaMostrador = rec && rec.zonaEntrega ? rec.zonaEntrega : z ? z.GetComponent<BoxCollider>() : null;
        }
        EditorUtility.SetDirty(tut);

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"[Tienda] Marco de la puerta del sotano ({ancho:0.00} x {alto:0.00} m). Al final del tutorial: gira 180 solo desde el mostrador; si no, brilla el marco. Guarda la escena.", raiz);
    }

    static float Distancia(Vector3 desde, Vector3 dir, float max)
    {
        return Physics.Raycast(desde, dir, out RaycastHit h, max, ~0, QueryTriggerInteraction.Ignore) ? h.distance : max;
    }

    static void Pieza(Transform padre, string nombre, Vector3 posLocal, Vector3 tam, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());   // no estorba al pasar
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = tam;
        if (mat) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // La madera de los muebles de la tienda (la del mostrador)
    static Material MaterialMadera()
    {
        GameObject m = GameObject.Find("Tienda/mostrador");
        if (m)
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
                if (r.sharedMaterial && r.sharedMaterial.name.StartsWith("TiendaMueble")) return r.sharedMaterial;
        return null;
    }
}
