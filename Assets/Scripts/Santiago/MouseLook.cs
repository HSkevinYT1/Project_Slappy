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
    private float pendingYaw;
    private float playerYaw;
    private float peekLean;
    private Vector3 startLocalPosition;

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
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        pendingYaw += mouseX;

        xRotation = Mathf.Clamp(xRotation - mouseY, -80f, 80f);

        float peekInput = (Input.GetKey("e") ? 1f : 0f)
                        - (Input.GetKey("q") ? 1f : 0f);
        float blend = 1f - Mathf.Exp(-peekSmooth * Time.deltaTime);
        peekLean = Mathf.Lerp(peekLean, peekInput, blend);

        // aplica la rotacion de la camara y la inclinacion al mirar hacia los lados (peeking)
        transform.localRotation = Quaternion.Euler(xRotation, 0f, -peekRollAngle * peekLean);

        // controla la posicion de la camara al mirar hacia los lados (peeking)
        float targetX = startLocalPosition.x + peekDistance * peekLean;
        float currentX = Mathf.Lerp(transform.localPosition.x, targetX, blend);
        transform.localPosition = new Vector3(currentX, transform.localPosition.y, startLocalPosition.z);
    }

    void FixedUpdate()
    {
        if (playerRb == null)
            return;

        // aplica la rotacion del cuerpo del jugador
        playerYaw = Mathf.Repeat(playerYaw + pendingYaw, 360f);
        pendingYaw = 0f;
        playerRb.angularVelocity = Vector3.zero;
        playerRb.MoveRotation(Quaternion.Euler(0f, playerYaw, 0f));
    }
}
