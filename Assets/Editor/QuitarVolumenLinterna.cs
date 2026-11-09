using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// La linterna tenia un Volume propio ("Volumen Linterna") que al encenderla dejaba neutros los colores de
// sombras y tonos medios y el filtro de color, asi que cambiaba el aspecto de los Volumes de cada zona.
// Esto lo quita de la escena (una vez, solo). Para que la linterna se siga viendo con los Volumes de
// cada zona se le sube la potencia (prefab Linterna y las de la escena).
[InitializeOnLoad]
public static class QuitarVolumenLinterna
{
    const string Nombre = "Volumen Linterna";
    const string PrefabLinterna = "Assets/Prefabs/Linterna.prefab";
    const float Intensidad = 350f, Alcance = 30f;

    static QuitarVolumenLinterna()
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
        Potencia();
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v == null || v.gameObject.name != Nombre) continue;
            var escena = v.gameObject.scene;
            Undo.DestroyObjectImmediate(v.gameObject);
            EditorSceneManager.MarkSceneDirty(escena);
            Debug.Log("[Linterna] Quitado el \"" + Nombre + "\": la linterna ya no cambia los Volumes de las zonas. Guarda la escena.");
        }
    }

    // Linterna mas potente (con 100 casi no se veia sobre el color de las zonas)
    static void Potencia()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabLinterna);
        if (root && Necesita(root))
        {
            GameObject edit = PrefabUtility.LoadPrefabContents(PrefabLinterna);
            try { Subir(edit, false); PrefabUtility.SaveAsPrefabAsset(edit, PrefabLinterna); }
            finally { PrefabUtility.UnloadPrefabContents(edit); }
            Debug.Log("[Linterna] Prefab Linterna: intensidad " + Intensidad + ", alcance " + Alcance + ".");
        }
        foreach (Flashlight f in Object.FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Necesita(f.gameObject))
            {
                Subir(f.gameObject, true);
                EditorSceneManager.MarkSceneDirty(f.gameObject.scene);
                Debug.Log("[Linterna] Linterna de la escena mas potente. Guarda la escena.", f);
            }
    }

    static bool Necesita(GameObject go)
    {
        foreach (Flashlight f in go.GetComponentsInChildren<Flashlight>(true))
            if (f.lightObject && f.lightObject.TryGetComponent(out Light l) && l.intensity < Intensidad - 0.5f) return true;
        return false;
    }

    static void Subir(GameObject go, bool undo)
    {
        foreach (Flashlight f in go.GetComponentsInChildren<Flashlight>(true))
            if (f.lightObject && f.lightObject.TryGetComponent(out Light l) && l.intensity < Intensidad - 0.5f)
            {
                if (undo) Undo.RecordObject(l, "Linterna");
                l.intensity = Intensidad;
                l.range = Mathf.Max(l.range, Alcance);
                EditorUtility.SetDirty(l);
            }
    }
}
