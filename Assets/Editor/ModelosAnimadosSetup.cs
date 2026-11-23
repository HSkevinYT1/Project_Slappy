using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Cambia los modelos por los de "Assets/animaciones y modelos" con sus animaciones (AnimacionesModelo):
// - Enemy 1 (fede)  (laberinto):  Idle, Caminar, Correr, Jumpscare
// - Enemy 2 (gato)  (backrooms):  Idle, Caminar
// - Enemy 4 (Slappy, zona roja):  Caminar, Correr, Jumpscare
// - Cliente y cultista de la tienda (TutorialTienda): Idle, Caminar
// - Brazos del jugador (Player_Arms): Arms.fbx con su animacion en bucle; el reloj, el agarre de la linterna
//   y lo que cuelga de los huesos se pasan a los mismos huesos del modelo nuevo.
// La muneca (Muneca.fbx / Enemigo 3) NO se toca.
// Cada animacion es su propio FBX dentro de "Modelo" (cada archivo trae su propio esqueleto).
// Se aplica sola una vez; rehacer: Tools > Modelos > Intercambiar modelos y animaciones.
[InitializeOnLoad]
public static class ModelosAnimadosSetup
{
    const string Marca = "_anim v1";
    const string Base = "Assets/animaciones y modelos/";
    const string Prefabs = "Assets/Prefabs/";
    const string MatsFede = "Assets/Modelos De enemigos/Materiales Fede/";

    struct Def
    {
        public string nombre, archivo; public bool bucle;
        public Def(string n, string a, bool b = true) { nombre = n; archivo = a; bucle = b; }
    }

    static readonly Def[] Fede =
    {
        new Def(AnimacionesModelo.Idle, "Fede/FEDEIdle.fbx"),
        new Def(AnimacionesModelo.Caminar, "Fede/FEDEwalk.fbx"),
        new Def(AnimacionesModelo.Correr, "Fede/FEDERun.fbx"),
        new Def(AnimacionesModelo.Jumpscare, "Fede/JumpscareFede.fbx", false),
    };
    static readonly Def[] Gato =
    {
        new Def(AnimacionesModelo.Idle, "Cat/CatIdle.fbx"),
        new Def(AnimacionesModelo.Caminar, "Cat/CatWalk.fbx"),
    };
    static readonly Def[] Slappy =
    {
        new Def(AnimacionesModelo.Caminar, "Slappy/Slappywalk.fbx"),
        new Def(AnimacionesModelo.Correr, "Slappy/Mutant Run (3).fbx"),
        new Def(AnimacionesModelo.Jumpscare, "Slappy/JumpscareSlappy.fbx", false),
    };
    static readonly Def[] Cliente =
    {
        new Def(AnimacionesModelo.Idle, "NPC/NPCIDLE.fbx"),
        new Def(AnimacionesModelo.Caminar, "NPC/NPCwalk.fbx"),
    };
    static readonly Def[] Cultista =
    {
        new Def(AnimacionesModelo.Idle, "Cultista/CultistIdle.fbx"),
        new Def(AnimacionesModelo.Caminar, "Cultista/CultistWalkPose.fbx"),
    };
    const string ArchivoBrazos = "NPC/Arms.fbx";
    // Lo que cuelga de los brazos y hay que conservar
    static readonly HashSet<string> ExtrasBrazos = new HashSet<string> { "Agarre_Linterna", "Reloj 3D", "HandPoint", "Hand" };

