using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramientas de editor para probar el culling de la cámara.
/// Menú: Tools > Thomas > Optimización
/// </summary>
public static class CullingTestSceneTools
{
    const string RaizNombre = "EscenarioPruebaCulling";
    const string CapaProps = "PropsPequenos"; // Si existe en Tags & Layers, los props se ponen en esta capa.
    const int Semilla = 1337;                 // Fija para que cada generación sea idéntica y las mediciones comparables.

    [MenuItem("Tools/Thomas/Optimización/Generar escenario de prueba")]
    static void GenerarEscenario()
    {
        var existente = GameObject.Find(RaizNombre);
        if (existente != null &&
            !EditorUtility.DisplayDialog("Escenario de prueba",
                "Ya existe un escenario de prueba en la escena. ¿Reemplazarlo?", "Reemplazar", "Cancelar"))
            return;
        if (existente != null) Undo.DestroyObjectImmediate(existente);

        var estadoRandom = Random.state;
        Random.InitState(Semilla);

        var raiz = new GameObject(RaizNombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Generar escenario de prueba");

        int capaProps = LayerMask.NameToLayer(CapaProps);
        if (capaProps < 0)
            Debug.Log($"[Culling] No existe la capa '{CapaProps}'. Los props quedan en Default. " +
                      "Créala en Project Settings > Tags and Layers para probar distancias por capa.");

        // Piso
        var piso = Crear(PrimitiveType.Plane, "Piso", raiz.transform, Vector3.zero, new Vector3(40f, 1f, 40f), false);
        MarcarEstatico(piso, ocluye: false);

        // Anillos de columnas a distancias conocidas: sirven para ver dónde corta el far clip.
        var anillos = Grupo("Anillos", raiz.transform);
        float[] radios = { 10f, 25f, 50f, 75f, 100f, 150f, 190f };
        foreach (float r in radios)
        {
            int cantidad = Mathf.RoundToInt(r * 0.6f) + 8;
            for (int i = 0; i < cantidad; i++)
            {
                float ang = i * Mathf.PI * 2f / cantidad;
                var pos = new Vector3(Mathf.Cos(ang) * r, 2f, Mathf.Sin(ang) * r);
                var go = Crear(PrimitiveType.Cube, $"Columna_{r:0}m_{i}", anillos, pos, new Vector3(1f, 4f, 1f), true);
                MarcarEstatico(go, ocluye: false);
            }
        }

        // Props pequeños dispersos: simulan decoración (latas, cajas, velas...).
        var props = Grupo("PropsPequenos", raiz.transform);
        for (int i = 0; i < 600; i++)
        {
            var p2 = Random.insideUnitCircle * 190f;
            float s = Random.Range(0.2f, 0.6f);
            var tipo = Random.value > 0.5f ? PrimitiveType.Cube : PrimitiveType.Sphere;
            var go = Crear(tipo, $"Prop_{i}", props, new Vector3(p2.x, s * 0.5f, p2.y), Vector3.one * s, true);
            if (capaProps >= 0) go.layer = capaProps;
            MarcarEstatico(go, ocluye: false);
        }

        // Muros grandes: ocluyen lo que está detrás (para probar occlusion culling horneado).
        var muros = Grupo("Muros", raiz.transform);
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.PI * 2f / 8f;
            var pos = new Vector3(Mathf.Cos(ang) * 18f, 4f, Mathf.Sin(ang) * 18f);
            var go = Crear(PrimitiveType.Cube, $"Muro_{i}", muros, pos, new Vector3(14f, 8f, 1f), true);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z));
            MarcarEstatico(go, ocluye: true);
        }

        Random.state = estadoRandom;
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        Debug.Log("[Culling] Escenario de prueba generado. Columnas a 10/25/50/75/100/150/190 m, 600 props y 8 muros oclusores. " +
                  "Para occlusion: Window > Rendering > Occlusion Culling > Bake.");
    }

    [MenuItem("Tools/Thomas/Optimización/Eliminar escenario de prueba")]
    static void EliminarEscenario()
    {
        var existente = GameObject.Find(RaizNombre);
        if (existente == null) { Debug.Log("[Culling] No hay escenario de prueba en la escena."); return; }
        Undo.DestroyObjectImmediate(existente);
    }

    [MenuItem("Tools/Thomas/Optimización/Agregar optimizador a la cámara principal")]
    static void AgregarACamaraPrincipal()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Optimizador de cámara",
                "No hay ninguna cámara con el tag MainCamera en la escena abierta.", "OK");
            return;
        }

        if (cam.GetComponent<CameraCullingOptimizer>() == null)
            Undo.AddComponent<CameraCullingOptimizer>(cam.gameObject);
        if (Object.FindFirstObjectByType<CullingDebugOverlay>() == null)
            Undo.AddComponent<CullingDebugOverlay>(cam.gameObject);

        Selection.activeGameObject = cam.gameObject;
        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log($"[Culling] Optimizador agregado a '{cam.name}'.");
    }

    static Transform Grupo(string nombre, Transform padre)
    {
        var g = new GameObject(nombre);
        g.transform.SetParent(padre, false);
        return g.transform;
    }

    static GameObject Crear(PrimitiveType tipo, string nombre, Transform padre, Vector3 pos, Vector3 escala, bool quitarCollider)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        if (quitarCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void MarcarEstatico(GameObject go, bool ocluye)
    {
        var flags = StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;
        if (ocluye) flags |= StaticEditorFlags.OccluderStatic;
        GameObjectUtility.SetStaticEditorFlags(go, flags);
    }
}
