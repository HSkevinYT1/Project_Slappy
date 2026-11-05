using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arma el tutorial de la tienda en la escena (se ejecuta solo una vez; se puede rehacer desde
// Tools > Tutorial Tienda > Reconstruir). Ver TutorialTienda.cs para la secuencia.
[InitializeOnLoad]
public static class TutorialTiendaSetup
{
    const string Version = "_tutorial v1";
    const string CarpetaDialogos = "Assets/Dialogos/Tutorial";

    // Puntos en coordenadas LOCALES del objeto "Tienda" (asi siguen sirviendo si la tienda se mueve).
    static readonly Vector3 LocalAfuera = new Vector3(3.1f, 51.2f, 80.8f);      // fuera de la puerta norte
    static readonly Vector3 LocalPuerta = new Vector3(3.1f, 51.2f, 35.8f);      // recien entrando
    static readonly Vector3 LocalCliente = new Vector3(-33.8f, 51.2f, 24.9f);   // frente al mostrador
    static readonly Vector3 LocalJugador = new Vector3(-65.8f, 51.2f, 24.9f);   // detras del mostrador

    static TutorialTiendaSetup()
    {
        EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!GameObject.Find("Tienda")) return;
        GameObject t = GameObject.Find("Tutorial Tienda");
        if (t && t.transform.Find(Version)) return;
        Crear();
    }

    [MenuItem("Tools/Tutorial Tienda/Reconstruir")]
    static void Menu()
    {
        Crear();
        Selection.activeGameObject = GameObject.Find("Tutorial Tienda");
    }

    [MenuItem("Tools/Tutorial Tienda/Mover jugador detras del mostrador")]
    static void MoverJugador()
    {
        GameObject tienda = GameObject.Find("Tienda");
        var jugador = Object.FindFirstObjectByType<PlayerMovement>();
        if (!tienda || !jugador) return;
        Vector3 p = Piso(tienda.transform.TransformPoint(LocalJugador));
        Undo.RecordObject(jugador.transform, "Mover jugador");
        jugador.transform.position = p + Vector3.up * 1.1f;
        jugador.transform.rotation = Quaternion.LookRotation(tienda.transform.TransformDirection(Vector3.right), Vector3.up);
        EditorSceneManager.MarkSceneDirty(jugador.gameObject.scene);
        Debug.Log("[Tutorial Tienda] Jugador colocado detras del mostrador, mirando hacia los clientes.", jugador);
    }

    // ------------------------------------------------------------------

    static void Crear()
    {
        GameObject tiendaGO = GameObject.Find("Tienda");
        if (!tiendaGO) { Debug.LogWarning("[Tutorial Tienda] No hay un objeto \"Tienda\" en la escena."); return; }
        Transform tienda = tiendaGO.transform;

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Armar tutorial de la tienda");

        GameObject viejo = GameObject.Find("Tutorial Tienda");
        if (viejo) Undo.DestroyObjectImmediate(viejo);

        AsegurarDialogos();
        DialogueData pideCuchillo = AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Dialogos/Final/Cliente.asset");
        DialogueData gracias = CrearDialogo("Cliente_Gracias", new[]
        {
            "Perfecto, es justo el que estaba buscando.",
            "Muchas gracias. Que tenga buen dia.",
        });
        DialogueData pideMuneco = CrearDialogo("Cultista_PideMuneco", new[]
        {
            "...",
            "Vengo por el muneco.",
            "Esta abajo, en el sotano. Traelo.",
        });

        Grabbable cuchillo = PrepararCuchillo(tienda);

        // Raiz del tutorial
        var raiz = new GameObject("Tutorial Tienda");
        Undo.RegisterCreatedObjectUndo(raiz, "Crear tutorial");
        new GameObject(Version).transform.SetParent(raiz.transform, false);

        // Recorrido: afuera -> puerta -> mostrador
        Transform camino = Hijo(raiz.transform, "Camino NPCs");
        Transform pAfuera = Punto(camino, "0 Afuera", Piso(tienda.TransformPoint(LocalAfuera)));
        Transform pPuerta = Punto(camino, "1 Puerta", Piso(tienda.TransformPoint(LocalPuerta)));
        Transform pMostrador = Punto(camino, "2 Mostrador", Piso(tienda.TransformPoint(LocalCliente)));
        Punto(raiz.transform, "Lugar del jugador (detras del mostrador)", Piso(tienda.TransformPoint(LocalJugador)));

        // NPCs
        Quaternion haciaJugador = Quaternion.LookRotation(tienda.TransformDirection(Vector3.left), Vector3.up);
        Transform cliente = CrearNPC(raiz.transform, "Cliente", "Assets/Prefabs/NPC_1.prefab", pMostrador.position, haciaJugador);
        Transform cultista = CrearNPC(raiz.transform, "Cultista", "Assets/Modelos De enemigos/CultistWalkPose.fbx", pMostrador.position, haciaJugador);

        var receptor = cliente.gameObject.AddComponent<ReceptorObjeto>();
        receptor.objetoEsperado = cuchillo;
        receptor.prompt = "Entregar cuchillo";

        // Componente del tutorial
        var tut = raiz.AddComponent<TutorialTienda>();
        tut.areaTienda = AreaTienda(tienda);
        tut.caminoEntrada = new[] { pAfuera, pPuerta, pMostrador };
        tut.cliente = cliente;
        tut.cultista = cultista;
        tut.dialogoClientePide = pideCuchillo;
        tut.dialogoClienteGracias = gracias;
        tut.dialogoCultistaPide = pideMuneco;
        tut.cuchillo = cuchillo;

        bool canvasNuevo = AsegurarDialogueManager();

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log("[Tutorial Tienda] Tutorial armado" + (canvasNuevo ? " (se agrego el Canvas de dialogos)" : "") +
                  $". Cliente y cultista entran por ({pPuerta.position.x:0.#}, {pPuerta.position.z:0.#}) y se paran en ({pMostrador.position.x:0.#}, {pMostrador.position.z:0.#})." +
                  (pideCuchillo ? "" : " FALTA el dialogo Dialogos/Final/Cliente.") + " Guarda la escena (Ctrl+S).", raiz);
    }

    // El panel de dialogos (prefab Canvas con DialogueManager) si la escena no tiene uno.
    static bool AsegurarDialogueManager()
    {
        if (Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include)) return false;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Canvas.prefab");
        if (!prefab) { Debug.LogWarning("[Tutorial Tienda] No se encontro Assets/Prefabs/Canvas.prefab."); return false; }
        var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(canvas, "Agregar Canvas de dialogos");
        return true;
    }

    // El cuchillo: agarrable, con brillo (apagado al empezar) y sin poder agarrarse hasta que lo pidan.
    static Grabbable PrepararCuchillo(Transform tienda)
    {
        Transform t = tienda.Find("cuchillo");
        if (!t)
        {
            foreach (Transform c in tienda.GetComponentsInChildren<Transform>(true))
                if (c.name.ToLowerInvariant().Contains("cuchillo")) { t = c; break; }
        }
        if (!t) { Debug.LogWarning("[Tutorial Tienda] No se encontro el cuchillo dentro de la Tienda."); return null; }
        GameObject go = t.gameObject;

        if (!go.GetComponent<Rigidbody>())
        {
            var rb = Undo.AddComponent<Rigidbody>(go);
            rb.mass = 0.3f;
            rb.isKinematic = true; // quieto en el estante hasta que lo agarren
        }
        var grab = go.GetComponent<Grabbable>();
        if (!grab) grab = Undo.AddComponent<Grabbable>(go); // agrega tambien el Interactable (RequireComponent)

        var inter = go.GetComponent<Interactable>();
        if (!inter) inter = Undo.AddComponent<Interactable>(go);
        var soI = new SerializedObject(inter);
        soI.FindProperty("prompt").stringValue = "Agarrar";
        soI.ApplyModifiedPropertiesWithoutUndo();
        bool tienePickUp = false;
        for (int i = 0; i < inter.onInteract.GetPersistentEventCount(); i++)
            if (inter.onInteract.GetPersistentMethodName(i) == "PickUp") tienePickUp = true;
        if (!tienePickUp) UnityEventTools.AddPersistentListener(inter.onInteract, grab.PickUp);

        var brillo = go.GetComponent<Highlightable>();
        if (!brillo) brillo = Undo.AddComponent<Highlightable>(go);
        var soH = new SerializedObject(brillo);
        soH.FindProperty("outlineMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GlowMaterial.mat");
        soH.FindProperty("startHighlighted").boolValue = false;
        soH.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(go);
        return grab;
    }

    static Transform CrearNPC(Transform padre, string nombre, string rutaModelo, Vector3 pos, Quaternion rot)
    {
        var raiz = new GameObject(nombre);
        raiz.transform.SetParent(padre, false);
        raiz.transform.SetPositionAndRotation(pos, rot);

        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
        GameObject visual = modelo ? (GameObject)PrefabUtility.InstantiatePrefab(modelo) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Modelo";
        visual.transform.SetParent(raiz.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // Altura humana (~1.8 m) y los pies en el suelo.
        if (Limites(visual, out Bounds b) && b.size.y > 0.01f)
        {
            if (b.size.y < 1.2f || b.size.y > 2.6f) visual.transform.localScale *= 1.8f / b.size.y;
            Limites(visual, out b);
            visual.transform.position += Vector3.up * (pos.y - b.min.y);
        }

        // Colision para que el jugador le pueda apuntar (y entregarle cosas).
        foreach (Collider c in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        var cap = raiz.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0f, 0.9f, 0f);
        cap.height = 1.8f;
        cap.radius = 0.35f;
        return raiz.transform;
    }

    static bool Limites(GameObject go, out Bounds b)
    {
        b = default; bool hay = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return hay;
    }

    // Zona de la tienda (paredes) en el mundo, con altura de sobra.
    static Bounds AreaTienda(Transform tienda)
    {
        Transform paredes = tienda.Find("paredes");
        Limites(paredes ? paredes.gameObject : tienda.gameObject, out Bounds b);
        b.Expand(new Vector3(-0.3f, 0f, -0.3f));
        b.size = new Vector3(b.size.x, 30f, b.size.z);
        return b;
    }

    // Altura del piso bajo un punto (raycast hacia abajo); si no hay nada, se deja como esta.
    static Vector3 Piso(Vector3 p)
    {
        Physics.SyncTransforms();
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return p;
    }

    static Transform Punto(Transform padre, string nombre, Vector3 pos)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        return go.transform;
    }

    static Transform Hijo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    static void AsegurarDialogos()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaDialogos)) AssetDatabase.CreateFolder("Assets/Dialogos", "Tutorial");
    }

    // Crea el asset de dialogo si no existe (si ya existe, se respeta lo que se haya escrito).
    static DialogueData CrearDialogo(string nombre, string[] lineas)
    {
        string ruta = CarpetaDialogos + "/" + nombre + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<DialogueData>(ruta);
        if (d) return d;
        d = ScriptableObject.CreateInstance<DialogueData>();
        d.modo = DialogueMode.Conversacion;
        d.lineas = lineas;
        AssetDatabase.CreateAsset(d, ruta);
        AssetDatabase.SaveAssets();
        return d;
    }
}
