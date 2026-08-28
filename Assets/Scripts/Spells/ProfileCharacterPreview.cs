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
    ///
    /// อนิเมชันเรียกสูตรเดียวกับ CharacterAnimator2D ที่ใช้ในสนามรบ
    /// ไม่ได้ก๊อปตัวเลขมาวางซ้ำ แก้ที่เดียวจึงเปลี่ยนทั้งสองที่พร้อมกัน
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

        [Header("อนิเมชันตัวอย่าง")]
        [Tooltip("ยืนหายใจกี่วินาทีก่อนสลับไปเดิน")]
        [SerializeField] private float idleSeconds = 2.5f;

        [Tooltip("เดินกี่วินาทีก่อนสลับกลับมายืน ตั้ง 0 ถ้าอยากให้ยืนอย่างเดียว")]
        [SerializeField] private float walkSeconds = 2.5f;

        [Tooltip("ระยะที่เดินไปมาในกรอบ หน่วยพิกเซล")]
        [SerializeField] private float walkSwayPixels = 30f;

        private RectTransform characterRect;
        private Vector3 baseScale = Vector3.one;
        private Vector2 basePosition;

        private float phase;
        private float cycleTimer;
        private float currentTilt;
        private Vector3 currentScale = Vector3.one;

        private void Awake()
        {
            if (characterImage == null) return;

            characterRect = characterImage.rectTransform;
            baseScale = characterRect.localScale;
            currentScale = baseScale;
            basePosition = characterRect.anchoredPosition;
        }

        private void Update()
        {
            SyncCharacter();
            SyncName();
            Animate();
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

        /// <summary>
        /// สลับท่ายืนกับท่าเดินวนไปเรื่อย ๆ ให้เห็นทั้งสองแบบโดยไม่ต้องเข้าเกม
        ///
        /// ใช้เวลาแบบไม่สนใจ Time.scale เพราะนี่คือหน้าเมนู ถ้าเกมหยุดเวลาไว้
        /// ด้วยเหตุผลอะไรก็ตาม ตัวอย่างก็ยังควรขยับให้ดูอยู่
        /// </summary>
        private void Animate()
        {
            if (characterRect == null) return;

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            cycleTimer += dt;

            float total = idleSeconds + walkSeconds;
            bool walking = walkSeconds > 0f && total > 0f && (cycleTimer % total) >= idleSeconds;

            float targetTilt;
            Vector3 targetScale;
            Vector2 targetPosition = basePosition;

            if (walking)
            {
                phase += dt * CharacterAnimator2D.DefaultWalkTiltSpeed;

                Vector2 mul = CharacterAnimator2D.WalkScale(
                    phase, CharacterAnimator2D.DefaultWalkBobAmount, out float swing);

                targetTilt = swing * CharacterAnimator2D.DefaultWalkTiltDegrees;
                targetScale = new Vector3(baseScale.x * mul.x, baseScale.y * mul.y, baseScale.z);

                // เดินไปมาช้า ๆ ในกรอบ ใช้ครึ่งความถี่ของการเอียง
                // จะได้เอียงสองครั้งต่อการเดินไปกลับหนึ่งรอบ เหมือนสองก้าว
                targetPosition = basePosition
                    + new Vector2(Mathf.Sin(phase * 0.5f) * walkSwayPixels, 0f);
            }
            else
            {
                phase = 0f;
                targetTilt = 0f;

                Vector2 mul = CharacterAnimator2D.IdleScale(
                    Time.unscaledTime,
                    CharacterAnimator2D.DefaultIdleBreathAmount,
                    CharacterAnimator2D.DefaultIdleBreathSpeed);

                targetScale = new Vector3(baseScale.x * mul.x, baseScale.y * mul.y, baseScale.z);
            }

            float blend = CharacterAnimator2D.DefaultSmoothing * dt;

            currentTilt = Mathf.Lerp(currentTilt, targetTilt, blend);
            currentScale = Vector3.Lerp(currentScale, targetScale, blend);

            // จุดหมุนอยู่ที่เท้า (pivot 0.5, 0) การเอียงจึงดูเหมือนโยกตัว
            // ไม่ใช่หมุนรอบกลางตัวแล้วเท้าลอย
            characterRect.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
            characterRect.localScale = currentScale;
            characterRect.anchoredPosition = Vector2.Lerp(
                characterRect.anchoredPosition, targetPosition, blend);
        }
    }
}
