using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Asigna "sonido presentacion de fede" a la presentacion de Fede en el laberinto (suena cuando aparece en pantalla).
[InitializeOnLoad]
public static class FedeSonidoSetup
{
    const string Ruta = "Assets/Sounds/SFX/sonido presentacion de fede.mp3";

    static FedeSonidoSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Ruta);
                var pres = Object.FindFirstObjectByType<PresentacionEnemigo>(FindObjectsInactive.Include);
                if (!clip || !pres || pres.sonido == clip) return;
                Undo.RecordObject(pres, "Sonido presentacion de Fede");
                pres.sonido = clip;
                EditorUtility.SetDirty(pres);
                EditorSceneManager.MarkSceneDirty(pres.gameObject.scene);
                Debug.Log("[Fede] Sonido de presentacion asignado. Guarda la escena (Ctrl+S).", pres);
            };
    }
}
