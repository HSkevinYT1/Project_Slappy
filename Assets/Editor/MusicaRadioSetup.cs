using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Radio con la musica de la tienda (y del menu principal, que es la misma escena):
// crea "Radio (musica)" encima del mostrador con AudioSource + MusicaRadio, y le pone las alturas
// de la escalera al sotano (arriba suena, abajo se apaga). Lleva dentro el modelo radio.obj (como las radios
// de la tienda) y un Interactable para encenderla y apagarla. Se puede mover a donde se quiera.
// Se aplica sola una vez; rehacer: Tools > Tienda > Radio con musica.
[InitializeOnLoad]
public static class MusicaRadioSetup
{
    const string Nombre = "Radio (musica)";
    const string ModeloRadio = "Assets/Models/Props/radio.obj";
    const float EscalaModelo = 0.0125f;   // como las radios de las mesas (~37 cm de alto)

    static MusicaRadioSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += AutoAplicar;
        };
    }

    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include)) return;
        MusicaRadio r = Object.FindFirstObjectByType<MusicaRadio>(FindObjectsInactive.Include);
        if (r == null) { Aplicar(); return; }
        bool cambio = false;
        if (!r.GetComponentInChildren<Renderer>(true))
        {
            // Si quedo flotando (sin nada debajo), se apoya en el mostrador
            if (!Apoyada(r.transform) && LugarEnMostrador(out Vector3 lugar)) { Undo.RecordObject(r.transform, "Radio"); r.transform.position = lugar; }
            PonerModelo(r);
            cambio = true;
        }
        if (!r.GetComponent<Interactable>()) { Interaccion(r); cambio = true; }
        if (cambio) EditorSceneManager.MarkSceneDirty(r.gameObject.scene);
    }

    [MenuItem("Tools/Tienda/Radio con musica")]
    static void Menu()
    {
        MusicaRadio vieja = Object.FindFirstObjectByType<MusicaRadio>(FindObjectsInactive.Include);
        if (vieja)
        {
            // Ya existe: solo se recalculan las alturas, sin moverla
            Alturas(vieja);
            if (!vieja.GetComponentInChildren<Renderer>(true)) PonerModelo(vieja);
            if (!vieja.GetComponent<Interactable>()) Interaccion(vieja);
            EditorSceneManager.MarkSceneDirty(vieja.gameObject.scene);
            Selection.activeObject = vieja.gameObject;
            return;
        }
        Aplicar();
    }

    static void Aplicar()
    {
        GameObject tienda = GameObject.Find("Tienda");

        var go = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(go, "Radio musica");
        if (tienda) go.transform.SetParent(tienda.transform, false);

        if (LugarEnMostrador(out Vector3 lugar)) go.transform.position = lugar;
        go.transform.rotation = Quaternion.identity;

        var fuente = go.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = true;
        var radio = go.AddComponent<MusicaRadio>();
        Alturas(radio);
        PonerModelo(radio);
        Interaccion(radio);

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeObject = go;
        Debug.Log("[Tienda] Creada \"" + Nombre + "\" sobre el mostrador. Asigna la cancion en Musica, muevela donde ira la radio y guarda la escena.", go);
    }

    static bool Apoyada(Transform t)
    {
        foreach (RaycastHit h in Physics.RaycastAll(t.position + Vector3.up * 0.05f, Vector3.down, 0.12f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(t)) return true;
        return false;
    }

    // Un hueco libre encima de la tabla del mostrador (la pieza fina de arriba), lo mas cerca de un extremo
    static bool LugarEnMostrador(out Vector3 lugar)
    {
        lugar = default;
        GameObject mostrador = GameObject.Find("Tienda/mostrador") ?? GameObject.Find("mostrador");
        if (!mostrador) return false;
        Renderer tabla = null; float area = 0f;
        foreach (Renderer r in mostrador.GetComponentsInChildren<Renderer>())
        {
            Bounds b = r.bounds;
            if (b.size.y > 0.2f || b.size.y > Mathf.Min(b.size.x, b.size.z) * 0.5f) continue;
            if (b.size.x * b.size.z > area) { area = b.size.x * b.size.z; tabla = r; }
        }
        if (!tabla) return false;
        Bounds bt = tabla.bounds;
        bool largoEnX = bt.size.x > bt.size.z;
        Vector3 eje = largoEnX ? Vector3.right : Vector3.forward;
        float mitad = (largoEnX ? bt.extents.x : bt.extents.z) - 0.25f;
        var otros = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        // De los extremos hacia el centro, alternando lados
        for (int i = 0; i <= 10; i++)
        {
            foreach (float lado in new[] { 1f, -1f })
            {
                Vector3 p = new Vector3(bt.center.x, bt.max.y, bt.center.z) + eje * lado * mitad * (1f - i / 10f);
                var hueco = new Bounds(p + Vector3.up * 0.2f, new Vector3(0.4f, 0.36f, 0.4f));
                bool libre = true;
                foreach (Renderer o in otros)
                    if (o != tabla && o.enabled && !o.GetComponentInParent<MusicaRadio>() && o.bounds.Intersects(hueco)) { libre = false; break; }
                if (libre) { lugar = p; return true; }
            }
        }
        return false;
    }

    // Modelo de radio (el mismo de las radios de la tienda), apoyado donde esta el objeto, y su colision
    static void PonerModelo(MusicaRadio radio)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloRadio);
        if (!asset) { Debug.LogWarning("[Tienda] No encuentro " + ModeloRadio, radio); return; }
        Transform t = radio.transform;
        var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, t);
        Undo.RegisterCreatedObjectUndo(m, "Modelo radio");
        m.name = "Modelo";
        m.transform.localPosition = Vector3.zero;
        m.transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // como las radios de las mesas
        Vector3 ps = t.lossyScale;
        m.transform.localScale = new Vector3(EscalaModelo / ps.x, EscalaModelo / ps.y, EscalaModelo / ps.z);
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        CopiarMateriales(m, asset);
        // Apoyada: la base del modelo justo en el punto del objeto y centrada
        if (Limites(m.transform, out Bounds b)) m.transform.position += new Vector3(t.position.x - b.center.x, t.position.y - b.min.y, t.position.z - b.center.z);

        // Colision del tamano del modelo (para apuntarla)
        var box = t.GetComponent<BoxCollider>();
        if (!box) box = Undo.AddComponent<BoxCollider>(t.gameObject);
        else Undo.RecordObject(box, "Radio");
        if (Limites(m.transform, out b))
        {
            box.center = t.InverseTransformPoint(b.center);
            Vector3 s = t.lossyScale;
            Vector3 tam = Quaternion.Inverse(t.rotation) * b.size;
            box.size = new Vector3(Mathf.Abs(tam.x) / s.x, Mathf.Abs(tam.y) / s.y, Mathf.Abs(tam.z) / s.z);
        }
        EditorUtility.SetDirty(box);
    }

    // Los mismos materiales que una radio de las mesas de la tienda (el .obj viene sin color)
    static void CopiarMateriales(GameObject m, GameObject asset)
    {
        GameObject muestra = null;
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (g == m || !PrefabUtility.IsAnyPrefabInstanceRoot(g) || PrefabUtility.GetCorrespondingObjectFromSource(g) != asset) continue;
            if (g.GetComponentInParent<MusicaRadio>()) continue;
            muestra = g;
            if (g.transform.parent && g.transform.parent.name.StartsWith("mesa")) break;
        }
        if (!muestra) return;
        var porNombre = new System.Collections.Generic.Dictionary<string, Material[]>();
        foreach (Renderer r in muestra.GetComponentsInChildren<Renderer>(true)) porNombre[r.name] = r.sharedMaterials;
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
            if (porNombre.TryGetValue(r.name, out Material[] mats)) r.sharedMaterials = mats;
    }

    // Interactuar con la radio la enciende o la apaga
    static void Interaccion(MusicaRadio radio)
    {
        GameObject go = radio.gameObject;
        if (!go.GetComponentInChildren<Collider>())
        {
            var box = Undo.AddComponent<BoxCollider>(go);
            box.center = new Vector3(0f, 0.1f, 0f);
            box.size = new Vector3(0.3f, 0.2f, 0.12f);
        }
        var inter = Undo.AddComponent<Interactable>(go);
        inter.SetPrompt(radio.encendidaAlIniciar ? radio.textoApagar : radio.textoEncender);
        UnityEventTools.AddVoidPersistentListener(inter.onInteract, radio.Alternar);
        EditorUtility.SetDirty(inter);
    }

    // Arriba de la escalera = tienda (suena), abajo = sotano (apagada)
    static void Alturas(MusicaRadio radio)
    {
        GameObject e = GameObject.Find("escaleras");
        Transform rampa = e ? e.transform.Find("escalera") : null;
        Collider c = rampa ? rampa.GetComponent<Collider>() : null;
        if (!c) { Debug.LogWarning("[Tienda] No encuentro la escalera al sotano (escaleras/escalera); pon las alturas a mano.", radio); return; }
        Bounds b = c.bounds;
        float alto = b.size.y;
        Undo.RecordObject(radio, "Radio musica");
        radio.alturaTienda = b.max.y - alto * 0.1f;   // empieza a bajar al poco de entrar en la escalera
        radio.alturaSotano = b.min.y + alto * 0.15f;  // antes del ultimo escalon ya no suena
        EditorUtility.SetDirty(radio);
    }

    static bool Limites(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return hay;
    }
}
