using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ตัวละคร 2D มุมมองด้านข้าง เดินซ้ายขวา ยืนบนพื้น และกระโดดได้
///
/// กระโดดด้วย W หรือลูกศรขึ้น ไม่ใช้ Space เพราะ Space ถูกใช้ยืนยันคาถาไปแล้ว
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
[RequireComponent(typeof(CapsuleCollider2D))]
public class NetworkPlayer2D : NetworkBehaviour
{
    [Header("การเดิน")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("ตัวคูณแรงโน้มถ่วง ไม่ใช่ค่า m/s2 — Unity ดึงลง 9.81 อยู่แล้ว "
             + "ใส่ 1 = แรงโน้มถ่วงเท่าโลกจริง, 3 = ตกเร็วแบบเกมแพลตฟอร์ม, "
             + "0 = ลอยอยู่กับที่ (ค่าที่มากเกินไปจะทำให้ตกเร็วจนทะลุพื้น)")]
    [SerializeField] private float gravityScale = 3f;

    [Header("การกระโดด")]
    [Tooltip("ความเร็วพุ่งขึ้นตอนกระโดด ยิ่งมากยิ่งกระโดดสูง")]
    [SerializeField] private float jumpSpeed = 11f;

    [Tooltip("ระยะตรวจพื้นใต้เท้า เล็กไปจะกระโดดไม่ติด ใหญ่ไปจะกระโดดกลางอากาศได้")]
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Tooltip("ช่วงผ่อนผันหลังตกจากขอบ ยังกระโดดได้อีกเสี้ยววินาที ทำให้คุมง่ายขึ้นมาก")]
    [SerializeField] private float coyoteTime = 0.12f;

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
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private float moveInput;
    private Vector3 spawnPosition;

    private bool jumpRequested;
    private float lastGroundedTime = float.NegativeInfinity;

    /// <summary>ยืนอยู่บนอะไรอยู่หรือเปล่า</summary>
    public bool IsGrounded { get; private set; }

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
            // ทิ้งคำสั่งกระโดดที่ค้างไว้ด้วย ไม่งั้นพอร่ายเวทเสร็จจะเด้งขึ้นเองทันที
            jumpRequested = false;
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

        // กระโดดด้วย W หรือลูกศรขึ้น ไม่ใช้ Space เพราะ Space ถูกใช้ยืนยันคาถาไปแล้ว
        // จำการกดไว้ก่อน แล้วค่อยไปใช้ใน FixedUpdate ที่เป็นจังหวะของฟิสิกส์
        // ถ้าสั่งกระโดดใน Update ตรง ๆ บางเฟรมจะหลุดหายเพราะสองจังหวะไม่ตรงกัน
        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            jumpRequested = true;

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
        bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null) return;

        var added = gameObject.AddComponent<CapsuleCollider2D>();
        added.size = new Vector2(1f, 1.5f);
        added.direction = CapsuleDirection2D.Vertical;
        bodyCollider = added;

        Debug.LogWarning(
            "[NetworkPlayer2D] ตัวละครไม่มี Collider จึงเติม CapsuleCollider2D ให้อัตโนมัติ\n"
            + "ควรใส่ไว้ใน Prefab ด้วย สั่ง Tools > เกมวาดวงเวท > ติดตั้งฉากอัตโนมัติ",
            this);
    }

    /// <summary>
    /// ตรวจว่ามีอะไรรองอยู่ใต้เท้าไหม
    ///
    /// ยิงวงกลมเล็ก ๆ ที่ใต้ขอบล่างของ collider แทนการใช้ OnCollisionStay
    /// เพราะ OnCollisionStay จะนับการชนด้านข้างกำแพงว่าเป็นพื้นด้วย
    /// ทำให้กระโดดค้างบนกำแพงได้ ซึ่งไม่ใช่สิ่งที่ต้องการ
    /// </summary>
    private void UpdateGrounded()
    {
        if (bodyCollider == null)
        {
            IsGrounded = false;
            return;
        }

        Bounds bounds = bodyCollider.bounds;
        var feet = new Vector2(bounds.center.x, bounds.min.y);

        Collider2D[] hits = Physics2D.OverlapCircleAll(feet, groundCheckRadius);

        IsGrounded = false;
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            // ตัวเราเองและของที่ติดอยู่กับเรา (เช่น โล่) ไม่นับเป็นพื้น
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            IsGrounded = true;
            break;
        }

        if (IsGrounded) lastGroundedTime = Time.time;
    }

    private void FixedUpdate()
    {
        // ตรวจพื้นให้ทุกตัว ไม่ใช่เฉพาะตัวเรา
        //
        // เดิมอยู่ใต้บรรทัด IsOwner ข้างล่าง ทำให้ตัวละครของคนอื่นมี IsGrounded
        // เป็น false ตลอด อนิเมชันจึงคิดว่าเขาลอยอยู่กลางอากาศตลอดเวลา
        // แล้วไม่ยอมโยกตัวตอนเดิน
        //
        // เป็นการถามฟิสิกส์เฉย ๆ ไม่ได้ขยับอะไร จึงเรียกฝั่งไหนก็ปลอดภัย
        UpdateGrounded();

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

        // ยอมให้กระโดดได้อีกนิดหลังเพิ่งตกจากขอบ (coyote time)
        // เกมแพลตฟอร์มเกือบทุกเกมทำแบบนี้ เพราะคนกดช้ากว่าที่คิดเสมอ
        // ถ้าเช็คแค่ "ยืนอยู่ตอนนี้ไหม" จะรู้สึกว่าเกมไม่รับปุ่มทั้งที่กดแล้ว
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;

        if (jumpRequested && canJump)
        {
            velocity.y = jumpSpeed;
            // กันกระโดดซ้ำทันทีในเฟรมถัดไปตอนที่ยังไม่ทันลอยพ้นพื้น
            lastGroundedTime = float.NegativeInfinity;
            MagicDrawing.SpellAudio.Play(MagicDrawing.SpellSound.Jump, transform.position);
        }
        jumpRequested = false;

        // จำกัดความเร็วตก ถ้าปล่อยให้เร่งไปเรื่อย ๆ ตอนตั้ง Gravity Scale สูง ๆ
        // ระยะที่เคลื่อนในหนึ่งเฟรมจะยาวกว่าความหนาของพื้นจนทะลุผ่านไปได้
        if (maxFallSpeed > 0f && velocity.y < -maxFallSpeed)
            velocity.y = -maxFallSpeed;

        body.linearVelocity = velocity;
    }
}
