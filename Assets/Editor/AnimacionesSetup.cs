using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Animaciones de los personajes (Assets/Animaciones, cada FBX trae su modelo con esqueleto y una animacion):
// - Fede (Enemy 1): caminar, correr y el grito (al ver al jugador).
// - Gato (Enemy 2): quieto y caminar (corriendo = caminar mas rapido). Sigue siendo una sombra negra con ojos.
// - Slappy (Enemy 4, Zona Roja): caminar, correr y el jumpscare (al atrapar al jugador).
// - Cliente y cultista (la tienda): caminar.
// Para cada uno: importa los FBX como Generic (las de caminar/correr/quieto en bucle), arma un Animator Controller
// (Quieto / Caminar / Correr / Grito / Atacar) y pone el modelo animado en el enemigo (prefab) o NPC (escena):
// escalado al tamano del modelo de antes, apoyado en el piso, mirando hacia adelante, y apaga el modelo viejo.
// AnimadorPersonaje mueve las animaciones en el juego. Si una animacion trae otro esqueleto que el modelo (no le
// sirve), se avisa en la consola y se usa otra (ej: caminar = correr mas lento).
// Se aplica solo si falta; rehacer: Tools > Personajes > Animaciones (rehacer). Brazos.fbx no se usa (ver abajo).
[InitializeOnLoad]
public static class AnimacionesSetup
{
    const string Carpeta = "Assets/Animaciones";
    const string Marca = "animaciones v1";
    const string NombreModelo = "Modelo Animado";

    class Personaje
    {
        public string nombre, modelo, quieto, caminar, correr, grito, atacar;
        public string prefab;      // enemigo (prefab)
        public bool cliente, cultista;   // NPC de la escena (TutorialTienda)
        public bool gato;
        public float altoPorDefecto = 1.8f;
        public float velCaminar = 1.6f, velCorrer = 4.5f;
    }

    static readonly Personaje[] Personajes =
    {
        new Personaje { nombre = "Fede", modelo = "Fede/FedeCaminar", caminar = "Fede/FedeCaminar", correr = "Fede/FedeCorrer", grito = "Fede/FedeGrito",
                        prefab = "Assets/Prefabs/Enemy 1 (fede).prefab", altoPorDefecto = 2.3f, velCaminar = 1.8f, velCorrer = 4.5f },
        new Personaje { nombre = "Gato", modelo = "Gato/GatoCaminar", quieto = "Gato/GatoQuieto", caminar = "Gato/GatoCaminar",
                        prefab = "Assets/Prefabs/Enemy 2 (gato).prefab", gato = true, velCaminar = 1.8f, velCorrer = 4.5f },
        new Personaje { nombre = "Slappy", modelo = "Slappy/SlappyJumpscare", caminar = "Slappy/SlappyCaminar", correr = "Slappy/SlappyCorrer", atacar = "Slappy/SlappyJumpscare",
                        prefab = "Assets/Prefabs/Enemy 4.prefab", altoPorDefecto = 2.4f, velCaminar = 1.8f, velCorrer = 4.5f },
        new Personaje { nombre = "Cliente", modelo = "Cliente/ClienteCaminar", caminar = "Cliente/ClienteCaminar", cliente = true, velCaminar = 1.4f },
        new Personaje { nombre = "Cultista", modelo = "Cultista/CultistaCaminar", caminar = "Cultista/CultistaCaminar", cultista = true, velCaminar = 1.4f },
    };

