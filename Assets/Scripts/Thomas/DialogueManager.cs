using System;
using System.Collections;
using UnityEngine;
using TMPro;

// Unico dueño de los subtitulos de dialogo. Los NPCs y objetos no tocan la interfaz:
// solo llaman a DialogueManager.Instance.StartDialogue(asset).
// Se coloca en un objeto vacio de la escena (NO dentro del panel, porque el panel se desactiva).
//
// Estilo GTA V: subtitulos abajo al centro, sin caja, texto blanco con sombra y el nombre de quien
// habla en color. Cada linea aparece completa, se queda lo que tarda en leerse y pasa sola.
// El jugador se sigue moviendo e interactuando mientras se habla.
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Interfaz")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Tiempo de cada linea")]
    [Tooltip("Segundos minimos que se ve una linea")]
    [SerializeField] private float segundosMinimos = 2f;
    [Tooltip("Segundos que se suman por cada letra (lo que tarda en leerse)")]
    [SerializeField] private float segundosPorLetra = 0.055f;
    [Tooltip("Segundos maximos que se ve una linea")]
    [SerializeField] private float segundosMaximos = 7f;
    [Tooltip("Pausa entre una linea y la siguiente")]
    [SerializeField] private float pausaEntreLineas = 0.25f;
    [Tooltip("Duracion de la aparicion y desaparicion del subtitulo")]
    [SerializeField] private float fundido = 0.15f;
    [Tooltip("La tecla de avanzar dialogo (Espacio) adelanta la linea actual")]
    [SerializeField] private bool permitirAdelantar = true;

    [Header("Estilo")]
    [SerializeField] private Color colorTexto = Color.white;
    [SerializeField] private Color colorExaminar = new Color(0.82f, 0.82f, 0.82f);
    [Tooltip("Sombra/contorno negro para que se lea sobre cualquier fondo")]
    [SerializeField] private bool sombra = true;

    // Estado de lectura para otros scripts
    public bool IsActive { get; private set; }
    public DialogueData CurrentData { get; private set; }

    // Eventos para que otros sistemas reaccionen (ej: musica, camara, misiones)
    // CurrentData ya tiene el dialogo cuando se lanza OnDialogueStarted.
    public event Action OnDialogueStarted;
    public event Action OnDialogueEnded;

    private int index;
    private bool adelantar;
    private int startFrame = -1;
    private Coroutine rutina;
    private CanvasGroup grupo;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        grupo = dialoguePanel.GetComponent<CanvasGroup>();
        if (grupo == null) grupo = dialoguePanel.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;
        if (sombra) AplicarSombra();
        dialoguePanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Se ignora la tecla en el mismo frame en que empezo el dialogo
        // (evita que una misma pulsacion inicie y adelante a la vez)
        if (!IsActive || !permitirAdelantar || Time.frameCount == startFrame) return;
        if (GameInput.NextDialogue) adelantar = true;
    }

    // Inicia un dialogo con el contenido de un asset DialogueData
    public void StartDialogue(DialogueData data)
    {
        if (IsActive) return;
        if (data == null || data.lineas == null || data.lineas.Length == 0) return;

        CurrentData = data;
        index = 0;
        IsActive = true;
        startFrame = Time.frameCount;

        dialoguePanel.SetActive(true);
        grupo.alpha = 0f;
        OnDialogueStarted?.Invoke();

        rutina = StartCoroutine(Mostrar());
    }

    // Cierra el dialogo (se llama solo al terminar, o desde fuera si hace falta)
    public void EndDialogue()
    {
        if (!IsActive) return;

        if (rutina != null) StopCoroutine(rutina);
        rutina = null;

        adelantar = false;
        IsActive = false;
        CurrentData = null;
        dialoguePanel.SetActive(false);

        OnDialogueEnded?.Invoke();
    }

    // Recorre las lineas: aparece, se queda lo que tarda en leerse (o hasta que se adelante) y desaparece
    private IEnumerator Mostrar()
    {
        for (index = 0; index < CurrentData.lineas.Length; index++)
        {
            string linea = CurrentData.lineas[index];
            if (string.IsNullOrWhiteSpace(linea)) continue;

            dialogueText.text = Formatear(linea, out int letras);
            dialogueText.maxVisibleCharacters = int.MaxValue;
            adelantar = false;

            yield return Fundir(1f);

            float duracion = Mathf.Clamp(segundosMinimos * 0.5f + letras * segundosPorLetra, segundosMinimos, segundosMaximos)
                             + CurrentData.segundosExtra;
            for (float t = 0f; t < duracion && !adelantar; t += Time.deltaTime) yield return null;

            yield return Fundir(0f);
            if (index < CurrentData.lineas.Length - 1)
                for (float t = 0f; t < pausaEntreLineas; t += Time.deltaTime) yield return null;
        }

        rutina = null;
        EndDialogue();
    }

    private IEnumerator Fundir(float destino)
    {
        float inicio = grupo.alpha;
        if (fundido <= 0f) { grupo.alpha = destino; yield break; }
        for (float t = 0f; t < fundido; t += Time.deltaTime)
        {
            grupo.alpha = Mathf.Lerp(inicio, destino, t / fundido);
            yield return null;
        }
        grupo.alpha = destino;
    }

    // "Nombre: texto" con el nombre en color. Una linea puede traer su propio hablante: "[Nombre] texto".
    private string Formatear(string linea, out int letras)
    {
        string hablante = CurrentData.hablante;
        Color colorNombre = CurrentData.colorHablante;
        string texto = linea.Trim();

        if (texto.StartsWith("["))
        {
            int cierre = texto.IndexOf(']');
            if (cierre > 0)
            {
                hablante = texto.Substring(1, cierre - 1).Trim();
                texto = texto.Substring(cierre + 1).TrimStart();
            }
        }

        letras = texto.Length;
        bool examinar = CurrentData.modo == DialogueMode.Examinar;
        string cuerpo = $"<color=#{ColorUtility.ToHtmlStringRGB(examinar ? colorExaminar : colorTexto)}>{texto}</color>";
        if (examinar || string.IsNullOrEmpty(hablante)) return cuerpo;
        return $"<color=#{ColorUtility.ToHtmlStringRGB(colorNombre)}>{hablante}:</color> {cuerpo}";
    }

    // Sombra suave y un contorno fino (instancia del material: no cambia la fuente para otros textos)
    private void AplicarSombra()
    {
        Material m = dialogueText.fontMaterial;
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
        dialogueText.outlineWidth = 0.12f;
        dialogueText.outlineColor = new Color32(0, 0, 0, 220);
    }
}
