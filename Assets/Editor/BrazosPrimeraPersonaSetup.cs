using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Crea la capa "Primera Persona" y pone BrazosPrimeraPersona en la Main Camera del jugador
// (en el prefab Player y en la escena, si el jugador de la escena no es el prefab).
// Se aplica solo si falta; rehacer: Tools > Jugador > Brazos sin atravesar paredes.
[InitializeOnLoad]
public static class BrazosPrimeraPersonaSetup
{
    const string Capa = "Primera Persona";
    const string RutaPlayer = "Assets/Prefabs/Player.prefab";

    static BrazosPrimeraPersonaSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var ml = Object.FindFirstObjectByType<MouseLook>();
            if (LayerMask.NameToLayer(Capa) < 0 || (ml && !ml.GetComponent<BrazosPrimeraPersona>())) Aplicar();
        };
    }

    [MenuItem("Tools/Jugador/Brazos sin atravesar paredes")]
    static void Aplicar()
    {
        int capa = AsegurarCapa();
        if (capa < 0) { Debug.LogWarning("[Brazos] No hay capas libres."); return; }

        // Prefab del jugador
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPlayer);
        if (prefab)
        {
            GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPlayer);
            try
            {
                var ml = raiz.GetComponentInChildren<MouseLook>(true);
                if (ml && !ml.GetComponent<BrazosPrimeraPersona>()) { ml.gameObject.AddComponent<BrazosPrimeraPersona>(); PrefabUtility.SaveAsPrefabAsset(raiz, RutaPlayer); }
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        // Jugador de la escena (por si no viene del prefab o tiene la camara cambiada)
        var mlEscena = Object.FindFirstObjectByType<MouseLook>();
        if (mlEscena && !mlEscena.GetComponent<BrazosPrimeraPersona>())
        {
            Undo.AddComponent<BrazosPrimeraPersona>(mlEscena.gameObject);
            EditorSceneManager.MarkSceneDirty(mlEscena.gameObject.scene);
        }
        Debug.Log($"[Brazos] Capa \"{Capa}\" ({capa}) y camara de brazos en la Main Camera: los brazos y lo que se tiene en la mano ya no atraviesan paredes. Guarda la escena (Ctrl+S).");
    }

    static int AsegurarCapa()
    {
        int ya = LayerMask.NameToLayer(Capa);
        if (ya >= 0) return ya;
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty capas = tm.FindProperty("layers");
        for (int i = 8; i < capas.arraySize; i++)
        {
            SerializedProperty c = capas.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(c.stringValue)) { c.stringValue = Capa; tm.ApplyModifiedProperties(); return i; }
        }
        return -1;
    }
}
