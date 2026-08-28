using System;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ผูกภาพอนุภาคของแต่ละธาตุเข้ากับระบบเอฟเฟกต์
    ///
    /// วางไว้ตัวเดียวในซีน (สคริปต์ติดตั้งวางไว้ให้บน NetworkManager แล้ว)
    /// เปลี่ยนภาพเมื่อไรก็ลากใส่ช่องแล้วเห็นผลทันทีตอนเล่น
    ///
    /// ไม่ใส่อะไรเลยก็ไม่พัง แค่จะไม่มีเอฟเฟกต์
    /// </summary>
    public class SpellVfxLibrary : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public SpellElement element;
            public Sprite sprite;
        }

        [Header("ภาพอนุภาคแยกตามธาตุ")]
        [SerializeField] private Entry[] sprites;

        [Header("ภาพสำรอง")]
        [Tooltip("ใช้เมื่อธาตุนั้นยังไม่ได้ใส่ภาพของตัวเอง")]
        [SerializeField] private Sprite genericSprite;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            // เปลี่ยนภาพใน Inspector ตอนเล่นแล้วเห็นผลทันที ไม่ต้องหยุดแล้วเล่นใหม่
            if (Application.isPlaying) Apply();
        }

        private void Apply()
        {
            SpellVfx.RegisterGeneric(genericSprite);

            if (sprites == null) return;

            foreach (Entry entry in sprites)
            {
                if (entry == null) continue;
                SpellVfx.Register(entry.element, entry.sprite);
            }
        }
    }
}
