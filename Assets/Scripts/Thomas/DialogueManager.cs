using System;
using System.Collections;
using UnityEngine;
using TMPro;

// Unico dueño del panel de dialogo. Los NPCs y objetos no tocan la interfaz:
// solo llaman a DialogueManager.Instance.StartDialogue(asset).
// Se coloca en un objeto vacio de la escena (NO dentro del panel, porque el panel se desactiva).
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Interfaz")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Ajustes")]
    [SerializeField] private float typingSpeed = 0.05f;

    [Header("Jugador")]
    [Tooltip("Opcional. Si se deja vacio se busca solo el PlayerMovement de la escena.")]
    [SerializeField] private PlayerMovement playerMovement;
    private Rigidbody playerRb;

    // Estado de lectura para otros scripts
    public bool IsActive { get; private set; }
    public DialogueData CurrentData { get; private set; }

    // Eventos para que otros sistemas reaccionen (ej: musica, camara, misiones)
    // CurrentData ya tiene el dialogo cuando se lanza OnDialogueStarted.
    public event Action OnDialogueStarted;
    public event Action OnDialogueEnded;

    private int index;
    private bool isTyping;
    private bool skipTyping;
    private int startFrame = -1;
    private Coroutine typingCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        dialoguePanel.SetActive(false);
    }

    void Start()
    {
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (playerMovement != null)
            playerRb = playerMovement.GetComponent<Rigidbody>();
        else
            Debug.LogWarning("DialogueManager: no hay un PlayerMovement en la escena, no se bloqueara al jugador.");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Se ignora la tecla en el mismo frame en que empezo el dialogo
        // (evita que una misma pulsacion inicie y avance a la vez)
        if (!IsActive || Time.frameCount == startFrame) return;

        if (GameInput.NextDialogue)
        {
            if (isTyping)
                skipTyping = true;   // muestra de golpe lo que falta de la linea
            else
                NextLine();
        }
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
        LockPlayer();
        OnDialogueStarted?.Invoke();

        typingCoroutine = StartCoroutine(ShowLine());
    }

    // Cierra el dialogo (se llama solo al terminar, o desde fuera si hace falta)
    public void EndDialogue()
    {
        if (!IsActive) return;

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        isTyping = false;
        skipTyping = false;
        IsActive = false;
        CurrentData = null;
        dialoguePanel.SetActive(false);
        UnlockPlayer();

        OnDialogueEnded?.Invoke();
    }

    // Bloquea al jugador mientras dura una Conversacion (en Examinar puede seguir moviendose)
    private void LockPlayer()
    {
        if (CurrentData.modo == DialogueMode.Examinar) return;

        if (playerMovement != null) playerMovement.enabled = false;

        // Al desactivar PlayerMovement, el Rigidbody conservaria su ultima velocidad
        // y el jugador seguiria deslizandose: se frena en seco (se conserva la Y por la gravedad).
        if (playerRb != null)
        {
            playerRb.linearVelocity = new Vector3(0f, playerRb.linearVelocity.y, 0f);
            playerRb.angularVelocity = Vector3.zero;
        }
    }

    private void UnlockPlayer()
    {
        if (playerMovement != null) playerMovement.enabled = true;
    }

    private void NextLine()
    {
        // Se detiene la corrutina anterior (importante en modo Examinar, que espera sola)
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        index++;

        if (index < CurrentData.lineas.Length)
            typingCoroutine = StartCoroutine(ShowLine());
        else
            EndDialogue();
    }

    // Efecto de maquina de escribir con maxVisibleCharacters
    private IEnumerator ShowLine()
    {
        isTyping = true;
        skipTyping = false;

        dialogueText.text = CurrentData.lineas[index];
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int total = dialogueText.textInfo.characterCount;

        for (int i = 1; i <= total; i++)
        {
            if (skipTyping) break;

            dialogueText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typingSpeed);
        }

        // Linea completa (por haber terminado o por haberse saltado el efecto)
        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;

        // En modo Examinar la linea pasa sola tras unos segundos
        if (CurrentData.modo == DialogueMode.Examinar)
        {
            yield return new WaitForSeconds(CurrentData.segundosPorLinea);
            NextLine();
        }
    }
}
