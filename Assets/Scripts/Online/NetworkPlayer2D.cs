using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ตัวละคร 2D ที่ขยับได้ในเกมออนไลน์
///
/// หัวใจอยู่ที่ IsOwner: ทุกเครื่องจะเห็นตัวละครของ "ทุกคน" แต่เครื่องเราจะรับปุ่ม
/// ให้เฉพาะตัวที่เราเป็นเจ้าของเท่านั้น ตัวของคนอื่นปล่อยให้ NetworkTransform
/// ซิงก์ตำแหน่งมาให้เอง ถ้าลืมเช็ค IsOwner จะกลายเป็นกดทีเดียวขยับทุกตัวพร้อมกัน
///
/// โปรเจกต์นี้ตั้ง Active Input Handling เป็น Input System (New) จึงใช้
/// Keyboard.current ไม่ใช่ Input.GetAxisRaw ของระบบเก่า (เรียกแล้วจะ error)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class NetworkPlayer2D : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("ให้กล้องหลักตามตัวเราอัตโนมัติตอนเกิด")]
    [SerializeField] private bool followWithMainCamera = true;

    private Rigidbody2D body;
    private Vector2 moveInput;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // เกมมองจากด้านบน ไม่ต้องมีแรงโน้มถ่วง และห้ามตัวหมุนตอนชนกัน
        body.gravityScale = 0f;
        body.freezeRotation = true;

        if (!IsOwner)
        {
            // ตัวของคนอื่น: ตำแหน่งมาจาก NetworkTransform ล้วน ๆ
            // ปล่อยให้ฟิสิกส์ในเครื่องเราคำนวณด้วยจะตีกันจนตัวสั่น
            body.bodyType = RigidbodyType2D.Kinematic;
            return;
        }

        if (followWithMainCamera && Camera.main != null)
            Camera.main.transform.SetParent(transform, false);
    }

    public override void OnNetworkDespawn()
    {
        // ถ้าไม่ปลดกล้องออกก่อน กล้องจะถูกทำลายไปพร้อมตัวละคร แล้วจอจะดำ
        if (IsOwner && followWithMainCamera && Camera.main != null
            && Camera.main.transform.parent == transform)
        {
            Camera.main.transform.SetParent(null, true);
        }

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsOwner) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float x = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

        // normalize กันเดินทแยงเร็วกว่าเดินตรง
        moveInput = new Vector2(x, y).normalized;
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // Unity 6 เปลี่ยนชื่อ velocity เป็น linearVelocity แล้ว
        body.linearVelocity = moveInput * moveSpeed;
    }
}
