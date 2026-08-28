using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ลูกเวทที่พุ่งไปตามทิศที่เล็ง
    ///
    /// เป็นภาพล้วน ๆ ไม่ใช่ NetworkObject โดยตั้งใจ ทุกเครื่องสร้างของตัวเองจาก
    /// ทิศและตำแหน่งเดียวกันที่ ClientRpc ส่งมา จึงเห็นตรงกันโดยไม่ต้องซิงก์ทีละเฟรม
    ///
    /// ยังไม่มีระบบดาเมจ เพราะเอกสารข้อกำหนดยังไม่ได้ระบุเรื่องเลือดหรือการตาย
    /// ถ้าจะเพิ่มทีหลัง ให้ Server เป็นคนตัดสินการชนแล้วส่งผลมา ไม่ใช่ให้ลูกเวท
    /// ในเครื่องแต่ละคนตัดสินเอง ไม่งั้นสองเครื่องจะเห็นผลไม่ตรงกัน
    /// </summary>
    public class SpellProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 9f;
        [SerializeField] private float lifetime = 2.5f;

        [Tooltip("ย้อมสีตามธาตุ ปิดถ้าอาร์ตแต่ละธาตุมีสีในตัวอยู่แล้ว")]
        [SerializeField] private bool tintByElement = true;

        [Tooltip("หมุนตัวเองระหว่างพุ่ง (องศาต่อวินาที)")]
        [SerializeField] private float spinDegreesPerSecond = 0f;

        [Header("การชน (ทำงานเฉพาะฝั่ง Server)")]
        [Tooltip("รัศมีที่ใช้ตรวจว่าโดนตัวใคร")]
        [SerializeField] private float hitRadius = 0.35f;

        private Vector2 velocity;
        private float remaining;
        private bool launched;

        // ฝั่งที่มีสิทธิ์ตัดสินการชน มีแค่ Server เท่านั้นที่เป็น true
        // เครื่องผู้เล่นสร้างลูกเวทเหมือนกันแต่เป็นภาพล้วน ๆ ไม่คิดดาเมจ
        // ถ้าปล่อยให้ทุกเครื่องคิด คนหนึ่งยิงจะกลายเป็นโดนหลายครั้ง
        private bool authoritative;
        private int damage;
        private SpellElement element;
        private Transform caster;

        private static Sprite runtimeOrbSprite;
        private static Material runtimeTrailMaterial;

        /// <summary>
        /// สร้างลูกเวทขึ้นมาเองโดยไม่ต้องมี Prefab
        ///
        /// มีไว้เพื่อไม่ให้เกมพังเงียบ ๆ ถ้า Prefab หายหรือยังไม่ได้ผูก
        /// เดิมถ้าช่อง Prefab ว่าง เวทจะไม่ออกโดยไม่มี error อะไรเลย
        /// ซึ่งหาสาเหตุยากมากเพราะดูเหมือนโค้ดไม่ทำงาน ทั้งที่จริงแค่ของหาย
        ///
        /// ถ้ามี Prefab อยู่ SpellCaster จะใช้ Prefab ก่อนเสมอ ตัวนี้เป็นแค่ตาข่ายรอง
        /// </summary>
        public static SpellProjectile CreateFallback(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("SpellProjectile (สร้างอัตโนมัติ)");
            go.transform.SetPositionAndRotation(position, rotation);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeOrbSprite;
            renderer.sortingOrder = 450;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.35f;
            trail.endWidth = 0f;
            trail.numCapVertices = 4;
            trail.material = RuntimeTrailMaterial;
            trail.sortingOrder = 440;
            trail.autodestruct = false;

            return go.AddComponent<SpellProjectile>();
        }

        /// <summary>ลูกกลมเรืองแสงที่วาดด้วยโค้ด สร้างครั้งเดียวแล้วใช้ซ้ำ</summary>
        private static Sprite RuntimeOrbSprite
        {
            get
            {
                if (runtimeOrbSprite != null) return runtimeOrbSprite;

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                float center = (size - 1) * 0.5f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float radius = Mathf.Sqrt(dx * dx + dy * dy) / center;

                        // ยกกำลังทำให้แกนกลางทึบและขอบจางเร็ว ดูเป็นลูกพลังงาน
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.8f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                // pixelsPerUnit เป็นสองเท่าของขนาดภาพ ลูกเวทจึงกว้างประมาณครึ่งหน่วย
                runtimeOrbSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    size * 2f);

                return runtimeOrbSprite;
            }
        }

        private static Material RuntimeTrailMaterial
        {
            get
            {
                // ใช้วัสดุร่วมกันทุกลูก ไม่งั้นยิงรัว ๆ จะสร้างวัสดุใหม่ทิ้งไว้เรื่อย ๆ
                if (runtimeTrailMaterial == null)
                    runtimeTrailMaterial = new Material(Shader.Find("Sprites/Default"));
                return runtimeTrailMaterial;
            }
        }

        /// <summary>เรียกทันทีหลัง Instantiate เพื่อกำหนดทิศและธาตุ</summary>
        /// <param name="isAuthoritative">true เฉพาะบน Server ตัวที่จะคิดดาเมจ</param>
        /// <param name="hitDamage">ดาเมจพื้นฐานก่อนคิดผลของโล่</param>
        /// <param name="owner">คนร่าย เอาไว้กันยิงโดนตัวเอง</param>
        public void Launch(
            Vector2 direction,
            SpellElement spellElement,
            bool isAuthoritative = false,
            int hitDamage = 0,
            Transform owner = null)
        {
            velocity = direction.normalized * speed;
            remaining = lifetime;
            launched = true;

            element = spellElement;
            authoritative = isAuthoritative;
            damage = hitDamage;
            caster = owner;

            if (!tintByElement) return;

            Color tint = spellElement.ToColor();

            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null) continue;
                // เก็บ alpha เดิมไว้ ไม่งั้นภาพที่ตั้งใจให้จางจะทึบหมด
                renderer.color = new Color(tint.r, tint.g, tint.b, renderer.color.a);
            }

            // หางลูกเวทต้องย้อมแยกต่างหาก TrailRenderer ไม่ใช่ SpriteRenderer
            foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
            {
                if (trail == null) continue;
                trail.startColor = new Color(tint.r, tint.g, tint.b, 0.85f);
                trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            }
        }

        private void Update()
        {
            if (!launched) return;

            transform.position += (Vector3)(velocity * Time.deltaTime);

            if (!Mathf.Approximately(spinDegreesPerSecond, 0f))
                transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);

            if (authoritative && CheckHit()) return;

            remaining -= Time.deltaTime;
            if (remaining <= 0f) Destroy(gameObject);
        }

        /// <summary>
        /// ตรวจว่าโดนใครไหม คืน true ถ้าโดนแล้วลูกเวทถูกทำลายไปแล้ว
        ///
        /// ใช้ OverlapCircle แทน Collider จริงเพราะลูกเวทเคลื่อนที่ด้วย transform
        /// ไม่ได้ใช้ Rigidbody จึงไม่มี event การชนให้ดัก และวิธีนี้ควบคุมได้ตรงกว่า
        /// ว่าจะให้ชนอะไรบ้าง
        /// </summary>
        private bool CheckHit()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);

            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;

                var health = hit.GetComponentInParent<PlayerHealth>();
                if (health == null) continue;

                // ยิงโดนตัวเองไม่นับ ไม่งั้นเวทจะระเบิดใส่คนร่ายทันทีที่ออกจากมือ
                if (caster != null && health.transform == caster) continue;

                health.TakeSpellDamage(damage, element);
                Destroy(gameObject);
                return true;
            }

            return false;
        }
    }
}
