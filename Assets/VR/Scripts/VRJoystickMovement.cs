using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class VRJoystickMovement : MonoBehaviour
{
    [Header("Referencias")]
    public Transform vrCamera;
    [Tooltip("Mientras este apagado (minijuego, pausa) el jugador no se mueve ni gira.")]
    public PlayerInteractor interactor;
    private CharacterController characterController;

    [Header("Movimiento")]
    public float moveSpeed = 2f;

    [Header("Giro")]
    // Grados de cada giro con el stick derecho. Girar a saltos marea menos que un giro continuo
    public float snapTurnAngle = 45f;
    private bool turnReady = true;

    [Header("Gravedad")]
    public float gravity = -9.81f;
    private float verticalVelocity;

    // En una conversacion tampoco. Asi los scripts compartidos no tienen que saber nada de VR
    private bool Locked
    {
        get
        {
            if (interactor != null && !interactor.enabled) return true;

            DialogueManager dialogue = DialogueManager.Instance;
            return dialogue != null && dialogue.IsActive && dialogue.CurrentData.modo == DialogueMode.Conversacion;
        }
    }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        VRSession.Start();
    }

    void Update()
    {
        MovePlayer();
        if (!Locked) TurnPlayer();
    }

    void TurnPlayer()
    {
        float turn = GameInput.ReadStick("Girar").x;

        // El stick tiene que volver al centro para poder girar otra vez
        if (Mathf.Abs(turn) < 0.3f)
        {
            turnReady = true;
            return;
        }

        if (!turnReady || Mathf.Abs(turn) < 0.7f || vrCamera == null)
            return;

        // Se gira alrededor de la cabeza para que el jugador no se desplace al girar
        transform.RotateAround(vrCamera.position, Vector3.up, Mathf.Sign(turn) * snapTurnAngle);
        turnReady = false;
    }

    void MovePlayer()
    {
        // Leer entrada del Input Manager clásico
        Vector2 input = Locked ? Vector2.zero : GameInput.ReadStick("Mover");
        float horizontal = input.x;
        float vertical = input.y;

        if (vrCamera == null)
            return;

        // Dirección horizontal según la cámara VR
        Vector3 forward = vrCamera.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = vrCamera.right;
        right.y = 0f;
        right.Normalize();

        // Calcular movimiento del joystick
        Vector3 movement =
            forward * vertical + right * horizontal;

        movement = Vector3.ClampMagnitude(movement, 1f);

        // Aplicar gravedad
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = movement * moveSpeed;
        velocity.y = verticalVelocity;

        // Mover al jugador respetando colisiones
        characterController.Move(velocity * Time.deltaTime);
    }
}
