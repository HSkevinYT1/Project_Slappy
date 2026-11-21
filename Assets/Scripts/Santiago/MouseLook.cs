using UnityEngine;

// este script controla la camara y el movimiento de la cabeza del jugador, incluyendo la inclinacion al mirar hacia los lados (peeking)
public class MouseLook : MonoBehaviour
{
    [Header("Look")]
    public float mouseSensitivity = 100f;
    public Transform playerBody;

    [Header("Peeking")]
    [SerializeField] private float peekDistance = 0.25f;
    [SerializeField] private float peekRollAngle = 10f;
    [SerializeField] private float peekSmooth = 8f;

    private Rigidbody playerRb;
    private float xRotation;
    // Arriba/abajo igual que izquierda/derecha: el mouse se acumula, se aplica en cada paso de fisica y la camara
    // se interpola entre el paso anterior y el actual (asi los dos ejes se sienten igual de suaves)
    private float pendingPitch;
    private float pitchAnterior;
    private float pendingYaw;
    private float playerYaw;
    private float peekLean;
    private Vector3 startLocalPosition;

    // Al reactivarse toma el giro actual del cuerpo (ej: tras un teletransporte que lo haya girado)
    void OnEnable()
    {
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
        pendingYaw += mouseX;
        pendingPitch -= mouseY;

        // Sin cuerpo con fisica no hay pasos de fisica que esperar: se aplica directo
        if (playerRb == null) { xRotation = Mathf.Clamp(xRotation + pendingPitch, -80f, 80f); pitchAnterior = xRotation; pendingPitch = 0f; }
        float alfa = Mathf.Clamp01((Time.time - Time.fixedTime) / Mathf.Max(0.0001f, Time.fixedDeltaTime));
        float pitch = Mathf.Lerp(pitchAnterior, xRotation, alfa);

        float peekInput = (Input.GetKey("e") ? 1f : 0f)
                        - (Input.GetKey("q") ? 1f : 0f);
        float blend = 1f - Mathf.Exp(-peekSmooth * Time.deltaTime);
        peekLean = Mathf.Lerp(peekLean, peekInput, blend);

        // aplica la rotacion de la camara y la inclinacion al mirar hacia los lados (peeking)
        transform.localRotation = Quaternion.Euler(pitch, 0f, -peekRollAngle * peekLean);

        // controla la posicion de la camara al mirar hacia los lados (peeking)
        float targetX = startLocalPosition.x + peekDistance * peekLean;
        float currentX = Mathf.Lerp(transform.localPosition.x, targetX, blend);
        transform.localPosition = new Vector3(currentX, transform.localPosition.y, startLocalPosition.z);
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
