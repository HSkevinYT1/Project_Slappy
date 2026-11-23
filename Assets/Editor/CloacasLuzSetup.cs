using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Las Cloacas nuevas (ver AparicionMunecas y MunecaLuz):
// - Se quitan las muñecas que estaban puestas: ahora aparecen cada cierto tiempo en "Aparicion Munecas/Puntos"
//   (donde estaban las de antes y otros puntos repartidos dentro de las paredes).
// - La muñeca de practica, quieta en medio del camino a unos metros de la reja, con una pared invisible detras.
// - La llave: la trae una de las muñecas que aparecen.
// - Pinturas rupestres en algunas paredes (no muy seguidas): la luz deshace a las muñecas.
// Se aplica sola si falta; rehacer: Tools > Cloacas > Muñecas y luz (rehacer). Despues guarda la escena (Ctrl+S).
[InitializeOnLoad]
public static class CloacasLuzSetup
{
    const string Nombre = "Aparicion Munecas";
    const string PrefabMuneca = "Assets/Prefabs/Enemigo 3.prefab";
    const string Carpeta = "Assets/Cloacas/Pinturas";
    const int Pinturas = 7;
    const float SeparacionPinturas = 18f;

    static CloacasLuzSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (Raiz("Cloacas", out Transform cl) && !cl.GetComponentInChildren<AparicionMunecas>(true)) Aplicar(cl);
            };
    }

    [MenuItem("Tools/Cloacas/Muñecas y luz (rehacer)")]
    static void Menu()
    {
        if (!Raiz("Cloacas", out Transform cl)) { EditorUtility.DisplayDialog("Cloacas", "No encontre \"Cloacas\".", "OK"); return; }
        foreach (AparicionMunecas a in cl.GetComponentsInChildren<AparicionMunecas>(true)) Undo.DestroyObjectImmediate(a.gameObject);
        foreach (string n in new[] { "Muñeca de practica", "Barrera Muñeca", "Pinturas Rupestres" })
        {
            Transform t = cl.Find(n);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }
        Aplicar(cl);
    }

    static void Aplicar(Transform cl)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabMuneca);
        if (!prefab) { Debug.LogWarning("[Cloacas] No encuentro " + PrefabMuneca); return; }
        SalidaConLlave salida = cl.GetComponentInChildren<SalidaConLlave>(true);
        Transform grupo = EnemigosEnSuNivelSetup.Grupo("Cloacas");
        if (!grupo) grupo = EnemigosEnSuNivelSetup.GrupoEnNivel("Cloacas");
        ZonaVolumen area = null;
        foreach (ZonaVolumen z in Object.FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (z.name == "Trigger Cloacas") area = z;
        bool Dentro(Vector3 p) => (area == null || area.Contiene(p + Vector3.up)) && ZonaCerrada.Dentro(cl, p);
        Physics.SyncTransforms();

        // La reja de la entrada: su eje azul apunta hacia afuera de la zona
        Transform reja = salida != null && salida.reja != null ? salida.reja.transform : cl.Find("Entrada Pasillo");
        Vector3 entrada = reja ? reja.position : cl.position;
        Vector3 adentro = reja ? -reja.forward : Vector3.forward;
        adentro.y = 0f;
        adentro.Normalize();

        // ---- Las muñecas puestas: se quitan (sus sitios quedan como puntos de aparicion)
        var puntos = new List<Vector3>();
        var quitar = new List<GameObject>();
        foreach (Transform t in grupo)
            if (t.GetComponent<EstatuaAI>()) { puntos.Add(t.position - Vector3.up * (t.TryGetComponent(out NavMeshAgent a) ? a.baseOffset : 0f)); quitar.Add(t.gameObject); }
        foreach (GameObject g in quitar) Undo.DestroyObjectImmediate(g);

        // Mas puntos repartidos dentro de las paredes, lejos de la entrada
        var nav = CentrosNavMesh();
        var rnd = new System.Random(3);
        for (int intento = 0; intento < 4000 && puntos.Count < 14 && nav.Count > 0; intento++)
        {
            Vector3 p = nav[rnd.Next(nav.Count)];
            if (Plano(p - entrada) < 16f || !Dentro(p)) continue;
            bool cerca = false;
            foreach (Vector3 q in puntos) if (Plano(p - q) < 9f) { cerca = true; break; }
            if (!cerca) puntos.Add(p);
        }

        var ap = new GameObject(Nombre).AddComponent<AparicionMunecas>();
        Undo.RegisterCreatedObjectUndo(ap.gameObject, Nombre);
        ap.transform.SetParent(grupo, false);
        ap.prefab = prefab;
        ap.salida = salida;
        Transform contPuntos = new GameObject("Puntos").transform;
        contPuntos.SetParent(ap.transform, false);
        var lista = new List<Transform>();
        for (int i = 0; i < puntos.Count; i++)
        {
            var t = new GameObject("Punto " + (i + 1)).transform;
            t.SetParent(contPuntos, false);
            t.position = puntos[i];
            lista.Add(t);
        }
        ap.puntos = lista.ToArray();
        ap.llave = grupo.GetComponentInChildren<LlaveAleatoria>(true);
        if (ap.llave == null) ap.llave = cl.GetComponentInChildren<LlaveAleatoria>(true);
        if (ap.llave != null) { Undo.RecordObject(ap.llave, "Llave"); ap.llave.manos = new Transform[0]; }

        // ---- La muñeca de practica: en medio del camino, mirando hacia la reja
        Vector3 sitio = entrada + adentro * 10f;
        if (NavMesh.SamplePosition(sitio, out NavMeshHit h, 4f, NavMesh.AllAreas)) sitio = h.position;
        var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cl);
        m.name = "Muñeca de practica";
        float alto = m.TryGetComponent(out NavMeshAgent ag) ? ag.baseOffset : 0f;
        m.transform.SetPositionAndRotation(sitio + Vector3.up * alto, Quaternion.LookRotation(-adentro, Vector3.up));
        if (m.TryGetComponent(out EstatuaAI ai)) ai.enabled = false;
        if (ag) ag.enabled = false;
        var luz = m.AddComponent<MunecaLuz>();
        ap.practica = luz;
        Undo.RegisterCreatedObjectUndo(m, "Muñeca de practica");

        // Pared invisible de lado a lado del tunel, un poco detras de la muñeca
        Vector3 lado = Vector3.Cross(Vector3.up, adentro).normalized;
        Vector3 o = sitio + Vector3.up * 1.2f + adentro * 0.8f;
        float izq = Physics.Raycast(o, -lado, out RaycastHit hi, 12f, ~0, QueryTriggerInteraction.Ignore) ? hi.distance : 6f;
        float der = Physics.Raycast(o, lado, out RaycastHit hd, 12f, ~0, QueryTriggerInteraction.Ignore) ? hd.distance : 6f;
        var barrera = new GameObject("Barrera Muñeca");
        Undo.RegisterCreatedObjectUndo(barrera, "Barrera");
        barrera.transform.SetParent(cl, true);
        barrera.transform.SetPositionAndRotation(o + lado * ((der - izq) * 0.5f) + Vector3.up * 0.8f, Quaternion.LookRotation(adentro, Vector3.up));
        var caja = barrera.AddComponent<BoxCollider>();
        caja.size = new Vector3(izq + der + 0.4f, 5f, 0.4f);
        barrera.layer = 2;   // no tapa el rayo de la linterna ni el de interaccion
        ap.barrera = caja;

        // ---- Pinturas rupestres
        int pintadas = PintarParedes(cl, nav, Dentro, sitio, entrada);

        EditorSceneManager.MarkSceneDirty(cl.gameObject.scene);
        Debug.Log("[Cloacas] Listo: " + quitar.Count + " muñecas puestas quitadas, " + puntos.Count + " puntos de aparicion, la muñeca de practica " +
                  "(con su pared invisible), la llave en una de las que aparecen y " + pintadas + " pinturas rupestres. Guarda la escena (Ctrl+S).", ap);
    }

    // ------------------------------------------------------------------ Pinturas

    static int PintarParedes(Transform cl, List<Vector3> nav, System.Func<Vector3, bool> dentro, Vector3 practica, Vector3 entrada)
    {
        // Paredes posibles: desde puntos caminables, una pared lisa a menos de 3 m
        var paredes = new List<(Vector3 p, Vector3 n)>();
        var rnd = new System.Random(11);
        for (int i = 0; i < nav.Count; i += 3)
        {
            Vector3 c = nav[i];
            if (!dentro(c)) continue;
            foreach (Vector3 d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Vector3 o = c + Vector3.up * 1.6f;
                if (!Physics.Raycast(o, d, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(h.normal.y) > 0.25f || !h.collider.transform.IsChildOf(cl)) continue;
                Vector3 lado = Vector3.Cross(Vector3.up, h.normal).normalized;
                if (!Physics.Raycast(o + lado * 0.8f, d, 3.6f, ~0, QueryTriggerInteraction.Ignore) || !Physics.Raycast(o - lado * 0.8f, d, 3.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                paredes.Add((h.point, new Vector3(h.normal.x, 0f, h.normal.z).normalized));
            }
        }
        if (paredes.Count == 0) { Debug.LogWarning("[Cloacas] No encontre paredes para las pinturas."); return 0; }

        // La primera, cerca de la muñeca de practica (se ve al entrar); las demas, separadas
        var elegidas = new List<(Vector3 p, Vector3 n)>();
        paredes.Sort((a, b) => Vector3.Distance(a.p, practica).CompareTo(Vector3.Distance(b.p, practica)));
        foreach (var w in paredes) if (Vector3.Distance(w.p, practica) > 2f && Vector3.Distance(w.p, entrada) > 4f) { elegidas.Add(w); break; }
        for (int k = 0; k < 400 && elegidas.Count < Pinturas; k++)
        {
            var w = paredes[rnd.Next(paredes.Count)];
            bool cerca = false;
            foreach (var e in elegidas) if (Vector3.Distance(e.p, w.p) < SeparacionPinturas) { cerca = true; break; }
            if (!cerca) elegidas.Add(w);
        }

        Directory.CreateDirectory(Carpeta);
        var mats = new[] { Material("Pintura Linterna", Dibujo(0)), Material("Pintura Sol", Dibujo(1)), Material("Pintura Huida", Dibujo(2)) };
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var grupo = new GameObject("Pinturas Rupestres");
        Undo.RegisterCreatedObjectUndo(grupo, "Pinturas");
        grupo.transform.SetParent(cl, false);
        for (int i = 0; i < elegidas.Count; i++)
        {
            var go = new GameObject("Pintura " + (i + 1));
            go.transform.SetParent(grupo.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mats[i % mats.Length];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetPositionAndRotation(elegidas[i].p + elegidas[i].n * 0.012f, Quaternion.LookRotation(-elegidas[i].n, Vector3.up));
            go.transform.Rotate(0f, 0f, (float)(rnd.NextDouble() * 6.0 - 3.0), Space.Self);
            float tam = 1.3f + (float)rnd.NextDouble() * 0.4f;
            go.transform.localScale = new Vector3(tam, tam * 0.5f, 1f);
        }
        return elegidas.Count;
    }

    static Material Material(string nombre, Texture2D tex)
    {
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.35f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = 2450;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---- Dibujos (trazos de pintura roja/ocre, irregulares)

    const int W = 512, H = 256;
    static Color32[] px;
    static System.Random ruido;

    static Texture2D Dibujo(int tipo)
    {
        string ruta = Carpeta + "/Pintura" + tipo + ".png";
        px = new Color32[W * H];
        ruido = new System.Random(20 + tipo);
        switch (tipo)
        {
            case 0:   // Alguien con una luz: los rayos alcanzan a una muñeca, que se rompe
                Persona(110, 40, 160);
                Disco(165, 135, 11);
                for (int i = -2; i <= 2; i++) Linea(180, 135, 300, 135 + i * 22, 3);
                Muneca(370, 40, 150, true);
                break;
            case 1:   // Un sol (la luz) y abajo muñecas tiradas, deshechas
                Anillo(256, 190, 30, 4);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    Linea(256 + Mathf.Cos(a) * 42, 190 + Mathf.Sin(a) * 42, 256 + Mathf.Cos(a) * 62, 190 + Mathf.Sin(a) * 62, 3);
                }
                Muneca(130, 20, 110, true);
                Muneca(256, 15, 100, true);
                Muneca(385, 20, 110, true);
                break;
            default:  // Las muñecas huyen de alguien con una luz; la que se queda en la luz, tachada
                Persona(70, 40, 150);
                Disco(120, 128, 10);
                Linea(132, 128, 230, 165, 3); Linea(132, 128, 230, 90, 3);
                Muneca(200, 40, 115, false);
                Linea(170, 60, 230, 140, 4); Linea(230, 60, 170, 140, 4);
                Muneca(340, 40, 115, false);
                Muneca(440, 40, 115, false);
                Linea(375, 110, 405, 110, 3); Linea(395, 120, 410, 110, 3); Linea(395, 100, 410, 110, 3);
                break;
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta);
        if (AssetImporter.GetAtPath(ruta) is TextureImporter ti) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    static void Punto(int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        // Pintura gastada: con huecos y tono que varia
        if (ruido.NextDouble() < 0.18) return;
        float t = (float)ruido.NextDouble();
        px[y * W + x] = new Color32((byte)(130 + t * 40), (byte)(40 + t * 25), (byte)(22 + t * 12), (byte)(200 + t * 55));
    }

    static void Brocha(float x, float y, float r)
    {
        int ri = Mathf.CeilToInt(r);
        for (int dx = -ri; dx <= ri; dx++)
            for (int dy = -ri; dy <= ri; dy++)
                if (dx * dx + dy * dy <= r * r) Punto(Mathf.RoundToInt(x) + dx, Mathf.RoundToInt(y) + dy);
    }

    static void Linea(float x0, float y0, float x1, float y1, float g)
    {
        int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)));
        for (int i = 0; i <= n; i++)
        {
            float k = n == 0 ? 0f : i / (float)n;
            // Trazo a mano: grosor y posicion que tiemblan un poco
            float tiembla = Mathf.Sin(k * 9f + x0) * 0.8f;
            Brocha(Mathf.Lerp(x0, x1, k) + tiembla, Mathf.Lerp(y0, y1, k) - tiembla, g * (0.8f + 0.4f * Mathf.PerlinNoise(k * 6f, x0 * 0.1f)));
        }
    }

    static void Anillo(float cx, float cy, float r, float g)
    {
        for (int i = 0; i < 64; i++)
        {
            float a0 = i / 64f * Mathf.PI * 2f, a1 = (i + 1) / 64f * Mathf.PI * 2f;
            Linea(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r, g);
        }
    }

    static void Disco(float cx, float cy, float r) { for (float rr = r; rr > 0f; rr -= 2f) Anillo(cx, cy, rr, 2f); }

    // Persona de palitos (pies en y), con un brazo estirado hacia adelante (sostiene la luz)
    static void Persona(float x, float y, float s)
    {
        Anillo(x, y + s * 0.86f, s * 0.09f, 3);
        Linea(x, y + s * 0.76f, x, y + s * 0.36f, 4);
        Linea(x, y + s * 0.36f, x - s * 0.16f, y, 4);
        Linea(x, y + s * 0.36f, x + s * 0.14f, y, 4);
        Linea(x, y + s * 0.66f, x + s * 0.32f, y + s * 0.6f, 3);
        Linea(x, y + s * 0.66f, x - s * 0.2f, y + s * 0.42f, 3);
    }

    // Muñeca: cabeza grande, vestido triangular. Rota = las partes separadas, con grietas
    static void Muneca(float x, float y, float s, bool rota)
    {
        float sep = rota ? s * 0.08f : 0f;
        Disco(x + sep, y + s * 0.78f + sep, s * 0.11f);
        float cy = y + s * 0.64f, by = y + s * 0.08f;
        Linea(x - sep, cy, x - s * 0.22f - sep, by, 3);
        Linea(x + sep, cy, x + s * 0.22f + sep, by, 3);
        Linea(x - s * 0.22f - sep, by, x + s * 0.22f + sep, by, 3);
        Linea(x - s * 0.08f, y + s * 0.52f, x - s * 0.24f - sep * 2f, y + s * 0.4f, 3);
        Linea(x + s * 0.08f, y + s * 0.52f, x + s * 0.24f + sep * 2f, y + s * 0.4f, 3);
        Linea(x - s * 0.08f, by, x - s * 0.08f, y, 3);
        Linea(x + s * 0.08f, by, x + s * 0.08f, y, 3);
        if (!rota) return;
        // Grietas y pedacitos que saltan
        Linea(x - s * 0.05f, y + s * 0.6f, x + s * 0.04f, y + s * 0.4f, 2);
        Linea(x + s * 0.04f, y + s * 0.4f, x - s * 0.03f, y + s * 0.25f, 2);
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI + 0.3f;
            Brocha(x + Mathf.Cos(a) * s * 0.42f, y + s * 0.45f + Mathf.Sin(a) * s * 0.35f, 3f);
        }
    }

    // ------------------------------------------------------------------ Utilidades

    static List<Vector3> CentrosNavMesh()
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        var res = new List<Vector3>();
        for (int i = 0; i + 2 < tri.indices.Length; i += 3)
            res.Add((tri.vertices[tri.indices[i]] + tri.vertices[tri.indices[i + 1]] + tri.vertices[tri.indices[i + 2]]) / 3f);
        return res;
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    static bool Raiz(string nombre, out Transform t)
    {
        t = null;
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == nombre) { t = g.transform; return true; }
        return false;
    }
}
