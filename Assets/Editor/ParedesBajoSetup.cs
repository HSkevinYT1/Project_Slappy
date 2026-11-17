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
    // Hijo con un collider que va desde el piso hasta la pared: asi se puede apuntar tambien al hueco de abajo
    const string Hitbox = "Hitbox Paso Bajo";

    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject c = GameObject.Find(Contenedor);
        if (!c) return;
        foreach (Transform t in c.transform)
            if (!t.GetComponent<PasoBajo>() || !t.Find(Hitbox)) { Aplicar(c.transform); return; }
    }

    [MenuItem("Tools/Laberinto/Paredes elevadas: pasar por debajo")]
    static void Menu()
    {
        GameObject c = GameObject.Find(Contenedor);
        if (!c) { EditorUtility.DisplayDialog("Laberinto", "No se encontro \"" + Contenedor + "\".", "OK"); return; }
        Aplicar(c.transform);
    }

    // Collider desde el piso hasta la base de la pared, del mismo ancho y grosor (hijo de la pared:
    // el Interactable de la pared lo encuentra con GetComponentInParent)
    static bool CrearHitbox(Transform pared)
    {
        Collider c = pared.GetComponent<Collider>();
        Bounds b = c.bounds;
        Physics.SyncTransforms();
        float piso = b.min.y - 1.5f;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(b.center.x, b.min.y - 0.01f, b.center.z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(pared)) { piso = Mathf.Max(piso, hit.point.y); }
        float alto = b.min.y - piso;
        if (alto < 0.05f) return false;

        var go = new GameObject(Hitbox);
        Undo.RegisterCreatedObjectUndo(go, "Hitbox paso bajo");
        go.layer = pared.gameObject.layer;
        go.transform.SetParent(pared, false);
        // En los ejes de la pared: mismo centro horizontal y largo/grosor del BoxCollider, debajo de el
        var box = go.AddComponent<BoxCollider>();
        Vector3 ls = pared.lossyScale;
        Vector3 centroMundo = new Vector3(b.center.x, piso + alto * 0.5f, b.center.z);
        Vector3 local = pared.InverseTransformPoint(centroMundo);
        if (c is BoxCollider pb)
        {
            box.center = new Vector3(pb.center.x, local.y, pb.center.z);
            Vector3 eUp = pared.InverseTransformVector(Vector3.up * alto);
            box.size = new Vector3(pb.size.x, Mathf.Abs(eUp.y), pb.size.z);
        }
        else
        {
            box.center = local;
            box.size = new Vector3(b.size.x / Mathf.Abs(ls.x), alto / Mathf.Abs(ls.y), b.size.z / Mathf.Abs(ls.z));
        }
        return true;
    }

    static void Aplicar(Transform contenedor)
    {
        int n = 0;
        int h = 0;
        foreach (Transform t in contenedor)
            if (t.GetComponent<Collider>() && !t.Find(Hitbox) && CrearHitbox(t)) h++;
        if (h > 0)
        {
            EditorSceneManager.MarkSceneDirty(contenedor.gameObject.scene);
            Debug.Log("[Laberinto] " + h + " paredes elevadas: la zona para interactuar llega hasta el piso.", contenedor);
        }

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