    static AnimacionesSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Aplicar(false); };
    }

    [MenuItem("Tools/Personajes/Animaciones (rehacer)")]
    static void Menu() => Aplicar(true);

    static void Aplicar(bool rehacer)
    {
        if (!AssetDatabase.IsValidFolder(Carpeta)) return;
        Importar(rehacer);
        var hechos = new List<string>();
        foreach (Personaje p in Personajes)
        {
            GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Carpeta + "/" + p.modelo + ".fbx");
            if (modelo == null) continue;
            AnimatorController ctrl = Controlador(p, modelo, rehacer);
            if (ctrl == null) continue;
            if (p.prefab != null && Prefab(p, modelo, ctrl, rehacer)) hechos.Add(p.nombre);
            if ((p.cliente || p.cultista) && Escena(p, modelo, ctrl, rehacer)) hechos.Add(p.nombre);
        }
        if (hechos.Count > 0)
            Debug.Log("[Animaciones] Listo: " + string.Join(", ", hechos) + " con su modelo animado (quieto/caminar/correr/grito/ataque segun cada uno). " +
                      "Si alguno quedo mirando para atras o muy grande/chico, se ajusta en su hijo \"" + NombreModelo + "\". Guarda la escena (Ctrl+S).");
    }

    // ------------------------------------------------------------------ Importacion

    static void Importar(bool forzar)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(ruta) is ModelImporter mi) || (!forzar && mi.userData == Marca)) continue;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.userData = Marca;
            mi.SaveAndReimport();
            // Ya importado: las de un solo golpe (grito, jumpscare) sin bucle, el resto en bucle
            ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
            bool unaVez = ruta.Contains("Grito") || ruta.Contains("Jumpscare");
            foreach (ModelImporterClipAnimation c in clips) { c.loopTime = !unaVez; c.loopPose = false; }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }
    }

    static AnimationClip Clip(string archivo)
    {
        if (string.IsNullOrEmpty(archivo)) return null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Carpeta + "/" + archivo + ".fbx"))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    // La animacion sirve para este modelo si la mayoria de los huesos que mueve existen en el
    static bool Compatible(AnimationClip clip, GameObject modelo)
    {
        if (clip == null) return false;
        var rutas = new HashSet<string>();
        Rutas(modelo.transform, "", rutas);
        int total = 0, bien = 0;
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.type != typeof(Transform)) continue;
            total++;
            if (rutas.Contains(b.path)) bien++;
        }
        return total == 0 || bien >= total * 0.7f;
    }

    static void Rutas(Transform t, string ruta, HashSet<string> salida)
    {
        salida.Add(ruta);
        foreach (Transform h in t) Rutas(h, ruta.Length == 0 ? h.name : ruta + "/" + h.name, salida);
    }

    // ------------------------------------------------------------------ Animator Controller

    static AnimatorController Controlador(Personaje p, GameObject modelo, bool rehacer)
    {
        string ruta = Carpeta + "/" + p.nombre + "/" + p.nombre + ".controller";
        var existente = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
        if (existente != null && !rehacer) return existente;
        if (existente != null) AssetDatabase.DeleteAsset(ruta);

        AnimationClip caminar = Usable(p, p.caminar, modelo), quieto = Usable(p, p.quieto, modelo), correr = Usable(p, p.correr, modelo);
        AnimationClip grito = Usable(p, p.grito, modelo), atacar = Usable(p, p.atacar, modelo);
        float ritmoCaminar = 1f, ritmoCorrer = 1f;
        if (caminar == null && correr != null) { caminar = correr; ritmoCaminar = 0.55f; Debug.Log("[Animaciones] " + p.nombre + ": caminar usa la de correr, mas lenta."); }
        if (correr == null && caminar != null) { correr = caminar; ritmoCorrer = 1.8f; }
        if (caminar == null) { Debug.LogWarning("[Animaciones] " + p.nombre + ": ninguna animacion le sirve a su modelo."); return null; }

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ruta);
        ctrl.AddParameter("Velocidad", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Ritmo", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Corriendo", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Caminando", AnimatorControllerParameterType.Bool);   // lo usan TutorialTienda e IntroTienda
        foreach (AnimatorControllerParameter par in ctrl.parameters) if (par.name == "Ritmo") par.defaultFloat = 1f;
        ctrl.parameters = ctrl.parameters;
        if (grito != null) ctrl.AddParameter("Gritar", AnimatorControllerParameterType.Trigger);
        if (atacar != null) ctrl.AddParameter("Atacar", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState sQuieto = sm.AddState("Quieto", new Vector3(250f, 0f));
        sQuieto.motion = quieto != null ? quieto : caminar;
        if (quieto == null) sQuieto.speed = 0f;   // sin animacion de quieto: la pose de caminar, quieta
        AnimatorState sCaminar = sm.AddState("Caminar", new Vector3(250f, 120f));
        sCaminar.motion = caminar; sCaminar.speed = ritmoCaminar; sCaminar.speedParameter = "Ritmo"; sCaminar.speedParameterActive = true;
        AnimatorState sCorrer = sm.AddState("Correr", new Vector3(250f, 240f));
        sCorrer.motion = correr; sCorrer.speed = ritmoCorrer; sCorrer.speedParameter = "Ritmo"; sCorrer.speedParameterActive = true;
        sm.defaultState = sQuieto;

        Paso(sQuieto, sCaminar, (AnimatorConditionMode.Greater, 0.15f, "Velocidad"));
        Paso(sQuieto, sCaminar, (AnimatorConditionMode.If, 0f, "Caminando"));
        Paso(sCaminar, sQuieto, (AnimatorConditionMode.Less, 0.08f, "Velocidad"), (AnimatorConditionMode.IfNot, 0f, "Caminando"));
        Paso(sQuieto, sCorrer, (AnimatorConditionMode.If, 0f, "Corriendo"));
        Paso(sCaminar, sCorrer, (AnimatorConditionMode.If, 0f, "Corriendo"));
        Paso(sCorrer, sCaminar, (AnimatorConditionMode.IfNot, 0f, "Corriendo"));

        foreach (var (clip, trig, pos) in new[] { (grito, "Gritar", 0f), (atacar, "Atacar", 1f) })
        {
            if (clip == null) continue;
            AnimatorState s = sm.AddState(trig == "Gritar" ? "Grito" : "Ataque", new Vector3(550f, 60f + pos * 120f));
            s.motion = clip;
            AnimatorStateTransition t = sm.AddAnyStateTransition(s);
            t.AddCondition(AnimatorConditionMode.If, 0f, trig);
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            AnimatorStateTransition vuelta = s.AddTransition(sQuieto);
            vuelta.hasExitTime = true;
            vuelta.exitTime = 0.95f;
            vuelta.duration = 0.2f;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static AnimationClip Usable(Personaje p, string archivo, GameObject modelo)
    {
        AnimationClip c = Clip(archivo);
        if (c == null) return null;
        if (Compatible(c, modelo)) return c;
        Debug.LogWarning("[Animaciones] " + p.nombre + ": " + archivo + " tiene otro esqueleto que el modelo (" + p.modelo + "): no se usa.");
        return null;
    }

    static void Paso(AnimatorState de, AnimatorState a, params (AnimatorConditionMode modo, float valor, string param)[] condiciones)
    {
        AnimatorStateTransition t = de.AddTransition(a);
        t.hasExitTime = false;
        t.duration = 0.18f;
        foreach (var c in condiciones) t.AddCondition(c.modo, c.valor, c.param);
    }

    // ------------------------------------------------------------------ Poner el modelo

    static bool Prefab(Personaje p, GameObject modelo, AnimatorController ctrl, bool rehacer)
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(p.prefab)) return false;
        GameObject root = PrefabUtility.LoadPrefabContents(p.prefab);
        try
        {
            Transform viejo = root.transform.Find(NombreModelo);
            if (viejo && !rehacer) return false;
            if (viejo) Object.DestroyImmediate(viejo.gameObject);
            var agent = root.GetComponent<NavMeshAgent>();
            float pies = agent ? -agent.baseOffset : 0f;
            Poner(p, root.transform, modelo, ctrl, pies, false);
            var an = root.GetComponent<AnimadorPersonaje>();
            if (!an) an = root.AddComponent<AnimadorPersonaje>();
            an.velocidadCaminar = p.velCaminar;
            an.velocidadCorrer = p.velCorrer;
            foreach (AnimacionCaminar ac in root.GetComponentsInChildren<AnimacionCaminar>(true)) ac.enabled = false;
            PrefabUtility.SaveAsPrefabAsset(root, p.prefab);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static bool Escena(Personaje p, GameObject modelo, AnimatorController ctrl, bool rehacer)
    {
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        Transform npc = tut == null ? null : p.cliente ? tut.cliente : tut.cultista;
        if (npc == null) return false;
        Transform viejo = npc.Find(NombreModelo);
        if (viejo && !rehacer) return false;
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        // Los pies del NPC: el punto mas bajo de su modelo de antes (respecto a su pivote)
        float pies = Limites(npc, null, out Bounds b) ? npc.InverseTransformPoint(b.min).y : 0f;
        Poner(p, npc, modelo, ctrl, pies, true);
        var an = npc.GetComponent<AnimadorPersonaje>();
        if (!an) an = Undo.AddComponent<AnimadorPersonaje>(npc.gameObject);
        an.velocidadCaminar = p.velCaminar;
        EditorSceneManager.MarkSceneDirty(npc.gameObject.scene);
        return true;
    }

    // Pone el modelo animado bajo "padre": mismo alto que el modelo de antes, pies en "pies", mirando a +Z;
    // los renderers de antes se apagan
    static void Poner(Personaje p, Transform padre, GameObject modelo, AnimatorController ctrl, float pies, bool conUndo)
    {
        bool hayAntes = Limites(padre, null, out Bounds antes);
        float alto = hayAntes ? padre.InverseTransformVector(Vector3.up * antes.size.y).magnitude : p.altoPorDefecto;
        if (alto < 0.3f || alto > 6f) alto = p.altoPorDefecto;
        var viejas = new List<Renderer>(padre.GetComponentsInChildren<Renderer>(true));

        var m = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
        if (conUndo) Undo.RegisterCreatedObjectUndo(m, NombreModelo);
        m.name = NombreModelo;
        foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        m.transform.localScale = Vector3.one;
        var anim = m.GetComponent<Animator>();
        if (!anim) anim = m.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // Mirando hacia adelante
        float giro = Giro(m.transform, p.gato);
        m.transform.localRotation = Quaternion.Euler(0f, giro, 0f);

        // Tamano: el gato por su largo, los demas por su alto
        if (Limites(m.transform, padre, out Bounds bm) && bm.size.y > 0.001f)
        {
            float s = p.gato ? 0.75f / Mathf.Max(bm.size.x, bm.size.z) : alto / bm.size.y;
            m.transform.localScale = Vector3.one * s;
            Limites(m.transform, padre, out bm);
            m.transform.localPosition += new Vector3(-bm.center.x, pies - bm.min.y, -bm.center.z);
        }

        foreach (Renderer r in viejas) if (r != null && !r.transform.IsChildOf(m.transform)) { if (conUndo) Undo.RecordObject(r, "Modelo viejo"); r.enabled = false; }

        if (p.gato) Sombra(padre, m.transform);
    }

    // El gato sigue siendo una sombra: todo negro, y los ojos (los de antes) pegados a la cabeza del modelo nuevo
    static void Sombra(Transform padre, Transform m)
    {
        var sombra = AssetDatabase.LoadAssetAtPath<Material>("Assets/Backrooms/Palancas/SombraGato.mat");
        if (sombra)
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = sombra;
                r.sharedMaterials = mats;
            }
        Transform cabeza = Hueso(m, "head");
        if (cabeza == null) return;
        Limites(m, padre, out Bounds b);
        float tam = Mathf.Max(0.012f, b.size.y * 0.06f);
        int lado = 0;
        foreach (Transform t in padre.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Ojo" || t.IsChildOf(m)) continue;
            Vector3 adelante = padre.forward;
            t.SetParent(cabeza, true);
            t.position = cabeza.position + adelante * (b.size.z * 0.06f) + padre.right * ((lado++ == 0 ? -1f : 1f) * b.size.x * 0.12f) + padre.up * (b.size.y * 0.04f);
            t.localScale = Vector3.one * (tam / Mathf.Max(0.0001f, cabeza.lossyScale.x));
            if (t.TryGetComponent(out Renderer r)) r.enabled = true;
        }
    }

    // Hacia donde mira el modelo (por sus huesos): en un cuadrupedo de la cola a la cabeza; en una persona del
    // talon a la punta del pie. Devuelve los grados (en Y) que hay que girarlo para que mire a +Z
    static float Giro(Transform m, bool cuadrupedo)
    {
        Vector3 adelante = Vector3.zero;
        if (cuadrupedo)
        {
            Transform cabeza = Hueso(m, "head"), cola = Hueso(m, "tail");
            Transform atras = cola != null ? cola : Hueso(m, "spine");
            if (cabeza != null && atras != null) adelante = cabeza.position - atras.position;
        }
        else
        {
            Transform punta = Hueso(m, "toe");
            if (punta != null && punta.parent != null) adelante = punta.position - punta.parent.position;
        }
        adelante = m.parent != null ? m.parent.InverseTransformDirection(adelante) : adelante;
        adelante.y = 0f;
        if (adelante.sqrMagnitude < 0.0001f) return 0f;
        return -Vector3.SignedAngle(Vector3.forward, adelante.normalized, Vector3.up);
    }

    static Transform Hueso(Transform m, string parte)
    {
        foreach (Transform t in m.GetComponentsInChildren<Transform>(true))
            if (t.name.ToLowerInvariant().Contains(parte) && t.GetComponent<Renderer>() == null) return t;
        return null;
    }

    // Limites (en el espacio de "espacio", o del mundo si es null) de las mallas de t, con la pose actual
    static bool Limites(Transform t, Transform espacio, out Bounds b)
    {
        Bounds caja = default;
        bool hay = false;
        void Sumar(Vector3 mundo)
        {
            Vector3 p = espacio != null ? espacio.InverseTransformPoint(mundo) : mundo;
            if (!hay) { caja = new Bounds(p, Vector3.zero); hay = true; } else caja.Encapsulate(p);
        }
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled) continue;
            Mesh mesh = null;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                mesh = new Mesh();
                smr.BakeMesh(mesh, true);
            }
            else if (r.TryGetComponent(out MeshFilter mf)) mesh = mf.sharedMesh;
            if (mesh == null) continue;
            Vector3[] vs = mesh.vertices;
            for (int i = 0; i < vs.Length; i += Mathf.Max(1, vs.Length / 4000)) Sumar(m.MultiplyPoint3x4(vs[i]));
            if (r is SkinnedMeshRenderer) Object.DestroyImmediate(mesh);
        }
        b = caja;
        return hay;
    }
}
