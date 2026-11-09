using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tutorial de la tienda:
// - El cliente lleva un Interactable ("Atender") que llama a TutorialTienda.Atender: el dialogo empieza al hablarle.
// - "Zona Mostrador": caja detras del mostrador (del lado contrario al cliente). El cuchillo solo se le
//   puede entregar desde ahi (ReceptorObjeto > Zona Entrega).
// Se aplica sola una vez; rehacer: Tools > Tienda > Atender cliente y entregar desde el mostrador.
[InitializeOnLoad]
public static class TutorialMostradorSetup
{
    const string NombreZona = "Zona Mostrador";

    static TutorialMostradorSetup()
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
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        if (!tut || !tut.cliente || tut.cliente.GetComponent<Interactable>()) return;
        Aplicar(tut);
    }

    [MenuItem("Tools/Tienda/Atender cliente y entregar desde el mostrador")]
    static void Menu()
    {
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        if (!tut || !tut.cliente) { EditorUtility.DisplayDialog("Tienda", "No hay TutorialTienda con cliente en la escena.", "OK"); return; }
        var viejo = tut.cliente.GetComponent<Interactable>();
        if (viejo) Undo.DestroyObjectImmediate(viejo);
        Transform zona = tut.transform.Find(NombreZona);
        if (zona) Undo.DestroyObjectImmediate(zona.gameObject);
        Aplicar(tut);
    }

    static void Aplicar(TutorialTienda tut)
    {
        // Hablar con el cliente
        var hablar = Undo.AddComponent<Interactable>(tut.cliente.gameObject);
        hablar.SetPrompt("Atender");
        UnityEventTools.AddVoidPersistentListener(hablar.onInteract, tut.Atender);
        EditorUtility.SetDirty(hablar);
        Undo.RecordObject(tut, "Atender cliente");
        tut.atenderParaEmpezar = true;
        EditorUtility.SetDirty(tut);

        // Zona detras del mostrador
        ReceptorObjeto receptor = tut.cliente.GetComponentInChildren<ReceptorObjeto>(true);
        if (receptor && Tabla(out Bounds bt))
        {
            bool largoEnX = bt.size.x > bt.size.z;
            // Lado del cliente (punto donde se para frente al mostrador)
            Vector3 puntoCliente = tut.caminoEntrada != null && tut.caminoEntrada.Length > 0 && tut.caminoEntrada[tut.caminoEntrada.Length - 1]
                ? tut.caminoEntrada[tut.caminoEntrada.Length - 1].position : tut.cliente.position;
            float ladoCliente = Mathf.Sign(largoEnX ? puntoCliente.z - bt.center.z : puntoCliente.x - bt.center.x);

            // Desde el borde del mostrador del lado del cliente hasta 3 m hacia dentro, a lo largo de todo el mostrador
            const float Fondo = 3f;
            float ancho = largoEnX ? bt.size.z : bt.size.x;
            float borde = (largoEnX ? bt.center.z : bt.center.x) + ladoCliente * ancho * 0.5f;
            float centroCorto = borde - ladoCliente * (ancho + Fondo) * 0.5f;
            Vector3 centro = largoEnX ? new Vector3(bt.center.x, bt.max.y, centroCorto) : new Vector3(centroCorto, bt.max.y, bt.center.z);
            Vector3 tam = largoEnX ? new Vector3(bt.size.x + 1f, 4f, ancho + Fondo) : new Vector3(ancho + Fondo, 4f, bt.size.z + 1f);

            var go = new GameObject(NombreZona);
            Undo.RegisterCreatedObjectUndo(go, "Zona mostrador");
            go.layer = 2;
            go.transform.SetParent(tut.transform, false);
            go.transform.SetPositionAndRotation(centro, Quaternion.identity);
            Vector3 s = go.transform.lossyScale;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(tam.x / s.x, tam.y / s.y, tam.z / s.z);

            Undo.RecordObject(receptor, "Zona mostrador");
            receptor.zonaEntrega = box;
            EditorUtility.SetDirty(receptor);
        }
        else Debug.LogWarning("[Tienda] No encontre el mostrador o el ReceptorObjeto del cliente: el cuchillo se podra entregar desde cualquier lado.", tut);

        EditorSceneManager.MarkSceneDirty(tut.gameObject.scene);
        Debug.Log("[Tienda] Tutorial: el cliente espera a que lo atiendas y el cuchillo solo se entrega desde detras del mostrador. Guarda la escena.", tut);
    }

    // La tabla del mostrador: la pieza fina mas grande de arriba
    static bool Tabla(out Bounds bt)
    {
        bt = default;
        GameObject mostrador = GameObject.Find("Tienda/mostrador") ?? GameObject.Find("mostrador");
        if (!mostrador) return false;
        float area = 0f;
        foreach (Renderer r in mostrador.GetComponentsInChildren<Renderer>())
        {
            Bounds b = r.bounds;
            if (b.size.y > 0.2f || b.size.y > Mathf.Min(b.size.x, b.size.z) * 0.5f) continue;
            if (b.size.x * b.size.z > area) { area = b.size.x * b.size.z; bt = b; }
        }
        return area > 0f;
    }
}
