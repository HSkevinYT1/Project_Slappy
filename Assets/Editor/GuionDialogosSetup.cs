using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Dialogos > Guion de ayudas: pone en la escena el objeto "Guion Dialogos" (GuionDialogos) para editar
// en el Inspector lo que dice el protagonista. Sin el objeto en la escena igual funciona (se crea solo al jugar
// con los textos por defecto). Tools > Dialogos > Nuevo trigger de dialogo: una zona que dice algo al entrar.
public static class GuionDialogosSetup
{
    [MenuItem("Tools/Dialogos/Guion de ayudas (editar textos)")]
    static void Guion()
    {
        GuionDialogos g = Object.FindFirstObjectByType<GuionDialogos>(FindObjectsInactive.Include);
        if (g == null)
        {
            var go = new GameObject("Guion Dialogos");
            Undo.RegisterCreatedObjectUndo(go, "Guion Dialogos");
            g = go.AddComponent<GuionDialogos>();
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[Dialogos] \"Guion Dialogos\" creado: edita los textos en el Inspector y guarda la escena (Ctrl+S).", go);
        }
        Selection.activeGameObject = g.gameObject;
        EditorGUIUtility.PingObject(g.gameObject);
    }

    [MenuItem("Tools/Dialogos/Nuevo trigger de dialogo")]
    static void Trigger()
    {
        var go = new GameObject("Trigger Dialogo");
        Undo.RegisterCreatedObjectUndo(go, "Trigger Dialogo");
        go.AddComponent<BoxCollider>();
        go.AddComponent<DialogoTrigger>();
        // Delante de la vista de la escena
        if (SceneView.lastActiveSceneView != null)
        {
            Transform cam = SceneView.lastActiveSceneView.camera.transform;
            Vector3 p = cam.position + cam.forward * 6f;
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 60f)) p = hit.point;
            go.transform.position = p;
        }
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
