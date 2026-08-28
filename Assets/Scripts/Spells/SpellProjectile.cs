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

        private Vector2 velocity;
        private float remaining;
        private bool launched;

        /// <summary>เรียกทันทีหลัง Instantiate เพื่อกำหนดทิศและธาตุ</summary>
        public void Launch(Vector2 direction, SpellElement element)
        {
            velocity = direction.normalized * speed;
            remaining = lifetime;
            launched = true;

            if (!tintByElement) return;

            Color tint = element.ToColor();
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null) continue;
                // เก็บ alpha เดิมไว้ ไม่งั้นภาพที่ตั้งใจให้จางจะทึบหมด
                renderer.color = new Color(tint.r, tint.g, tint.b, renderer.color.a);
            }
        }

        private void Update()
        {
            if (!launched) return;

            transform.position += (Vector3)(velocity * Time.deltaTime);

            if (!Mathf.Approximately(spinDegreesPerSecond, 0f))
                transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);

            remaining -= Time.deltaTime;
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
