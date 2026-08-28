using System;
using Unity.Netcode;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// เลือดของตัวละคร
    ///
    /// Server เป็นคนตัดสินความเสียหายทั้งหมด เครื่องผู้เล่นแค่อ่านค่าไปแสดงผล
    /// ถ้าปล่อยให้แต่ละเครื่องคิดเลือดเอง สองฝั่งจะเห็นไม่ตรงกันทันทีที่เน็ตหน่วง
    /// คนหนึ่งเห็นตัวเองตาย อีกคนเห็นยังไม่ตาย แล้วเถียงกันไม่จบ
    ///
    /// ใช้ NetworkVariable เพราะมันซิงก์ค่าให้เองและส่งเฉพาะตอนค่าเปลี่ยน
    /// เขียนได้เฉพาะ Server อ่านได้ทุกคน ตรงกับที่ต้องการพอดี
    /// </summary>
    public class PlayerHealth : NetworkBehaviour
    {
        [SerializeField] private int maxHp = 100;


        [Tooltip("โดนเวทที่โล่กันได้ จะเหลือดาเมจกี่เปอร์เซ็นต์")]
        [Range(0f, 1f)]
        [SerializeField] private float blockedDamageMultiplier = 0.15f;

        [Tooltip("ยิงธาตุที่แก้โล่ได้ จะได้ดาเมจเพิ่มกี่เท่า")]
        [SerializeField] private float counterDamageMultiplier = 1.5f;

        private readonly NetworkVariable<int> currentHp = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// ตกรอบแล้วหรือยัง ต้องเป็น NetworkVariable เพราะกล้องของคนที่ตายแล้ว
        /// ต้องรู้ว่าใครยังรอดอยู่บ้างเพื่อไปตามดู
        /// </summary>
        private readonly NetworkVariable<bool> eliminated = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Vector3 spawnPosition;
        private NetworkPlayer2D player;

        public int CurrentHp => currentHp.Value;
        public int MaxHp => maxHp;
        public bool IsDead => currentHp.Value <= 0;

        /// <summary>ตกรอบแล้ว รอรอบใหม่ ระหว่างนี้ดูคนอื่นเล่นได้</summary>
        public bool IsEliminated => eliminated.Value;

        /// <summary>แจ้งเมื่อเลือดเปลี่ยน ใช้ผูก UI ภายนอกได้</summary>
        public event Action<int, int> HealthChanged;

        private void Awake()
        {
            player = GetComponent<NetworkPlayer2D>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            currentHp.OnValueChanged += HandleHpChanged;
            spawnPosition = transform.position;

            if (IsServer)
            {
                currentHp.Value = maxHp;
                eliminated.Value = false;

                // แจ้งให้ตัวจัดการรอบทบทวนว่าคนครบพอจะเริ่มสู้แล้วหรือยัง
                if (MatchManager.Instance != null) MatchManager.Instance.ReportSpawned();
            }
        }

        public override void OnNetworkDespawn()
        {
            currentHp.OnValueChanged -= HandleHpChanged;
            base.OnNetworkDespawn();
        }

        private void HandleHpChanged(int oldValue, int newValue)
        {
            HealthChanged?.Invoke(newValue, maxHp);
        }

        /// <summary>
        /// รับความเสียหายจากเวท เรียกได้จาก Server เท่านั้น
        /// คำนวณผลของโล่ตรงนี้ที่เดียว จะได้ไม่มีสูตรกระจายอยู่หลายที่
        /// </summary>
        public void TakeSpellDamage(int baseDamage, SpellElement attackElement)
        {
            if (!IsServer || IsDead || baseDamage <= 0) return;

            SpellShield shield = SpellShield.FindActiveOn(transform);
            int finalDamage;

            if (shield == null)
            {
                finalDamage = baseDamage;
            }
            else if (attackElement.CountersShield(shield.Element))
            {
                // ยิงถูกธาตุที่แก้โล่ได้ โล่แตกและเจ็บหนักกว่าเดิม
                finalDamage = Mathf.RoundToInt(baseDamage * counterDamageMultiplier);
                BreakShieldClientRpc();
            }
            else
            {
                // โล่กันไว้เกือบหมด เหลือทะลุเข้ามานิดหน่อย
                finalDamage = Mathf.RoundToInt(baseDamage * blockedDamageMultiplier);
            }

            finalDamage = Mathf.Max(finalDamage, 0);
            currentHp.Value = Mathf.Max(0, currentHp.Value - finalDamage);

            PlayHitSoundClientRpc((byte)attackElement, shield != null && finalDamage < baseDamage);

            if (currentHp.Value <= 0) HandleDeath();
        }

        /// <summary>ให้ทุกเครื่องเก็บภาพโล่ที่แตกไปพร้อมกัน</summary>
        [ClientRpc]
        private void BreakShieldClientRpc()
        {
            SpellShield shield = SpellShield.FindActiveOn(transform);
            if (shield != null) Destroy(shield.gameObject);

            SpellAudio.Play(SpellSound.ShieldBreak, transform.position);
            // กระจายแรงและกว้างกว่าปกติ ให้รู้สึกว่าโล่แตกจริง ๆ ไม่ใช่แค่โดน
            SpellVfx.Burst(SpellElement.Wind, transform.position, 20, 7f, 0.7f, 0.5f);
        }

        /// <summary>
        /// เสียงตอนโดน ต้องส่งเป็น RPC เพราะการคิดดาเมจเกิดบน Server เท่านั้น
        /// ถ้าเล่นเสียงตรงนั้นเลย จะมีแค่เครื่อง Host ที่ได้ยิน
        /// </summary>
        [ClientRpc]
        private void PlayDeathSoundClientRpc()
        {
            SpellAudio.Play(SpellSound.Death, transform.position);
        }

        [ClientRpc]
        private void PlayHitSoundClientRpc(byte elementId, bool blocked)
        {
            SpellElement element = SpellElementExtensions.FromNetworkId(elementId);

            SpellAudio.Play(
                blocked ? SpellSound.Blocked : SpellSound.Hit,
                transform.position,
                element);

            // โดนเต็ม ๆ กระจายแรงกว่าตอนที่โล่กันไว้ได้ ดูออกจากเอฟเฟกต์เลย
            SpellVfx.Burst(element, transform.position,
                blocked ? 5 : 12, blocked ? 2f : 5.5f, 0.45f, blocked ? 0.3f : 0.5f);
        }

        /// <summary>
        /// ตายแล้วตกรอบเลย ไม่เกิดใหม่เอง
        /// ตัวจัดการรอบเป็นคนตัดสินว่าเมื่อไรจะปลุกทุกคนกลับมา
        /// ถ้าให้เกิดใหม่เองอัตโนมัติจะไม่มีวันรู้ผลแพ้ชนะ
        /// </summary>
        private void HandleDeath()
        {
            eliminated.Value = true;

            PlayDeathSoundClientRpc();
            SetVisibleClientRpc(false);

            if (MatchManager.Instance != null) MatchManager.Instance.ReportElimination(this);
        }

        /// <summary>ปลุกกลับมาเล่นรอบใหม่ เรียกจาก MatchManager ฝั่ง Server</summary>
        public void ReviveServer()
        {
            if (!IsServer) return;

            eliminated.Value = false;
            currentHp.Value = maxHp;

            TeleportClientRpc(spawnPosition);
            SetVisibleClientRpc(true);
        }

        /// <summary>
        /// ย้ายตัวละครกลับจุดเกิด
        ///
        /// ต้องให้เจ้าของเป็นคนย้ายเอง เพราะตำแหน่งเป็นแบบ owner authoritative
        /// (ClientNetworkTransform2D) ถ้า Server ไปสั่งย้ายตรง ๆ เจ้าของจะส่ง
        /// ตำแหน่งเดิมกลับมาทับทันที กลายเป็นย้ายไม่ติด
        /// </summary>
        [ClientRpc]
        private void TeleportClientRpc(Vector3 position)
        {
            if (!IsOwner) return;

            transform.position = position;

            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        [Header("หลอดเลือด")]
        [SerializeField] private bool showHealthBar = true;

        [Tooltip("ลอยเหนือหัวเท่าไร (หน่วยโลก)")]
        [SerializeField] private float barHeightOffset = 0.9f;

        /// <summary>
        /// วาดหลอดเลือดลอยเหนือหัวทุกตัว ทั้งของเราและของคู่ต่อสู้
        ///
        /// ใช้ OnGUI เพราะไม่ต้องตั้ง Canvas หรือ prefab เพิ่มเลย
        /// ถ้าจะทำ UI จริงจังค่อยเปลี่ยนไปใช้ world-space Canvas ทีหลัง
        /// </summary>
        private void OnGUI()
        {
            if (!showHealthBar || !IsSpawned) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 world = transform.position + Vector3.up * barHeightOffset;
            Vector3 screen = camera.WorldToScreenPoint(world);

            // อยู่หลังกล้องแปลว่ามองไม่เห็น ไม่ต้องวาด
            if (screen.z < 0f) return;

            const float width = 70f;
            const float height = 9f;

            // GUI นับแกน y จากบนลงล่าง ส่วนกล้องนับจากล่างขึ้นบน ต้องกลับด้าน
            var back = new Rect(screen.x - width * 0.5f, Screen.height - screen.y, width, height);

            Color previous = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);

            float ratio = maxHp > 0 ? Mathf.Clamp01((float)currentHp.Value / maxHp) : 0f;
            var fill = new Rect(back.x, back.y, back.width * ratio, back.height);

            // เขียวเมื่อเลือดเยอะ ไล่ไปแดงเมื่อใกล้ตาย อ่านสถานะได้โดยไม่ต้องดูตัวเลข
            GUI.color = Color.Lerp(Color.red, Color.green, ratio);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);

            GUI.color = previous;

            // บอกธาตุของโล่ที่กางอยู่ และธาตุที่ใช้แก้ได้ ให้คู่ต่อสู้วางแผนได้
            SpellShield shield = SpellShield.FindActiveOn(transform);
            if (shield != null)
            {
                SpellElement counter = SpellElementExtensions.CounterFor(shield.Element);
                var label = new Rect(back.x - 25f, back.y - 20f, width + 50f, 20f);
                GUI.Label(label, $"โล่{shield.Element.ToThai()} (แก้ด้วย{counter.ToThai()})");
            }
        }

        [ClientRpc]
        private void SetVisibleClientRpc(bool visible)
        {
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null) renderer.enabled = visible;

            // ต้องปิด SpellDrawing ก่อนแล้วค่อยล็อกการเดิน
            // เพราะ OnDisable ของมันปลดล็อกการเดินให้อัตโนมัติ (กันติดค้างตอนถูกปิด)
            // ถ้าสลับลำดับ ตัวที่ตายแล้วจะยังเดินได้
            var drawing = GetComponent<SpellDrawing>();
            if (drawing != null) drawing.enabled = visible;

            // ตอนตายห้ามเดินและห้ามร่ายเวท
            if (player != null) player.MovementLocked = !visible;
        }
    }
}
