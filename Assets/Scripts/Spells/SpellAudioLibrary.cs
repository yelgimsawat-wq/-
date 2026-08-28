using System;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ใส่ไฟล์เสียงจริงมาทับเสียงที่สังเคราะห์ด้วยโค้ด
    ///
    /// ไม่ใส่อะไรเลยก็ทำงานได้ ระบบจะใช้เสียงสังเคราะห์ต่อไป
    /// อยากเปลี่ยนเสียงไหนก็ลากไฟล์ใส่เฉพาะช่องนั้น ผสมกันได้
    ///
    /// วางไว้ที่ GameObject ไหนก็ได้ในซีนเกม ตัวเดียวพอ
    /// (สคริปต์ติดตั้งอัตโนมัติวางไว้ให้แล้วบน NetworkManager)
    /// </summary>
    public class SpellAudioLibrary : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public SpellSound sound;
            public AudioClip clip;
        }

        [Header("ความดังรวม")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.5f;

        [Header("ไฟล์เสียงที่จะใช้แทนเสียงสังเคราะห์")]
        [Tooltip("ปล่อยว่างทั้งหมดก็ได้ ระบบจะสังเคราะห์เสียงเองทุกตัว")]
        [SerializeField] private Entry[] clips;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            // ปรับค่าใน Inspector ตอนกำลังเล่นแล้วได้ยินผลทันที ไม่ต้องหยุดแล้วเล่นใหม่
            if (Application.isPlaying) Apply();
        }

        private void Apply()
        {
            SpellAudio.MasterVolume = masterVolume;

            if (clips == null) return;

            foreach (Entry entry in clips)
            {
                if (entry == null) continue;
                SpellAudio.SetOverride(entry.sound, entry.clip);
            }
        }
    }
}
