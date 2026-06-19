using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("⚙️ ตั้งค่าความเร็ว (Movement Settings)")]
    [Tooltip("ความเร็วในการเดินซ้าย-ขวา")]
    public float moveSpeedX = 6f;
    [Tooltip("ความเร็วในการเดินลึกเข้า-ออก (แกน Z)")]
    public float moveSpeedZ = 4f;
    [Tooltip("ความเร็วในการหันหน้าตัวละคร")]
    public float rotationSpeed = 15f;

    [Header("🦘 ตั้งค่าการกระโดด (Jump Settings)")]
    public float jumpForce = 7f;
    public LayerMask groundLayer;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 movementInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // ล็อกไม่ให้ตัวละครล้มกลิ้ง
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezeRotationY;
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        float x = 0f;
        float z = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x = -1f;
        else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x = 1f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) z = 1f;
        else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) z = -1f;

        movementInput = new Vector3(x, 0f, z).normalized;

        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        // 🚨 🪐 ปรับความเร่งให้ตอบสนองไวขึ้นนิดนึง (จาก 10f เป็น 15f) เพื่อลดอาการหน่วง
        Vector3 targetVelocity = new Vector3(movementInput.x * moveSpeedX, rb.linearVelocity.y, movementInput.z * moveSpeedZ);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, 15f * Time.fixedDeltaTime);

        if (movementInput.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movementInput);

            // 🚨 🪐 เปลี่ยนมาใช้ rb.MoveRotation แทน transform.rotation เพื่อแก้ปัญหาภาพสั่น!
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}