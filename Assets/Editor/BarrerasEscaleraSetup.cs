using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Barreras invisibles en la escalera al sotano:
// - "Barrera Arriba": cerrada hasta que termina el tutorial de la tienda (TutorialTienda > Al Terminar > Abrir).
// - "Barrera Abajo": abierta; se cierra sola cuando el jugador ya esta abajo (su "Zona Cerrar"),
//   asi no puede volver a subir.
// Se aplica sola una vez y se puede rehacer con Tools > Sotano > Barreras de la escalera.
[InitializeOnLoad]
public static class BarrerasEscaleraSetup
{
    const string Nombre = "Barreras Escalera";
    const float Alto = 2.8f, Grosor = 0.3f;

    static BarrerasEscaleraSetup()
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
        GameObject e = GameObject.Find("escaleras");
        if (!e || e.transform.Find(Nombre) || !Object.FindFirstObjectByType<TutorialTienda>()) return;
        Aplicar(e.transform);
    }

    [MenuItem("Tools/Sotano/Barreras de la escalera")]
    static void Menu()
    {
        GameObject e = GameObject.Find("escaleras");
        if (!e) { EditorUtility.DisplayDialog("Sotano", "No se encontro \"escaleras\".", "OK"); return; }
        Transform vieja = e.transform.Find(Nombre);
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
        Aplicar(e.transform);
    }

    static void Aplicar(Transform escaleras)
    {
        // La rampa (con colision) que baja al sotano: "escalera"
        Transform rampa = escaleras.Find("escalera");
        if (!rampa || !rampa.GetComponent<Collider>()) { Debug.LogWarning("[Sotano] No encuentro la rampa \"escaleras/escalera\".", escaleras); return; }

        // Extremos de la rampa sobre su eje largo (X local)
        Vector3 eje = rampa.right;
        float mitad = rampa.lossyScale.x * 0.5f;
        Vector3 extA = rampa.position + eje * mitad, extB = rampa.position - eje * mitad;
        Vector3 arriba = extA.y > extB.y ? extA : extB;
        Vector3 abajo = extA.y > extB.y ? extB : extA;
        Vector3 bajada = abajo - arriba; bajada.y = 0f; bajada.Normalize();
        float ancho = rampa.lossyScale.z + 0.4f;
        Quaternion giro = Quaternion.LookRotation(bajada);

        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Barreras escalera");
        raiz.transform.SetParent(escaleras, false);
        raiz.transform.localPosition = Vector3.zero;

        // Arriba: justo al empezar la rampa, cerrada hasta que termine el tutorial
        var bArriba = CrearBarrera(raiz.transform, "Barrera Arriba", arriba + bajada * 0.2f, giro, ancho, true);

        // Abajo: al terminar la rampa, ya en el sotano; se cierra cuando el jugador pasa mas adelante
        var bAbajo = CrearBarrera(raiz.transform, "Barrera Abajo", abajo + bajada * 0.35f, giro, ancho, false);
        var zona = new GameObject("Zona Cerrar");
        zona.layer = 2;
        zona.transform.SetParent(bAbajo.transform, false);
        zona.transform.localPosition = new Vector3(0f, 0f, 2.2f);
        var caja = zona.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.center = new Vector3(0f, Alto * 0.5f, 0f);
        caja.size = new Vector3(ancho + 6f, Alto + 1f, 3.4f);
        bAbajo.zonaCerrar = caja;

        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>();
        Undo.RecordObject(tut, "Barrera escalera");
        UnityEventTools.AddVoidPersistentListener(tut.alTerminar, bArriba.Abrir);
        EditorUtility.SetDirty(tut);

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log("[Sotano] Barreras en la escalera: arriba se abre al terminar el tutorial, abajo se cierra al llegar al sotano. Guarda la escena.", raiz);
    }

    static Barrera CrearBarrera(Transform padre, string nombre, Vector3 pie, Quaternion giro, float ancho, bool cerrada)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pie, giro);
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, Alto * 0.5f - 0.3f, 0f);
        box.size = new Vector3(ancho, Alto, Grosor);
        var b = go.AddComponent<Barrera>();
        b.cerradaAlIniciar = cerrada;
        return b;
    }
}
