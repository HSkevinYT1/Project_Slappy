using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Cuchillo del tutorial: era un cubo de prueba de 1.4 m. Se cambia por el modelo "cuchillo de david":
// - Al cubo se le quita la malla (sigue siendo el objeto que se agarra) y el modelo va dentro, acostado en el mostrador.
// - El jugador lo agarra por el mango, con la hoja hacia adelante (holdPosition / holdRotation del Grabbable).
// - El cliente lo recibe en su mano derecha ("Agarre cuchillo" en el hueso de la mano) en vez de en el pecho.
// Se aplica sola una vez; tambien en Tools > Tienda > Arreglar cuchillo del tutorial.
[InitializeOnLoad]
public static class CuchilloTutorialSetup
{
    const string Modelo = "Assets/Models/Props/cuchillo de david.obj";
    const float Largo = 0.28f;   // largo del cuchillo en metros
    const float Giro = 90f;      // giro sobre su largo en la mano (con -90 salia boca abajo)

    static CuchilloTutorialSetup()
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
        if (!tut || !tut.cuchillo) return;
        // Sin arreglar: el cubo todavia tiene malla
        if (tut.cuchillo.GetComponent<MeshFilter>()) { Aplicar(tut); return; }
        // Version anterior: salia boca abajo (giro -90); se le da la vuelta
        var so = new SerializedObject(tut.cuchillo);
        var rot = so.FindProperty("holdRotation");
        if (Mathf.Abs(rot.vector3Value.z + 90f) < 1f) DarVuelta(tut, so);
    }

    // Gira el cuchillo 180 grados sobre su largo: en la mano del jugador y en la del cliente
    static void DarVuelta(TutorialTienda tut, SerializedObject so)
    {
        Vector3 r = so.FindProperty("holdRotation").vector3Value;
        so.FindProperty("holdRotation").vector3Value = new Vector3(r.x, r.y, Giro);
        so.ApplyModifiedProperties();
        ReceptorObjeto receptor = tut.cliente ? tut.cliente.GetComponentInChildren<ReceptorObjeto>(true) : null;
        if (receptor != null && receptor.puntoMano != null && receptor.puntoMano.name == "Agarre cuchillo")
        {
            Undo.RecordObject(receptor.puntoMano, "Cuchillo");
            receptor.puntoMano.localRotation *= Quaternion.Euler(0f, 0f, 180f);
        }
        EditorSceneManager.MarkSceneDirty(tut.gameObject.scene);
        Debug.Log("[Tienda] Cuchillo del tutorial girado (ya no sale boca abajo). Guarda la escena.", tut.cuchillo);
    }

    [MenuItem("Tools/Tienda/Arreglar cuchillo del tutorial")]
    static void Menu()
    {
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        if (!tut || !tut.cuchillo) { EditorUtility.DisplayDialog("Tienda", "No hay TutorialTienda con cuchillo en la escena.", "OK"); return; }
        Aplicar(tut);
    }

    static void Aplicar(TutorialTienda tut)
    {
        Transform k = tut.cuchillo.transform;
        Limites(k, out Bounds antes);

        Undo.RecordObject(k, "Cuchillo");
        // Escala 1 en el mundo (el padre "mostrador" tiene escala), asi el modelo no se deforma
        Vector3 ps = k.parent ? k.parent.lossyScale : Vector3.one;
        k.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);

        // Fuera la malla del cubo (si solo se ocultara, Grabbable la seguiria midiendo al agarrarlo);
        // el modelo va dentro, acostado (hoja plana sobre el mostrador)
        var cubo = k.GetComponent<MeshRenderer>();
        if (cubo) Undo.DestroyObjectImmediate(cubo);
        var malla = k.GetComponent<MeshFilter>();
        if (malla) Undo.DestroyObjectImmediate(malla);
        // Si el cuchillo ya tiene el modelo dentro, se usa ese (sin cambiarle el nombre); si no, se pone
        GameObject m = null;
        foreach (Transform h in k)
            if (h.GetComponentInChildren<Renderer>(true)) { m = h.gameObject; break; }
        if (m == null)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Modelo);
            if (!modelo) { Debug.LogWarning("[Tienda] No encuentro " + Modelo); return; }
            m = (GameObject)PrefabUtility.InstantiatePrefab(modelo, k);
            Undo.RegisterCreatedObjectUndo(m, "Modelo cuchillo");
            m.name = "Modelo";
            m.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else
        {
            Undo.RecordObject(m.transform, "Cuchillo");
            m.transform.localRotation = Quaternion.identity;
        }
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        m.transform.localPosition = Vector3.zero;
        m.transform.localScale = Vector3.one;
        Limites(m.transform, out Bounds bm);
        float escala = Largo / Mathf.Max(0.0001f, Mathf.Max(bm.size.x, bm.size.z));
        m.transform.localScale = Vector3.one * escala;
        Limites(m.transform, out bm);
        m.transform.position += k.position - bm.center;   // centrado en el objeto

        // Colision del tamano del cuchillo
        var box = k.GetComponent<BoxCollider>();
        if (box)
        {
            Undo.RecordObject(box, "Cuchillo");
            Limites(m.transform, out bm);
            box.center = k.InverseTransformPoint(bm.center);
            Vector3 s = k.lossyScale;
            box.size = new Vector3(bm.size.x / s.x, bm.size.y / s.y, bm.size.z / s.z);
        }

        // Mismo sitio del mostrador
        Limites(k, out Bounds ahora);
        k.position += new Vector3(antes.center.x - ahora.center.x, antes.min.y - ahora.min.y, antes.center.z - ahora.center.z);

        // Mango (lado -Z del modelo) en el puno del jugador y la hoja vertical hacia adelante
        float mango = Largo * 0.34f;
        var so = new SerializedObject(tut.cuchillo);
        so.FindProperty("holdPosition").vector3Value = new Vector3(0f, 0f, mango * 0.9f);
        so.FindProperty("holdRotation").vector3Value = new Vector3(-10f, 0f, Giro);
        so.ApplyModifiedProperties();

        // El cliente lo agarra con la mano derecha
        ReceptorObjeto receptor = tut.cliente ? tut.cliente.GetComponentInChildren<ReceptorObjeto>(true) : null;
        if (receptor == null) receptor = tut.cliente ? tut.cliente.GetComponent<ReceptorObjeto>() : null;
        Transform mano = Buscar(tut.cliente, "RightHand");
        if (receptor != null && mano != null)
        {
            Transform medio = Buscar(mano, "RightHandMiddle2") ?? Buscar(mano, "RightHandMiddle1");
            Transform pulgar = Buscar(mano, "RightHandThumb1");
            Vector3 agarre = medio ? Vector3.Lerp(mano.position, medio.position, 0.75f) : mano.position;
            if (pulgar) agarre = Vector3.Lerp(agarre, pulgar.position, 0.25f);
            // Hoja hacia adelante del cliente y un poco hacia abajo
            Vector3 dir = (tut.cliente.forward * 0.85f + Vector3.down * 0.5f).normalized;
            var punto = new GameObject("Agarre cuchillo").transform;
            Undo.RegisterCreatedObjectUndo(punto.gameObject, "Agarre cuchillo");
            punto.SetParent(mano, true);
            punto.SetPositionAndRotation(agarre + dir * mango, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, Giro));
            Undo.RecordObject(receptor, "Receptor");
            receptor.puntoMano = punto;
            receptor.alinearConMano = true;
            EditorUtility.SetDirty(receptor);
        }
        else Debug.LogWarning("[Tienda] No encontre la mano derecha del cliente; el cuchillo quedara frente a su pecho.");

        EditorSceneManager.MarkSceneDirty(k.gameObject.scene);
        Debug.Log("[Tienda] Cuchillo del tutorial: modelo real de " + Largo + " m, se agarra por el mango y el cliente lo toma con la mano derecha. Guarda la escena.", k);
    }

    static Transform Buscar(Transform raiz, string fin)
    {
        if (!raiz) return null;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(fin)) return t;
        return null;
    }

    static bool Limites(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        if (!hay) b = new Bounds(t.position, Vector3.zero);
        return hay;
    }
}
