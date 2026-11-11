using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Fede (Enemy 1) usa su modelo 3D (Assets/Modelos De enemigos/FEDE.fbx) en lugar de la capsula:
// - Hijo "Modelo" dentro del prefab, de 2.6 m de alto, con los pies en el suelo (el cuerpo sigue siendo la capsula).
// - Materiales: hueso para el cuerpo, dientes/placas un poco mas claros y los ojos con brillo indigo
//   (emision a media intensidad y una luz indigo muy tenue en la cara).
// Se aplica solo si el prefab no tiene "Modelo"; rehacer: Tools > Enemigos > Modelo de Fede.
[InitializeOnLoad]
public static class FedeModeloSetup
{
    const string Prefab = "Assets/Prefabs/Enemy 1 (fede).prefab";
    const string Modelo = "Assets/Modelos De enemigos/FEDE.fbx";
    const string Carpeta = "Assets/Modelos De enemigos/Materiales Fede";
    const float Alto = 2.6f;
    static readonly Color Indigo = new Color(0.33f, 0.05f, 0.85f);

    // Sube este numero para que se vuelva a aplicar solo al cambiar el modelo/los ojos
    const int Version = 5;

    static FedeModeloSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (p && !p.transform.Find("Modelo/_fede v" + Version) && AssetDatabase.LoadAssetAtPath<GameObject>(Modelo)) Aplicar();
        };
    }

    [MenuItem("Tools/Enemigos/Modelo de Fede")]
    static void Aplicar()
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Modelo);
        if (!modelo) { Debug.LogWarning("[Fede] No encuentro " + Modelo); return; }
        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            Transform viejo = raiz.transform.Find("Modelo");
            if (viejo) Object.DestroyImmediate(viejo.gameObject);

            var m = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
            m.name = "Modelo";
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one;
            foreach (Collider c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // Materiales
            Material hueso = Mat("FedeHueso", new Color(0.6f, 0.58f, 0.53f), 0.3f, default);
            Material dientes = Mat("FedeDientes", new Color(0.82f, 0.8f, 0.72f), 0.45f, default);
            Material ojos = Mat("FedeOjos", Indigo * 0.5f, 0.8f, Indigo * 1.7f); // medio brillo
            // fede.001 = boca con dientes, fede.002 = cuerpo, fede.003 = craneo (los ojos son huecos del craneo)
            Renderer rBoca = null;
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
            {
                Material mat = r.name.EndsWith(".001") ? dientes : hueso;
                if (r.name.EndsWith(".001")) rBoca = r;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                // Que no desaparezca por los limites de la pose (la malla con huesos)
                if (r is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true;
            }

            // Tamano y pies en el suelo
            if (Limites(m.transform, out Bounds b) && b.size.y > 0.0001f)
            {
                var agent = raiz.GetComponent<NavMeshAgent>();
                float pies = agent ? -agent.baseOffset : -1f;
                m.transform.localScale = Vector3.one * (Alto / b.size.y);
                Limites(m.transform, out b);
                Vector3 centro = raiz.transform.InverseTransformPoint(b.center);
                float abajo = raiz.transform.InverseTransformPoint(b.min).y;
                m.transform.localPosition = new Vector3(-centro.x, pies - abajo, -centro.z);
            }

            // Ojos: dos brillos indigo dentro de las cuencas (un poco arriba de la boca, a cada lado)
            if (rBoca)
            {
                Bounds boca = rBoca is SkinnedMeshRenderer skb ? Transformar(null, skb) : rBoca.bounds;
                float k = boca.size.y / 0.19f;                         // proporcional al tamano de la cara
                Vector3 adelante = raiz.transform.forward, arriba = Vector3.up, derecha = raiz.transform.right;
                Vector3 frente = boca.center + adelante * (boca.extents.z * 0.5f);
                Vector3 centroOjos = frente + arriba * (0.195f * k) + adelante * (-0.015f * k);
                foreach (float lado in new[] { -1f, 1f })
                {
                    var ojo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ojo.name = lado < 0 ? "Ojo Izquierdo" : "Ojo Derecho";
                    Object.DestroyImmediate(ojo.GetComponent<Collider>());
                    ojo.transform.SetParent(m.transform, true);
                    ojo.transform.position = centroOjos + derecha * (lado * 0.078f * k);
                    ojo.transform.rotation = raiz.transform.rotation;
                    Vector3 s = m.transform.lossyScale;
                    ojo.transform.localScale = new Vector3(0.09f * k / s.x, 0.055f * k / s.y, 0.03f * k / s.z);
                    var ro = ojo.GetComponent<Renderer>();
                    ro.sharedMaterial = ojos;
                    ro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var luzGo = new GameObject("Luz Ojos");
                luzGo.transform.SetParent(m.transform, true);
                luzGo.transform.position = centroOjos + adelante * (0.12f * k);
                var l = luzGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = Indigo;
                l.range = 1.6f;
                l.intensity = 0.25f;
                l.shadows = LightShadows.None;
            }

            new GameObject("_fede v" + Version).transform.SetParent(m.transform, false);
            var capsula = raiz.GetComponent<MeshRenderer>();
            if (capsula) capsula.enabled = false;

            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            Debug.Log($"[Fede] Modelo 3D integrado en {Prefab} ({Alto} m, ojos indigo). Las instancias de la escena se actualizan solas.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    static bool Limites(Transform t, out Bounds b)
    {
        b = default; bool hay = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rb = r is SkinnedMeshRenderer sk && sk.sharedMesh ? Transformar(sk.rootBone ? sk.rootBone : sk.transform, sk) : r.bounds;
            if (!hay) { b = rb; hay = true; } else b.Encapsulate(rb);
        }
        return hay;
    }

    // Limites de una malla con huesos en su pose actual (el bounds del renderer puede venir mal en el prefab)
    static Bounds Transformar(Transform _, SkinnedMeshRenderer sk)
    {
        var m = new Mesh();
        sk.BakeMesh(m, true);
        Bounds b = default; bool hay = false;
        foreach (Vector3 v in m.vertices)
        {
            Vector3 w = sk.transform.TransformPoint(v);
            if (!hay) { b = new Bounds(w, Vector3.zero); hay = true; } else b.Encapsulate(w);
        }
        Object.DestroyImmediate(m);
        return b;
    }

    static Material Mat(string nombre, Color c, float suave, Color emision)
    {
        System.IO.Directory.CreateDirectory(Carpeta);
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", suave);
        if (emision != default)
        {
            m.SetColor("_EmissionColor", emision);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (nuevo) AssetDatabase.CreateAsset(m, ruta); else EditorUtility.SetDirty(m);
        return m;
    }
}
