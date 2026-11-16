using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramientas para aplicar la optimización de cámara en escenas reales (la Tienda).
/// Menú: Tools > Thomas > Optimización > Escena real
///
/// Orden recomendado:
///   1. Agregar optimizador a Player.prefab
///   2. Preparar estáticos para occlusion (con la escena abierta)
///   3. Agregar overlay de medición (temporal) y medir con F4
///   4. Window > Rendering > Occlusion Culling > Bake, y volver a medir
/// </summary>
public static class CullingSetupTools
{
    const string PlayerPrefabGuid = "9f728eaee9df189478048e732dfde22d"; // Assets/Prefabs/Player.prefab
    const float FarClipInicial = 100f; // Punto de partida; ajustar recorriendo la escena.
    const string NombreOverlay = "CullingDebug (BORRAR antes del merge)";

    // ---------------------------------------------------------------- 1. Prefab del jugador

    [MenuItem("Tools/Thomas/Optimización/Escena real/1. Agregar optimizador a Player.prefab")]
    static void AgregarAPlayerPrefab()
    {
        string ruta = AssetDatabase.GUIDToAssetPath(PlayerPrefabGuid);
        if (string.IsNullOrEmpty(ruta))
        {
            EditorUtility.DisplayDialog("Optimizador", "No se encontró Player.prefab.", "OK");
            return;
        }

        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            var cam = raiz.GetComponentInChildren<Camera>(true);
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Optimizador", $"{ruta} no tiene ninguna cámara.", "OK");
                return;
            }

            var opt = cam.GetComponent<CameraCullingOptimizer>();
            if (opt != null)
            {
                EditorUtility.DisplayDialog("Optimizador",
                    $"'{cam.name}' en {ruta} ya tiene el optimizador (far clip {opt.farClip:0} m).", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Agregar optimizador a Player.prefab",
                    $"Se agregará CameraCullingOptimizer a '{cam.name}' con far clip {FarClipInicial:0} m " +
                    $"(hoy la cámara tiene {cam.farClipPlane:0.#} m).\n\n" +
                    "Afecta a TODAS las escenas que usan Player.prefab (Tienda, Kevin, jesus, kanutencio...).",
                    "Agregar", "Cancelar"))
                return;

            opt = cam.gameObject.AddComponent<CameraCullingOptimizer>();
            opt.farClip = FarClipInicial;
            opt.Aplicar();

            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            Debug.Log($"[Culling] Optimizador agregado a '{cam.name}' en {ruta} (far clip {FarClipInicial:0} m).",
                AssetDatabase.LoadAssetAtPath<GameObject>(ruta));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    // ---------------------------------------------------------------- 2. Flags estáticos
    //
    // Criterio (automático, no hay que revisar objeto por objeto):
    //   - Solo se tocan objetos que YA son estáticos (Batching Static o Contribute GI). Esos objetos
    //     no se mueven, así que es seguro hacerlos participar en el occlusion. Lo dinámico
    //     (puertas, enemigos, objetos agarrables) no se toca: Unity lo oculta igual en runtime.
    //   - Occludee (puede SER ocultado): todo objeto estático.
    //   - Occluder (puede OCULTAR a otros): solo si además es
    //       * opaco: un vidrio o reja marcado como occluder escondería lo que se ve a través, y
    //       * grande (lado mayor >= TamanoMinimoOccluder): muros, pisos, techos, muebles grandes.
    //         Un prop pequeño casi no tapa nada y solo hace más lento y pesado el bake.

    const float TamanoMinimoOccluder = 1.5f; // metros

    enum Accion { OccluderYOccludee, SoloOccludeeTransparente, SoloOccludeePequeno }

    struct Candidato
    {
        public GameObject go;
        public StaticEditorFlags flags;
        public Accion accion;
    }

