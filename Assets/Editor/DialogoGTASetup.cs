using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Cambia el panel de dialogo del prefab Canvas al estilo de subtitulos de GTA V:
// sin caja negra, texto centrado abajo, fuente sans limpia (LiberationSans), tamano de subtitulo.
// (La sombra, los nombres en color y el tiempo de cada linea los maneja DialogueManager.)
// Se aplica sola una vez; rehacer: Tools > Dialogo > Estilo GTA V.
[InitializeOnLoad]
public static class DialogoGTASetup
{
    const string RutaPrefab = "Assets/Prefabs/Canvas.prefab";
    const string RutaFuente = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    static DialogoGTASetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
            if (!fuente || !prefab) return;
            var texto = BuscarTexto(prefab);
            if (texto && texto.font != fuente) Aplicar();
        };
    }

    static TMP_Text BuscarTexto(GameObject raiz)
    {
        foreach (TMP_Text t in raiz.GetComponentsInChildren<TMP_Text>(true))
            if (t.name == "Text_dialogue_1") return t;
        return null;
    }

    [MenuItem("Tools/Dialogo/Estilo GTA V")]
    static void Aplicar()
    {
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
        GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPrefab);
        try
        {
            TMP_Text texto = BuscarTexto(raiz);
            if (!texto) { Debug.LogWarning("[Dialogo] No se encontro Text_dialogue_1 en el Canvas."); return; }
            Transform panel = texto.transform.parent;

            // Panel: sin caja (el Image queda transparente), una franja abajo a lo ancho
            if (panel.TryGetComponent(out Image img)) { img.color = new Color(0f, 0f, 0f, 0f); img.raycastTarget = false; }
            var rp = (RectTransform)panel;
            rp.localRotation = Quaternion.identity;
            rp.anchorMin = new Vector2(0f, 0f);
            rp.anchorMax = new Vector2(1f, 0f);
            rp.pivot = new Vector2(0.5f, 0f);
            rp.anchoredPosition = Vector2.zero;
            rp.sizeDelta = new Vector2(0f, 260f);

            // Texto: centrado, pegado abajo, ancho de subtitulo (60% de la pantalla)
            var rt = (RectTransform)texto.transform;
            rt.anchorMin = new Vector2(0.2f, 0f);
            rt.anchorMax = new Vector2(0.8f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, 70f);
            rt.offsetMax = new Vector2(0f, 0f);
            texto.font = fuente;
            texto.fontSharedMaterial = fuente.material;
            texto.fontSize = 34f;
            texto.fontStyle = FontStyles.Normal;
            texto.alignment = TextAlignmentOptions.Bottom;
            texto.textWrappingMode = TextWrappingModes.Normal;
            texto.lineSpacing = -4f;
            texto.color = Color.white;
            texto.raycastTarget = false;
            texto.text = "";

            PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            Debug.Log("[Dialogo] Panel de dialogo con estilo de subtitulos GTA V (prefab Canvas). Ya no bloquea al jugador.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }
}
