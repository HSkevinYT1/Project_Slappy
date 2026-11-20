using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Pasos en el agua de las Cloacas: "Cloacas/Pasos en el agua" con PasosZona (Sounds/SFX/Pisadas_Agua.mp3),
// que suena mientras el jugador camina dentro de "Trigger Cloacas".
// Se crea solo si no existe; rehacer: Tools > Cloacas > Pasos en el agua. Ctrl+Z deshace. Despues guarda la escena.
[InitializeOnLoad]
public static class CloacasPasosSetup
{
    const string Nombre = "Pasos en el agua";
    const string Clip = "Assets/Sounds/SFX/Pisadas_Agua.mp3";

    static CloacasPasosSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject c = GameObject.Find("Cloacas");
        if (!c || c.transform.Find(Nombre) || !Area() || !AssetDatabase.LoadAssetAtPath<AudioClip>(Clip)) return;
        Aplicar(c.transform);
    }

    [MenuItem("Tools/Cloacas/Pasos en el agua")]
    static void Menu()
    {
        GameObject c = GameObject.Find("Cloacas");
        if (!c) { EditorUtility.DisplayDialog("Cloacas", "No se encontro \"Cloacas\".", "OK"); return; }
        Transform viejo = c.transform.Find(Nombre);
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        Aplicar(c.transform);
    }

    static ZonaVolumen Area()
    {
        foreach (ZonaVolumen z in Object.FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (z.name == "Trigger Cloacas") return z;
        return null;
    }

    static void Aplicar(Transform cloacas)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Clip);
        ZonaVolumen area = Area();
        if (!clip || !area) { Debug.LogWarning("[Cloacas] Falta " + Clip + " o \"Trigger Cloacas\".", cloacas); return; }

        var go = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(go, "Pasos en el agua");
        go.transform.SetParent(cloacas, false);
        var pasos = go.AddComponent<PasosZona>();
        pasos.clip = clip;
        pasos.area = area;

        EditorSceneManager.MarkSceneDirty(cloacas.gameObject.scene);
        Debug.Log("[Cloacas] Pasos en el agua listos (suenan al caminar dentro de \"Trigger Cloacas\"). Guarda la escena (Ctrl+S).", go);
    }
}
