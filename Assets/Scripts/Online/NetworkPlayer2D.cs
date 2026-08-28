using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ตัวละคร 2D มุมมองด้านข้าง เดินได้แค่ซ้ายกับขวา
///
/// หัวใจอยู่ที่ IsOwner: ทุกเครื่องเห็นตัวละครของทุกคน แต่เครื่องเรารับปุ่มให้เฉพาะ
/// ตัวที่เราเป็นเจ้าของ ตัวของคนอื่นปล่อยให้ NetworkTransform ซิงก์ตำแหน่งมาให้
/// ถ้าลืมเช็ค IsOwner จะกลายเป็นกดทีเดียวขยับทุกตัวพร้อมกัน
///
/// ระหว่างร่ายเวทจะเดินไม่ได้ ระบบวาด (SpellDrawing) เป็นคนสั่งล็อกผ่าน
/// MovementLocked ไม่ได้ปิดสคริปต์ทิ้ง เพราะยังต้องให้แรงเสียดทานหยุดตัวและ
/// ยังต้องรู้ทิศที่หันหน้าอยู่
///
/// โปรเจกต์นี้ตั้ง Active Input Handling เป็น Input System แบบใหม่
/// จึงใช้ Keyboard.current ไม่ใช่ Input.GetAxisRaw ของระบบเก่า (เรียกแล้วจะ error)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class NetworkPlayer2D : NetworkBehaviour
{
    [Header("การเดิน")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("แรงโน้มถ่วง ตั้ง 0 ถ้าอยากได้แบบลอยไม่มีพื้น")]
    [SerializeField] private float gravityScale = 3f;

    [Tooltip("แรงหน่วงแนวนอน ทำให้หยุดเร็วเวลาปล่อยปุ่ม")]
    [SerializeField] private float stopDamping = 12f;

    [Header("กล้อง")]
    [Tooltip("ให้กล้องหลักตามตัวเราอัตโนมัติตอนเกิด")]
    [SerializeField] private bool followWithMainCamera = true;

    [Header("การหันหน้า")]
    [Tooltip("พลิกภาพตามทิศที่เดิน")]
    [SerializeField] private bool flipSpriteWithFacing = true;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private float moveInput;

    /// <summary>ล็อกการเดิน ใช้ตอนกำลังร่ายเวท</summary>
    public bool MovementLocked { get; set; }

    /// <summary>ทิศที่ตัวละครหันอยู่ตอนนี้ (1 = ขวา, -1 = ซ้าย) ใช้เป็นทิศร่ายเริ่มต้น</summary>
    public float Facing { get; private set; } = 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        body.freezeRotation = true;
        body.gravityScale = gravityScale;

        if (!IsOwner)
        {
            // ตัวของคนอื่น: ตำแหน่งมาจาก NetworkTransform ล้วน ๆ
            // ปล่อยให้ฟิสิกส์ในเครื่องเราคำนวณด้วยจะตีกันจนตัวสั่น
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
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

        if (MovementLocked)
        {
            moveInput = 0f;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            moveInput = 0f;
            return;
        }

        float direction = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction += 1f;

        moveInput = direction;

        if (!Mathf.Approximately(direction, 0f))
        {
            Facing = Mathf.Sign(direction);
            if (flipSpriteWithFacing && spriteRenderer != null)
                spriteRenderer.flipX = Facing < 0f;
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        Vector2 velocity = body.linearVelocity;

        if (Mathf.Approximately(moveInput, 0f))
        {
            // ค่อย ๆ หยุดแทนการตัดความเร็วเป็นศูนย์ทันที จะได้ไม่กระตุก
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, stopDamping * Time.fixedDeltaTime);
        }
        else
        {
            velocity.x = moveInput * moveSpeed;
        }

        // ไม่แตะแกน y เลย ปล่อยให้แรงโน้มถ่วงจัดการ
        body.linearVelocity = velocity;
    }
}
