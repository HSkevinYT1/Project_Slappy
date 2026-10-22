using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Tooltip("Objeto raiz del menu: se activa al pausar y se oculta al continuar.")]
    public GameObject pausePanel;
    public KeyCode pauseKey = KeyCode.Escape;

    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    // scripts del jugador que se apagan mientras esta en pausa
    private readonly List<Behaviour> disabledWhilePaused = new List<Behaviour>();

    void Start()
    {
        if (pausePanel == null)
        {
            Debug.LogWarning("PauseMenu: no hay un pausePanel asignado.");
        }
        else
        {
            pausePanel.SetActive(false);
        }
        IsPaused = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    void OnDestroy()
    {
        // por si se cambia de escena estando en pausa
        if (IsPaused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsPaused = false;
        }
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // la camara, la linterna y las interacciones leen el input directamente,
        // asi que se apagan para que no reaccionen mientras el menu esta abierto
        disabledWhilePaused.Clear();
        DisableAll<MouseLook>();
        DisableAll<Flashlight>();
        DisableAll<PlayerInteractor>();
        DisableAll<DialogueManager>();
        DisableAll<GraspObject>();

        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);

        foreach (Behaviour b in disabledWhilePaused)
        {
            if (b != null) b.enabled = true;
        }
        disabledWhilePaused.Clear();

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;

        AudioListener.pause = false;
        Time.timeScale = 1f;
        IsPaused = false;
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void DisableAll<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (!b.enabled) continue;
            b.enabled = false;
            disabledWhilePaused.Add(b);
        }
    }
}
