using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Cajas abiertas del sotano: al bajar se marcan, se revisan una por una y en la ultima esta la linterna.
// - Cada caja (la que se ve, donde este): Interactable ("Revisar caja") + CajaRevisable + Highlightable +
//   un BoxCollider para apuntarle, y un objeto encima del papel que el jugador saca y mira.
//   No se mueve ninguna caja.
// - "Cajas abiertas": RevisionCajas con un trigger del tamano del sotano y la linterna de la escena.
// Se aplica sola una vez (despues de decorar el sotano) y se puede rehacer con Tools > Sotano > Cajas con linterna.
[InitializeOnLoad]
public static class SotanoCajasSetup
{
    const string RutaCajas = "zotano/Decoracion Sotano/Cajas abiertas";
    const string Texto = "Revisar caja";
    const string Brillo = "Assets/Materials/GlowMaterial.mat";
    static readonly string[] Objetos = { "radio", "reloj", "farol" };

    static SotanoCajasSetup()
    {
        // Despues de SotanoSetup (que espera dos ciclos)
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        // Si se recompilo estando en Play (o justo al darle Play), se aplica al volver al editor
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += AutoAplicar;
        };
    }

    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject c = GameObject.Find(RutaCajas);
        if (!c || c.GetComponent<RevisionCajas>()) return;
        Aplicar(c.transform);
    }

    [MenuItem("Tools/Sotano/Cajas con linterna")]
    static void Menu()
    {
        GameObject c = GameObject.Find(RutaCajas);
        if (!c) { EditorUtility.DisplayDialog("Sotano", "No se encontro \"" + RutaCajas + "\".", "OK"); return; }
        Aplicar(c.transform);
    }

    static void Aplicar(Transform contenedor)
    {
        Transform zotano = GameObject.Find("zotano").transform;
        var glow = AssetDatabase.LoadAssetAtPath<Material>(Brillo);
        var cajas = new List<CajaRevisable>();
        var rnd = new System.Random(4321);
        int i = 0;

        foreach (Transform grupo in contenedor)
        {
            // Todo va en la caja que se ve, donde este ahora (no se mueve nada)
            if (!grupo.name.StartsWith("Caja abierta")) continue;
            if (!LimitesCaja(grupo, out Bounds bc)) continue;

            // Objeto encima del papel (si no tiene)
            Transform objeto = null;
            foreach (Transform h in grupo) if (h.name.StartsWith("Objeto en la caja")) objeto = h;
            Transform papel = grupo.Find("Papel protector");
            if (!objeto)
            {
                string clave = Objetos[i % Objetos.Length];
                float y = papel && papel.TryGetComponent(out Renderer rp) ? rp.bounds.max.y - 0.02f : bc.max.y - 0.15f;
                GameObject go = TiendaPropsSetup.CrearProp(clave, grupo, new Vector3(bc.center.x, y, bc.center.z),
                                                           Mathf.Min(bc.size.x, bc.size.z) * 0.5f, (float)rnd.NextDouble() * 360f, false, rnd);
                if (go)
                {
                    go.name = "Objeto en la caja (" + clave + ")";
                    Undo.RegisterCreatedObjectUndo(go, "Objeto en la caja");
                    objeto = go.transform;
                }
            }

            // De donde sale la linterna: dentro de la caja, a la altura del papel
            Transform fondo = grupo.Find("Fondo linterna");
            if (!fondo)
            {
                fondo = new GameObject("Fondo linterna").transform;
                Undo.RegisterCreatedObjectUndo(fondo.gameObject, "Fondo linterna");
                fondo.SetParent(grupo, false);
                fondo.position = new Vector3(bc.center.x, papel ? papel.position.y : bc.center.y, bc.center.z);
            }

            var marca = grupo.GetComponent<Highlightable>();
            if (!marca) marca = Undo.AddComponent<Highlightable>(grupo.gameObject);
            var soM = new SerializedObject(marca);
            soM.FindProperty("outlineMaterial").objectReferenceValue = glow;
            soM.ApplyModifiedProperties();

            // Collider para apuntarle, del tamano de la caja visible
            var col = grupo.GetComponent<BoxCollider>();
            if (!col)
            {
                col = Undo.AddComponent<BoxCollider>(grupo.gameObject);
                Vector3 esc = grupo.lossyScale;
                col.center = grupo.InverseTransformPoint(bc.center);
                col.size = new Vector3(bc.size.x / Mathf.Abs(esc.x), bc.size.y / Mathf.Abs(esc.y), bc.size.z / Mathf.Abs(esc.z));
            }

            var interactable = grupo.GetComponent<Interactable>();
            if (!interactable) interactable = Undo.AddComponent<Interactable>(grupo.gameObject);
            var soI = new SerializedObject(interactable);
            soI.FindProperty("prompt").stringValue = Texto;
            soI.ApplyModifiedProperties();
            var caja = grupo.GetComponent<CajaRevisable>();
            if (!caja)
            {
                caja = Undo.AddComponent<CajaRevisable>(grupo.gameObject);
                UnityEventTools.AddPersistentListener(interactable.onInteract, caja.Revisar);
            }
            Undo.RecordObject(caja, "Caja revisable");
            caja.marca = marca;
            caja.objeto = objeto;
            caja.fondo = fondo;
            cajas.Add(caja);
            i++;
        }

        // Controlador con un trigger que cubre el sotano
        var revision = contenedor.GetComponent<RevisionCajas>();
        if (!revision) revision = Undo.AddComponent<RevisionCajas>(contenedor.gameObject);
        Undo.RecordObject(revision, "Revision cajas");
        revision.cajas = cajas.ToArray();
        foreach (CajaRevisable c in cajas) { c.revision = revision; EditorUtility.SetDirty(c); }

        Flashlight linterna = Object.FindFirstObjectByType<Flashlight>(FindObjectsInactive.Include);
        revision.linterna = linterna ? linterna.GetComponentInParent<Grabbable>(true) : null;
        if (!revision.linterna) Debug.LogWarning("[Sotano] No encuentro la linterna (Grabbable) en la escena.");

        contenedor.gameObject.layer = 2; // Ignore Raycast: el trigger no tapa la interaccion
        var box = contenedor.GetComponent<BoxCollider>();
        if (!box) box = Undo.AddComponent<BoxCollider>(contenedor.gameObject);
        box.isTrigger = true;
        Transform piso = zotano.Find("zona/piso");
        Bounds sotano = piso && piso.TryGetComponent(out Renderer r) ? r.bounds : new Bounds(zotano.position, Vector3.one * 10f);
        sotano.min = new Vector3(sotano.min.x, sotano.max.y, sotano.min.z);
        sotano.max = new Vector3(sotano.max.x, sotano.max.y + 2.5f, sotano.max.z);
        // El BoxCollider esta en local del contenedor (que tiene escala)
        Transform t = contenedor;
        box.center = t.InverseTransformPoint(sotano.center);
        Vector3 s = t.lossyScale;
        box.size = new Vector3(sotano.size.x / Mathf.Abs(s.x), sotano.size.y / Mathf.Abs(s.y), sotano.size.z / Mathf.Abs(s.z));
        EditorUtility.SetDirty(revision);

        EditorSceneManager.MarkSceneDirty(contenedor.gameObject.scene);
        Debug.Log("[Sotano] " + cajas.Count + " cajas: se marcan al bajar y la linterna sale en la ultima que se revise. Guarda la escena.", contenedor);
    }

    // Limites de la caja visible (sin el objeto de encima)
    static bool LimitesCaja(Transform grupo, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in grupo.GetComponentsInChildren<Renderer>())
        {
            bool enObjeto = false;
            for (Transform t = r.transform; t != grupo; t = t.parent) if (t.name.StartsWith("Objeto en la caja")) enObjeto = true;
            if (enObjeto) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return hay;
    }
}
