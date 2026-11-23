using UnityEditor;
using UnityEngine;

// IA de los enemigos (JefeIndigoAI) en sus prefabs:
// - v1: inteligencia (oye los pasos, investiga y busca mejor) solo en "Enemy 1 (fede)".
// - v2: inteligencia en TODOS (Fede, el gato y Slappy), y mucha mas diferencia entre caminar y correr:
//   caminando casi no lo oyen (3.5 m), corriendo lo oyen de lejos (16 m; el gato 18) y lo ven desde mas lejos.
// Cada prefab se marca con versionIA para no volver a tocarlo: si despues se cambia a mano en el Inspector, se respeta.
[InitializeOnLoad]
public static class EnemigoIASetup
{
    // v3: para encontrarse mas seguido: acecho (cada 14-26 s va a revisar donde anda el jugador) y, si pasa mucho
    // tiempo lejos, reaparece mas cerca (fuera de la vista y del alcance del reloj)
    const int Version = 3;

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
                string n = root.name.ToLowerInvariant();
                ai.versionIA = Version;
                ai.inteligente = true;
                ai.oidoCaminando = 3.5f;
                ai.oidoCorriendo = n.Contains("gato") ? 18f : 16f;
                ai.oidoTrasPared = 0.55f;
                ai.vistaCorriendo = 1.35f;
                ai.vistaCaminando = 0.85f;
                ai.prisaAlOirCorrer = 1.4f;
                ai.acechar = true;
                ai.acechoCada = n.Contains("gato") ? new Vector2(12f, 22f) : new Vector2(14f, 26f);
                ai.errorAcecho = 7f;
                ai.acercarseSiLejos = true;
                ai.esperaAcercarse = new Vector2(12f, 22f);
                PrefabUtility.SaveAsPrefabAsset(root, ruta);
                Debug.Log("[Enemigos] " + root.name + ": IA v2 (inteligente; caminando lo oye a " + ai.oidoCaminando + " m, corriendo a " + ai.oidoCorriendo + " m).", prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
