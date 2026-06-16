using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 🚨 บรรทัดสำคัญ: ดึงระบบ Input ตัวใหม่มาใช้

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public Camera playerCamera;
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 10f;
    [Tooltip("ความเร็วในการหันมุมกล้อง (อาจต้องปรับตัวเลขให้เข้ากับเมาส์ของคุณในหน้า Inspector)")]
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 3f;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private CharacterController characterController;

    private bool canMove = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // สร้างตัวแปรไว้เก็บค่าปุ่มกดตามระบบ New Input System
        float inputVertical = 0f;
        float inputHorizontal = 0f;
        bool isRunning = false;
        bool isJumping = false;
        bool isCrouching = false;
        float mouseX = 0f;
        float mouseY = 0f;

        // 🚨 1. ตรวจจับการกดคีย์บอร์ด (New Input System)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) inputVertical += 1f;
            if (Keyboard.current.sKey.isPressed) inputVertical -= 1f;
            if (Keyboard.current.dKey.isPressed) inputHorizontal += 1f;
            if (Keyboard.current.aKey.isPressed) inputHorizontal -= 1f;

            isRunning = Keyboard.current.leftShiftKey.isPressed;
            isJumping = Keyboard.current.spaceKey.isPressed;
            isCrouching = Keyboard.current.rKey.isPressed;
        }

        // 🚨 2. ตรวจจับการขยับเมาส์ (New Input System)
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            // คูณ 0.05f เพื่อทอนค่าพิกเซลเมาส์ระบบใหม่ ให้ใกล้เคียงกับ GetAxis ระบบเก่าครับ
            mouseX = mouseDelta.x * 0.05f;
            mouseY = mouseDelta.y * 0.05f;
        }

        // --- ระบบคำนวณการเดิน (เหมือนโครงสร้างเดิมของคุณ) ---
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        float curSpeedX = canMove ? (isRunning ? runSpeed : walkSpeed) * inputVertical : 0;
        float curSpeedY = canMove ? (isRunning ? runSpeed : walkSpeed) * inputHorizontal : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        // ระบบกระโดด
        if (isJumping && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        // แรงโน้มถ่วง
        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        // ระบบหมอบ (ปุ่ม R)
        if (isCrouching && canMove)
        {
            characterController.height = crouchHeight;
            walkSpeed = crouchSpeed;
            runSpeed = crouchSpeed;
        }
        else
        {
            characterController.height = defaultHeight;
            walkSpeed = 6f;
            runSpeed = 12f;
        }

        // สั่งเคลื่อนที่ตัวละคร
        characterController.Move(moveDirection * Time.deltaTime);

        // --- ระบบหมุนมุมกล้องมองรอบตัว ---
        if (canMove)
        {
            rotationX += -mouseY * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, mouseX * lookSpeed, 0);
        }
    }
}