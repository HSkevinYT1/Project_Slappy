using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// El "Pano encima de la Tienda" no mide un multiplo del patron de ventanas de su textura (columnas de 2.4 m dentro
// de un bloque de 12 m), asi que las ventanas del borde derecho salian cortadas. Esto rehace SOLO la cara del frente:
// la parte en columnas enteras (cada una un poco mas ancha o angosta para que quepan justas) usando de la textura
// solo las 4 columnas completas de cada bloque. El resto de la malla (lados, fondo, techo) queda igual.
// Se aplica solo; rehacer: Tools > Tienda > Ventanas del pano encima de la Tienda.
[InitializeOnLoad]
public static class PanoEncimaVentanas
{
    const string NombrePano = "Pano encima de la Tienda";
    const string Sufijo = "_ventanas";
    const float AnchoBloque = 12f, AltoTextura = 24f, Columna = 2.4f, Margen = 0.6f, VDesde = 4f;
    const int ColumnasPorBloque = 4;

    static PanoEncimaVentanas()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject pano = GameObject.Find(NombrePano);
            var mf = pano ? pano.GetComponent<MeshFilter>() : null;
            if (mf && mf.sharedMesh && !mf.sharedMesh.name.EndsWith(Sufijo)) Arreglar();
        };
    }

    [MenuItem("Tools/Tienda/Ventanas del pano encima de la Tienda")]
    static void Arreglar()
    {
        GameObject pano = GameObject.Find(NombrePano);
        var mf = pano ? pano.GetComponent<MeshFilter>() : null;
        if (!mf || !mf.sharedMesh) { Debug.LogWarning("[Pano] No se encontro \"" + NombrePano + "\"."); return; }
        Mesh orig = mf.sharedMesh;
        if (orig.name.EndsWith(Sufijo))
        {
            string rutaVieja = AssetDatabase.GetAssetPath(orig);
            string original = rutaVieja.Replace(Sufijo + ".asset", ".asset");
            orig = AssetDatabase.LoadAssetAtPath<Mesh>(original) ?? orig;
        }
        Bounds b = orig.bounds;
        float ancho = b.size.x, alto = b.size.y;

        var v = new List<Vector3>(orig.vertices);
        var n = new List<Vector3>(orig.normals);
        var uv = new List<Vector2>(); orig.GetUVs(0, uv);
        var subs = new List<int>[orig.subMeshCount];
        for (int i = 0; i < subs.Length; i++) subs[i] = new List<int>(orig.GetTriangles(i));

        // Frente nuevo: n columnas enteras, en tramos de hasta 4 columnas (las completas de cada bloque de 12 m)
        int columnas = Mathf.Max(1, Mathf.RoundToInt(ancho / Columna));
        float celda = ancho / columnas;
        subs[0].Clear();
        float v0 = VDesde / AltoTextura, v1 = (alto + VDesde) / AltoTextura;
        for (int c = 0; c < columnas; c += ColumnasPorBloque)
        {
            int k = Mathf.Min(ColumnasPorBloque, columnas - c);
            float xa = b.min.x + c * celda, xb = xa + k * celda;
            float ua = Margen / AnchoBloque, ub = (Margen + k * Columna) / AnchoBloque;
            int i0 = v.Count;
            v.Add(new Vector3(xa, b.min.y, b.min.z)); uv.Add(new Vector2(ua, v0));
            v.Add(new Vector3(xb, b.min.y, b.min.z)); uv.Add(new Vector2(ub, v0));
            v.Add(new Vector3(xb, b.max.y, b.min.z)); uv.Add(new Vector2(ub, v1));
            v.Add(new Vector3(xa, b.max.y, b.min.z)); uv.Add(new Vector2(ua, v1));
            for (int j = 0; j < 4; j++) n.Add(Vector3.back);
            subs[0].AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
        }

        var m = new Mesh { name = orig.name + Sufijo };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
        m.subMeshCount = subs.Length;
        for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
        m.RecalculateTangents(); m.RecalculateBounds();

        string ruta = AssetDatabase.GetAssetPath(orig).Replace(".asset", Sufijo + ".asset");
        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(orig))) ruta = "Assets/Tienda/Calle/Mallas/" + m.name + ".asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(m, ruta);

        Undo.RecordObject(mf, "Ventanas del pano");
        mf.sharedMesh = m;
        EditorSceneManager.MarkSceneDirty(pano.scene);
        Debug.Log($"[Pano] Ventanas del pano encima de la Tienda: {columnas} columnas enteras de {celda:0.00} m. Guarda la escena (Ctrl+S).", pano);
    }
}
