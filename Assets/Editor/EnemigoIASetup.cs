using UnityEditor;
using UnityEngine;

// Inteligencia del enemigo 1 (JefeIndigoAI.inteligente): se activa sola UNA vez en el prefab "Enemy 1 (fede)"
// (el gato y el enemigo 4 quedan como estaban). Cada prefab se marca con versionIA para no volver a tocarlo:
// si despues se cambia a mano en el Inspector, se respeta.
[InitializeOnLoad]
public static class EnemigoIASetup
{
    const int Version = 1;

    static EnemigoIASetup()
    {
        EditorApplication.delayCall += Aplicar;
    }

    static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            JefeIndigoAI ia = prefab ? prefab.GetComponent<JefeIndigoAI>() : null;
            if (ia == null || ia.versionIA >= Version) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                var ai = root.GetComponent<JefeIndigoAI>();
                ai.versionIA = Version;
                ai.inteligente = root.name.ToLowerInvariant().Contains("fede");
                PrefabUtility.SaveAsPrefabAsset(root, ruta);
                if (ai.inteligente) Debug.Log("[Enemigos] " + root.name + ": inteligencia activada (oye los pasos, investiga y busca mejor).", prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
