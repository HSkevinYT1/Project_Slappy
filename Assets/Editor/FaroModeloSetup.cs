using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Exterior del faro de la Zona Roja (por encima de los muros de bloques de ZonaRojaSetup, que siguen siendo los que
// chocan y se ven por dentro):
// - Torre lisa, un poco mas ancha abajo, con franjas blancas y rojas; zocalo de piedra y ventanas.
// - Balcon con barandal alrededor de la sala de la luz (con hueco donde llega el pasillo).
// - Barrotes de metal en el vidrio y una cupula lisa con remate y pararrayos (se esconden los cilindros viejos).
// - Las puertas: la de abajo queda de 3.4 m (con marco) y la de arriba a la altura del pasillo (los huecos eran de
//   10 m de alto); lo de encima se rellena con muro que choca.
// Todo en la capa del faro (el foco no lo ilumina por dentro). Se aplica solo si falta; rehacer:
// Tools > Zona Roja > Exterior del faro (rehacer).
[InitializeOnLoad]
public static class FaroModeloSetup
{
    const string Nombre = "Exterior (modelo)";
    const string Mallas = "Assets/ZonaRoja/Mallas/Exterior";
    const string Mats = "Assets/ZonaRoja/Materiales/";
    const float AltoPiso = 10f, RadioFaro = 9f, Grosor = 0.6f, AnchoPuerta = 13f;
    const float AltoPuertaAbajo = 3.4f;

    static FaroModeloSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Transform faro = Faro();
                if (faro && !faro.Find(Nombre)) Aplicar(faro);
            };
    }

    [MenuItem("Tools/Zona Roja/Exterior del faro (rehacer)")]
    static void Menu()
    {
        Transform faro = Faro();
        if (!faro) { EditorUtility.DisplayDialog("Faro", "No encontre \"Zona Roja/Faro\".", "OK"); return; }
        Transform viejo = faro.Find(Nombre);
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        Aplicar(faro);
    }

    static Transform Faro()
    {
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == "Zona Roja") return g.transform.Find("Faro");
        return null;
    }

    static int capa;
    static uint capaRender;
    static Transform raiz;

    static void Aplicar(Transform faro)
    {
        Directory.CreateDirectory(Mallas);
        Renderer muestra = faro.GetComponentInChildren<Renderer>();
        capa = muestra ? muestra.gameObject.layer : 0;
        capaRender = muestra ? muestra.renderingLayerMask : 1u;
        Material blanco = Cargar("FaroBlanco"), rojo = Cargar("FaroRojo"), metal = Cargar("Metal"), roca = Cargar("Roca"), negro = Cargar("Negro");

        raiz = new GameObject(Nombre).transform;
        Undo.RegisterCreatedObjectUndo(raiz.gameObject, Nombre);
        raiz.SetParent(faro, false);
        raiz.gameObject.layer = capa;

        float y1 = -2f * AltoPiso, y3 = 0f;
        float rAbajo = RadioFaro + 0.9f, rArriba = RadioFaro + 0.12f;
        float R(float y) => Mathf.Lerp(rAbajo, rArriba, Mathf.InverseLerp(y1, y3, y));

        // ---- Torre: 4 franjas (blanco, rojo, blanco, rojo) de 5 m, con el hueco de la puerta de abajo
        Material[] franjas = { blanco, rojo, blanco, rojo };
        for (int i = 0; i < 4; i++)
        {
            float a = y1 + i * 5f, b = a + 5f;
            if (i == 0)
            {
                Pieza("Franja 1 (puerta)", Tubo(R(a), R(a + AltoPuertaAbajo), a, a + AltoPuertaAbajo, 180f + AnchoPuerta, 180f - AnchoPuerta + 360f, 64), franjas[0]);
                Pieza("Franja 1", Tubo(R(a + AltoPuertaAbajo), R(b), a + AltoPuertaAbajo, b, 0f, 360f, 64), franjas[0]);
            }
            else Pieza("Franja " + (i + 1), Tubo(R(a), R(b), a, b, 0f, 360f, 64), franjas[i]);
        }
        // Zocalo de piedra y una moldura donde empieza la sala de la luz
        Pieza("Zocalo", Cilindro(rAbajo + 0.6f, y1 - 0.4f, y1 + 1.1f, 48, 180f + AnchoPuerta + 2f, 180f - AnchoPuerta - 2f + 360f), roca);
        Pieza("Moldura", Cilindro(rArriba + 0.25f, y3 - 0.6f, y3, 64, 0f, 360f), blanco);

        // Ventanas (oscuras, con marco) en los dos pisos
        foreach (float y in new[] { y1 + 6.5f, y1 + 15.5f })
            foreach (float ang in new[] { 60f, 300f, 120f, 240f })
                Ventana(ang, y, R(y), negro, blanco);

        // ---- Puertas: marco de la de abajo, relleno encima (choca), y relleno encima del pasillo arriba
        Marco(180f, y1, R(y1 + 1f), blanco, rojo);
        RellenoHueco(faro, 180f, y1 + AltoPuertaAbajo, y1 + AltoPiso, blanco);
        float altoPasillo = AltoPasillo(faro);
        RellenoHueco(faro, 0f, y3 + altoPasillo, y3 + AltoPiso * 0.9f, blanco);

        // ---- Balcon de la sala de la luz (hueco de 32 grados donde llega el pasillo)
        float hueco = 16f;
        Pieza("Balcon", Anillo(RadioFaro - 0.05f, RadioFaro + 2.3f, y3 - 0.25f, y3, hueco, 360f - hueco, 72), metal);
        for (float ang = hueco + 4f; ang <= 360f - hueco - 4f; ang += 10f)
        {
            Caja("Poste", ang, RadioFaro + 2.15f, y3 + 0.55f, new Vector3(0.07f, 1.1f, 0.07f), metal);
            Caja("Mensula", ang, RadioFaro + 1.2f, y3 - 0.7f, new Vector3(0.18f, 0.6f, 1.9f), metal);
        }
        Pieza("Pasamanos", Tubo(RadioFaro + 2.12f, RadioFaro + 2.12f, y3 + 1.05f, y3 + 1.12f, hueco + 4f, 360f - hueco - 4f, 72, true), metal);
        Pieza("Barra media", Tubo(RadioFaro + 2.13f, RadioFaro + 2.13f, y3 + 0.55f, y3 + 0.59f, hueco + 4f, 360f - hueco - 4f, 72, true), metal);

        // ---- Barrotes del vidrio
        for (float ang = 7.5f; ang < 360f; ang += 15f)
        {
            if (ang < AnchoPuerta + 1f || ang > 360f - AnchoPuerta - 1f) continue;
            Caja("Barrote", ang, RadioFaro - 0.15f, y3 + 5.1f, new Vector3(0.08f, 7.8f, 0.12f), metal);
        }

        // ---- Cupula lisa (se esconden los cilindros escalonados viejos)
        Transform techo = faro.Find("Piso 3 - Linterna/Techo");
        if (techo)
            foreach (Transform t in techo)
                if (t.name.StartsWith("Cupula") || t.name == "Remate")
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) { Undo.RecordObject(r, "Cupula"); r.enabled = false; }
        Pieza("Cupula", Cupula(RadioFaro + 0.3f, y3 + AltoPiso + 0.3f, 4.6f, 48, 14), rojo);
        Pieza("Remate", Cupula(0.9f, y3 + AltoPiso + 4.7f, 1.1f, 16, 6), metal);
        Caja("Pararrayos", 0f, 0f, y3 + AltoPiso + 6.8f, new Vector3(0.06f, 2.6f, 0.06f), metal);

        EditorSceneManager.MarkSceneDirty(faro.gameObject.scene);
        Debug.Log("[Zona Roja] Exterior del faro: torre con franjas, zocalo, ventanas, balcon con barandal, barrotes, cupula y puertas con marco. Guarda la escena (Ctrl+S).", raiz);
    }

    // Altura libre del pasillo donde entra al faro (piso 3): rayo hacia arriba en la punta del pasillo
    static float AltoPasillo(Transform faro)
    {
        Vector3 p = faro.TransformPoint(new Vector3(RadioFaro + 1.5f, 1f, 0f));
        if (Physics.Raycast(p, Vector3.up, out RaycastHit h, 8f, ~0, QueryTriggerInteraction.Ignore)) return Mathf.Clamp(h.point.y - faro.position.y + 0.05f, 2.6f, 6f);
        return 4f;
    }

    // ------------------------------------------------------------------ Piezas

    static Material Cargar(string n) => AssetDatabase.LoadAssetAtPath<Material>(Mats + n + ".mat");

    static GameObject Pieza(string nombre, Mesh malla, Material mat)
    {
        string ruta = Mallas + "/" + nombre.Replace(" ", "_").Replace("(", "").Replace(")", "") + ".asset";
        var vieja = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (vieja) AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(malla, ruta);
        var go = new GameObject(nombre);
        go.layer = capa;
        go.transform.SetParent(raiz, false);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.renderingLayerMask = capaRender;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    // Caja orientada hacia afuera, en un angulo (grados, 0 = +X) y radio
    static GameObject Caja(string nombre, float ang, float radio, float y, Vector3 tam, Material mat, bool choca = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.layer = capa;
        go.transform.SetParent(raiz, false);
        float rad = ang * Mathf.Deg2Rad;
        go.transform.localPosition = new Vector3(Mathf.Cos(rad) * radio, y, Mathf.Sin(rad) * radio);
        go.transform.localRotation = Quaternion.Euler(0f, 90f - ang, 0f);
        go.transform.localScale = tam;
        if (!choca) Object.DestroyImmediate(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (mat) r.sharedMaterial = mat;
        r.renderingLayerMask = capaRender;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static void Ventana(float ang, float y, float radio, Material vidrio, Material marco)
    {
        Caja("Ventana", ang, radio + 0.02f, y, new Vector3(1.1f, 1.7f, 0.05f), vidrio);
        Caja("Marco ventana", ang, radio + 0.06f, y + 0.92f, new Vector3(1.35f, 0.14f, 0.12f), marco);
        Caja("Marco ventana", ang, radio + 0.06f, y - 0.92f, new Vector3(1.35f, 0.18f, 0.2f), marco);
    }

    static void Marco(float ang, float y, float radio, Material blanco, Material rojo)
    {
        float ancho = 2f * RadioFaro * Mathf.Sin(AnchoPuerta * Mathf.Deg2Rad);
        float rad = ang * Mathf.Deg2Rad;
        Vector3 lado = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        foreach (float s in new[] { -1f, 1f })
        {
            var j = Caja("Jamba", ang, radio + 0.1f, y + AltoPuertaAbajo * 0.5f, new Vector3(0.35f, AltoPuertaAbajo, 0.5f), blanco);
            j.transform.localPosition += lado * s * (ancho * 0.5f + 0.1f);
        }
        Caja("Dintel", ang, radio + 0.12f, y + AltoPuertaAbajo + 0.2f, new Vector3(ancho + 1f, 0.4f, 0.55f), rojo);
        Caja("Escalon", ang, radio + 0.5f, y + 0.1f, new Vector3(ancho + 0.6f, 0.2f, 1.2f), blanco);
    }

    // Tapa el hueco del muro (los huecos de las puertas eran de piso a techo) entre dos alturas, con colision
    static void RellenoHueco(Transform faro, float ang, float desde, float hasta, Material mat)
    {
        if (hasta - desde < 0.2f) return;
        float rMed = RadioFaro - Grosor * 0.5f;
        for (float a = ang - AnchoPuerta + 2.2f; a <= ang + AnchoPuerta - 2.2f + 0.01f; a += 4.4f)
            Caja("Relleno puerta", a, rMed, (desde + hasta) * 0.5f, new Vector3(2f * Mathf.PI * RadioFaro * 4.6f / 360f, hasta - desde, Grosor), mat, true);
    }

    // ------------------------------------------------------------------ Mallas

    // Superficie de un tronco de cono (hacia afuera), entre dos angulos; doble = con cara por dentro tambien
    static Mesh Tubo(float r0, float r1, float y0, float y1, float a0, float a1, int segs, bool doble = false)
    {
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        int n = Mathf.Max(2, Mathf.CeilToInt(segs * (a1 - a0) / 360f));
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vs.Add(d * r0 + Vector3.up * y0); vs.Add(d * r1 + Vector3.up * y1);
            float u = (a1 - a0) * Mathf.Deg2Rad * r0 / 4f * i / n;
            uv.Add(new Vector2(u, y0 / 4f)); uv.Add(new Vector2(u, y1 / 4f));
        }
        for (int i = 0; i < n; i++)
        {
            int b = i * 2;
            tr.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
            if (doble) tr.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
        }
        return Malla(vs, uv, tr);
    }

    // Cilindro macizo (lados y tapa de arriba)
    static Mesh Cilindro(float r, float y0, float y1, int segs, float a0, float a1)
    {
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        int n = Mathf.Max(2, Mathf.CeilToInt(segs * (a1 - a0) / 360f));
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vs.Add(d * r + Vector3.up * y0); vs.Add(d * r + Vector3.up * y1); vs.Add(d * r + Vector3.up * y1); vs.Add(d * (r - 1.2f) + Vector3.up * y1);
            float u = i / (float)n * 12f;
            uv.Add(new Vector2(u, 0f)); uv.Add(new Vector2(u, 0.4f)); uv.Add(new Vector2(u, 0.4f)); uv.Add(new Vector2(u, 0.7f));
        }
        for (int i = 0; i < n; i++)
        {
            int b = i * 4, c = b + 4;
            tr.AddRange(new[] { b, b + 1, c, c, b + 1, c + 1 });
            tr.AddRange(new[] { b + 2, b + 3, c + 2, c + 2, b + 3, c + 3 });
        }
        return Malla(vs, uv, tr);
    }

    // Anillo plano con grosor (tapa de arriba, de abajo y borde exterior)
    static Mesh Anillo(float rIn, float rOut, float y0, float y1, float a0, float a1, int segs)
    {
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        int n = Mathf.Max(2, Mathf.CeilToInt(segs * (a1 - a0) / 360f));
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vs.Add(d * rIn + Vector3.up * y1); vs.Add(d * rOut + Vector3.up * y1);
            vs.Add(d * rIn + Vector3.up * y0); vs.Add(d * rOut + Vector3.up * y0);
            vs.Add(d * rOut + Vector3.up * y1); vs.Add(d * rOut + Vector3.up * y0);
            float u = i / (float)n * 20f;
            for (int k = 0; k < 6; k++) uv.Add(new Vector2(u, k * 0.2f));
        }
        for (int i = 0; i < n; i++)
        {
            int b = i * 6, c = b + 6;
            tr.AddRange(new[] { b, c, b + 1, b + 1, c, c + 1 });             // arriba
            tr.AddRange(new[] { b + 2, b + 3, c + 2, c + 2, b + 3, c + 3 }); // abajo
            tr.AddRange(new[] { b + 5, b + 4, c + 5, c + 5, b + 4, c + 4 }); // borde
        }
        return Malla(vs, uv, tr);
    }

    // Media esfera aplastada (alto) sobre y0
    static Mesh Cupula(float r, float y0, float alto, int segs, int anillos)
    {
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        for (int j = 0; j <= anillos; j++)
        {
            float t = j / (float)anillos * Mathf.PI * 0.5f;
            float rr = Mathf.Cos(t) * r, yy = y0 + Mathf.Sin(t) * alto;
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                vs.Add(new Vector3(Mathf.Cos(a) * rr, yy, Mathf.Sin(a) * rr));
                uv.Add(new Vector2(i / (float)segs * 8f, j / (float)anillos * 2f));
            }
        }
        for (int j = 0; j < anillos; j++)
            for (int i = 0; i < segs; i++)
            {
                int a = j * (segs + 1) + i, b = a + segs + 1;
                tr.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        return Malla(vs, uv, tr);
    }

    static Mesh Malla(List<Vector3> vs, List<Vector2> uv, List<int> tr)
    {
        var m = new Mesh { name = "FaroExterior" };
        m.SetVertices(vs);
        m.SetUVs(0, uv);
        m.SetTriangles(tr, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
