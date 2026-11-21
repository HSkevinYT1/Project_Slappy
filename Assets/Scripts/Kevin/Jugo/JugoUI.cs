using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Jugo para un boton o deslizador de UI (lo usa el menu de pausa). Todo en tiempo real (el juego esta en pausa):
// - Entrar(retraso): llega desde la izquierda pasandose un poco, con un fundido (para entradas escalonadas).
// - Al pasar el mouse o seleccionarlo con teclado: se corre a la derecha, crece un poco y aparece una marca roja
//   a la izquierda.
// - Al apretarlo: se aplasta y vuelve con rebote.
public class JugoUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, ISelectHandler, IDeselectHandler
{
    [Tooltip("Lo que se mueve y escala (ej: el texto). Vacio = este mismo objeto")]
    public RectTransform visual;
    [Tooltip("Marca que crece a la izquierda al pasar el mouse (opcional)")]
    public RectTransform marca;
    public bool moverPosicion = true;
    public float desplazarHover = 16f;
    public float escalaHover = 1.06f;
    public float distanciaEntrada = 80f;

    CanvasGroup grupo;
    Selectable selectable;
    Vector2 posBase;
    float anchoMarca;
    bool encima, seleccionado;
    float hover, pulso, entrada = 1f, tEntrada = -1f, retraso;
    bool iniciado;

    // Se inicia la primera vez que se usa (asi se pueden asignar los campos justo despues de AddComponent)
    void Iniciar()
    {
        if (iniciado) return;
        iniciado = true;
        if (visual == null) visual = (RectTransform)transform;
        posBase = visual.anchoredPosition;
        selectable = GetComponent<Selectable>();
        if (moverPosicion)
        {
            grupo = GetComponent<CanvasGroup>();
            if (grupo == null) grupo = gameObject.AddComponent<CanvasGroup>();
        }
        if (marca != null) anchoMarca = marca.sizeDelta.x;
    }

    // Empieza la animacion de entrada despues de "retraso" segundos
    public void Entrar(float retraso)
    {
        Iniciar();
        this.retraso = retraso;
        tEntrada = 0f;
        entrada = 0f;
        hover = 0f;
        Aplicar();
    }

    public void OnPointerEnter(PointerEventData e) => encima = true;
    public void OnPointerExit(PointerEventData e) => encima = false;
    public void OnSelect(BaseEventData e) => seleccionado = true;
    public void OnDeselect(BaseEventData e) => seleccionado = false;

    public void OnPointerDown(PointerEventData e)
    {
        if (selectable != null && !selectable.IsInteractable()) return;
        pulso = 1f;
    }

    void OnDisable()
    {
        encima = false;
        hover = 0f;
        pulso = 0f;
        Aplicar();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool activo = (encima || seleccionado) && (selectable == null || selectable.IsInteractable());
        hover = Mathf.MoveTowards(hover, activo ? 1f : 0f, dt * 9f);
        pulso = Mathf.MoveTowards(pulso, 0f, dt * 5f);
        if (tEntrada >= 0f)
        {
            tEntrada += dt;
            float t = (tEntrada - retraso) / 0.4f;
            entrada = t <= 0f ? 0f : Mathf.Min(Jugo.SalidaAtras(t, 2f), 1.2f);
            if (t >= 1f) { entrada = 1f; tEntrada = -1f; }
        }
        Aplicar();
    }

    void Aplicar()
    {
        Iniciar();
        if (visual == null) return;
        float h = Jugo.SalidaCubica(hover);
        if (moverPosicion)
            visual.anchoredPosition = posBase + new Vector2(desplazarHover * h - distanciaEntrada * (1f - entrada), 0f);
        // Al apretar: se aplasta y vuelve (el seno baja y sube una vez)
        float aplaste = 1f - 0.1f * Mathf.Sin(pulso * Mathf.PI);
        visual.localScale = Vector3.one * Mathf.LerpUnclamped(1f, escalaHover, h) * aplaste;
        if (grupo != null) grupo.alpha = Mathf.Clamp01(entrada);
        if (marca != null)
        {
            marca.sizeDelta = new Vector2(anchoMarca * h, marca.sizeDelta.y);
            marca.gameObject.SetActive(h > 0.01f);
        }
    }
}
