using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Al abrir el juego (antes del menu): "Se recomienda el uso de auriculares para una mejor experiencia", con unos
// auriculares dibujados. Aparece y se va con un fundido; cualquier tecla o clic la salta. Una vez por sesion
// (no al volver al inicio). Se crea sola en la escena del menu.
public class PantallaAuriculares : MonoBehaviour
{
    public string texto = "Se recomienda el uso de auriculares\npara una mejor experiencia";
    public float segundos = 3.2f;

    static bool mostrada;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => mostrada = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        if (mostrada || !FindFirstObjectByType<MenuPrincipal>()) return;
        mostrada = true;
        new GameObject("Pantalla Auriculares").AddComponent<PantallaAuriculares>();
    }

    IEnumerator Start()
    {
        Canvas c = HUD.CrearCanvas("Auriculares (UI)", 2000, transform);
        var grupo = c.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 1f;
        // Fondo negro que tapa el menu (y no deja hacer clic en el mientras tanto)
        Image fondo = HUD.Imagen(HUD.Llenar(c.transform, "Fondo"), Color.black);
        fondo.raycastTarget = true;

        RectTransform contenido = HUD.Llenar(c.transform, "Contenido");
        var cg = contenido.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        Color claro = new Color(0.86f, 0.84f, 0.8f);
        Auriculares(contenido, new Vector2(0f, 70f), claro);
        TextMeshProUGUI t = HUD.Texto(contenido, "Texto", texto, 30f, TextAlignmentOptions.Center, claro);
        var rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1200f, 120f);
        rt.anchoredPosition = new Vector2(0f, -70f);
        t.lineSpacing = 8f;

        // Aparece, se queda y se va (se puede saltar)
        float tiempo = 0f;
        yield return Fundido(cg, 0f, 1f, 0.9f);
        while (tiempo < segundos)
        {
            tiempo += Time.unscaledDeltaTime;
            if (Saltar()) break;
            yield return null;
        }
        yield return Fundido(cg, 1f, 0f, 0.6f);
        yield return Fundido(grupo, 1f, 0f, 0.7f);
        Destroy(gameObject);
    }

    static IEnumerator Fundido(CanvasGroup g, float de, float a, float duracion)
    {
        for (float t = 0f; t < duracion; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(de, a, Mathf.SmoothStep(0f, 1f, t / duracion));
            yield return null;
        }
        g.alpha = a;
    }

    static bool Saltar()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
        return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    }

    // Auriculares: la vincha (medio anillo) y dos almohadillas
    static void Auriculares(RectTransform padre, Vector2 centro, Color color)
    {
        RectTransform ic = HUD.Rect(padre, "Auriculares", new Vector2(0.5f, 0.5f), centro, new Vector2(160f, 140f));
        // Vincha: anillo con la mitad de abajo tapada (con una mascara)
        RectTransform mascara = HUD.Rect(ic, "Vincha", new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(150f, 82f));
        mascara.pivot = new Vector2(0.5f, 1f);
        mascara.anchoredPosition = Vector2.zero;
        var mi = mascara.gameObject.AddComponent<Image>();
        mi.color = Color.white;
        mascara.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        RectTransform aro = HUD.Rect(mascara, "Aro", new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(150f, 150f));
        aro.pivot = new Vector2(0.5f, 1f);
        aro.anchoredPosition = Vector2.zero;
        HUD.Imagen(aro, color, HUD.Circulo(0.09f));
        // Bajadas de la vincha y almohadillas
        foreach (float s in new[] { -1f, 1f })
        {
            RectTransform bajada = HUD.Rect(ic, "Bajada", new Vector2(0.5f, 0.5f), new Vector2(s * 68f, -6f), new Vector2(13f, 40f));
            HUD.Imagen(bajada, color);
            RectTransform almohadilla = HUD.Rect(ic, "Almohadilla", new Vector2(0.5f, 0.5f), new Vector2(s * 62f, -42f), new Vector2(38f, 56f));
            HUD.Imagen(almohadilla, color, HUD.Circulo());
        }
    }
}
