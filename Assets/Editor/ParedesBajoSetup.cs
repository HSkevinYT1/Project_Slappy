using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Las paredes elevadas (rosadas) del laberinto: se les agrega Interactable + PasoBajo para que el jugador
// pueda pasar por debajo al otro lado. Se aplica solo al abrir la escena (si les falta) y desde el menu.
// Para otra pared cualquiera: agregarle Interactable + PasoBajo y en On Interact elegir PasoBajo > Pasar.
public static class ParedesBajoSetup
{
    const string Contenedor = "Laberinto/Paredes Elevadas";
    const string Texto = "Pasar por debajo";

    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject c = GameObject.Find(Contenedor);
        if (!c) return;
        foreach (Transform t in c.transform)
            if (!t.GetComponent<PasoBajo>()) { Aplicar(c.transform); return; }
    }

    [MenuItem("Tools/Laberinto/Paredes elevadas: pasar por debajo")]
    static void Menu()
    {
        GameObject c = GameObject.Find(Contenedor);
        if (!c) { EditorUtility.DisplayDialog("Laberinto", "No se encontro \"" + Contenedor + "\".", "OK"); return; }
        Aplicar(c.transform);
    }

    static void Aplicar(Transform contenedor)
    {
        int n = 0;
        foreach (Transform t in contenedor)
        {
            if (!t.GetComponent<Collider>() || t.GetComponent<PasoBajo>()) continue;
            var interactable = t.GetComponent<Interactable>();
            if (!interactable) interactable = Undo.AddComponent<Interactable>(t.gameObject);
            var so = new SerializedObject(interactable);
            so.FindProperty("prompt").stringValue = Texto;
            so.ApplyModifiedProperties();
            var paso = Undo.AddComponent<PasoBajo>(t.gameObject);
            UnityEventTools.AddPersistentListener(interactable.onInteract, paso.Pasar);
            EditorUtility.SetDirty(interactable);
            n++;
        }
        if (n > 0)
        {
            EditorSceneManager.MarkSceneDirty(contenedor.gameObject.scene);
            Debug.Log("[Laberinto] " + n + " paredes elevadas: ahora se puede pasar por debajo (F). Guarda la escena.", contenedor);
        }
    }
}
