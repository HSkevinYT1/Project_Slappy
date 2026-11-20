using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Pasos normales del jugador: objeto "Pasos normales" (en la raiz de la escena) con PasosZona sin area
// (Sounds/SFX/Pasos.mp3). Suenan en todos lados menos donde hay pasos propios (ej: el agua de las Cloacas).
// Se crea solo en escenas con jugador (PlayerMovement) si no existe; rehacer: Tools > Jugador > Pasos normales.
// Ctrl+Z deshace. Despues guarda la escena.
[InitializeOnLoad]
public static class PasosNormalesSetup
{
    const string Nombre = "Pasos normales";
    const string Clip = "Assets/Sounds/SFX/Pasos.mp3";

    static PasosNormalesSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Existente()) return;
        if (!Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include) || !AssetDatabase.LoadAssetAtPath<AudioClip>(Clip)) return;
        Aplicar();
    }

    [MenuItem("Tools/Jugador/Pasos normales")]
    static void Menu()
    {
        GameObject viejo = Existente();
        if (viejo) Undo.DestroyObjectImmediate(viejo);
        Aplicar();
    }

    static GameObject Existente()
    {
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == Nombre) return g;
        return null;
    }

    static void Aplicar()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Clip);
        if (!clip) { Debug.LogWarning("[Pasos] No encuentro " + Clip); return; }

        var go = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(go, "Pasos normales");
        var pasos = go.AddComponent<PasosZona>();
        pasos.clip = clip;
        pasos.area = null;
        pasos.volumen = 0.5f;

        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Pasos] Pasos normales listos (en todos lados menos en el agua de las Cloacas). Guarda la escena (Ctrl+S).", go);
    }
}
