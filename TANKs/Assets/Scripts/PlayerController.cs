using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("⚙️ ตั้งค่าความเร็ว (Movement Settings)")]
    [Tooltip("ความเร็วในการเดินซ้าย-ขวา")]
    public float moveSpeedX = 6f;
    [Tooltip("ความเร็วในการเดินลึกเข้า-ออก (แกน Z)")]
    public float moveSpeedZ = 4f; // ปกติเดินลึกเข้าฉากมักจะตั้งให้ช้ากว่านิดหน่อยเพื่อมิติภาพ
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
        // 1. รับค่าปุ่มกด (A/D = ซ้ายขวา, W/S = ลึกเข้าออก)
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        movementInput = new Vector3(x, 0f, z).normalized;

        // 2. เช็คการแตะพื้น
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        }

        // 3. กด Spacebar กระโดด
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // รีเซ็ตแรงตกก่อนกระโดดเพื่อให้โดดได้ความสูงคงที่
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        // 🚨 จัดการการเดินใน FixedUpdate เพื่อให้ฟิสิกส์ลื่นไหล ไม่ทะลุกำแพง
        Vector3 targetVelocity = new Vector3(movementInput.x * moveSpeedX, rb.linearVelocity.y, movementInput.z * moveSpeedZ);

        // เกลี่ยความเร็วให้สมูทขึ้น (Acceleration)
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, 10f * Time.fixedDeltaTime);

        // 🚨 หมุนหันหน้าตัวละครไปตามทิศที่เดิน
        if (movementInput.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movementInput);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    // เอาไว้วาดวงกลมดูจุดเช็คพื้นในหน้า Scene
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}