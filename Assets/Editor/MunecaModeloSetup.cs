using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// El modelo de la muñeca de las Cloacas (prefab "Enemigo 3"): cambia la capsula por el modelo "Animations/alfin.fbx"
// (la malla "muñeca"):
// - La malla se separa en dos (en "Modelos De enemigos/Muneca"): el cuerpo y el pelo. El pelo va en su propio hijo
//   "Pelo" con PeloAleatorio, asi cada muñeca que aparece tiene un peinado y un color de pelo distintos.
// - Mide ALTO de alto, con los pies en el piso del NavMesh y la cara hacia adelante (donde mira EstatuaAI).
//   El collider y el NavMeshAgent se ajustan al modelo. "Mano" (donde va la llave) pasa a la punta de su mano.
// - PasosMuneca: pisadas en el agua (3D) mientras se mueve.
// Se aplica sola una vez; rehacer: Tools > Cloacas > Modelo de la muñeca (rehacer).
[InitializeOnLoad]
public static class MunecaModeloSetup
{
    const string Fbx = "Assets/Animations/alfin.fbx";
    const string Prefab = "Assets/Prefabs/Enemigo 3.prefab";
    const string Carpeta = "Assets/Modelos De enemigos/Muneca";
    const string ClipPasos = "Assets/Sounds/SFX/Pisadas_Agua.mp3";
    const string MaterialPelo = "initialShadingGroup.003";   // el pelo castaño del FBX
    const float Alto = 1.5f;
    // v2: materiales propios (copias de los del FBX) con Smoothness 0: la linterna se reflejaba en la muñeca
    // v3: colores apagados y sin reflejos: casi blanca (0.9) brillaba como un sol con la linterna (bloom de las Cloacas)
    const string Marca = "_muneca v3";

    // Colores (sRGB) por material del FBX. Oscuros a proposito: la linterna es muy fuerte y el bloom de las Cloacas
    // empieza en 0.77, asi que nada de la muñeca debe pasar de ~0.45
    static readonly Dictionary<string, Color> Colores = new Dictionary<string, Color>
    {
        { "initialShadingGroup.004", new Color(0.42f, 0.37f, 0.32f) },   // porcelana vieja (cara, cuerpo)
        { "initialShadingGroup.014", new Color(0.03f, 0.03f, 0.03f) },   // ojos
        { "initialShadingGroup.009", new Color(0.30f, 0.17f, 0.17f) },   // vestido (rosa viejo, sucio)
        { "initialShadingGroup.005", new Color(0.30f, 0.17f, 0.17f) },   // falda
        { "initialShadingGroup.010", new Color(0.27f, 0.16f, 0.16f) },   // mangas
        { "initialShadingGroup.013", new Color(0.27f, 0.16f, 0.16f) },
        { "initialShadingGroup.011", new Color(0.14f, 0.11f, 0.10f) },   // piernas / zapatos
        { "initialShadingGroup.012", new Color(0.14f, 0.11f, 0.10f) },
    };
    const float BrilloMaximo = 0.45f;

