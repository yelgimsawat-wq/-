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
// บังคับให้มี Collider ติดมาด้วยเสมอ
// Unity จะไม่ยอมให้ลบ component ที่ถูก require ไว้ และจะเติมให้เองถ้าหายไป
// ใส่ไว้เพราะเคยมีอุบัติเหตุลบ Collider ทิ้งแล้วตัวละครร่วงทะลุพื้นโดยไม่มี error
// ถ้าวันหนึ่งอยากใช้ collider ทรงอื่น ให้ลบบรรทัด RequireComponent อันล่างออก
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class NetworkPlayer2D : NetworkBehaviour
{
    [Header("การเดิน")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("ตัวคูณแรงโน้มถ่วง ไม่ใช่ค่า m/s2 — Unity ดึงลง 9.81 อยู่แล้ว "
             + "ใส่ 1 = แรงโน้มถ่วงเท่าโลกจริง, 2-3 = ตกเร็วแบบเกมแพลตฟอร์ม, "
             + "0 = ลอยอยู่กับที่ (ค่าที่มากเกินไปจะทำให้ตกเร็วจนทะลุพื้น)")]
    [SerializeField] private float gravityScale = 0f;

    [Tooltip("ความเร็วตกสูงสุด กันตกเร็วจนทะลุพื้นเมื่อตั้งแรงโน้มถ่วงไว้สูง")]
    [SerializeField] private float maxFallSpeed = 25f;

    [Tooltip("แรงหน่วงแนวนอน ทำให้หยุดเร็วเวลาปล่อยปุ่ม")]
    [SerializeField] private float stopDamping = 12f;

    [Tooltip("ตกต่ำกว่าระดับนี้แล้วดีดกลับจุดเกิด กันหลุดแมพหายไปเลย")]
    [SerializeField] private float respawnBelowY = -30f;

    // เคยผูกกล้องเป็นลูกของตัวละครตรงนี้ ย้ายไปให้ CameraFollow2D บนกล้องทำแทนแล้ว
    // เพราะพอแยกซีน การโหลดซีนใหม่จะทำลายกล้องเก่าทิ้ง แต่ตัวละครย้ายข้ามซีนไปด้วย
    // ผลคือตัวละครยังอยู่แต่กล้องหายไป จอดำโดยไม่มี error

    [Header("การหันหน้า")]
    [Tooltip("พลิกภาพตามทิศที่เดิน")]
    [SerializeField] private bool flipSpriteWithFacing = true;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private float moveInput;
    private Vector3 spawnPosition;

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

        // ตั้งค่าฟิสิกส์จากสคริปต์ทั้งหมด ไม่พึ่งค่าที่ค้างอยู่ใน prefab
        // เพราะ prefab อาจถูกสร้างไว้ตั้งแต่โค้ดเวอร์ชันเก่า แล้วค่าจะไม่ตรงกัน
        body.freezeRotation = true;
        body.gravityScale = gravityScale;
        // หยุดตัวเองด้วย MoveTowards อยู่แล้ว ถ้ามี damping ซ้ำจะเดินหนืด
        body.linearDamping = 0f;
        // ตรวจการชนแบบต่อเนื่อง ตัวที่ตกเร็วจะได้ไม่กระโดดข้ามพื้นไปในเฟรมเดียว
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        EnsureCollider();
        spawnPosition = transform.position;

        if (!IsOwner)
        {
            // ตัวของคนอื่น: ตำแหน่งมาจาก NetworkTransform ล้วน ๆ
            // ปล่อยให้ฟิสิกส์ในเครื่องเราคำนวณด้วยจะตีกันจนตัวสั่น
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
        }
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

    /// <summary>
    /// ไม่มี Collider = ไม่มีอะไรให้ชนพื้น ตัวละครจะร่วงทะลุลงไปเรื่อย ๆ
    /// โดยไม่มี error ให้เห็นเลย เป็นอาการที่หาสาเหตุยากมาก
    /// RequireComponent กันไว้ชั้นหนึ่งแล้ว ตรงนี้เป็นชั้นสุดท้ายเผื่อใน build
    /// </summary>
    private void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null) return;

        var added = gameObject.AddComponent<CircleCollider2D>();
        added.radius = 0.5f;

        Debug.LogWarning(
            "[NetworkPlayer2D] ตัวละครไม่มี Collider จึงเติม CircleCollider2D ให้อัตโนมัติ\n"
            + "ควรใส่ไว้ใน Prefab ด้วย สั่ง Tools > เกมวาดวงเวท > ติดตั้งฉากอัตโนมัติ",
            this);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // ตาข่ายกันตก: ถ้าฉากยังไม่มีพื้นหรือมีช่องโหว่ ก็ยังเล่นต่อได้
        // ไม่ต้องออกห้องแล้วเข้าใหม่
        if (transform.position.y < respawnBelowY)
        {
            transform.position = spawnPosition;
            body.linearVelocity = Vector2.zero;
            return;
        }

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

        // จำกัดความเร็วตก ถ้าปล่อยให้เร่งไปเรื่อย ๆ ตอนตั้ง Gravity Scale สูง ๆ
        // ระยะที่เคลื่อนในหนึ่งเฟรมจะยาวกว่าความหนาของพื้นจนทะลุผ่านไปได้
        if (maxFallSpeed > 0f && velocity.y < -maxFallSpeed)
            velocity.y = -maxFallSpeed;

        body.linearVelocity = velocity;
    }
}
