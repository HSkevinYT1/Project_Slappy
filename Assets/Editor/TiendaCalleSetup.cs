using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Ventanas de la Tienda (paredes/Cube (20) y Cube (21)) con el shader Slappy/CalleFalsa:
// a traves del vidrio se ve una calle de noche con paralaje (sin geometria real).
// Ambas ventanas usan el MISMO material, asi la calle sigue de una a la otra.
// Se aplica sola si las ventanas no tienen el material; rehacer: Tools > Tienda > Calle en las ventanas.
[InitializeOnLoad]
public static class TiendaCalleSetup
{
    static readonly string[] Ventanas = { "Tienda/paredes/Cube (20)", "Tienda/paredes/Cube (21)" };
    const string RutaMaterial = "Assets/Tienda/Materiales/CalleFalsa.mat";
    const string SueloAfuera = "Tienda/rt (7)"; // piso que hay del otro lado de la pared (se ve por la puerta)

    static TiendaCalleSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (string v in Ventanas)
        {
            GameObject g = GameObject.Find(v);
            if (g && g.TryGetComponent(out Renderer r) && (!r.sharedMaterial || r.sharedMaterial.name != "CalleFalsa")) { Aplicar(); return; }
        }
    }

    [MenuItem("Tools/Tienda/Calle en las ventanas")]
    static void Aplicar()
    {
        Shader shader = Shader.Find("Slappy/CalleFalsa");
        if (!shader) { Debug.LogWarning("[Tienda] No se encontro el shader Slappy/CalleFalsa."); return; }

        Renderer primera = null;
        foreach (string v in Ventanas)
        {
            GameObject g = GameObject.Find(v);
            if (g && g.TryGetComponent(out Renderer r)) { primera = r; break; }
        }
        if (!primera) return;

        // El vidrio es el lado mas delgado del cubo; "afuera" es hacia el piso de afuera.
        Bounds b = primera.bounds;
        Vector3 afuera = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
        float sueloCalle = b.min.y - 0.5f;
        GameObject suelo = GameObject.Find(SueloAfuera);
        if (suelo && suelo.TryGetComponent(out Renderer rs))
        {
            if (Vector3.Dot(rs.bounds.center - b.center, afuera) < 0f) afuera = -afuera;
            sueloCalle = rs.bounds.max.y;
        }
        else
        {
            var jugador = Object.FindFirstObjectByType<PlayerMovement>();
            if (jugador && Vector3.Dot(b.center - jugador.transform.position, afuera) < 0f) afuera = -afuera;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        bool nuevo = !mat;
        if (nuevo) mat = new Material(shader) { name = "CalleFalsa" };
        else mat.shader = shader;
        float alturaAcera = mat.GetFloat("_AlturaAcera");
        // Origen: sobre el plano del vidrio, a nivel de la calzada (la acera queda a la altura del piso de afuera)
        mat.SetVector("_Origen", new Vector4(b.center.x, sueloCalle - alturaAcera, b.center.z, 0f));
        mat.SetVector("_Afuera", afuera);
        if (nuevo) AssetDatabase.CreateAsset(mat, RutaMaterial);
        else EditorUtility.SetDirty(mat);

        foreach (string v in Ventanas)
        {
            GameObject g = GameObject.Find(v);
            if (!g || !g.TryGetComponent(out Renderer r)) continue;
            Undo.RecordObject(r, "Calle en las ventanas");
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
        EditorSceneManager.MarkSceneDirty(primera.gameObject.scene);
        Debug.Log($"[Tienda] Ventanas con calle falsa (afuera = {afuera}, calle a y = {sueloCalle - alturaAcera:0.00}). Guarda la escena (Ctrl+S).");
    }
}