    static MunecaModeloSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (!p || !AssetDatabase.LoadAssetAtPath<GameObject>(Fbx)) return;
        Transform m = p.transform.Find("Modelo");
        if (m && m.Find(Marca)) return;
        Aplicar();
    }

    [MenuItem("Tools/Cloacas/Modelo de la muñeca (rehacer)")]
    static void Menu() => Aplicar();

    static void Aplicar()
    {
        if (AssetImporter.GetAtPath(Fbx) is ModelImporter imp && (imp.importCameras || imp.importLights || !imp.isReadable))
        {
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = true;
            imp.SaveAndReimport();
        }
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (!fbx) { Debug.LogWarning("[Muñeca] No encuentro " + Fbx); return; }
        MeshFilter mfFbx = fbx.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer mrFbx = mfFbx ? mfFbx.GetComponent<MeshRenderer>() : null;
        if (!mfFbx || !mfFbx.sharedMesh || !mrFbx) { Debug.LogWarning("[Muñeca] " + Fbx + " no tiene malla."); return; }

        Mesh fuente = mfFbx.sharedMesh;
        Material[] mats = mrFbx.sharedMaterials;
        int pelo = IndicePelo(fuente, mats);
        int ojos = IndiceOjos(fuente, pelo);
        var restoIdx = Enumerable.Range(0, fuente.subMeshCount).Where(i => i != pelo).ToList();

        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Modelos De enemigos", "Muneca");
        // Los del FBX no se pueden editar: copias en la carpeta, sin brillo
        mats = mats.Select((m, i) => CopiaSinBrillo(m, i == pelo ? "Muneca Pelo" : "Muneca " + i)).ToArray();
        Mesh cuerpo = Guardar(Sub(fuente, restoIdx, "Muneca Cuerpo"), Carpeta + "/Muneca Cuerpo.asset");
        Mesh malllaPelo = Guardar(Sub(fuente, new List<int> { pelo }, "Muneca Pelo"), Carpeta + "/Muneca Pelo.asset");
        Vector3 ojosLocal = Centro(fuente, ojos);

        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            Transform viejo = raiz.transform.Find("Modelo");
            if (viejo) Object.DestroyImmediate(viejo.gameObject);
            // La capsula de antes
            if (raiz.TryGetComponent(out MeshRenderer capsula)) Object.DestroyImmediate(capsula);
            if (raiz.TryGetComponent(out MeshFilter capsulaMf)) Object.DestroyImmediate(capsulaMf);

            var agente = raiz.GetComponent<NavMeshAgent>();
            float pies = agente ? -agente.baseOffset : -1f;

            var cont = new GameObject("Modelo").transform;
            cont.SetParent(raiz.transform, false);
            var inst = Object.Instantiate(fbx, cont);
            inst.name = "Muneca";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            foreach (Animator a in inst.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (Camera c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (Light l in inst.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);

            MeshFilter mf = inst.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            mf.sharedMesh = cuerpo;
            mr.sharedMaterials = restoIdx.Select(i => i < mats.Length ? mats[i] : mats[0]).ToArray();

            var goPelo = new GameObject("Pelo", typeof(MeshFilter), typeof(MeshRenderer));
            goPelo.transform.SetParent(mf.transform, false);
            goPelo.GetComponent<MeshFilter>().sharedMesh = malllaPelo;
            goPelo.GetComponent<MeshRenderer>().sharedMaterial = pelo < mats.Length ? mats[pelo] : mats[0];

            // La cara hacia adelante (+Z de la muñeca: hacia donde la gira EstatuaAI)
            Vector3 frente = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(ojosLocal)) -
                             raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(cuerpo.bounds.center));
            frente.y = 0f;
            if (frente.sqrMagnitude > 1e-8f)
                cont.localRotation = Quaternion.Euler(0f, Vector3.SignedAngle(frente, Vector3.forward, Vector3.up), 0f);

            // Tamaño y pies en el piso
            var filtros = new[] { mf, goPelo.GetComponent<MeshFilter>() };
            Bounds b = Limites(raiz.transform, filtros);
            if (b.size.y > 1e-5f) cont.localScale = Vector3.one * (Alto / b.size.y);
            b = Limites(raiz.transform, filtros);
            cont.localPosition = new Vector3(-b.center.x, pies - b.min.y, -b.center.z);
            b = Limites(raiz.transform, filtros);

            // Collider y agente a la medida
            if (raiz.TryGetComponent(out CapsuleCollider col))
            {
                col.direction = 1;
                col.height = b.size.y;
                col.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.6f, 0.2f, 0.5f);
                col.center = new Vector3(0f, b.center.y, 0f);
            }
            if (agente) agente.height = b.size.y;

            // "Mano" (donde va la llave): en la punta del brazo derecho
            Transform mano = raiz.transform.Find("Mano");
            if (mano && PuntaMano(raiz.transform, mf, out Vector3 punta))
                mano.localPosition = Vector3.Lerp(punta, new Vector3(0f, punta.y, 0f), 0.08f) + Vector3.forward * 0.05f;

            // Pelo distinto en cada una
            var pa = goPelo.AddComponent<PeloAleatorio>();
            pa.arriba = goPelo.transform.InverseTransformDirection(raiz.transform.up).normalized;
            pa.frente = goPelo.transform.InverseTransformDirection(raiz.transform.forward).normalized;

            // Pisadas en el agua
            var pasos = raiz.GetComponent<PasosMuneca>();
            if (!pasos) pasos = raiz.AddComponent<PasosMuneca>();
            pasos.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPasos);
            if (!pasos.clip) Debug.LogWarning("[Muñeca] No encuentro " + ClipPasos + " (las pisadas no van a sonar).");

            new GameObject(Marca).transform.SetParent(cont, false);
            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Muñeca] Enemigo 3: modelo nuevo ({b.size.y:0.00} m), pelo aleatorio (submalla {pelo}: {(pelo < mats.Length ? mats[pelo].name : "?")}) y pisadas en el agua.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // ------------------------------------------------------------------ Materiales

    static Material CopiaSinBrillo(Material origen, string nombre)
    {
        if (!origen) return null;
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m)
        {
            m = new Material(origen) { name = nombre };
            AssetDatabase.CreateAsset(m, ruta);
        }
        else
        {
            m.shader = origen.shader;
            m.CopyPropertiesFromMaterial(origen);
        }
        SinBrillo(m);
        SinReflejos(m);

        // Color apagado (el del FBX era casi blanco). El pelo lo pone PeloAleatorio, aqui solo se oscurece
        if (m.HasProperty("_BaseColor"))
        {
            Color c = Colores.TryGetValue(origen.name, out Color fijo) ? fijo : m.GetColor("_BaseColor");
            Color.RGBToHSV(c, out float h, out float s, out float v);
            c = Color.HSVToRGB(h, s, Mathf.Min(v, BrilloMaximo));
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // Sin brillos especulares ni reflejos del entorno (URP Lit): nada de destellos con la linterna
    static void SinReflejos(Material m)
    {
        if (m.HasProperty("_SpecularHighlights")) { m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
        if (m.HasProperty("_EnvironmentReflections")) { m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
    }

    // Smoothness 0 (la luz de la linterna no se refleja). Tambien lo usa LlaveModeloSetup
    internal static void SinBrillo(Material m)
    {
        if (!m) return;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
        if (m.HasProperty("_GlossMapScale")) m.SetFloat("_GlossMapScale", 0f);
        EditorUtility.SetDirty(m);
    }

    // ------------------------------------------------------------------ Malla

    static int IndicePelo(Mesh m, Material[] mats)
    {
        for (int i = 0; i < mats.Length && i < m.subMeshCount; i++)
            if (mats[i] && mats[i].name == MaterialPelo) return i;
        // Si cambia el nombre: la parte que llega mas arriba (con bastantes triangulos)
        int mejor = 0; float alto = float.MinValue;
        for (int i = 0; i < m.subMeshCount; i++)
        {
            var d = m.GetSubMesh(i);
            if (d.indexCount / 3 < 50) continue;
            if (d.bounds.max.y > alto) { alto = d.bounds.max.y; mejor = i; }
        }
        return mejor;
    }

    // Los ojos: la parte mas chica (sirve para saber donde esta la cara)
    static int IndiceOjos(Mesh m, int pelo)
    {
        int mejor = -1, menos = int.MaxValue;
        for (int i = 0; i < m.subMeshCount; i++)
        {
            if (i == pelo) continue;
            int n = m.GetSubMesh(i).indexCount;
            if (n < menos) { menos = n; mejor = i; }
        }
        return mejor;
    }

    static Vector3 Centro(Mesh m, int sub)
    {
        if (sub < 0) return m.bounds.center + Vector3.forward;
        var v = m.vertices;
        var tris = m.GetTriangles(sub);
        if (tris.Length == 0) return m.bounds.center;
        Vector3 s = Vector3.zero;
        foreach (int t in tris) s += v[t];
        return s / tris.Length;
    }

    // Una malla nueva solo con esas submallas (los vertices que usan)
    static Mesh Sub(Mesh src, List<int> subs, string nombre)
    {
        Vector3[] v = src.vertices, n = src.normals;
        Vector4[] tg = src.tangents;
        Vector2[] uv = src.uv, uv2 = src.uv2;
        Color[] col = src.colors;
        var mapa = new Dictionary<int, int>();
        var orden = new List<int>();
        var tris = new List<int[]>();
        foreach (int s in subs)
        {
            int[] t = src.GetTriangles(s);
            for (int i = 0; i < t.Length; i++)
            {
                if (!mapa.TryGetValue(t[i], out int ni)) { ni = orden.Count; mapa[t[i]] = ni; orden.Add(t[i]); }
                t[i] = ni;
            }
            tris.Add(t);
        }
        T[] Tomar<T>(T[] a) => a != null && a.Length == v.Length ? orden.Select(i => a[i]).ToArray() : null;

        var m = new Mesh { name = nombre };
        if (orden.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = Tomar(v);
        var nn = Tomar(n); if (nn != null) m.normals = nn;
        var tt = Tomar(tg); if (tt != null) m.tangents = tt;
        var u1 = Tomar(uv); if (u1 != null) m.uv = u1;
        var u2 = Tomar(uv2); if (u2 != null) m.uv2 = u2;
        var cc = Tomar(col); if (cc != null) m.colors = cc;
        m.subMeshCount = tris.Count;
        for (int i = 0; i < tris.Count; i++) m.SetTriangles(tris[i], i);
        m.RecalculateBounds();
        return m;
    }

    // Guarda la malla como asset (si ya existe la reemplaza conservando el GUID)
    static Mesh Guardar(Mesh m, string ruta)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (!existente) { AssetDatabase.CreateAsset(m, ruta); return m; }
        existente.Clear();
        EditorUtility.CopySerialized(m, existente);
        existente.name = m.name;
        EditorUtility.SetDirty(existente);
        AssetDatabase.SaveAssets();
        Object.DestroyImmediate(m);
        return existente;
    }

    // Limites exactos (por vertices) en el espacio de la raiz
    static Bounds Limites(Transform raiz, IEnumerable<MeshFilter> filtros)
    {
        bool hay = false;
        Bounds b = default;
        foreach (MeshFilter f in filtros)
        {
            if (!f || !f.sharedMesh) continue;
            Matrix4x4 a = raiz.worldToLocalMatrix * f.transform.localToWorldMatrix;
            foreach (Vector3 v in f.sharedMesh.vertices)
            {
                Vector3 p = a.MultiplyPoint3x4(v);
                if (!hay) { b = new Bounds(p, Vector3.zero); hay = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    // El vertice mas a la derecha del cuerpo (los brazos son lo mas ancho de la muñeca)
    static bool PuntaMano(Transform raiz, MeshFilter mf, out Vector3 punta)
    {
        punta = default;
        if (!mf.sharedMesh) return false;
        Matrix4x4 a = raiz.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        float mx = float.MinValue;
        foreach (Vector3 v in mf.sharedMesh.vertices)
        {
            Vector3 p = a.MultiplyPoint3x4(v);
            if (p.x > mx) { mx = p.x; punta = p; }
        }
        return mx > float.MinValue;
    }
}
