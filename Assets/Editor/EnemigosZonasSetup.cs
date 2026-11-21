using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// Prepara las 4 areas de la escena:
// - NavMesh SOLO en los pisos: cada area tiene su NavMeshSurface (en la raiz del area) que trata todo como
//   "Not Walkable" (paredes, techos, arboles... siguen siendo obstaculos) menos los pisos, que llevan un
//   NavMeshModifier "Walkable". Asi ya no sale NavMesh encima de los techos ni de las paredes.
// - Enemigos en su area: Laberinto = Enemy 1 (fede), Backrooms = Enemy 2 (gato), Cloacas = varias estatuas
//   (Enemigo 3, una lleva la llave), Zona Roja = Enemy 4 (con el modelo "Enemigo 4").
// - Triggers que cambian el Volume al entrar a cada area (Level1..Level4Volume).
//
// Se aplica SOLO una vez (si no existe "Enemigos y Zonas") y se puede rehacer desde Tools > Enemigos y Zonas.
// Todo se puede deshacer con Ctrl+Z. Despues hay que guardar la escena.
public static class EnemigosZonasSetup
{
    const string Raiz = "Enemigos y Zonas";
    const int Caminable = 0;
    const int NoCaminable = 1;
    // Capa "Ignore Raycast": los triggers no bloquean el raycast de interaccion ni entran al NavMesh
    const int CapaTriggers = 2;

    const string Prefabs = "Assets/Prefabs/";
    const string PrefabEstatua = Prefabs + "Enemigo 3.prefab";
    const string PrefabEnemigo4 = Prefabs + "Enemy 4.prefab";
    const string ModeloEnemigo4 = "Assets/Modelos De enemigos/monstruos/Enemigo 4.obj";
    const string MaterialLlave = "Assets/Materials/Llave Cloacas.mat";
    const int EstatuasCloacas = 4;

    class Area
    {
        public string raiz;
        public string[] pisos;      // rutas dentro de la raiz del area
        public string perfil;       // Volume Profile
        public string prefab;       // enemigo del area
        public int cantidad = 1;
        public float separacion = 8f;
        public float distanciaEntrada = 15f;  // que no aparezcan pegados a la entrada
        // Recinto cerrado por paredes: los enemigos solo aparecen DENTRO de ellas (ver ZonaCerrada).
        // En las Cloacas el piso de agua sobresale unos metros por fuera de las paredes y tiene NavMesh ahi tambien.
        public bool cerrada;
    }

    static readonly Area[] Areas =
    {
        new Area { raiz = "Laberinto", pisos = new[] { "Piso" },
                   perfil = "Assets/Volumenes/Level1Volume.asset", prefab = Prefabs + "Enemy 1 (fede).prefab" },
        new Area { raiz = "Backrooms", pisos = new[] { "Piso" },
                   perfil = "Assets/Volumenes/Level2Volume.asset", prefab = Prefabs + "Enemy 2 (gato).prefab" },
        new Area { raiz = "Cloacas", pisos = new[] { "Piso", "agua" },
                   perfil = "Assets/Volumenes/Level3Volume.asset", prefab = PrefabEstatua,
                   cantidad = EstatuasCloacas, separacion = 10f, distanciaEntrada = 10f, cerrada = true },
        new Area { raiz = "Zona Roja",
                   pisos = new[] { "Isla", "Faro/Piso 1/Suelo", "Faro/Piso 2/Suelo", "Faro/Piso 3 - Linterna/Suelo",
                                   "Faro/Piso 1/Escalera de caracol (1 a 2)", "Faro/Piso 2/Escalera de caracol (2 a 3)" },
                   perfil = "Assets/Volumenes/Level4Volume.asset", prefab = PrefabEnemigo4, distanciaEntrada = 0f },
    };

