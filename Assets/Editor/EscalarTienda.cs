using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Escala la tienda, las escaleras y el zotano a metros (personaje de 1.75 m).
// Toma como referencia la altura de las paredes de la tienda y escala los tres
// grupos juntos alrededor del piso de la tienda, para que sigan conectados.
// Menu: Tools > Laberinto > Escalar Tienda. Si ya estan a escala no hace nada.
[InitializeOnLoad]
public static class EscalarTienda
{
    const float AltoParedTienda = 3f;
    const string FlagPath = "Library/EscalarTienda.flag";
    static readonly string[] Grupos = { "Tienda", "escaleras", "zotano" };

    static EscalarTienda()
    {
        if (System.IO.File.Exists(FlagPath))
        {
            System.IO.File.Delete(FlagPath);
            EditorApplication.delayCall += Escalar;
        }
    }

    [MenuItem("Tools/Laberinto/Escalar Tienda")]
    public static void Escalar()
    {
        var tienda = GameObject.Find("Tienda");
        var paredes = tienda != null ? tienda.transform.Find("paredes") : null;
        if (paredes == null)
        {
            Debug.LogWarning("EscalarTienda: no encontre Tienda/paredes en la escena.");
            return;
        }

        var ref_ = Limites(paredes.gameObject);
        float factor = AltoParedTienda / ref_.size.y;
        if (Mathf.Abs(factor - 1f) < 0.05f)
        {
            Debug.Log("EscalarTienda: la tienda ya esta a escala.");
            return;
        }

        var pivote = new Vector3(ref_.center.x, ref_.min.y, ref_.center.z);
        foreach (var nombre in Grupos)
        {
            var go = GameObject.Find(nombre);
            if (go == null) { Debug.LogWarning("EscalarTienda: falta " + nombre); continue; }
            var t = go.transform;
            Undo.RecordObject(t, "Escalar tienda");
            t.position = pivote + (t.position - pivote) * factor;
            t.localScale *= factor;
        }

        EditorSceneManager.MarkSceneDirty(tienda.scene);
        Selection.activeGameObject = tienda;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log($"EscalarTienda: factor {factor:0.0000}, paredes de la tienda ahora {AltoParedTienda} m.");
    }

    static Bounds Limites(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
