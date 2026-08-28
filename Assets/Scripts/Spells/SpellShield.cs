using System.Collections;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// โล่ธาตุที่ห่อรอบตัวละคร
    ///
    /// เกิดจากการ "วาดทับตัวเอง" แทนที่จะวาดแล้วเล็งออกไป รูปทรงเดียวกันจึงให้ผล
    /// ต่างกันตามที่วาด วงกลมทับตัว = โล่น้ำ วงกลมข้าง ๆ ตัว = ยิงเวทน้ำ
    /// ทำให้ผู้เล่นเลือกรุกหรือรับได้โดยไม่ต้องจำปุ่มเพิ่ม
    ///
    /// เกาะไปกับตัวละครเพราะ parent อยู่กับมัน เดินไปไหนโล่ตามไปด้วย
    ///
    /// เป็นภาพล้วน ๆ ยังไม่กันดาเมจจริง เพราะเกมยังไม่มีระบบเลือด
    /// เมื่อทำระบบดาเมจแล้ว ให้ Server เป็นคนตรวจว่าโล่ยังอยู่ไหมตอนโดนยิง
    /// ไม่ใช่ให้แต่ละเครื่องตัดสินเอง ไม่งั้นสองฝั่งจะเห็นผลไม่ตรงกัน
    /// </summary>
    public class SpellShield : MonoBehaviour
    {
        [SerializeField] private float duration = 4f;
        [SerializeField] private float fadeTime = 0.3f;
        [SerializeField] private float spinDegreesPerSecond = 25f;

        [Tooltip("ขนาดโล่เทียบกับตัวละคร")]
        [SerializeField] private float scale = 1.8f;

        [Tooltip("ความแรงของการเต้นเป็นจังหวะ ตั้ง 0 = นิ่ง")]
        [SerializeField] private float pulseAmount = 0.06f;

        [SerializeField] private float pulseSpeed = 2.5f;

        [Tooltip("ย้อมสีตามธาตุ ปิดถ้าอาร์ตแต่ละธาตุมีสีในตัวอยู่แล้ว")]
        [SerializeField] private bool tintByElement = true;

        private SpriteRenderer[] renderers;
        private static Sprite runtimeShieldSprite;

        /// <summary>โล่นี้เป็นธาตุอะไร ระบบดาเมจใช้ตัดสินว่าเวทที่ยิงมาแก้ได้ไหม</summary>
        public SpellElement Element { get; private set; }

        /// <summary>โล่ที่กำลังเปิดอยู่ของตัวละครแต่ละตัว ใช้กันซ้อนกันหลายชั้น</summary>
        public static SpellShield FindActiveOn(Transform owner)
        {
            return owner == null ? null : owner.GetComponentInChildren<SpellShield>();
        }

        /// <summary>
        /// สร้างโล่ขึ้นมาเองโดยไม่ต้องมี Prefab
        /// เหตุผลเดียวกับลูกเวท: ของหายแล้วเกมต้องไม่พังเงียบ ๆ
        /// </summary>
        public static SpellShield CreateFallback(Transform owner)
        {
            var go = new GameObject("SpellShield (สร้างอัตโนมัติ)");

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeShieldSprite;
            // อยู่หน้าตัวละครแต่หลังวงเวทและลูกเวท
            renderer.sortingOrder = 300;

            var shield = go.AddComponent<SpellShield>();
            shield.AttachTo(owner);
            return shield;
        }

        /// <summary>เกาะไปกับตัวละคร ให้โล่เดินตามเจ้าของ</summary>
        public void AttachTo(Transform owner)
        {
            transform.SetParent(owner, false);
            transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// ตั้งขนาดและอายุจากข้างนอก ใช้ตอนที่โล่ถูกสร้างด้วยโค้ด
        /// เพราะไม่มี Prefab ให้กดแก้ค่าใน Inspector
        /// ค่าที่น้อยกว่าหรือเท่ากับ 0 แปลว่าไม่เปลี่ยน ใช้ของเดิมต่อ
        /// </summary>
        public void Configure(float newScale, float newDuration)
        {
            if (newScale > 0f) scale = newScale;
            if (newDuration > 0f) duration = newDuration;
        }

        public void Play(SpellElement element)
        {
            Element = element;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (tintByElement)
            {
                Color tint = element.ToColor();
                foreach (SpriteRenderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    renderer.color = new Color(tint.r, tint.g, tint.b, renderer.color.a);
                }
            }

            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            var baseAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseAlphas[i] = renderers[i] != null ? renderers[i].color.a : 1f;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float alpha;
                if (elapsed < fadeTime)
                    alpha = fadeTime > 0f ? elapsed / fadeTime : 1f;
                else if (elapsed > duration - fadeTime)
                    alpha = fadeTime > 0f ? (duration - elapsed) / fadeTime : 0f;
                else
                    alpha = 1f;

                alpha = Mathf.Clamp01(alpha);

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    Color c = renderers[i].color;
                    renderers[i].color = new Color(c.r, c.g, c.b, baseAlphas[i] * alpha);
                }

                float pulse = 1f + Mathf.Sin(elapsed * pulseSpeed * Mathf.PI) * pulseAmount;
                transform.localScale = Vector3.one * (scale * pulse);
                transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);

                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>วงแหวนโปร่งกลางที่วาดด้วยโค้ด สร้างครั้งเดียวแล้วใช้ซ้ำ</summary>
        private static Sprite RuntimeShieldSprite
        {
            get
            {
                if (runtimeShieldSprite != null) return runtimeShieldSprite;

                const int size = 128;
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

                        // ขอบวงหนา ๆ หนึ่งวง บวกเรืองแสงจาง ๆ ข้างในให้ดูเป็นเกราะพลังงาน
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.92f) / 0.08f);
                        float inner = radius < 0.92f ? 0.12f * (1f - radius * 0.5f) : 0f;

                        float alpha = Mathf.Clamp01(rim + inner);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                runtimeShieldSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    size);

                return runtimeShieldSprite;
            }
        }
    }
}
