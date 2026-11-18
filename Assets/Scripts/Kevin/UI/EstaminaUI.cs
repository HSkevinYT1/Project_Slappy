using UnityEngine;
using UnityEngine.UI;

// Barra de estamina (abajo al centro). Lee PlayerMovement.Estamina.
// Se esconde sola cuando esta llena y aparece al correr; en rojo y parpadeando si el jugador se agoto.
// El HUD la crea sola; no hace falta ponerla en la escena.
public class EstaminaUI : MonoBehaviour
{
    const float Ancho = 380f, Alto = 9f, Abajo = 64f;

    static readonly Color Normal = new Color(0.92f, 0.9f, 0.86f, 0.95f);
    static readonly Color Baja = new Color(1f, 0.55f, 0.25f, 0.95f);
    static readonly Color Agotada = new Color(0.85f, 0.12f, 0.1f, 0.95f);

    Canvas canvas;
    CanvasGroup grupo;
    RectTransform relleno, rastro, minimo;
    Image rellenoImg;
    PlayerMovement jugador;
    float rastroValor = 1f, visibleHasta;

    void Awake()
    {
        canvas = HUD.CrearCanvas("HUD Estamina", 42);
        canvas.transform.SetParent(transform, false);
        grupo = canvas.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        RectTransform barra = HUD.Rect(canvas.transform, "Barra", new Vector2(0.5f, 0f), new Vector2(0f, Abajo), new Vector2(Ancho, Alto));
        HUD.Imagen(barra, new Color(0f, 0f, 0f, 0.55f));
        HUD.Borde(barra, new Color(1f, 1f, 1f, 0.2f), 1.5f);

        // Rastro: lo que se acaba de gastar, se vacia con retraso (como en los juegos de pelea)
        rastro = HUD.Rect(barra, "Rastro", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        HUD.Imagen(rastro, new Color(1f, 1f, 1f, 0.25f));
        relleno = HUD.Rect(barra, "Relleno", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        rellenoImg = HUD.Imagen(relleno, Normal);

        // Marca del minimo para volver a correr tras agotarse
        minimo = HUD.Rect(barra, "Minimo", new Vector2(0.3f, 0f), new Vector2(0.3f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 0f));
        HUD.Imagen(minimo, new Color(1f, 1f, 1f, 0.35f));
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void Update()
    {
        if (jugador == null)
        {
            Transform t = HUD.Jugador();
            if (t != null) jugador = t.GetComponent<PlayerMovement>();
            if (jugador == null) return;
        }

        float e = jugador.Estamina;
        relleno.anchorMax = new Vector2(e, 1f);
        rastroValor = e >= rastroValor ? e : Mathf.MoveTowards(rastroValor, e, Time.deltaTime * 0.6f);
        rastro.anchorMax = new Vector2(rastroValor, 1f);
        // El marcador del minimo sigue al valor configurado
        minimo.anchorMin = new Vector2(jugador.minimoTrasAgotarse, 0f);
        minimo.anchorMax = new Vector2(jugador.minimoTrasAgotarse, 1f);

        if (jugador.Agotado)
        {
            float p = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            rellenoImg.color = new Color(Agotada.r, Agotada.g, Agotada.b, p);
        }
        else rellenoImg.color = Color.Lerp(Baja, Normal, Mathf.InverseLerp(0.15f, 0.5f, e));

        bool ocupado = e < 0.999f || jugador.Corriendo;
        if (ocupado) visibleHasta = Time.time + 1.2f;
        bool mostrar = Time.time < visibleHasta && jugador.enabled && !HUD.Oculto && !PantallaMuerte.Muerto && !PauseMenu.IsPaused;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, mostrar ? 1f : 0f, Time.unscaledDeltaTime * (mostrar ? 5f : 2f));
    }
}
