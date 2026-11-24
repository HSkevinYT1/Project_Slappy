using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Cambia los modelos por los de "Assets/animaciones y modelos" con sus animaciones (AnimacionesModelo):
// - Enemy 1 (fede)  (laberinto):  Caminar (fedewalkv2, clip metarig) y Grito (espeletito_griton, clip metarig; el
//   ".003" no: mueve la raiz por el aire). Madera vieja de muneco de ventrilocuo roto (pintura crema descascarada,
//   rajaduras, polilla) con textura triplanar (shader "Slappy/Fede Hueso"), ojos indigo
//   que brillan (emision + una luz tenue) y los pies siempre en el piso (AnimacionesModelo.pegarAlSuelo).
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
    const string Marca = "_anim v5";   // v5: sin materiales rotos (morados): solo se copian los que existen. Se rehacen todos
    // Fede va aparte: subir solo esta para rehacer a Fede sin tocar a los demas
    // v2: medido bien (salia diminuto). v3: cuerpo/dientes bien asignados. v4: los ojos son las cuencas (brillan en el shader)
    const string MarcaFede = "_anim fede v8";   // v5: ojos mas indigo y la luz mas suave. v6: madera vieja de muneco. v7: Correr (FEDERun). v8: Correr nuevo (Fede_Run)
    const string Base = "Assets/Animations/";   // (antes "Assets/animaciones y modelos/")
    const string Prefabs = "Assets/Prefabs/";
    const string MatsFede = "Assets/Modelos De enemigos/Materiales Fede/";
    const string TexturasFede = "Assets/Modelos De enemigos/Texturas Fede/";
    const string ShaderFede = "Slappy/Fede Hueso (triplanar)";
    static readonly Color Indigo = new Color(0.33f, 0.05f, 0.85f);

    struct Def
    {
        public string nombre, archivo, clip; public bool bucle;
        // clip: el que se usa de ese FBX (vacio = el que mas anima, y los que animan otras partes)
        // modelo: FBX del que se toma el MODELO (vacio = el mismo "archivo"). Sirve cuando el FBX de una animacion trae
        // otro modelo distinto (ej. el de correr de Fede): se usa la animacion de "archivo" sobre el modelo de siempre
        public string modelo;
        public Def(string n, string a, bool b = true, string c = null, string m = null) { nombre = n; archivo = a; bucle = b; clip = c; modelo = m; }
        public string Modelo => string.IsNullOrEmpty(modelo) ? archivo : modelo;
    }

    static readonly Def[] Fede =
    {
        new Def(AnimacionesModelo.Caminar, "fede/fedewalkv2.fbx", true, "metarig|metarigAction"),
        // Persiguiendo (JefeIndigoAI en Chase). Fede_Run.fbx trae 6 animaciones: la de correr es
        // "metarig|metarig|metarig|metarigAction" (0.8 s, la que mas mueve; "metarig|Action" casi no se mueve). Trae
        // OTRO modelo de Fede (3 mallas distintas): se usa solo su animacion, sobre el modelo de caminar (mismo
        // esqueleto "metarig", mismas rutas de huesos), asi Fede no cambia de aspecto al empezar a correr
        new Def(AnimacionesModelo.Correr, "fede/Fede_Run.fbx", true, "metarig|metarig|metarig|metarigAction", "fede/fedewalkv2.fbx"),
        new Def(AnimacionesModelo.Grito, "fede/espeletito_griton.fbx", false, "metarig|metarigAction"),
    };
    static readonly Def[] Gato =
    {
        new Def(AnimacionesModelo.Idle, "gato/CatIdle.fbx"),
        new Def(AnimacionesModelo.Caminar, "gato/CatWalk.fbx"),
    };
    static readonly Def[] Slappy =
    {
        new Def(AnimacionesModelo.Caminar, "slappy/Enemigo4_Walk.fbx"),
        new Def(AnimacionesModelo.Correr, "slappy/Enemigo4_Run.fbx"),
        new Def(AnimacionesModelo.Jumpscare, "slappy/Enemigo4_Jumpscare.fbx", false),
    };
    static readonly Def[] Cliente =
    {
        new Def(AnimacionesModelo.Idle, "npc/NPCIdle.fbx"),
        new Def(AnimacionesModelo.Caminar, "npc/NPCWalk.fbx"),
    };
    static readonly Def[] Cultista =
    {
        // (sin idle por ahora: solo esta cultistawalk). Su toma "Layer0.002" (no "Armature|mixamo.com")
        new Def(AnimacionesModelo.Caminar, "cultista/cultistawalk.fbx", true, "Armature|Armature|mixamo.com|Layer0.002"),
    };
    const string ArchivoBrazos = "NPC/Arms.fbx";
    // Lo que cuelga de los brazos y hay que conservar
    static readonly HashSet<string> ExtrasBrazos = new HashSet<string> { "Agarre_Linterna", "Reloj 3D", "HandPoint", "Hand" };

    static ModelosAnimadosSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += AutoAplicar;
    }

    // Se aplica solo (pone los modelos nuevos). Se animan (AnimacionesModelo.reproducir) solo las animaciones que funcionan (las
    // animaciones no estaban bien). Los brazos del jugador no se cambian solos (solo desde el menu).
    const bool Automatico = true;

    static void AutoAplicar()
    {
        if (!Automatico || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!AssetDatabase.IsValidFolder(Base.TrimEnd('/'))) return;
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
        // Los brazos solo a mano (Tools > Modelos): con Arms.fbx la mano del jugador dejaba de verse
        if (forzar) BrazosPrefab(forzar);
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
            // Fede: mallas legibles (se copian con la pose de reposo para la textura, y los pies se miden en el juego)
            bool leer = Fede.Any(d => d.archivo == a);
            if (!imp.importCameras && !imp.importLights && (!leer || imp.isReadable)) continue;
            imp.importCameras = false;
            imp.importLights = false;
            if (leer) imp.isReadable = true;
            imp.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ Enemigos (prefabs)

    static void Enemigo(string ruta, Def[] defs, float altoFijo, bool esFede, bool forzar)
    {
        // Sin ningun FBX de este personaje en la carpeta (ej. Fede por ahora): se queda con su modelo de siempre
        if (!Hay(defs)) return;
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(ruta)) { Debug.LogWarning("[Modelos] No encuentro " + ruta); return; }
        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            string marca = esFede ? MarcaFede : Marca;
            Transform viejo = raiz.transform.Find("Modelo");
            if (viejo && viejo.Find(marca) && !forzar) return;

            var viejos = new List<Transform>();
            if (viejo) viejos.Add(viejo);
            // Copias sueltas de FBX arrastradas al prefab
            foreach (Transform h in raiz.transform)
                if (h != viejo && h.GetComponentInChildren<Animator>(true) && !h.GetComponent<NavMeshAgent>()) viejos.Add(h);

            var mapa = Materiales(viejos);
            Rescatar(raiz.transform, viejos, false);
            // Los viejos se borran DESPUES de crear el modelo nuevo: el mapa de materiales apunta a sus renderers
            // (borrarlos antes daba MissingReferenceException en AsignarMateriales y cortaba todo el setup)
            foreach (Transform v in viejos) v.name += " (viejo)";
            foreach (AnimacionCaminar ac in raiz.GetComponentsInChildren<AnimacionCaminar>(true)) Object.DestroyImmediate(ac);

            var agent = raiz.GetComponent<NavMeshAgent>();
            float alto = altoFijo > 0f ? altoFijo : (agent ? agent.height : 2f);
            float pies = agent ? -agent.baseOffset : -1f;

            System.Action<Renderer, List<Renderer>> mats;
            if (esFede)
            {
                var (hueso, dientes) = MaterialesFede();
                Transform raizT = raiz.transform;
                mats = (r, _) =>
                {
                    bool diente = EsDienteFede(r);
                    Material m = diente ? dientes : hueso;
                    if (m)
                    {
                        var arr = r.sharedMaterials;
                        for (int i = 0; i < arr.Length; i++) arr[i] = m;
                        r.sharedMaterials = arr;
                    }
                    // El cuerpo usa una copia de la malla con la pose de reposo guardada (la textura se pega al cuerpo)
                    if (!diente && r is SkinnedMeshRenderer sk && sk.sharedMesh)
                        sk.sharedMesh = MallaReposo(sk.sharedMesh, sk.transform.InverseTransformDirection(raizT.up));
                };
            }
            else mats = (r, sueltos) => AsignarMateriales(r, mapa, sueltos);

            var comp = CrearModelo(raiz.transform, defs, alto, pies, mats, mapa, marca, esFede);
            if (esFede)
            {
                comp.pegarAlSuelo = true;
                comp.usarPisoReal = true;
                OjosFede(raiz.transform, comp, AssetDatabase.LoadAssetAtPath<Material>(MatsFede + "FedeHueso.mat"));
            }
            foreach (Transform v in viejos) if (v) Object.DestroyImmediate(v.gameObject);
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
            cambio |= NPC(tut.cliente, Cliente, forzar, "Assets/Modelos De enemigos/Idle (1).fbx");
            cambio |= NPC(tut.cultista, Cultista, forzar, "Assets/Modelos De enemigos/IdleCultist.fbx");
        }
        // Brazos del jugador de la escena si no vienen del prefab
        var pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (forzar && pm && !PrefabUtility.IsPartOfPrefabInstance(pm))
        {
            Transform pa = Buscar(pm.transform, "Player_Arms");
            if (pa) cambio |= Brazos(pa, forzar, true);
        }
        if (!cambio) return;
        if (tut) EditorSceneManager.MarkSceneDirty(tut.gameObject.scene);
        if (pm) EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
        Debug.Log("[Modelos] Escena actualizada (NPCs de la tienda). Guarda la escena (Ctrl+S).");
    }

    static bool NPC(Transform npc, Def[] defs, bool forzar, string respaldo)
    {
        if (!npc) return false;
        if (!Hay(defs)) return false;
        Transform actual = npc.Find("Modelo");
        if (actual && actual.Find(Marca) && !forzar) return false;

        var viejos = new List<Transform>();
        foreach (Transform h in npc)
            if (h.name == "Modelo" || h.GetComponentInChildren<Animator>(true)) viejos.Add(h);

        float alto = 1.8f, pies = 0f;
        if (LimitesEn(viejos, npc, out Bounds b) && b.size.y > 0.2f) { alto = b.size.y; pies = b.min.y; }
        // Una persona: si la medida da algo absurdo (el cliente salia de 155 m), 1.8 m con los pies en el pivote
        if (alto < 1.3f || alto > 2.4f) { alto = 1.8f; pies = 0f; }

        var mapa = Materiales(viejos);
        // Si el modelo viejo ya tenia materiales rotos, los del modelo original del equipo
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(respaldo);
        if (original) foreach (var kv in Materiales(new[] { original.transform })) if (!mapa.ContainsKey(kv.Key)) mapa[kv.Key] = kv.Value;
        Rescatar(npc, viejos, true);
        // Se apartan (y se borran despues de crear el nuevo: el mapa de materiales apunta a sus renderers)
        foreach (Transform v in viejos) { Undo.RecordObject(v.gameObject, "Modelo NPC"); v.gameObject.name += " (viejo)"; }
        Undo.RecordObject(npc, "Modelo NPC");
        var comp = CrearModelo(npc, defs, alto, pies, (r, sueltos) => AsignarMateriales(r, mapa, sueltos), mapa);
        foreach (Transform v in viejos)
        {
            if (!v) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(v) && !PrefabUtility.IsOutermostPrefabInstanceRoot(v.gameObject))
                v.gameObject.SetActive(false);
            else Undo.DestroyObjectImmediate(v.gameObject);
        }
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
        System.Action<Renderer, List<Renderer>> mats, Dictionary<string, Renderer> mapa, string marca = Marca, bool mismaEscala = false)
    {
        float escalaComun = 0f;
        Vector3 centroComun = Vector3.zero;
        var cont = new GameObject("Modelo").transform;
        cont.SetParent(padre, false);
        var lista = new List<AnimacionesModelo.Pose>();
        foreach (Def d in defs)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Base + d.Modelo);
            if (!fbx) { Debug.LogWarning("[Modelos] No encuentro " + Base + d.Modelo); continue; }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Base + d.archivo)) { Debug.LogWarning("[Modelos] No encuentro " + Base + d.archivo); continue; }

            // Envoltura con la escala/posicion; el FBX dentro (sus propias curvas no la mueven)
            var env = new GameObject(d.nombre).transform;
            env.SetParent(cont, false);
            var inst = Object.Instantiate(fbx, env);
            inst.name = Path.GetFileNameWithoutExtension(d.archivo);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            Limpiar(inst);
            var anim = PrepararAnimator(inst);
            var clips = ElegirClips(Base + d.archivo, d.clip);
            foreach (var c in clips) c.SampleAnimation(inst, 0f);

            var sueltos = Sueltos(mapa, inst);
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                mats?.Invoke(r, sueltos);
                SinMorado(r);
                if (r is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true;
            }

            // Tamano y pies en el suelo. mismaEscala (mismo modelo y esqueleto en todos los FBX, como Fede): todas las
            // poses con la escala y el centro de la primera; si no, al cambiar de pose cambiaba de tamano o se corria
            if (LimitesEn(new[] { env }, padre, out Bounds b) && b.size.y > 0.0001f)
            {
                bool comun = mismaEscala && escalaComun > 0f;
                env.localScale = Vector3.one * (comun ? escalaComun : alto / b.size.y);
                LimitesEn(new[] { env }, padre, out b);
                env.localPosition = comun ? new Vector3(centroComun.x, pies - b.min.y, centroComun.z)
                                          : new Vector3(-b.center.x, pies - b.min.y, -b.center.z);
                if (escalaComun <= 0f) { escalaComun = env.localScale.x; centroComun = env.localPosition; }
            }

            if (lista.Count > 0) foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            lista.Add(new AnimacionesModelo.Pose { nombre = d.nombre, animator = anim, clips = clips, bucle = d.bucle });
            Debug.Log($"[Modelos] {padre.name} / {d.nombre}: {(clips.Length > 0 ? string.Join(" + ", clips.Select(c => c.name + " (" + c.length.ToString("0.00") + " s)")) : "SIN ANIMACION")}");
        }
        new GameObject(marca).transform.SetParent(cont, false);

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
        // Plano de suelo que traen algunos FBX de Blender (un cuadrado de 4 vertices): no es del personaje
        // (con el se media mal el tamano y se veia un piso flotando)
        foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true).ToArray())
            if (mf && mf.name.StartsWith("Plane") && mf.sharedMesh && mf.sharedMesh.vertexCount <= 4 && mf.transform.childCount == 0)
                Object.DestroyImmediate(mf.gameObject);
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
    static AnimationClip[] ElegirClips(string ruta, string preferido = null)
    {
        // Un clip concreto (ej. "metarig|metarigAction" de Fede, y no el ".003" que mueve la raiz por el aire)
        if (!string.IsNullOrEmpty(preferido))
        {
            var todos = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
            string fin = "|" + preferido.Substring(preferido.LastIndexOf('|') + 1);
            var elegido = todos.FirstOrDefault(c => c.name == preferido)
                       ?? todos.FirstOrDefault(c => c.name.StartsWith("metarig") && c.name.EndsWith(fin) && !c.name.Contains(".003"));
            if (elegido) return new[] { elegido };
            Debug.LogWarning($"[Modelos] {ruta}: no encuentro el clip \"{preferido}\" (hay: {string.Join(", ", todos.Select(c => c.name))}). Uso el que mas anima.");
        }
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
                // Solo los que se ven bien: un material borrado (ej. el que venia dentro de un FBX que ya no existe)
                // o con un shader roto se veia morado y se pasaba de un modelo al siguiente
                if (!mapa.ContainsKey(n) && r.sharedMaterials.Length > 0 && r.sharedMaterials.All(MaterialBueno)) mapa[n] = r;
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
        if (origen.Length == 0 || !origen.All(MaterialBueno)) return;
        var arr = r.sharedMaterials;
        for (int i = 0; i < arr.Length; i++) arr[i] = origen[Mathf.Min(i, origen.Length - 1)];
        r.sharedMaterials = arr;
        r.shadowCastingMode = viejo.shadowCastingMode;
    }

    static bool MaterialBueno(Material m) =>
        m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader";

    // Lo que quede sin material o con uno roto (se veria morado): un gris liso del pipeline (URP Lit)
    static void SinMorado(Renderer r)
    {
        var arr = r.sharedMaterials;
        bool cambio = false;
        for (int i = 0; i < arr.Length; i++)
            if (!MaterialBueno(arr[i])) { arr[i] = Respaldo(); cambio = true; }
        if (cambio) { r.sharedMaterials = arr; Debug.LogWarning($"[Modelos] {r.name}: tenia un material roto (morado), queda gris.", r); }
    }

    static Material respaldoMat;
    static Material Respaldo()
    {
        const string ruta = "Assets/Materials/ModeloRespaldo.mat";
        if (respaldoMat) return respaldoMat;
        respaldoMat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (respaldoMat) return respaldoMat;
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        respaldoMat = new Material(s) { name = "ModeloRespaldo" };
        if (respaldoMat.HasProperty("_BaseColor")) respaldoMat.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.5f));
        Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(respaldoMat, ruta);
        return respaldoMat;
    }

    // ------------------------------------------------------------------ Fede: textura, ojos

    // Los dientes de Fede: el objeto "fede.001" de los FBX nuevos (su malla se llama fede.002 dentro del FBX; el
    // cuerpo es el objeto "fede.002"). Los ojos NO son una malla: son las dos cuencas del craneo (ver OjosFede)
    static bool EsDienteFede(Renderer r) => r.name == "fede.001";

    static Color Hdr(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

    // Hueso: textura triplanar (RGB color, A altura) con el shader de Fede (las cuencas de los ojos brillan indigo
    // desde el mismo shader; el bloom del laberinto hace el halo). Dientes: hueso liso un poco mas claro
    static (Material hueso, Material dientes) MaterialesFede()
    {
        Directory.CreateDirectory(MatsFede);
        string rutaTex = TexturasFede + "FedeMadera.png";   // madera vieja con pintura descascarada
        if (AssetImporter.GetAtPath(rutaTex) is TextureImporter ti &&
            (ti.anisoLevel < 4 || ti.wrapMode != TextureWrapMode.Repeat || !ti.mipmapEnabled || ti.alphaSource != TextureImporterAlphaSource.FromInput))
        {
            ti.anisoLevel = 4;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader sh = Shader.Find(ShaderFede);

        var hueso = AssetDatabase.LoadAssetAtPath<Material>(MatsFede + "FedeHueso.mat");
        if (!hueso) { hueso = new Material(sh ? sh : lit) { name = "FedeHueso" }; AssetDatabase.CreateAsset(hueso, MatsFede + "FedeHueso.mat"); }
        if (sh && tex)
        {
            hueso.shader = sh;
            hueso.SetTexture("_BaseMap", tex);
            hueso.SetColor("_BaseColor", Color.white);
            // Madera: veta de ~50 cm, relieve marcado (rajaduras, pintura levantada), mate, mugre en las piernas
            hueso.SetFloat("_Escala", 4.5f);
            hueso.SetFloat("_Relieve", 1.8f);
            hueso.SetFloat("_Suavidad", 0.28f);
            hueso.SetFloat("_Suciedad", 0.5f);
            hueso.SetFloat("_AlturaSuciedad", 0.3f);
            hueso.SetColor("_ColorSuciedad", new Color(0.12f, 0.09f, 0.06f));
            hueso.SetFloat("_Oclusion", 0.7f);
        }
        else Debug.LogWarning($"[Modelos] Fede: falta {(sh ? "" : "el shader " + ShaderFede + " ")}{(tex ? "" : "la textura " + rutaTex)}: el hueso queda liso.");
        EditorUtility.SetDirty(hueso);

        var dientes = AssetDatabase.LoadAssetAtPath<Material>(MatsFede + "FedeDientes.mat");
        if (!dientes) { dientes = new Material(lit) { name = "FedeDientes" }; AssetDatabase.CreateAsset(dientes, MatsFede + "FedeDientes.mat"); }
        dientes.shader = lit;
        // Dientes pintados de blanco, gastados
        dientes.SetColor("_BaseColor", new Color(0.74f, 0.69f, 0.58f));
        dientes.SetFloat("_Smoothness", 0.35f);
        dientes.SetColor("_EmissionColor", Color.black);
        dientes.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(dientes);
        AssetDatabase.SaveAssets();
        return (hueso, dientes);
    }

    // Como se normalizo cada malla de reposo (para ubicar cosas en ese espacio, ej. los ojos)
    struct Reposo { public Quaternion q; public Vector3 centro; public float alto; }
    static readonly Dictionary<Mesh, Reposo> reposos = new Dictionary<Mesh, Reposo>();

    // Cuencas de los ojos respecto al centro de los dientes, en el espacio de reposo normalizado (1 = alto del cuerpo;
    // medidas en el FBX: 0.165 a cada lado, 0.395 por encima y 0.08 hacia atras, cuerpo de 6.9) y su radio
    static readonly Vector3 OjoDesdeDientes = new Vector3(0.0239f, 0.0573f, -0.0116f);
    static readonly Vector4 RadioOjo = new Vector4(0.0225f, 0.0158f, 0.0130f, 1f);
    // Indigo con poco rojo: si el rojo pasa de 1 con el HDR, el centro se satura y se ve rosado
    static readonly Color BrilloOjos = new Color(0.55f, 0.08f, 2.6f, 1f);

    // Los ojos brillan en el shader del hueso (las cuencas, ubicadas desde los dientes: asi no importa como Unity voltee
    // los ejes del FBX) y una luz indigo tenue delante de ellos, pegada al hueso de la cabeza de cada pose
    static void OjosFede(Transform raiz, AnimacionesModelo comp, Material hueso)
    {
        bool ojosPuestos = false;
        for (int k = 0; k < comp.poses.Length; k++)
        {
            var p = comp.poses[k];
            if (p == null || !p.animator) continue;
            var rends = p.animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var cuerpo = rends.FirstOrDefault(r => !EsDienteFede(r) && r.sharedMesh && reposos.ContainsKey(r.sharedMesh));
            var dientes = rends.FirstOrDefault(r => EsDienteFede(r) && r.sharedMesh);
            if (!cuerpo || !dientes) { Debug.LogWarning("[Modelos] Fede / " + p.nombre + ": no encuentro cuerpo y dientes para ubicar los ojos."); continue; }
            Reposo info = reposos[cuerpo.sharedMesh];

            // Dientes en el espacio de reposo normalizado del cuerpo
            Matrix4x4 aCuerpo = cuerpo.transform.worldToLocalMatrix * dientes.transform.localToWorldMatrix;
            Bounds bd = default; bool hay = false;
            foreach (Vector3 v in dientes.sharedMesh.vertices)
            {
                Vector3 n = (info.q * aCuerpo.MultiplyPoint3x4(v) - info.centro) / info.alto;
                if (!hay) { bd = new Bounds(n, Vector3.zero); hay = true; } else bd.Encapsulate(n);
            }
            if (!hay) continue;
            float frente = bd.center.z >= 0f ? 1f : -1f;   // los dientes estan delante del centro del cuerpo
            Vector3 izq = bd.center + new Vector3(-OjoDesdeDientes.x, OjoDesdeDientes.y, OjoDesdeDientes.z * frente);
            Vector3 der = bd.center + new Vector3(OjoDesdeDientes.x, OjoDesdeDientes.y, OjoDesdeDientes.z * frente);
            if (!ojosPuestos && hueso)
            {
                hueso.SetVector("_OjoIzq", izq);
                hueso.SetVector("_OjoDer", der);
                hueso.SetVector("_OjoRadio", RadioOjo);
                hueso.SetColor("_OjoColor", BrilloOjos);
                EditorUtility.SetDirty(hueso);
                ojosPuestos = true;
            }

            // Luz: entre los dos ojos y un poco delante, con la pose actual (el hueso de la cabeza mueve esa zona)
            Transform cabeza = HuesoDe(dientes, dientes.bounds.center, p.animator.transform);
            int b = System.Array.IndexOf(cuerpo.bones, cabeza);
            if (b < 0 || b >= cuerpo.sharedMesh.bindposes.Length) continue;
            Quaternion qi = Quaternion.Inverse(info.q);
            Vector3 medio = (izq + der) * 0.5f;
            Vector3 enMalla = qi * (medio * info.alto + info.centro);
            Matrix4x4 m = cabeza.localToWorldMatrix * cuerpo.sharedMesh.bindposes[b];
            Vector3 ojoMundo = m.MultiplyPoint3x4(enMalla);
            Vector3 adelante = m.MultiplyVector(qi * new Vector3(0f, 0f, frente)).normalized;
            var go = new GameObject("Luz Ojos");
            go.transform.SetParent(cabeza, false);
            go.transform.position = ojoMundo + adelante * 0.35f;   // (mas cerca teñia toda la cara de morado)
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Indigo;
            l.range = 2.2f;
            l.intensity = 0.3f;
            l.shadows = LightShadows.None;
            l.enabled = k == 0;   // como los renderers: al empezar solo se ve la primera pose
        }
        if (!ojosPuestos) Debug.LogWarning("[Modelos] Fede: no pude ubicar los ojos (no brillan).");
        AssetDatabase.SaveAssets();
    }

    // Copia de la malla con la pose de reposo en TEXCOORD2 (posicion) y TEXCOORD3 (normal), normalizada: 1 = alto del
    // cuerpo, y = arriba, 0 = centro. El shader proyecta la textura con eso: no resbala aunque los huesos la deformen
    static Mesh MallaReposo(Mesh origen, Vector3 arribaLocal)
    {
        string fbx = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(origen));
        string carpeta = TexturasFede + "Mallas";
        Directory.CreateDirectory(carpeta);
        string nombre = (fbx + "_" + origen.name).Replace('.', '_').Replace(':', '_').Replace('|', '_');
        string ruta = carpeta + "/" + nombre + ".asset";

        var m = Object.Instantiate(origen);
        m.name = nombre;
        Vector3[] v = m.vertices, n = m.normals;
        if (arribaLocal.sqrMagnitude < 1e-8f) arribaLocal = Vector3.up;
        Quaternion q = Quaternion.FromToRotation(arribaLocal.normalized, Vector3.up);
        Bounds b = default;
        for (int i = 0; i < v.Length; i++) { Vector3 p = q * v[i]; if (i == 0) b = new Bounds(p, Vector3.zero); else b.Encapsulate(p); }
        float alto = Mathf.Max(0.0001f, b.size.y);
        var rp = new List<Vector3>(v.Length);
        var rn = new List<Vector3>(v.Length);
        for (int i = 0; i < v.Length; i++)
        {
            rp.Add((q * v[i] - b.center) / alto);
            rn.Add(i < n.Length ? q * n[i] : Vector3.up);
        }
        m.SetUVs(2, rp);
        m.SetUVs(3, rn);
        var info = new Reposo { q = q, centro = b.center, alto = alto };

        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente)
        {
            EditorUtility.CopySerialized(m, existente);
            existente.name = nombre;
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(existente);
            reposos[existente] = info;
            return existente;
        }
        AssetDatabase.CreateAsset(m, ruta);
        reposos[m] = info;
        return m;
    }

    // El hueso que mas mueve esa malla (los ojos -> la cabeza); si no se puede saber, el mas cercano al punto
    static Transform HuesoDe(Renderer r, Vector3 punto, Transform modelo)
    {
        if (r is SkinnedMeshRenderer sk && sk.sharedMesh && sk.sharedMesh.isReadable && sk.bones.Length > 0)
        {
            var cuenta = new Dictionary<int, int>();
            foreach (BoneWeight w in sk.sharedMesh.boneWeights) { cuenta.TryGetValue(w.boneIndex0, out int c); cuenta[w.boneIndex0] = c + 1; }
            if (cuenta.Count > 0)
            {
                int hb = cuenta.OrderByDescending(kv => kv.Value).First().Key;
                if (hb < sk.bones.Length && sk.bones[hb]) return sk.bones[hb];
            }
        }
        Transform mejor = modelo; float menor = float.MaxValue;
        foreach (Transform t in modelo.GetComponentsInChildren<Transform>(true))
        {
            if (t == modelo || t.name.EndsWith("_end") || t.GetComponent<Renderer>()) continue;
            float d = (t.position - punto).sqrMagnitude;
            if (d < menor) { menor = d; mejor = t; }
        }
        return mejor;
    }

    // ------------------------------------------------------------------ Utilidades

    static bool Hay(Def[] defs) => defs.Any(d => AssetDatabase.LoadAssetAtPath<GameObject>(Base + d.archivo) != null);

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
            // AnimacionesModelo apunta a las poses viejas, pero se rehace con el modelo nuevo: no cuenta (si no, las
            // poses viejas se "rescataban" y quedaba el personaje dos veces)
            if (!mb || mb is AnimacionesModelo || Dentro(mb.transform, viejos)) continue;
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
                    // true: vertices en el espacio local de verdad (con false venian ya escalados y la matriz los escalaba
                    // otra vez: con el nodo a escala 100 de los FBX nuevos de Fede lo media 100 veces mas grande y lo encogia)
                    sk.BakeMesh(mesh, true);
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
