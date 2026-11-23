using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Fede (Enemy 1) usa su modelo 3D animado (Assets/Modelos De enemigos/FEDEwalk.fbx) en lugar de la capsula:
// - Animator con la caminata en bucle (Fede.controller); AnimacionCaminar ajusta la velocidad a lo rapido que se mueve.
// - Hijo "Modelo" dentro del prefab, de 2.6 m de alto, con los pies en el suelo (el cuerpo sigue siendo la capsula).
// - Materiales: hueso para el cuerpo, dientes/placas un poco mas claros y los ojos con brillo indigo
//   (emision a media intensidad y una luz indigo muy tenue en la cara).
// Se aplica solo si el prefab no tiene "Modelo"; rehacer: Tools > Enemigos > Modelo de Fede.
[InitializeOnLoad]
public static class FedeModeloSetup
{
    const string Prefab = "Assets/Prefabs/Enemy 1 (fede).prefab";
    const string Modelo = "Assets/Modelos De enemigos/FEDEwalk.fbx";
    const string Controlador = "Assets/Modelos De enemigos/Fede.controller";
    const string ClipCaminar = "metarig|metarig|metarig|metarigAction";
    // fede.003 (el manto) no tiene huesos: se mueve con su propia animacion, en una segunda capa
    const string ClipManto = "fede.003|arm|arm|fede.003Action";
    const string Carpeta = "Assets/Modelos De enemigos/Materiales Fede";
    const float Alto = 2.6f;
    static readonly Color Indigo = new Color(0.33f, 0.05f, 0.85f);

    // Sube este numero para que se vuelva a aplicar solo al cambiar el modelo/los ojos
    const int Version = 8;

    // Ya no se aplica solo: el modelo y las animaciones de Fede los pone ModelosAnimadosSetup
    // (Tools > Modelos > Intercambiar modelos y animaciones). Este menu deja el modelo viejo.
    static FedeModeloSetup() { }

    [MenuItem("Tools/Enemigos/Modelo de Fede (viejo)")]
    static void Aplicar()
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Modelo);
        if (!modelo) { Debug.LogWarning("[Fede] No encuentro " + Modelo); return; }
        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            Transform viejo = raiz.transform.Find("Modelo");
            if (viejo) Object.DestroyImmediate(viejo.gameObject);
            // Copias sueltas del FBX arrastradas al prefab (sin configurar): el modelo es "Modelo"
            foreach (Transform h in raiz.transform.Cast<Transform>().ToArray())
                if (h.name == "FEDEwalk" || h.name == "FEDE") Object.DestroyImmediate(h.gameObject);

            var m = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
            m.name = "Modelo";
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one;
            foreach (Collider c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // Materiales
            Material hueso = Mat("FedeHueso", new Color(0.6f, 0.58f, 0.53f), 0.3f, default);
                        Material ojos = Mat("FedeOjos", Indigo * 0.5f, 0.8f, Indigo * 1.7f); // medio brillo
            // FEDEwalk: fede.006 = ojos (brillo indigo), el resto hueso
            Renderer rOjos = null;
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
            {
                bool esOjo = r.name.EndsWith(".006");
                if (esOjo) rOjos = r;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = esOjo ? ojos : hueso;
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

            // Luz indigo tenue delante de los ojos, pegada al hueso de la cabeza
            if (rOjos)
            {
                Bounds bo = rOjos is SkinnedMeshRenderer sko ? Transformar(null, sko) : rOjos.bounds;
                Vector3 punto = bo.center + raiz.transform.forward * Mathf.Max(0.1f, bo.size.z);
                var luzGo = new GameObject("Luz Ojos");
                luzGo.transform.SetParent(HuesoCercano(m.transform, bo.center), true);
                luzGo.transform.position = punto;
                var l = luzGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = Indigo;
                l.range = 1.6f;
                l.intensity = 0.25f;
                l.shadows = LightShadows.None;
            }

            // Caminata
            var anim = m.GetComponent<Animator>();
            if (!anim) anim = m.AddComponent<Animator>();
            anim.runtimeAnimatorController = ControladorCaminar();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (!raiz.GetComponent<AnimacionCaminar>()) raiz.AddComponent<AnimacionCaminar>();

            new GameObject("_fede v" + Version).transform.SetParent(m.transform, false);
            var capsula = raiz.GetComponent<MeshRenderer>();
            if (capsula) capsula.enabled = false;

            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            Debug.Log($"[Fede] Modelo 3D integrado en {Prefab} ({Alto} m, ojos indigo). Las instancias de la escena se actualizan solas.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // Hueso del esqueleto mas cercano a un punto (los ojos siguen a la cabeza al caminar)
    static Transform HuesoCercano(Transform modelo, Vector3 punto)
    {
        Transform rig = modelo.Find("metarig");
        if (!rig) return modelo;
        Transform mejor = modelo; float menor = float.MaxValue;
        foreach (Transform t in rig.GetComponentsInChildren<Transform>())
        {
            if (t.name.EndsWith("_end")) continue;
            float d = (t.position - punto).sqrMagnitude;
            if (d < menor) { menor = d; mejor = t; }
        }
        return mejor;
    }

    // Controlador con un solo estado: la caminata en bucle
    static RuntimeAnimatorController ControladorCaminar()
    {
        // Que los clips del FBX esten en bucle
        var imp = (ModelImporter)AssetImporter.GetAtPath(Modelo);
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        bool cambio = false;
        foreach (var c in clips) if (!c.loopTime) { c.loopTime = true; cambio = true; }
        if (cambio || imp.clipAnimations.Length == 0) { imp.clipAnimations = clips; imp.SaveAndReimport(); }

        AnimationClip clip = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Modelo))
            if (o is AnimationClip c && c.name == ClipCaminar) clip = c;
        if (!clip)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Modelo))
                if (o is AnimationClip c && !c.name.StartsWith("__preview") && (clip == null || AnimationUtility.GetCurveBindings(c).Length > AnimationUtility.GetCurveBindings(clip).Length)) clip = c;

        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Controlador);
        if (!ctrl) ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(Controlador);
        while (ctrl.layers.Length > 1) ctrl.RemoveLayer(ctrl.layers.Length - 1);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var st in sm.states) sm.RemoveState(st.state);
        var estado = sm.AddState("Caminar");
        estado.motion = clip;
        estado.writeDefaultValues = false;
        sm.defaultState = estado;

        AnimationClip manto = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Modelo))
            if (o is AnimationClip c && c.name == ClipManto) manto = c;
        if (manto)
        {
            ctrl.AddLayer("Manto");
            var capas = ctrl.layers;
            capas[1].defaultWeight = 1f;
            var est2 = capas[1].stateMachine.AddState("Manto");
            est2.motion = manto;
            est2.writeDefaultValues = false;
            capas[1].stateMachine.defaultState = est2;
            ctrl.layers = capas;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[Fede] Caminata: " + (clip ? clip.name + " (" + clip.length.ToString("0.00") + " s)" : "SIN CLIP"));
        return ctrl;
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
