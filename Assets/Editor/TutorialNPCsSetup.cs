using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// NPCs del tutorial de la tienda (cliente y cultista):
// - Si su modelo quedo desactivado (no se veia el cultista), se vuelve a activar.
// - Su Animator: parametro "Caminando" (TutorialTienda lo enciende mientras camina) con
//   idle <-> walk segun ese parametro, y sin root motion (el movimiento lo hace el tutorial).
// Se aplica sola; rehacer: Tools > Tienda > Arreglar NPCs del tutorial.
[InitializeOnLoad]
public static class TutorialNPCsSetup
{
    const string Param = "Caminando";

    static TutorialNPCsSetup()
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
        if (tut) Aplicar(tut, false);
    }

    [MenuItem("Tools/Tienda/Arreglar NPCs del tutorial")]
    static void Menu()
    {
        TutorialTienda tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        if (!tut) { EditorUtility.DisplayDialog("Tienda", "No hay TutorialTienda en la escena.", "OK"); return; }
        Aplicar(tut, true);
    }

    static void Aplicar(TutorialTienda tut, bool avisar)
    {
        bool cambio = false;
        foreach (Transform npc in new[] { tut.cliente, tut.cultista })
        {
            if (!npc) continue;
            cambio |= ActivarModelo(npc);
            foreach (Animator a in npc.GetComponentsInChildren<Animator>(true))
            {
                if (a.applyRootMotion)
                {
                    Undo.RecordObject(a, "Animator NPC");
                    a.applyRootMotion = false;
                    EditorUtility.SetDirty(a);
                    cambio = true;
                }
                if (a.runtimeAnimatorController is AnimatorController ac)
                {
                    ArreglarController(ac);
                    foreach (AnimationClip clip in ac.animationClips) EnBucle(clip);
                }
            }
        }
        if (cambio)
        {
            EditorSceneManager.MarkSceneDirty(tut.gameObject.scene);
            Debug.Log("[Tienda] NPCs del tutorial arreglados (modelo visible y animacion de caminar). Guarda la escena.", tut);
        }
        else if (avisar) Debug.Log("[Tienda] Los NPCs del tutorial ya estaban bien.", tut);
    }

    // El NPC no tiene nada visible: se activan los hijos apagados que llevan su modelo
    static bool ActivarModelo(Transform npc)
    {
        foreach (Renderer r in npc.GetComponentsInChildren<Renderer>(false))
            if (r.enabled && r.gameObject.activeInHierarchy) return false;
        bool cambio = false;
        foreach (Transform h in npc)
        {
            if (h.gameObject.activeSelf || !h.GetComponentInChildren<Renderer>(true)) continue;
            Undo.RecordObject(h.gameObject, "Modelo NPC");
            h.gameObject.SetActive(true);
            Debug.Log("[Tienda] \"" + npc.name + "/" + h.name + "\" estaba desactivado (por eso no se veia): activado.", h);
            cambio = true;
        }
        return cambio;
    }

    // Las animaciones de Mixamo vienen sin "Loop Time": caminar se pararia a mitad del recorrido
    static void EnBucle(AnimationClip clip)
    {
        if (clip == null || clip.isLooping) return;
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as ModelImporter;
        if (importer == null) return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool cambio = false;
        foreach (ModelImporterClipAnimation c in clips)
            if (!c.loopTime) { c.loopTime = true; cambio = true; }
        if (!cambio) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log("[Tienda] Animacion en bucle: " + importer.assetPath, clip);
    }

    // idle <-> walk segun "Caminando" (sin esperar a que termine la animacion)
    static void ArreglarController(AnimatorController ac)
    {
        if (ac.layers.Length == 0) return;
        foreach (AnimatorControllerParameter p in ac.parameters) if (p.name == Param) return;   // ya esta
        AnimatorStateMachine sm = ac.layers[0].stateMachine;
        AnimatorState idle = null, walk = null;
        foreach (ChildAnimatorState c in sm.states)
        {
            string n = c.state.name.ToLowerInvariant();
            if (n.Contains("idle") || n.Contains("quieto")) idle = c.state;
            else if (n.Contains("walk") || n.Contains("camin")) walk = c.state;
        }
        if (idle == null || walk == null) return;

        Undo.RecordObject(ac, "Animator NPC");
        ac.AddParameter(Param, AnimatorControllerParameterType.Bool);
        foreach (AnimatorStateTransition t in idle.transitions) idle.RemoveTransition(t);
        foreach (AnimatorStateTransition t in walk.transitions) walk.RemoveTransition(t);

        AnimatorStateTransition aCaminar = idle.AddTransition(walk);
        aCaminar.hasExitTime = false;
        aCaminar.duration = 0.2f;
        aCaminar.AddCondition(AnimatorConditionMode.If, 0f, Param);

        AnimatorStateTransition aQuieto = walk.AddTransition(idle);
        aQuieto.hasExitTime = false;
        aQuieto.duration = 0.2f;
        aQuieto.AddCondition(AnimatorConditionMode.IfNot, 0f, Param);

        sm.defaultState = idle;
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Tienda] Animator \"" + ac.name + "\": camina con el parametro \"" + Param + "\" (idle al parar).", ac);
    }
}
