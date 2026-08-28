using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// ตัวอย่างว่าตัวละครที่วาดจะหน้าตาแบบไหนตอนอยู่ในเกมจริง
    ///
    /// แสดงคู่กับกระดานวาด เพื่อให้เห็นผลทันทีว่าที่วาดอยู่จะออกมาเป็นยังไง
    /// พร้อมป้ายชื่อเหนือหัวแบบเดียวกับในสนามรบ
    ///
    /// ยืมเท็กซ์เจอร์ใบเดียวกับกระดานวาดมาแสดงเลย ไม่อบใหม่
    /// เพราะภาพที่ใช้ในเกมก็มาจากชุดเส้นชุดเดียวกันนี้อยู่แล้ว
    /// </summary>
    public class ProfileCharacterPreview : MonoBehaviour
    {
        [Header("ของที่ต้องผูก")]
        [SerializeField] private ProfileDrawPad pad;

        [Tooltip("ที่แสดงตัวละคร")]
        [SerializeField] private RawImage characterImage;

        [Tooltip("ป้ายชื่อเหนือหัว")]
        [SerializeField] private Text nameLabel;

        [Tooltip("ช่องกรอกชื่อ เอาไว้อ่านชื่อที่พิมพ์อยู่")]
        [SerializeField] private InputField nameInput;

        [Tooltip("ข้อความเมื่อยังไม่ได้ตั้งชื่อ")]
        [SerializeField] private string placeholderName = "ผู้เล่น";

        private void Update()
        {
            SyncCharacter();
            SyncName();
        }

        private void SyncCharacter()
        {
            if (pad == null || characterImage == null) return;

            Texture2D current = pad.PreviewTexture;

            // เทียบก่อนค่อยเขียน ไม่งั้นสั่ง UI ให้วาดใหม่ทุกเฟรมโดยไม่จำเป็น
            if (!ReferenceEquals(characterImage.texture, current))
                characterImage.texture = current;

            // ยังไม่ได้วาดอะไรเลยก็ซ่อนไว้ ไม่ต้องโชว์กรอบว่าง ๆ
            characterImage.enabled = current != null;
        }

        private void SyncName()
        {
            if (nameLabel == null) return;

            string typed = nameInput != null ? nameInput.text : "";
            string shown = string.IsNullOrWhiteSpace(typed)
                ? placeholderName
                : PlayerProfile.Sanitize(typed);

            if (nameLabel.text != shown) nameLabel.text = shown;
        }
    }
}
