using UnityEngine;

// este script controla la camara y el movimiento de la cabeza del jugador
public class MouseLook : MonoBehaviour
{
    [Header("Look")]
    public float mouseSensitivity = 100f;
    public Transform playerBody;

    private Rigidbody playerRb;
    private float xRotation;
    // Arriba/abajo igual que izquierda/derecha: el mouse se acumula, se aplica en cada paso de fisica y la camara
    // se interpola entre el paso anterior y el actual (asi los dos ejes se sienten igual de suaves)
    private float pendingPitch;
    private float pitchAnterior;
    private float pendingYaw;
    private float playerYaw;
    private Vector3 startLocalPosition;

    private int framesSinMouse;

    void OnApplicationFocus(bool foco)
    {
        if (foco) framesSinMouse = 3;
    }

    // Al reactivarse toma el giro actual del cuerpo (ej: tras un teletransporte que lo haya girado)
    void OnEnable()
    {
        framesSinMouse = 2;   // al salir de la pausa tampoco cuenta el salto del mouse
        if (playerBody == null) return;
        playerYaw = playerBody.eulerAngles.y;
        pendingYaw = 0f;
        pendingPitch = 0f;
        pitchAnterior = xRotation;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerBody != null)
        {
            playerRb = playerBody.GetComponent<Rigidbody>();
            Vector3 bodyEuler = playerBody.eulerAngles;
            playerYaw = bodyEuler.y;
            playerBody.rotation = Quaternion.Euler(0f, playerYaw, 0f);
        }

        startLocalPosition = transform.localPosition;
        transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        // El mouse ya da cuanto se movio en este frame: no se multiplica por Time.deltaTime (eso hacia que cada frame
        // girara distinto segun lo que tardo, y la mirada iba a tirones). 1/60 = la misma sensibilidad de antes a 60 fps.
        const float escala = 1f / 60f;
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * escala;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * escala;
        // Sin foco (clic en otro monitor) o justo al volver: el mouse trae un salto enorme y la camara daba un tiron
        if (!Application.isFocused || framesSinMouse > 0) { mouseX = mouseY = 0f; if (framesSinMouse > 0) framesSinMouse--; }
        mouseX = Mathf.Clamp(mouseX, -30f, 30f);
        mouseY = Mathf.Clamp(mouseY, -30f, 30f);
        pendingYaw += mouseX;
        pendingPitch -= mouseY;

        // Sin cuerpo con fisica no hay pasos de fisica que esperar: se aplica directo
        if (playerRb == null) { xRotation = Mathf.Clamp(xRotation + pendingPitch, -80f, 80f); pitchAnterior = xRotation; pendingPitch = 0f; }
        float alfa = Mathf.Clamp01((Time.time - Time.fixedTime) / Mathf.Max(0.0001f, Time.fixedDeltaTime));
        float pitch = Mathf.Lerp(pitchAnterior, xRotation, alfa);

        // aplica la rotacion de la camara (ya no hay asomarse con Q/E: la camara no se mueve hacia los lados)
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.localPosition = new Vector3(startLocalPosition.x, transform.localPosition.y, startLocalPosition.z);
    }

    void FixedUpdate()
    {
        if (playerRb == null)
            return;

        // arriba/abajo de la camara (se interpola en Update hasta el proximo paso)
        pitchAnterior = xRotation;
        xRotation = Mathf.Clamp(xRotation + pendingPitch, -80f, 80f);
        pendingPitch = 0f;

        // aplica la rotacion del cuerpo del jugador
        playerYaw = Mathf.Repeat(playerYaw + pendingYaw, 360f);
        pendingYaw = 0f;
        // Escondido (armario) el cuerpo es kinematic: ahi no se puede tocar la velocidad (Unity avisaba 50 veces por segundo)
        if (!playerRb.isKinematic) playerRb.angularVelocity = Vector3.zero;
        playerRb.MoveRotation(Quaternion.Euler(0f, playerYaw, 0f));
    }
}