    static System.Collections.Generic.List<Candidato> Analizar(out int yaListos, out int dinamicos)
    {
        const StaticEditorFlags intencionEstatica = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI;
        yaListos = dinamicos = 0;
        var lista = new System.Collections.Generic.List<Candidato>();

        var renderers = SceneManager_GetActive().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true));

        foreach (var r in renderers)
        {
            var go = r.gameObject;
            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            if ((flags & intencionEstatica) == 0) { dinamicos++; continue; }

            bool opaco = r.sharedMaterials.All(m => m == null || m.renderQueue < 2450);
            var b = r.bounds.size;
            bool grande = Mathf.Max(b.x, Mathf.Max(b.y, b.z)) >= TamanoMinimoOccluder;

            var accion = !opaco ? Accion.SoloOccludeeTransparente
                       : !grande ? Accion.SoloOccludeePequeno
                       : Accion.OccluderYOccludee;

            var deseados = flags | StaticEditorFlags.OccludeeStatic;
            if (accion == Accion.OccluderYOccludee) deseados |= StaticEditorFlags.OccluderStatic;

            if (deseados == flags) { yaListos++; continue; }
            lista.Add(new Candidato { go = go, flags = deseados, accion = accion });
        }
        return lista;
    }

    static string Resumen(System.Collections.Generic.List<Candidato> c, int yaListos, int dinamicos) =>
        $"Objetos a marcar: {c.Count}\n" +
        $"  • {c.Count(x => x.accion == Accion.OccluderYOccludee)} Occluder + Occludee (opacos y grandes)\n" +
        $"  • {c.Count(x => x.accion == Accion.SoloOccludeePequeno)} solo Occludee (pequeños, < {TamanoMinimoOccluder} m)\n" +
        $"  • {c.Count(x => x.accion == Accion.SoloOccludeeTransparente)} solo Occludee (transparentes)\n\n" +
        $"Ya listos: {yaListos}. Dinámicos (no se tocan): {dinamicos}.";

    [MenuItem("Tools/Thomas/Optimización/Escena real/2a. Vista previa: seleccionar occluders (no modifica)")]
    static void VistaPreviaOccluders() => VistaPrevia(Accion.OccluderYOccludee);

    [MenuItem("Tools/Thomas/Optimización/Escena real/2b. Vista previa: seleccionar transparentes (no modifica)")]
    static void VistaPreviaTransparentes() => VistaPrevia(Accion.SoloOccludeeTransparente);

    static void VistaPrevia(Accion filtro)
    {
        var c = Analizar(out int yaListos, out int dinamicos);
        var seleccion = c.Where(x => x.accion == filtro).Select(x => (Object)x.go).ToArray();
        Selection.objects = seleccion;
        Debug.Log($"[Culling] Vista previa ({filtro}): {seleccion.Length} objetos seleccionados en la Jerarquía. " +
                  "No se modificó nada.\n" + Resumen(c, yaListos, dinamicos));
        if (seleccion.Length > 0) SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Tools/Thomas/Optimización/Escena real/2. Preparar estáticos para occlusion")]
    static void PrepararEstaticos()
    {
        var escena = SceneManager_GetActive();
        var cambios = Analizar(out int yaListos, out int dinamicos);

        if (cambios.Count == 0)
        {
            EditorUtility.DisplayDialog("Occlusion",
                $"Nada que cambiar en '{escena.name}'.\n{yaListos} objetos estáticos ya están listos, {dinamicos} son dinámicos.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog($"Preparar occlusion en '{escena.name}'",
                Resumen(cambios, yaListos, dinamicos) + "\n\nSe puede deshacer con Ctrl+Z.",
                "Aplicar", "Cancelar"))
            return;

        Undo.RecordObjects(cambios.Select(c => (Object)c.go).ToArray(), "Preparar estáticos para occlusion");
        foreach (var c in cambios) GameObjectUtility.SetStaticEditorFlags(c.go, c.flags);

        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log($"[Culling] '{escena.name}' preparada.\n{Resumen(cambios, yaListos, dinamicos)}\n" +
                  "Siguiente paso: Window > Rendering > Occlusion Culling > Bake.");
    }

    // ---------------------------------------------------------------- 3. Overlay temporal

    [MenuItem("Tools/Thomas/Optimización/Escena real/3. Agregar overlay de medición (temporal)")]
    static void AgregarOverlay()
    {
        var existente = Object.FindFirstObjectByType<CullingDebugOverlay>(FindObjectsInactive.Include);
        if (existente != null)
        {
            Selection.activeGameObject = existente.gameObject;
            Debug.Log("[Culling] La escena ya tiene un overlay de medición.", existente);
            return;
        }

        // En un objeto aparte (no en la cámara del prefab) para que sea fácil de borrar y no viaje en Player.prefab.
        var go = new GameObject(NombreOverlay);
        Undo.RegisterCreatedObjectUndo(go, "Agregar overlay de medición");
        go.AddComponent<CullingDebugOverlay>();
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Culling] Overlay agregado. F3 muestra/oculta, F4 mide 10 s. Bórralo antes de hacer merge a main.", go);
    }

    [MenuItem("Tools/Thomas/Optimización/Escena real/Quitar overlay de medición")]
    static void QuitarOverlay()
    {
        var overlays = Object.FindObjectsByType<CullingDebugOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (overlays.Length == 0) { Debug.Log("[Culling] No hay overlays en la escena."); return; }

        foreach (var o in overlays)
        {
            // Si el objeto solo existe para el overlay, se borra entero; si no, solo el componente.
            if (o.gameObject.name == NombreOverlay) Undo.DestroyObjectImmediate(o.gameObject);
            else Undo.DestroyObjectImmediate(o);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager_GetActive());
        Debug.Log($"[Culling] {overlays.Length} overlay(s) eliminados.");
    }

    static UnityEngine.SceneManagement.Scene SceneManager_GetActive() =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene();
}
