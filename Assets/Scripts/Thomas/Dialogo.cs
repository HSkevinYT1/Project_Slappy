using System.Collections;
using UnityEngine;
using TMPro;

// Sistema de dialogo simple (estilo Undertale).
// El nombre de la clase debe coincidir con el del archivo: Dialogo.cs
public class Dialogo : MonoBehaviour
{
    [SerializeField, TextArea(4, 10)] private string[] lineas_dialogo;
    [SerializeField] private GameObject marca_dialogo;
    [SerializeField] private GameObject dialogue_panel_1;
    [SerializeField] private TMP_Text text_dialogue_1;
    [SerializeField] private float typingSpeed = 0.05f;

    [Header("Teclas")]
    // Tecla para iniciar la conversacion
    [SerializeField] private KeyCode startKey = KeyCode.E;
    // Tecla para completar la linea y pasar a la siguiente
    [SerializeField] private KeyCode nextKey = KeyCode.Space;

    private bool isPlayerInRange;
    private bool didDialogueStart;
    private bool isTyping;
    private int index_dialogue;
    private Coroutine typingCoroutine;

    void Update()
    {
        if (!isPlayerInRange) return;

        if (!didDialogueStart)
        {
            // E (por defecto) inicia la conversacion
            if (Input.GetKeyDown(startKey)) Start_dialogue();
        }
        else if (Input.GetKeyDown(nextKey))
        {
            // Barra espaciadora (por defecto) avanza el dialogo
            if (isTyping)
                CompleteLine();   // Si todavia se esta escribiendo, se muestra completa la linea
            else
                NextLine();
        }
    }

    // Inicia el dialogo: activa el panel y muestra la primera linea
    private void Start_dialogue()
    {
        if (lineas_dialogo == null || lineas_dialogo.Length == 0) return;

        didDialogueStart = true;
        dialogue_panel_1.SetActive(true);
        marca_dialogo.SetActive(false);
        index_dialogue = 0;
        typingCoroutine = StartCoroutine(ShowLine());
    }

    // Pasa a la siguiente linea o cierra el dialogo si era la ultima
    private void NextLine()
    {
        index_dialogue++;

        if (index_dialogue < lineas_dialogo.Length)
            typingCoroutine = StartCoroutine(ShowLine());
        else
            End_dialogue();
    }

    // Efecto de maquina de escribir con maxVisibleCharacters
    // (funciona bien con las etiquetas de TextMeshPro como <b> o <color>)
    private IEnumerator ShowLine()
    {
        isTyping = true;

        text_dialogue_1.text = lineas_dialogo[index_dialogue];
        text_dialogue_1.maxVisibleCharacters = 0;
        text_dialogue_1.ForceMeshUpdate();

        int total = text_dialogue_1.textInfo.characterCount;

        for (int i = 1; i <= total; i++)
        {
            text_dialogue_1.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    // Muestra de golpe lo que falta de la linea actual
    private void CompleteLine()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        text_dialogue_1.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
    }

    // Cierra el panel y deja todo listo para volver a hablar
    private void End_dialogue()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        didDialogueStart = false;
        isTyping = false;
        index_dialogue = 0;
        dialogue_panel_1.SetActive(false);
        marca_dialogo.SetActive(isPlayerInRange);
    }

    // Entrando a la zona delimitada por el collider, se activa isPlayerInRange
    private void OnTriggerEnter(Collider collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
            if (!didDialogueStart) marca_dialogo.SetActive(true);
        }
    }

    // Saliendo de la zona, se desactiva isPlayerInRange y se cierra el dialogo si estaba abierto
    private void OnTriggerExit(Collider collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;
            marca_dialogo.SetActive(false);

            if (didDialogueStart) End_dialogue();
        }
    }
}