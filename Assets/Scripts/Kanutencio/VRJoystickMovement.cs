
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class VRJoystickMovement : MonoBehaviour
{
    [Header("Referencias")]
    public Transform vrCamera;
    private CharacterController characterController;

    [Header("Movimiento")]
    public float moveSpeed = 2f;

    [Header("Gravedad")]
    public float gravity = -9.81f;
    private float verticalVelocity;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        MovePlayer();
    }

    void MovePlayer()
    {
        // Leer entrada del Input Manager clásico
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

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