    // Lo llama ZonasSetup al abrir/compilar la escena
    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Ya armado: rehornea los NavMesh que se hayan perdido y revisa que las estatuas de las Cloacas
        // no hayan quedado por fuera de las paredes
        if (GameObject.Find(Raiz)) { HornearFaltantes(); RecolocarCloacas(false); return; }
        foreach (Area a in Areas) if (!GameObject.Find(a.raiz)) return;
        Aplicar();
    }

    [MenuItem("Tools/Enemigos y Zonas/Recolocar estatuas de las Cloacas (dentro de las paredes)")]
    static void RecolocarMenu()
    {
        if (!RecolocarCloacas(true))
            EditorUtility.DisplayDialog("Cloacas", "No encontre \"Cloacas\" o \"Cloacas/Enemigos Cloacas\".", "OK");
    }

    // Mueve las estatuas que ya existen (sin recrearlas, asi la llave y la cerradura siguen conectadas) a puntos
    // del NavMesh que esten DENTRO de las paredes de las Cloacas. Sin forzar: solo si alguna quedo por fuera.
    internal static bool RecolocarCloacas(bool forzar)
    {
        Area a = null;
        foreach (Area x in Areas) if (x.cerrada) a = x;
        GameObject area = a != null ? GameObject.Find(a.raiz) : null;
        Transform grupo = a != null ? EnemigosEnSuNivelSetup.Grupo(a.raiz) : null;
        if (!area || !grupo) return false;

        Physics.SyncTransforms();
        var estatuas = new List<Transform>();
        foreach (Transform t in grupo) if (t.GetComponent<EstatuaAI>()) estatuas.Add(t);
        if (estatuas.Count == 0) return false;

        LlaveAleatoria llave = grupo.GetComponentInChildren<LlaveAleatoria>(true);
        bool llaveSinRecinto = llave && llave.recinto != area.transform;

        var fuera = new List<string>();
        foreach (Transform e in estatuas) if (!ZonaCerrada.Dentro(area.transform, Pies(e))) fuera.Add(e.name);
        if (!forzar && fuera.Count == 0)
        {
            if (llaveSinRecinto) { Undo.RecordObject(llave, "Llave recinto"); llave.recinto = area.transform; EditorSceneManager.MarkSceneDirty(area.scene); }
            return true;
        }

        var pedido = new Area { raiz = a.raiz, pisos = a.pisos, cantidad = estatuas.Count, separacion = a.separacion,
                                distanciaEntrada = a.distanciaEntrada, cerrada = true };
        Vector3 entrada = PuntoEntrada(area.transform);
        List<Vector3> puntos = BuscarPuntos(pedido, area.transform, entrada);
        if (puntos.Count == 0)
        {
            Debug.LogWarning("[Enemigos y Zonas] Cloacas: no encontre puntos del NavMesh dentro de las paredes. " +
                             "Prueba Tools > Enemigos y Zonas > Hornear NavMesh (solo pisos).", area);
            return true;
        }

        for (int i = 0; i < estatuas.Count && i < puntos.Count; i++)
        {
            Transform e = estatuas[i];
            Undo.RecordObject(e, "Recolocar estatuas");
            float alto = e.TryGetComponent(out NavMeshAgent agent) ? agent.baseOffset : 0f;
            e.position = puntos[i] + Vector3.up * alto;
            Vector3 mirar = entrada - puntos[i];
            mirar.y = 0f;
            if (mirar.sqrMagnitude > 0.01f) e.rotation = Quaternion.LookRotation(mirar);
        }

        if (llave)
        {
            Undo.RecordObject(llave, "Recolocar estatuas");
            Undo.RecordObject(llave.transform, "Recolocar estatuas");
            llave.recinto = area.transform;
            // En el editor se ve en la mano de la primera; al jugar elige una al azar
            if (llave.manos != null && llave.manos.Length > 0 && llave.manos[0]) llave.transform.position = llave.manos[0].position;
        }

        EditorSceneManager.MarkSceneDirty(area.scene);
        Debug.Log("[Enemigos y Zonas] Cloacas: " + Mathf.Min(estatuas.Count, puntos.Count) + " estatuas recolocadas dentro de las paredes" +
                  (fuera.Count > 0 ? " (estaban por fuera: " + string.Join(", ", fuera) + ")" : "") + ". Guarda la escena (Ctrl+S).", grupo);
        return true;
    }

    static Vector3 Pies(Transform enemigo)
    {
        float alto = enemigo.TryGetComponent(out NavMeshAgent agent) ? agent.baseOffset : 0f;
        return enemigo.position - Vector3.up * alto;
    }

    [MenuItem("Tools/Enemigos y Zonas/Rehacer todo (NavMesh, enemigos, llave y triggers)")]
    static void RehacerMenu()
    {
        GameObject viejo = GameObject.Find(Raiz);
        if (viejo) Undo.DestroyObjectImmediate(viejo);
        Aplicar();
    }

    [MenuItem("Tools/Enemigos y Zonas/Hornear NavMesh (solo pisos)")]
    static void HornearMenu()
    {
        Hornear();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static void Aplicar()
    {
        PrepararPrefabs();
        Hornear();

        var raiz = new GameObject(Raiz);
        Undo.RegisterCreatedObjectUndo(raiz, "Enemigos y Zonas");

        CrearVolumenes(raiz.transform);
        CrearEnemigos(raiz.transform);

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log("[Enemigos y Zonas] Listo: NavMesh solo en pisos, enemigos en cada area, llave en las Cloacas y triggers de Volume. Guarda la escena (Ctrl+S).", raiz);
    }

    // ------------------------------------------------------------------ NavMesh

    public static void Hornear()
    {
        // Las surfaces sueltas (ej: en "Piso 0" o "agua") horneaban toda la escena, techos incluidos
        var raices = new HashSet<GameObject>();
        foreach (Area a in Areas) { GameObject g = GameObject.Find(a.raiz); if (g) raices.Add(g); }
        foreach (NavMeshSurface s in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (raices.Contains(s.gameObject)) continue;
            Debug.Log("[Enemigos y Zonas] Quitado el NavMeshSurface de \"" + s.name + "\" (horneaba tambien los techos).", s.gameObject);
            s.RemoveData();
            Undo.DestroyObjectImmediate(s);
        }

        foreach (Area a in Areas)
        {
            GameObject g = GameObject.Find(a.raiz);
            if (!g) { Debug.LogWarning("[Enemigos y Zonas] No encuentro el area \"" + a.raiz + "\"."); continue; }

            var surface = g.GetComponent<NavMeshSurface>();
            if (!surface) surface = Undo.AddComponent<NavMeshSurface>(g);
            Undo.RecordObject(surface, "NavMesh pisos");
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << CapaTriggers);
            surface.defaultArea = NoCaminable;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;

            foreach (string ruta in a.pisos)
            {
                Transform piso = g.transform.Find(ruta);
                if (!piso) { Debug.LogWarning("[Enemigos y Zonas] " + a.raiz + ": no encuentro el piso \"" + ruta + "\".", g); continue; }
                var mod = piso.GetComponent<NavMeshModifier>();
                if (!mod) mod = Undo.AddComponent<NavMeshModifier>(piso.gameObject);
                Undo.RecordObject(mod, "NavMesh pisos");
                mod.overrideArea = true;
                mod.area = Caminable;
                mod.applyToChildren = true;
                mod.ignoreFromBuild = false;
            }

            GuardarNavMesh(surface);
        }
    }

    // Areas cuya NavMeshSurface apunta a un NavMesh que ya no existe (ej: el NavMesh-*.asset se borro del repositorio).
    // Sin NavMesh los enemigos no se pueden mover. Se rehornean con los ajustes y modificadores que ya tienen.
    static void HornearFaltantes()
    {
        bool hubo = false;
        foreach (Area a in Areas)
        {
            GameObject g = GameObject.Find(a.raiz);
            NavMeshSurface s = g ? g.GetComponent<NavMeshSurface>() : null;
            if (!s || s.navMeshData != null) continue;
            if (!hubo) Physics.SyncTransforms();
            hubo = true;
            Debug.Log("[Enemigos y Zonas] " + a.raiz + " no tenia NavMesh (se perdio el archivo): horneando de nuevo.", g);
            GuardarNavMesh(s);
        }
        if (hubo) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void GuardarNavMesh(NavMeshSurface surface)
    {
        NavMeshData vieja = surface.navMeshData;
        surface.BuildNavMesh();
        if (surface.navMeshData == null || surface.navMeshData == vieja) return;

        string escena = surface.gameObject.scene.path;
        string carpeta = Path.Combine(Path.GetDirectoryName(escena), Path.GetFileNameWithoutExtension(escena)).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(carpeta))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(carpeta).Replace('\\', '/'), Path.GetFileName(carpeta));

        string ruta = carpeta + "/NavMesh-" + surface.name + ".asset";
        if (vieja && AssetDatabase.GetAssetPath(vieja) == ruta) AssetDatabase.DeleteAsset(ruta);
        else if (AssetDatabase.LoadAssetAtPath<NavMeshData>(ruta)) AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(surface.navMeshData, ruta);
        EditorUtility.SetDirty(surface);
    }

    // ---------------------------------------------------------------- Volumenes

    static void CrearVolumenes(Transform raiz)
    {
        var contenedor = new GameObject("Triggers Volumen");
        contenedor.transform.SetParent(raiz, false);

        var volGo = new GameObject("Volumen Zonas");
        volGo.transform.SetParent(contenedor.transform, false);
        var volume = volGo.AddComponent<Volume>();
        volume.isGlobal = true;
        // Por encima del volumen de la tienda
        volume.priority = 10f;
        volume.weight = 0f;
        var cambio = volGo.AddComponent<VolumenZonas>();

        foreach (Area a in Areas)
        {
            GameObject area = GameObject.Find(a.raiz);
            if (!area || !LimitesColliders(area.transform, out Bounds b)) continue;
            b.Expand(new Vector3(0f, 4f, 0f));
            RecortarContraZonaPrincipal(ref b);

            var go = new GameObject("Trigger " + a.raiz);
            go.layer = CapaTriggers;
            go.transform.SetParent(contenedor.transform, false);
            go.transform.position = b.center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = b.size;

            var zona = go.AddComponent<ZonaVolumen>();
            zona.perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(a.perfil);
            zona.volumen = cambio;
            if (!zona.perfil) Debug.LogWarning("[Enemigos y Zonas] No encuentro el perfil " + a.perfil, go);
        }
    }

    // La isla de la Zona Roja es tan grande que su caja llegaba a la Zona Principal: se recorta por el lado que menos entra
    static void RecortarContraZonaPrincipal(ref Bounds b)
    {
        GameObject zp = GameObject.Find("Zona_Principal/piso");
        if (!zp || !LimitesRender(zp.transform, out Bounds hub)) return;
        hub.Expand(new Vector3(0f, 10f, 0f));
        hub.Expand(2f);
        if (!b.Intersects(hub)) return;
        float[] cortes = { b.max.x - hub.min.x, hub.max.x - b.min.x, b.max.z - hub.min.z, hub.max.z - b.min.z };
        int lado = 0;
        for (int i = 1; i < 4; i++) if (cortes[i] < cortes[lado]) lado = i;
        Vector3 min = b.min, max = b.max;
        if (lado == 0) max.x = hub.min.x; else if (lado == 1) min.x = hub.max.x;
        else if (lado == 2) max.z = hub.min.z; else min.z = hub.max.z;
        b.SetMinMax(min, max);
    }

    static bool LimitesColliders(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Collider c in t.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!hay) { b = c.bounds; hay = true; }
            else b.Encapsulate(c.bounds);
        }
        return hay;
    }

    // ----------------------------------------------------------------- Enemigos

    // Cada grupo va dentro de su area (ver EnemigosEnSuNivelSetup); al rehacer se reemplaza el que habia
    static void CrearEnemigos(Transform raiz)
    {
        foreach (Area a in Areas)
        {
            GameObject area = GameObject.Find(a.raiz);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(a.prefab);
            if (!area || !prefab) { Debug.LogWarning("[Enemigos y Zonas] Falta el area o el prefab " + a.prefab); continue; }

            Transform anterior = EnemigosEnSuNivelSetup.Grupo(a.raiz);
            if (anterior) Undo.DestroyObjectImmediate(anterior.gameObject);
            Transform grupo = EnemigosEnSuNivelSetup.GrupoEnNivel(a.raiz);

            Vector3 entrada = PuntoEntrada(area.transform);
            List<Vector3> puntos = BuscarPuntos(a, area.transform, entrada);
            var manos = new List<Transform>();

            for (int i = 0; i < puntos.Count; i++)
            {
                var e = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo.transform);
                e.name = prefab.name + (puntos.Count > 1 ? " (" + (i + 1) + ")" : "");
                float alto = e.TryGetComponent(out NavMeshAgent agent) ? agent.baseOffset : 0f;
                e.transform.position = puntos[i] + Vector3.up * alto;
                Vector3 mirar = entrada - puntos[i];
                mirar.y = 0f;
                if (mirar.sqrMagnitude > 0.01f) e.transform.rotation = Quaternion.LookRotation(mirar);

                Transform mano = e.transform.Find("Mano");
                if (mano) manos.Add(mano);
            }

            if (a.prefab == PrefabEstatua) CrearLlave(grupo.transform, manos, a.cerrada ? area.transform : null);
            Debug.Log("[Enemigos y Zonas] " + a.raiz + ": " + puntos.Count + " x " + prefab.name, grupo);
        }
    }

    static Vector3 PuntoEntrada(Transform area)
    {
        // "Inicio" (Backrooms) esta ya dentro del area; la entrada del pasillo queda tras la puerta
        Transform t = area.Find("Inicio");
        if (!t) t = area.Find("Entrada Pasillo");
        Vector3 p = t ? t.position : area.position;
        return NavMesh.SamplePosition(p, out NavMeshHit hit, 20f, NavMesh.AllAreas) ? hit.position : p;
    }

    // Puntos del NavMesh del area, alcanzables desde la entrada, lejos de ella y separados entre si
    static List<Vector3> BuscarPuntos(Area a, Transform area, Vector3 entrada)
    {
        List<Vector3> res = BuscarPuntos(a, area, entrada, true);
        // Si desde la entrada no se llega (ej: la entrada queda fuera del NavMesh), sin exigir camino
        if (res.Count < a.cantidad) res = BuscarPuntos(a, area, entrada, false);
        if (res.Count < a.cantidad)
            Debug.LogWarning("[Enemigos y Zonas] " + a.raiz + ": solo encontre sitio para " + res.Count + " de " + a.cantidad + ".");
        return res;
    }

    static List<Vector3> BuscarPuntos(Area a, Transform area, Vector3 entrada, bool exigirCamino)
    {
        Bounds pisos = default;
        bool hay = false;
        foreach (string ruta in a.pisos)
        {
            Transform p = area.Find(ruta);
            if (p && LimitesColliders(p, out Bounds b)) { if (!hay) pisos = b; else pisos.Encapsulate(b); hay = true; }
        }
        var res = new List<Vector3>();
        if (!hay) return res;

        // En la Zona Roja: en la isla, alrededor del faro
        Transform faro = area.Find("Faro");
        bool cercaFaro = a.raiz == "Zona Roja" && faro;

        if (a.cerrada) Physics.SyncTransforms();
        var rnd = new System.Random(a.raiz.GetHashCode() & 0x7fffffff);
        var candidatos = new List<Vector3>();
        var camino = new NavMeshPath();
        for (int i = 0; i < 600 && candidatos.Count < 80; i++)
        {
            var p = new Vector3(Mathf.Lerp(pisos.min.x, pisos.max.x, (float)rnd.NextDouble()), pisos.max.y,
                                Mathf.Lerp(pisos.min.z, pisos.max.z, (float)rnd.NextDouble()));
            if (cercaFaro)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = Mathf.Lerp(12f, 30f, (float)rnd.NextDouble());
                p = faro.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                p.y = pisos.max.y;
            }
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, pisos.size.y + 3f, NavMesh.AllAreas)) continue;
            Vector3 q = hit.position;
            if (q.x < pisos.min.x || q.x > pisos.max.x || q.z < pisos.min.z || q.z > pisos.max.z) continue;
            if (Plano(q - entrada) < a.distanciaEntrada) continue;
            if (a.cerrada && !ZonaCerrada.Dentro(area, q)) continue;
            if (exigirCamino && !cercaFaro && (!NavMesh.CalculatePath(entrada, q, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete)) continue;
            candidatos.Add(q);
        }

        // Primero los mas lejanos de la entrada, cuidando la separacion
        candidatos.Sort((x, y) => Plano(y - entrada).CompareTo(Plano(x - entrada)));
        int saltar = a.cantidad == 1 ? candidatos.Count / 3 : 0;   // uno solo: no justo en la esquina mas lejana
        for (int i = saltar; i < candidatos.Count && res.Count < a.cantidad; i++)
        {
            bool lejos = true;
            foreach (Vector3 r in res) if (Plano(r - candidatos[i]) < a.separacion) { lejos = false; break; }
            if (lejos) res.Add(candidatos[i]);
        }
        return res;
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // ------------------------------------------------------------------- Llave

    static void CrearLlave(Transform grupo, List<Transform> manos, Transform recinto)
    {
        var llave = GameObject.CreatePrimitive(PrimitiveType.Cube);
        llave.name = "Llave Cloacas";
        llave.transform.SetParent(grupo, false);
        llave.transform.localScale = Vector3.one * 0.18f;
        if (manos.Count > 0) llave.transform.position = manos[0].position;
        llave.GetComponent<Renderer>().sharedMaterial = CrearMaterialLlave();

        var rb = llave.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var interactable = llave.AddComponent<Interactable>();
        var so = new SerializedObject(interactable);
        so.FindProperty("prompt").stringValue = "Agarrar llave";
        so.ApplyModifiedPropertiesWithoutUndo();
        var grabbable = llave.AddComponent<Grabbable>();
        UnityEventTools.AddPersistentListener(interactable.onInteract, grabbable.PickUp);

        var audio = llave.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.minDistance = 1f;
        audio.maxDistance = 18f;

        var aleatoria = llave.AddComponent<LlaveAleatoria>();
        aleatoria.manos = manos.ToArray();
        aleatoria.recinto = recinto;
    }

    static Material CrearMaterialLlave()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialLlave);
        if (mat) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(shader ? shader : Shader.Find("Standard"));
        Color oro = new Color(1f, 0.78f, 0.2f);
        mat.SetColor("_BaseColor", oro);
        mat.SetFloat("_Metallic", 0.8f);
        mat.SetFloat("_Smoothness", 0.6f);
        // Brilla un poco para verla en la oscuridad de las cloacas
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", oro * 0.6f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(mat, MaterialLlave);
        return mat;
    }

    // ----------------------------------------------------------------- Prefabs

    static void PrepararPrefabs()
    {
        // La estatua necesita una mano donde llevar la llave
        EditarPrefab(PrefabEstatua, root =>
        {
            if (root.transform.Find("Mano")) return false;
            var mano = new GameObject("Mano").transform;
            mano.SetParent(root.transform, false);
            mano.localPosition = new Vector3(0.55f, 0.1f, 0.45f);
            return true;
        });

        // Enemy 4 usa el modelo "Enemigo 4" en lugar de la capsula
        EditarPrefab(PrefabEnemigo4, root =>
        {
            if (root.transform.Find("Modelo")) return false;
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloEnemigo4);
            if (!modelo) { Debug.LogWarning("[Enemigos y Zonas] No encuentro " + ModeloEnemigo4); return false; }

            var m = (GameObject)PrefabUtility.InstantiatePrefab(modelo, root.transform);
            m.name = "Modelo";
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one;
            // Quita los colliders que pudiera traer: el cuerpo sigue siendo la capsula
            foreach (Collider c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            if (!LimitesRender(m.transform, out Bounds b) || b.size.y < 0.0001f) return true;
            // Mismo alto que el NavMeshAgent y con los pies en el suelo
            var agent = root.GetComponent<NavMeshAgent>();
            float alto = agent ? agent.height : 2f;
            float pies = agent ? -agent.baseOffset : -1f;
            m.transform.localScale = Vector3.one * (alto / b.size.y);
            LimitesRender(m.transform, out b);
            Vector3 centro = root.transform.InverseTransformPoint(b.center);
            float abajo = root.transform.InverseTransformPoint(b.min).y;
            m.transform.localPosition = new Vector3(-centro.x, pies - abajo, -centro.z);

            var capsula = root.GetComponent<MeshRenderer>();
            if (capsula) capsula.enabled = false;
            return true;
        });
    }

    static bool LimitesRender(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return hay;
    }

    static void EditarPrefab(string ruta, System.Func<GameObject, bool> editar)
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(ruta)) { Debug.LogWarning("[Enemigos y Zonas] No encuentro " + ruta); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            if (editar(root)) PrefabUtility.SaveAsPrefabAsset(root, ruta);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