    static ModelosAnimadosSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(Base + ArchivoBrazos)) return;
        Aplicar(false);
    }

    [MenuItem("Tools/Modelos/Intercambiar modelos y animaciones")]
    static void Menu() => Aplicar(true);

    static void Aplicar(bool forzar)
    {
        ConfigurarImports();
        Enemigo(Prefabs + "Enemy 1 (fede).prefab", Fede, 2.6f, true, forzar);
        Enemigo(Prefabs + "Enemy 2 (gato).prefab", Gato, 0f, false, forzar);
        Enemigo(Prefabs + "Enemy 4.prefab", Slappy, 0f, false, forzar);
        BrazosPrefab(forzar);
        Escena(forzar);
    }

    // ------------------------------------------------------------------ Imports

    // Los FBX traen camaras y luces de Blender/Maya: fuera (una camara importada renderizaria)
    static void ConfigurarImports()
    {
        var archivos = Fede.Concat(Gato).Concat(Slappy).Concat(Cliente).Concat(Cultista).Select(d => d.archivo).Append(ArchivoBrazos);
        foreach (string a in archivos)
        {
            if (!(AssetImporter.GetAtPath(Base + a) is ModelImporter imp)) continue;
            if (!imp.importCameras && !imp.importLights) continue;
            imp.importCameras = false;
            imp.importLights = false;
            imp.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ Enemigos (prefabs)

    static void Enemigo(string ruta, Def[] defs, float altoFijo, bool esFede, bool forzar)
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(ruta)) { Debug.LogWarning("[Modelos] No encuentro " + ruta); return; }
        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            Transform viejo = raiz.transform.Find("Modelo");
            if (viejo && viejo.Find(Marca) && !forzar) return;

            var viejos = new List<Transform>();
            if (viejo) viejos.Add(viejo);
            // Copias sueltas de FBX arrastradas al prefab
            foreach (Transform h in raiz.transform)
                if (h != viejo && h.GetComponentInChildren<Animator>(true) && !h.GetComponent<NavMeshAgent>()) viejos.Add(h);

            var mapa = Materiales(viejos);
            Rescatar(raiz.transform, viejos, false);
            foreach (Transform v in viejos) Object.DestroyImmediate(v.gameObject);
            foreach (AnimacionCaminar ac in raiz.GetComponentsInChildren<AnimacionCaminar>(true)) Object.DestroyImmediate(ac);

            var agent = raiz.GetComponent<NavMeshAgent>();
            float alto = altoFijo > 0f ? altoFijo : (agent ? agent.height : 2f);
            float pies = agent ? -agent.baseOffset : -1f;

            System.Action<Renderer, List<Renderer>> mats;
            if (esFede)
            {
                var hueso = AssetDatabase.LoadAssetAtPath<Material>(MatsFede + "FedeHueso.mat");
                var ojos = AssetDatabase.LoadAssetAtPath<Material>(MatsFede + "FedeOjos.mat");
                mats = (r, _) =>
                {
                    Material m = r.name.EndsWith(".006") && ojos ? ojos : hueso;
                    if (!m) return;
                    var arr = r.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++) arr[i] = m;
                    r.sharedMaterials = arr;
                };
            }
            else mats = (r, sueltos) => AsignarMateriales(r, mapa, sueltos);

            CrearModelo(raiz.transform, defs, alto, pies, mats, mapa);
            var capsula = raiz.GetComponent<MeshRenderer>();
            if (capsula) capsula.enabled = false;

            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            Debug.Log($"[Modelos] {Path.GetFileNameWithoutExtension(ruta)}: modelo nuevo con {string.Join(", ", defs.Select(d => d.nombre))} ({alto:0.0} m).");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // ------------------------------------------------------------------ NPCs de la tienda (escena)

    static void Escena(bool forzar)
    {
        bool cambio = false;
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        if (tut)
        {
            cambio |= NPC(tut.cliente, Cliente, forzar);
            cambio |= NPC(tut.cultista, Cultista, forzar);
        }
        // Brazos del jugador de la escena si no vienen del prefab
        var pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (pm && !PrefabUtility.IsPartOfPrefabInstance(pm))
        {
            Transform pa = Buscar(pm.transform, "Player_Arms");
            if (pa) cambio |= Brazos(pa, forzar, true);
        }
        if (!cambio) return;
        if (tut) EditorSceneManager.MarkSceneDirty(tut.gameObject.scene);
        if (pm) EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
        Debug.Log("[Modelos] Escena actualizada (NPCs de la tienda). Guarda la escena (Ctrl+S).");
    }

    static bool NPC(Transform npc, Def[] defs, bool forzar)
    {
        if (!npc) return false;
        Transform actual = npc.Find("Modelo");
        if (actual && actual.Find(Marca) && !forzar) return false;

        var viejos = new List<Transform>();
        foreach (Transform h in npc)
            if (h.name == "Modelo" || h.GetComponentInChildren<Animator>(true)) viejos.Add(h);

        float alto = 1.8f, pies = 0f;
        if (LimitesEn(viejos, npc, out Bounds b) && b.size.y > 0.2f) { alto = b.size.y; pies = b.min.y; }

        var mapa = Materiales(viejos);
        Rescatar(npc, viejos, true);
        foreach (Transform v in viejos)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(v) && !PrefabUtility.IsOutermostPrefabInstanceRoot(v.gameObject))
            {
                Undo.RecordObject(v.gameObject, "Modelo NPC");
                v.gameObject.SetActive(false);
                v.gameObject.name += " (viejo)";
            }
            else Undo.DestroyObjectImmediate(v.gameObject);
        }
        Undo.RecordObject(npc, "Modelo NPC");
        var comp = CrearModelo(npc, defs, alto, pies, (r, sueltos) => AsignarMateriales(r, mapa, sueltos), mapa);
        Undo.RegisterCreatedObjectUndo(npc.Find("Modelo").gameObject, "Modelo NPC");
        EditorUtility.SetDirty(comp);
        Debug.Log($"[Modelos] {npc.name}: modelo nuevo con {string.Join(", ", defs.Select(d => d.nombre))} ({alto:0.00} m).", npc);
        return true;
    }

    // ------------------------------------------------------------------ Brazos del jugador

    static void BrazosPrefab(bool forzar)
    {
        const string ruta = Prefabs + "Player.prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(ruta)) return;
        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            Transform pa = Buscar(raiz.transform, "Player_Arms");
            if (pa && Brazos(pa, forzar, false)) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    static bool Brazos(Transform pa, bool forzar, bool escena)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Base + ArchivoBrazos);
        if (!fbx) return false;
        Transform existente = pa.Find("Modelo Brazos");
        if (existente && existente.Find(Marca) && !forzar) return false;

        var viejos = pa.Cast<Transform>().ToList();
        var mapa = Materiales(viejos);

        // Lo que hay que conservar: reloj, agarre de la linterna, scripts/luces y lo que se referencia desde fuera
        var extras = new List<Transform>();
        void Rec(Transform t)
        {
            foreach (Transform c in t)
            {
                if (ExtrasBrazos.Contains(c.name) || c.GetComponent<MonoBehaviour>() || c.GetComponent<Light>()) extras.Add(c);
                else Rec(c);
            }
        }
        foreach (Transform v in viejos)
        {
            if (ExtrasBrazos.Contains(v.name) || v.GetComponent<MonoBehaviour>() || v.GetComponent<Light>()) extras.Add(v);
            else Rec(v);
        }
        foreach (Transform r in Referenciados(pa.root, viejos))
            if (!extras.Any(e => r == e || r.IsChildOf(e))) extras.Add(r);
        // Solo los de mas arriba
        extras = extras.Where(e => !extras.Any(o => o != e && e.IsChildOf(o))).ToList();
        var padres = extras.ToDictionary(e => e, e => e.parent.name);

        var inst = Object.Instantiate(fbx, pa);
        inst.name = "Modelo Brazos";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        Limpiar(inst);
        var anim = PrepararAnimator(inst);
        var clips = ElegirClips(Base + ArchivoBrazos);
        foreach (var c in clips) c.SampleAnimation(inst, 0f);
        var sueltos = Sueltos(mapa, inst);
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) AsignarMateriales(r, mapa, sueltos);
        new GameObject(Marca).transform.SetParent(inst.transform, false);

        // Los extras van al mismo hueso del modelo nuevo (misma posicion local)
        var huesos = new Dictionary<string, Transform>();
        foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) if (!huesos.ContainsKey(t.name)) huesos[t.name] = t;
        foreach (Transform e in extras)
        {
            if (huesos.TryGetValue(padres[e], out Transform destino))
            {
                Vector3 p = e.localPosition; Quaternion q = e.localRotation; Vector3 s = e.localScale;
                if (escena) Undo.SetTransformParent(e, destino, "Brazos"); else e.SetParent(destino, false);
                e.localPosition = p; e.localRotation = q; e.localScale = s;
            }
            else if (escena) Undo.SetTransformParent(e, pa, "Brazos");
            else e.SetParent(pa, true);
        }

        foreach (Transform v in viejos)
        {
            if (!v) continue;
            if (escena) Undo.DestroyObjectImmediate(v.gameObject); else Object.DestroyImmediate(v.gameObject);
        }

        var comp = pa.GetComponent<AnimacionesModelo>();
        if (!comp) comp = escena ? Undo.AddComponent<AnimacionesModelo>(pa.gameObject) : pa.gameObject.AddComponent<AnimacionesModelo>();
        comp.poses = new[] { new AnimacionesModelo.Pose { nombre = AnimacionesModelo.Idle, animator = anim, clips = clips, bucle = true } };
        if (escena) Undo.RegisterCreatedObjectUndo(inst, "Brazos");
        Debug.Log($"[Modelos] Brazos del jugador cambiados por Arms.fbx ({clips.Length} clip(s)). Pasados al modelo nuevo: {string.Join(", ", extras.Select(e => e.name))}.");
        return true;
    }

    // ------------------------------------------------------------------ Montaje comun

    static AnimacionesModelo CrearModelo(Transform padre, Def[] defs, float alto, float pies,
        System.Action<Renderer, List<Renderer>> mats, Dictionary<string, Renderer> mapa)
    {
        var cont = new GameObject("Modelo").transform;
        cont.SetParent(padre, false);
        var lista = new List<AnimacionesModelo.Pose>();
        foreach (Def d in defs)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Base + d.archivo);
            if (!fbx) { Debug.LogWarning("[Modelos] No encuentro " + Base + d.archivo); continue; }

            // Envoltura con la escala/posicion; el FBX dentro (sus propias curvas no la mueven)
            var env = new GameObject(d.nombre).transform;
            env.SetParent(cont, false);
            var inst = Object.Instantiate(fbx, env);
            inst.name = Path.GetFileNameWithoutExtension(d.archivo);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            Limpiar(inst);
            var anim = PrepararAnimator(inst);
            var clips = ElegirClips(Base + d.archivo);
            foreach (var c in clips) c.SampleAnimation(inst, 0f);

            var sueltos = Sueltos(mapa, inst);
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                mats?.Invoke(r, sueltos);
                if (r is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true;
            }

            // Tamano y pies en el suelo
            if (LimitesEn(new[] { env }, padre, out Bounds b) && b.size.y > 0.0001f)
            {
                env.localScale = Vector3.one * (alto / b.size.y);
                LimitesEn(new[] { env }, padre, out b);
                env.localPosition = new Vector3(-b.center.x, pies - b.min.y, -b.center.z);
            }

            if (lista.Count > 0) foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            lista.Add(new AnimacionesModelo.Pose { nombre = d.nombre, animator = anim, clips = clips, bucle = d.bucle });
            Debug.Log($"[Modelos] {padre.name} / {d.nombre}: {(clips.Length > 0 ? string.Join(" + ", clips.Select(c => c.name + " (" + c.length.ToString("0.00") + " s)")) : "SIN ANIMACION")}");
        }
        new GameObject(Marca).transform.SetParent(cont, false);

        var comp = padre.GetComponent<AnimacionesModelo>();
        if (!comp) comp = padre.gameObject.AddComponent<AnimacionesModelo>();
        comp.poses = lista.ToArray();
        return comp;
    }

    static void Limpiar(GameObject inst)
    {
        foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (Camera c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
        foreach (Light l in inst.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
        // Plano de referencia de Maya
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true).ToArray())
            if (r && r.name.ToLowerInvariant().Contains("imageplane")) Object.DestroyImmediate(r.gameObject);
    }

    static Animator PrepararAnimator(GameObject inst)
    {
        var anim = inst.GetComponent<Animator>();
        if (!anim) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = null;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return anim;
    }

    // Clips del FBX: el que mas anima y, ademas, los que animan otras partes (p. ej. el manto de Fede,
    // o los objetos del cultista que Blender exporto en tomas separadas)
    static AnimationClip[] ElegirClips(string ruta)
    {
        var info = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview"))
            .Select(c =>
            {
                var b = AnimationUtility.GetCurveBindings(c);
                return (clip: c, n: b.Length, rutas: new HashSet<string>(b.Select(x => x.path)));
            })
            .Where(x => x.n > 0)
            .OrderByDescending(x => x.n).ThenByDescending(x => x.clip.length).ToList();
        var elegidos = new List<AnimationClip>();
        var usadas = new HashSet<string>();
        foreach (var x in info)
        {
            if (elegidos.Count > 0 && x.rutas.Overlaps(usadas)) continue;
            elegidos.Add(x.clip);
            usadas.UnionWith(x.rutas);
            if (elegidos.Count >= 16) break;
        }
        return elegidos.ToArray();
    }

    // ------------------------------------------------------------------ Materiales

    static string Limpio(string n) { int i = n.LastIndexOf(':'); return i >= 0 ? n.Substring(i + 1) : n; }

    // Materiales del modelo viejo por nombre de objeto (para conservar las texturas que ya tenia)
    static Dictionary<string, Renderer> Materiales(IEnumerable<Transform> viejos)
    {
        var mapa = new Dictionary<string, Renderer>();
        foreach (Transform v in viejos)
            foreach (Renderer r in v.GetComponentsInChildren<Renderer>(true))
            {
                if (EnExtra(r.transform)) continue;   // piezas del reloj, etc.
                string n = Limpio(r.name);
                if (!mapa.ContainsKey(n) && r.sharedMaterials.Any(m => m)) mapa[n] = r;
            }
        return mapa;
    }

    static bool EnExtra(Transform t)
    {
        for (; t; t = t.parent) if (ExtrasBrazos.Contains(t.name)) return true;
        return false;
    }

    // Viejos sin pareja por nombre en el modelo nuevo: sus materiales sirven de respaldo
    // para las mallas nuevas sin pareja del mismo tipo (con huesos / sin huesos)
    static List<Renderer> Sueltos(Dictionary<string, Renderer> mapa, GameObject inst)
    {
        var nuevos = new HashSet<string>(inst.GetComponentsInChildren<Renderer>(true).Select(r => Limpio(r.name)));
        return mapa.Where(kv => !nuevos.Contains(kv.Key)).Select(kv => kv.Value).ToList();
    }

    static void AsignarMateriales(Renderer r, Dictionary<string, Renderer> mapa, List<Renderer> sueltos)
    {
        Renderer viejo;
        if (!mapa.TryGetValue(Limpio(r.name), out viejo))
        {
            if (sueltos == null) return;
            var mismos = sueltos.Where(s => (s is SkinnedMeshRenderer) == (r is SkinnedMeshRenderer)).ToList();
            if (mismos.Count == 0 || mismos.Count > 2) return;   // muchos sin pareja: no se puede adivinar
            viejo = mismos[0];
            sueltos.Remove(viejo);
        }
        var origen = viejo.sharedMaterials;
        var arr = r.sharedMaterials;
        for (int i = 0; i < arr.Length; i++) arr[i] = origen[Mathf.Min(i, origen.Length - 1)];
        r.sharedMaterials = arr;
        r.shadowCastingMode = viejo.shadowCastingMode;
    }

    // ------------------------------------------------------------------ Utilidades

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == nombre) return t;
        return null;
    }

    static bool Dentro(Transform t, IEnumerable<Transform> viejos) => viejos.Any(v => v && (t == v || t.IsChildOf(v)));

    // Objetos dentro del modelo viejo a los que apunta algun script de fuera (p. ej. puntoMano del cliente)
    static List<Transform> Referenciados(Transform raiz, List<Transform> viejos)
    {
        var res = new List<Transform>();
        foreach (MonoBehaviour mb in raiz.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!mb || Dentro(mb.transform, viejos)) continue;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || !it.objectReferenceValue) continue;
                Transform t = it.objectReferenceValue is GameObject g ? g.transform : it.objectReferenceValue is Component c ? c.transform : null;
                if (t && Dentro(t, viejos) && !res.Contains(t)) res.Add(t);
            }
        }
        return res;
    }

    // Saca del modelo viejo lo referenciado (y en los NPC nada mas) antes de borrarlo; queda donde estaba
    static void Rescatar(Transform raiz, List<Transform> viejos, bool escena)
    {
        foreach (Transform t in Referenciados(raiz, viejos))
        {
            if (viejos.Contains(t)) continue;
            if (escena) Undo.SetTransformParent(t, raiz, "Modelo"); else t.SetParent(raiz, true);
            Debug.Log($"[Modelos] \"{t.name}\" estaba dentro del modelo viejo y lo usa un script: se conserva en {raiz.name}.", t);
        }
    }

    // Limites de los renderers en el espacio local de "espacio" (mallas con huesos en su pose actual)
    static bool LimitesEn(IEnumerable<Transform> objs, Transform espacio, out Bounds b)
    {
        Bounds acum = default; bool hay = false;
        Matrix4x4 aLocal = espacio.worldToLocalMatrix;
        void Agregar(Vector3 p) { if (!hay) { acum = new Bounds(p, Vector3.zero); hay = true; } else acum.Encapsulate(p); }
        foreach (Transform o in objs)
        {
            if (!o) continue;
            foreach (Renderer r in o.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy && o.gameObject.activeInHierarchy) continue;
                Matrix4x4 m = aLocal * r.transform.localToWorldMatrix;
                if (r is SkinnedMeshRenderer sk && sk.sharedMesh)
                {
                    var mesh = new Mesh();
                    sk.BakeMesh(mesh, false);
                    foreach (Vector3 v in mesh.vertices) Agregar(m.MultiplyPoint3x4(v));
                    Object.DestroyImmediate(mesh);
                }
                else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh)
                {
                    Bounds mb = mf.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                        Agregar(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                }
            }
        }
        b = acum;
        return hay;
    }
}
