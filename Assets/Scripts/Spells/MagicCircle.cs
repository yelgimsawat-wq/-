using System.Collections;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// วงเวทที่โผล่หน้าตัวละครตอนร่าย ใส่ไว้บน Prefab ของวงเวท
    ///
    /// ข้อกำหนดข้อ 3.3 สามเรื่องที่จัดการให้ตรงนี้:
    /// - ไม่มีพื้นหลัง: เลือกได้ว่าจะใช้ Additive (สว่างทะลุ เหมาะกับเวทเรืองแสง)
    ///   หรือใช้วัสดุเดิมของ sprite ตรง ๆ ถ้าไฟล์ภาพโปร่งใสมาแล้ว
    /// - โผล่หน้าตัวละคร: SpellCaster เป็นคนกำหนดตำแหน่งตอน Instantiate
    /// - ไม่โดนอะไรบัง: ตั้ง Sorting Layer และ Order ให้อยู่หน้าสุดตอนเกิด
    ///
    /// ถ้าไม่ใส่ SpriteRenderer มาเลย สคริปต์จะไม่พังแต่จะไม่มีอะไรให้เห็น
    /// </summary>
    public class MagicCircle : MonoBehaviour
    {
        [Header("ภาพ")]
        [Tooltip("ปล่อยว่าง = เก็บ SpriteRenderer ทั้งหมดในตัวเองและลูก ๆ ให้อัตโนมัติ")]
        [SerializeField] private SpriteRenderer[] renderers;

        [Tooltip("ย้อมสีวงเวทตามธาตุ ปิดถ้าอาร์ตแต่ละธาตุมีสีในตัวอยู่แล้ว")]
        [SerializeField] private bool tintByElement = true;

        // เคยมีตัวเลือก Additive ตรงนี้ ถอดออกแล้วเพราะมันเปลี่ยนวัสดุไปใช้เชเดอร์
        // Particles/Standard Unlit ซึ่งเป็นของ Built-in Render Pipeline
        // โปรเจกต์นี้ใช้ URP ที่วาดเชเดอร์นั้นไม่ได้ ผลคือวงเวทหายไปทั้งวง
        // โดยไม่มี error ให้เห็น
        //
        // ไม่จำเป็นต้องใช้ Additive อยู่แล้ว เพราะภาพวงเวทมีพื้นหลังโปร่งใสมาแต่แรก
        // วัสดุเดิมของ SpriteRenderer (Sprites-Default) จึงแสดงผลถูกต้องและเข้ากับ URP

        [Header("การจัดชั้นการซ้อน")]
        [Tooltip("ชื่อ Sorting Layer ที่ต้องการ ปล่อยว่าง = ไม่ไปยุ่งกับค่าเดิมใน prefab")]
        [SerializeField] private string sortingLayerName = "";

        [SerializeField] private int sortingOrder = 500;

        [Header("จังหวะเวลา (วินาที)")]
        [SerializeField] private float fadeInTime = 0.12f;
        [SerializeField] private float holdTime = 0.55f;
        [SerializeField] private float fadeOutTime = 0.35f;

        [Header("การเคลื่อนไหว")]
        [SerializeField] private float spinDegreesPerSecond = 60f;
        [SerializeField] private float startScale = 0.6f;
        [SerializeField] private float endScale = 1.15f;

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        /// <summary>เรียกทันทีหลัง Instantiate เพื่อกำหนดธาตุและเริ่มอนิเมชัน</summary>
        public void Play(SpellElement element)
        {
            ApplyRendererSettings(element);
            StartCoroutine(Animate());
        }

        private void ApplyRendererSettings(SpellElement element)
        {
            if (renderers == null) return;

            Color tint = element.ToColor();

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null) continue;

                if (tintByElement)
                {
                    // เก็บค่า alpha เดิมของ sprite ไว้ ไม่งั้นภาพที่ตั้งใจให้จาง ๆ จะทึบหมด
                    renderer.color = new Color(tint.r, tint.g, tint.b, renderer.color.a);
                }

                if (!string.IsNullOrEmpty(sortingLayerName))
                    renderer.sortingLayerName = sortingLayerName;

                renderer.sortingOrder = sortingOrder;
            }
        }

        private IEnumerator Animate()
        {
            float total = fadeInTime + holdTime + fadeOutTime;
            float elapsed = 0f;

            var baseAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseAlphas[i] = renderers[i] != null ? renderers[i].color.a : 1f;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;

                float alpha;
                if (elapsed < fadeInTime)
                    alpha = fadeInTime > 0f ? elapsed / fadeInTime : 1f;
                else if (elapsed < fadeInTime + holdTime)
                    alpha = 1f;
                else
                    alpha = fadeOutTime > 0f
                        ? 1f - (elapsed - fadeInTime - holdTime) / fadeOutTime
                        : 0f;

                alpha = Mathf.Clamp01(alpha);

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    Color c = renderers[i].color;
                    renderers[i].color = new Color(c.r, c.g, c.b, baseAlphas[i] * alpha);
                }

                float t = Mathf.Clamp01(elapsed / total);
                transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
                transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
