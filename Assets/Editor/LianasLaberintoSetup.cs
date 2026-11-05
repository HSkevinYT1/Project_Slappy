using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu para dejar las lianas listas en un clic:
// Tools > Lianas > Configurar Laberinto
public static class LianasLaberintoSetup
{
    const string RutaMaterial = "Assets/Resources/LianasIndigo.mat";

    [MenuItem("Tools/Lianas/Configurar Laberinto")]
    static void Configurar()
    {
        GameObject laberinto = Selection.activeGameObject;
        if (!laberinto || laberinto.name != "Laberinto")
            laberinto = GameObject.Find("Laberinto");
        if (!laberinto)
        {
            EditorUtility.DisplayDialog("Lianas", "No se encontro un objeto llamado \"Laberinto\" en la escena.", "OK");
            return;
        }

        Material mat = ObtenerMaterial();

        var lianas = laberinto.GetComponent<LianasLaberinto>();
        if (!lianas) lianas = Undo.AddComponent<LianasLaberinto>(laberinto);
        Undo.RecordObject(lianas, "Configurar lianas");
        lianas.AutoConfigurar();
        lianas.materialLianas = mat;

        if (!lianas.zonaSegura)
        {
            var zona = new GameObject("Zona Segura");
            Undo.RegisterCreatedObjectUndo(zona, "Crear zona segura");
            zona.transform.SetParent(laberinto.transform, false);
            zona.transform.localPosition = new Vector3(0f, 1f, 0f);

            var box = zona.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3f, 3f, 3f);

            var zs = zona.AddComponent<ZonaSeguraLianas>();
            zs.lianas = lianas;

            lianas.zonaSegura = zona.transform;
            Selection.activeGameObject = zona;
            EditorGUIUtility.PingObject(zona);
            Debug.Log("[Lianas] Se creo \"Zona Segura\" dentro del Laberinto: muevela a donde debe estar y ajusta el tamano del BoxCollider.", zona);
        }

        EditorUtility.SetDirty(lianas);
        EditorSceneManager.MarkSceneDirty(laberinto.scene);
    }

    static Material ObtenerMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (mat) return mat;

        Shader shader = Shader.Find("Custom/LianasIndigo");
        if (!shader)
        {
            Debug.LogError("[Lianas] No se encontro el shader Custom/LianasIndigo.");
            return null;
        }

        mat = new Material(shader) { name = "LianasIndigo" };
        AssetDatabase.CreateAsset(mat, RutaMaterial);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
