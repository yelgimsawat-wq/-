using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ทำให้ตัวละครดูมีชีวิต โดยขยับเฉพาะภาพ ไม่แตะฟิสิกส์
    ///
    /// ต้องอยู่บน GameObject ลูกที่มี SpriteRenderer ไม่ใช่บนตัวหลัก
    /// เพราะถ้าหมุนหรือย่อตัวหลัก collider จะหมุนและย่อตามไปด้วย
    /// แล้วตัวละครจะจมพื้นหรือลอยขึ้นเองตอนอนิเมชันทำงาน
    ///
    /// อ่านความเร็วจากการเปลี่ยนตำแหน่งแทนการอ่าน Rigidbody2D
    /// เพราะตัวละครของคนอื่นเป็น Kinematic ที่ความเร็วเป็นศูนย์ตลอด
    /// ตำแหน่งมาจากการซิงก์ ถ้าอ่านความเร็วตรง ๆ ตัวคนอื่นจะยืนนิ่งตลอดเกม
    /// </summary>
    public class CharacterAnimator2D : MonoBehaviour
    {
        [Header("ตอนเดิน")]
        [Tooltip("องศาที่เอียงซ้ายขวาสูงสุด")]
        [SerializeField] private float walkTiltDegrees = DefaultWalkTiltDegrees;

        [Tooltip("เอียงไปมากี่รอบต่อวินาที")]
        [SerializeField] private float walkTiltSpeed = DefaultWalkTiltSpeed;

        [Tooltip("ย่อ-ยืดตอนเดิน ให้ดูเหมือนก้าวเท้า")]
        [SerializeField] private float walkBobAmount = DefaultWalkBobAmount;

        [Header("ตอนยืน")]
        [Tooltip("ยืดหดตอนอยู่เฉย ๆ เหมือนหายใจ")]
        [SerializeField] private float idleBreathAmount = DefaultIdleBreathAmount;

        [Tooltip("หายใจกี่รอบต่อวินาที")]
        [SerializeField] private float idleBreathSpeed = DefaultIdleBreathSpeed;

        [Header("ตอนลอยอยู่กลางอากาศ")]
        [Tooltip("ยืดตัวตอนพุ่งขึ้นหรือตกลง")]
        [SerializeField] private float airStretchAmount = 0.12f;

        [Header("ความนุ่มนวล")]
        [Tooltip("ยิ่งมากยิ่งเปลี่ยนท่าไว ยิ่งน้อยยิ่งลื่นแต่ตอบสนองช้า")]
        [SerializeField] private float smoothing = DefaultSmoothing;

        [Tooltip("เร็วกว่านี้ถือว่ากำลังเดิน (หน่วยโลกต่อวินาที)")]
        [SerializeField] private float walkThreshold = 0.4f;

        [Tooltip("เร็วกว่านี้ในแนวดิ่งถือว่าลอยอยู่")]
        [SerializeField] private float airThreshold = 1.5f;

        private Vector3 lastPosition;
        private float phase;

        private Vector3 baseScale;
        private float currentTilt;
        private Vector3 currentScale;

        // ---------- สูตรท่าทาง ----------
        //
        // แยกออกมาเป็น static เพื่อให้หน้าเมนูเรียกใช้สูตรเดียวกันได้
        // ตัวอย่างตัวละครในเมนูจะได้ขยับเหมือนในสนามรบเป๊ะ ๆ
        // ถ้าปล่อยให้ต่างคนต่างคำนวณ วันหนึ่งจะแก้ที่เดียวแล้วอีกที่ไม่ตาม

        public const float DefaultWalkTiltDegrees = 9f;
        public const float DefaultWalkTiltSpeed = 7f;
        public const float DefaultWalkBobAmount = 0.06f;
        public const float DefaultIdleBreathAmount = 0.035f;
        public const float DefaultIdleBreathSpeed = 1.8f;
        public const float DefaultSmoothing = 12f;

        /// <summary>
        /// ท่ายืนหายใจ คืนตัวคูณสเกล (x, y)
        /// ยืดสูงขึ้นก็แคบลง หดลงก็กว้างขึ้น เหมือนของที่มีปริมาตรคงที่
        /// </summary>
        public static Vector2 IdleScale(float time, float breathAmount, float breathSpeed)
        {
            float breath = Mathf.Sin(time * breathSpeed * Mathf.PI) * breathAmount;
            return new Vector2(1f - breath * 0.5f, 1f + breath);
        }

        /// <summary>
        /// ท่าเดิน คืนตัวคูณสเกล และส่งค่าคลื่นออกทาง swing
        /// เอา swing ไปคูณองศาเอียงเอง จะได้ปรับความแรงแยกจากจังหวะได้
        /// </summary>
        public static Vector2 WalkScale(float phase, float bobAmount, out float swing)
        {
            swing = Mathf.Sin(phase);

            // ย่อลงตอนเท้าแตะพื้น ซึ่งเป็นจังหวะที่การเอียงถึงจุดสุด
            // ใช้ค่าสัมบูรณ์จึงย่อสองครั้งต่อหนึ่งรอบการแกว่ง ตรงกับสองก้าว
            float bob = Mathf.Abs(swing) * bobAmount;
            return new Vector2(1f + bob * 0.5f, 1f - bob);
        }

        private void Awake()
        {
            baseScale = transform.localScale;
            currentScale = baseScale;
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            // กันกระโดดตอนเปิดกลับมา เช่นหลังเกิดใหม่
            lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;

            float horizontalSpeed = Mathf.Abs(delta.x) / dt;
            float verticalSpeed = Mathf.Abs(delta.y) / dt;

            bool airborne = verticalSpeed > airThreshold;
            bool walking = !airborne && horizontalSpeed > walkThreshold;

            float targetTilt;
            Vector3 targetScale;

            if (airborne)
            {
                // ลอยอยู่ก็ยืดตัวขึ้นและแคบลง เหมือนตัวการ์ตูนที่กระโดด
                targetTilt = 0f;
                targetScale = new Vector3(
                    baseScale.x * (1f - airStretchAmount * 0.6f),
                    baseScale.y * (1f + airStretchAmount),
                    baseScale.z);
            }
            else if (walking)
            {
                // เฟสเดินตามความเร็วจริง เดินเร็วก็แกว่งถี่ขึ้นเอง
                phase += dt * walkTiltSpeed * Mathf.Clamp(horizontalSpeed / 5f, 0.5f, 1.6f);

                Vector2 walkMul = WalkScale(phase, walkBobAmount, out float swing);

                targetTilt = swing * walkTiltDegrees;
                targetScale = new Vector3(
                    baseScale.x * walkMul.x,
                    baseScale.y * walkMul.y,
                    baseScale.z);
            }
            else
            {
                phase = 0f;
                targetTilt = 0f;

                Vector2 idleMul = IdleScale(Time.time, idleBreathAmount, idleBreathSpeed);
                targetScale = new Vector3(
                    baseScale.x * idleMul.x,
                    baseScale.y * idleMul.y,
                    baseScale.z);
            }

            currentTilt = Mathf.Lerp(currentTilt, targetTilt, smoothing * dt);
            currentScale = Vector3.Lerp(currentScale, targetScale, smoothing * dt);

            transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
            transform.localScale = currentScale;
        }
    }
}
