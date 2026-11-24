using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// La llave de las Cloacas ("Llave Cloacas", con LlaveAleatoria) era un cubo amarillo: se cambia por una llave antigua
// (anillo, vastago y paleton) hecha por codigo y guardada en "Models/Llave/Llave Cloacas.asset".
// Es el mismo objeto (no se pierde nada: agarrarla, su sonido, la cerradura que la espera); solo cambia la malla,
// la escala (1) y el collider. El pivote es el centro del anillo: en la mano de la muñeca cuelga hacia abajo, y en
// la cerradura ("Punto Llave") entra con el vastago hacia adentro.
// Se aplica sola si la llave sigue siendo un cubo; rehacer: Tools > Cloacas > Modelo de la llave. Despues guarda la escena (Ctrl+S).
[InitializeOnLoad]
public static class LlaveModeloSetup
{
    const string Carpeta = "Assets/Models/Llave";
    const string RutaMalla = Carpeta + "/Llave Cloacas.asset";
    const string MaterialLlave = "Assets/Materials/Llave Cloacas.mat";

    static LlaveModeloSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        LlaveAleatoria llave = Object.FindFirstObjectByType<LlaveAleatoria>(FindObjectsInactive.Include);
        // El oro sin brillo (con Smoothness se reflejaba la linterna)
        var oro = AssetDatabase.LoadAssetAtPath<Material>(MaterialLlave);
        if (oro && oro.HasProperty("_Smoothness") && oro.GetFloat("_Smoothness") > 0f) { MunecaModeloSetup.SinBrillo(oro); AssetDatabase.SaveAssets(); }
        if (!llave || !llave.TryGetComponent(out MeshFilter mf)) return;
        var malla = AssetDatabase.LoadAssetAtPath<Mesh>(RutaMalla);
        if (malla && mf.sharedMesh == malla) return;
        Aplicar(llave);
    }

    [MenuItem("Tools/Cloacas/Modelo de la llave")]
    static void Menu()
    {
        LlaveAleatoria llave = Object.FindFirstObjectByType<LlaveAleatoria>(FindObjectsInactive.Include);
        if (!llave) { EditorUtility.DisplayDialog("Cloacas", "No encontre la llave de las Cloacas (LlaveAleatoria).", "OK"); return; }
        Aplicar(llave);
    }

    static void Aplicar(LlaveAleatoria llave)
    {
        Mesh malla = Guardar(CrearMalla());
        GameObject go = llave.gameObject;

        var mf = go.GetComponent<MeshFilter>();
        if (!mf) mf = Undo.AddComponent<MeshFilter>(go);
        var mr = go.GetComponent<MeshRenderer>();
        if (!mr) mr = Undo.AddComponent<MeshRenderer>(go);
        Undo.RecordObject(mf, "Llave");
        Undo.RecordObject(mr, "Llave");
        Undo.RecordObject(go.transform, "Llave");
        mf.sharedMesh = malla;
        var oro = AssetDatabase.LoadAssetAtPath<Material>(MaterialLlave);
        if (oro) { MunecaModeloSetup.SinBrillo(oro); mr.sharedMaterial = oro; }
        go.transform.localScale = Vector3.one;

        // Collider a la medida (un poco mas grueso para que sea facil apuntarle y no atraviese el piso al caer)
        var col = go.GetComponent<BoxCollider>();
        if (!col)
        {
            foreach (Collider c in go.GetComponents<Collider>()) Undo.DestroyObjectImmediate(c);
            col = Undo.AddComponent<BoxCollider>(go);
        }
        Undo.RecordObject(col, "Llave");
        Bounds b = malla.bounds;
        col.center = b.center;
        col.size = new Vector3(Mathf.Max(b.size.x, 0.05f), b.size.y, Mathf.Max(b.size.z, 0.04f));

        // La cerradura: la llave entra con el vastago hacia adentro y el anillo de canto, mirando al jugador
        SalidaConLlave salida = Object.FindFirstObjectByType<SalidaConLlave>(FindObjectsInactive.Include);
        Transform punto = salida && salida.cerradura ? salida.cerradura.puntoMano : null;
        if (punto)
        {
            Undo.RecordObject(punto, "Llave");
            punto.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 90f, 0f);
        }

        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Cloacas] La llave ya no es un cubo: modelo de llave antigua (" + RutaMalla + "). Guarda la escena (Ctrl+S).", go);
    }

    static Mesh Guardar(Mesh m)
    {
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models", "Llave");
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(RutaMalla);
        if (!existente) { AssetDatabase.CreateAsset(m, RutaMalla); return m; }
        existente.Clear();
        EditorUtility.CopySerialized(m, existente);
        existente.name = m.name;
        EditorUtility.SetDirty(existente);
        AssetDatabase.SaveAssets();
        Object.DestroyImmediate(m);
        return existente;
    }

    // ------------------------------------------------------------------ Malla (metros)
    // Origen en el centro del anillo; el vastago baja por -Y y el paleton sale hacia +X. El anillo mira hacia Z.

    static readonly List<Vector3> V = new List<Vector3>();
    static readonly List<Vector3> N = new List<Vector3>();
    static readonly List<Vector2> U = new List<Vector2>();
    static readonly List<int> T = new List<int>();

    static Mesh CrearMalla()
    {
        V.Clear(); N.Clear(); U.Clear(); T.Clear();
        const float R = 0.036f;          // radio del anillo
        const float r = 0.009f;          // grosor del anillo
        const float largo = 0.215f;      // del centro del anillo a la punta
        const float vastago = 0.0075f;

        Toro(Vector3.zero, R, r, 28, 12);
        // Adorno: una barra cruzando el anillo
        Caja(new Vector3(0f, 0f, 0f), new Vector3(R * 1.6f, 0.007f, 0.007f));
        // Cuello entre el anillo y el vastago
        Cilindro(new Vector3(0f, -R - 0.004f, 0f), new Vector3(0f, -R - 0.016f, 0f), 0.012f, 16);
        Cilindro(new Vector3(0f, -R - 0.016f, 0f), new Vector3(0f, -largo, 0f), vastago, 14);
        Cilindro(new Vector3(0f, -R - 0.03f, 0f), new Vector3(0f, -R - 0.036f, 0f), 0.0105f, 16);
        // Paleton (dientes)
        float pal = -largo + 0.004f;
        Caja(new Vector3(0.019f, pal + 0.021f, 0f), new Vector3(0.03f, 0.042f, 0.008f));
        Caja(new Vector3(0.038f, pal + 0.008f, 0f), new Vector3(0.012f, 0.016f, 0.008f));
        Caja(new Vector3(0.038f, pal + 0.034f, 0f), new Vector3(0.012f, 0.012f, 0.008f));
        // Punta redondeada
        Cilindro(new Vector3(0f, -largo, 0f), new Vector3(0f, -largo - 0.004f, 0f), vastago * 0.8f, 14);

        var m = new Mesh { name = "Llave Cloacas" };
        m.SetVertices(V);
        m.SetNormals(N);
        m.SetUVs(0, U);
        m.SetTriangles(T, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        return m;
    }

    static void Toro(Vector3 c, float R, float r, int seg, int tubo)
    {
        int b = V.Count;
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= tubo; j++)
            {
                float t = j * Mathf.PI * 2f / tubo;
                Vector3 n = dir * Mathf.Cos(t) + Vector3.forward * Mathf.Sin(t);
                V.Add(c + dir * R + n * r);
                N.Add(n);
                U.Add(new Vector2((float)i / seg, (float)j / tubo));
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < tubo; j++)
            {
                int a0 = b + i * (tubo + 1) + j, a1 = a0 + tubo + 1;
                T.Add(a0); T.Add(a1); T.Add(a0 + 1);
                T.Add(a1); T.Add(a1 + 1); T.Add(a0 + 1);
            }
    }

    // Cilindro de "desde" a "hasta" con tapas
    static void Cilindro(Vector3 desde, Vector3 hasta, float radio, int seg)
    {
        Vector3 eje = (hasta - desde).normalized;
        Vector3 a = Vector3.Cross(eje, Mathf.Abs(eje.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 bb = Vector3.Cross(eje, a);
        int b = V.Count;
        for (int i = 0; i <= seg; i++)
        {
            float ang = i * Mathf.PI * 2f / seg;
            Vector3 n = a * Mathf.Cos(ang) + bb * Mathf.Sin(ang);
            V.Add(desde + n * radio); N.Add(n); U.Add(new Vector2((float)i / seg, 0f));
            V.Add(hasta + n * radio); N.Add(n); U.Add(new Vector2((float)i / seg, 1f));
        }
        for (int i = 0; i < seg; i++)
        {
            int p = b + i * 2;
            T.Add(p); T.Add(p + 2); T.Add(p + 1);
            T.Add(p + 1); T.Add(p + 2); T.Add(p + 3);
        }
        Tapa(desde, -eje, a, bb, radio, seg);
        Tapa(hasta, eje, a, bb, radio, seg);
    }

    static void Tapa(Vector3 c, Vector3 n, Vector3 a, Vector3 bb, float radio, int seg)
    {
        int b = V.Count;
        V.Add(c); N.Add(n); U.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= seg; i++)
        {
            float ang = i * Mathf.PI * 2f / seg;
            V.Add(c + (a * Mathf.Cos(ang) + bb * Mathf.Sin(ang)) * radio); N.Add(n);
            U.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
        }
        bool haciaEje = Vector3.Dot(Vector3.Cross(a, bb), n) > 0f;
        for (int i = 0; i < seg; i++)
        {
            if (haciaEje) { T.Add(b); T.Add(b + 1 + i); T.Add(b + 2 + i); }
            else { T.Add(b); T.Add(b + 2 + i); T.Add(b + 1 + i); }
        }
    }

    static void Caja(Vector3 c, Vector3 tam)
    {
        Vector3 h = tam * 0.5f;
        Vector3[] normales = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 n in normales)
        {
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(n, u);
            int b = V.Count;
            Vector3 cc = c + Vector3.Scale(n, h);
            Vector3 du = Vector3.Scale(u, h), dw = Vector3.Scale(w, h);
            V.Add(cc - du - dw); V.Add(cc + du - dw); V.Add(cc + du + dw); V.Add(cc - du + dw);
            for (int k = 0; k < 4; k++) N.Add(n);
            U.Add(new Vector2(0, 0)); U.Add(new Vector2(1, 0)); U.Add(new Vector2(1, 1)); U.Add(new Vector2(0, 1));
            // Orientado hacia afuera (en Unity la cara de (a,b,c) mira hacia Cross(b-a, c-a))
            if (Vector3.Dot(Vector3.Cross(du, dw), n) > 0f) { T.Add(b); T.Add(b + 1); T.Add(b + 2); T.Add(b); T.Add(b + 2); T.Add(b + 3); }
            else { T.Add(b); T.Add(b + 2); T.Add(b + 1); T.Add(b); T.Add(b + 3); T.Add(b + 2); }
        }
    }
}
