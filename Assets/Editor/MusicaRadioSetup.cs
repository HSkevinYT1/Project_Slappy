using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Radio con la musica de la tienda (y del menu principal, que es la misma escena):
// crea "Radio (musica)" encima del mostrador con AudioSource + MusicaRadio, y le pone las alturas
// de la escalera al sotano (arriba suena, abajo se apaga). Despues se mueve donde ira la radio y se le
// mete el modelo dentro. Se aplica sola una vez; rehacer: Tools > Tienda > Radio con musica.
[InitializeOnLoad]
public static class MusicaRadioSetup
{
    const string Nombre = "Radio (musica)";

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
        if (Object.FindFirstObjectByType<MusicaRadio>(FindObjectsInactive.Include)) return;
        Aplicar();
    }

    [MenuItem("Tools/Tienda/Radio con musica")]
    static void Menu()
    {
        MusicaRadio vieja = Object.FindFirstObjectByType<MusicaRadio>(FindObjectsInactive.Include);
        if (vieja)
        {
            // Ya existe: solo se recalculan las alturas, sin moverla
            Alturas(vieja);
            EditorSceneManager.MarkSceneDirty(vieja.gameObject.scene);
            Selection.activeObject = vieja.gameObject;
            return;
        }
        Aplicar();
    }

    static void Aplicar()
    {
        GameObject tienda = GameObject.Find("Tienda");
        GameObject mostrador = GameObject.Find("Tienda/mostrador") ?? GameObject.Find("mostrador");

        var go = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(go, "Radio musica");
        if (tienda) go.transform.SetParent(tienda.transform, false);

        // Encima del mostrador, hacia un extremo (el cuchillo esta en el medio)
        if (mostrador && Limites(mostrador.transform, out Bounds b))
        {
            Vector3 largo = b.size.x > b.size.z ? Vector3.right * b.extents.x : Vector3.forward * b.extents.z;
            go.transform.position = new Vector3(b.center.x, b.max.y, b.center.z) + largo * 0.7f;
        }
        go.transform.rotation = Quaternion.identity;

        var fuente = go.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = true;
        var radio = go.AddComponent<MusicaRadio>();
        Alturas(radio);

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeObject = go;
        Debug.Log("[Tienda] Creada \"" + Nombre + "\" sobre el mostrador. Asigna la cancion en Musica, muevela donde ira la radio y guarda la escena.", go);
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
