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

        [Tooltip("เร็วกว่านี้ในแนวดิ่งถือว่าลอยอยู่ ใช้เฉพาะตอนหา NetworkPlayer2D ไม่เจอ")]
        [SerializeField] private float airThreshold = 1.5f;

        [Header("แก้ปัญหา")]
        [Tooltip("พิมพ์ท่าที่เปลี่ยนลง Console เปิดเมื่อสงสัยว่าอนิเมชันไม่ทำงาน "
                 + "พิมพ์เฉพาะตอนเปลี่ยนท่า ไม่ได้พิมพ์ทุกเฟรม")]
        [SerializeField] private bool logStateChanges;

        private Vector3 lastPosition;
        private float phase;
        private NetworkPlayer2D player;

        private float smoothedHorizontal;
        private float smoothedVertical;

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

            // อยู่บน GameObject ลูก ตัวคุมการเดินอยู่บนตัวแม่
            player = GetComponentInParent<NetworkPlayer2D>();
        }

        private void OnEnable()
        {
            // กันกระโดดตอนเปิดกลับมา เช่นหลังเกิดใหม่
            lastPosition = transform.position;
        }

        private string lastReportedState;

        /// <summary>
        /// พิมพ์เฉพาะตอนเปลี่ยนท่า ไม่ใช่ทุกเฟรม
        /// ถ้าเดินอยู่แล้วยังขึ้น "ยืน" หรือ "ลอย" แปลว่าตัวตัดสินท่าผิด
        /// ไม่ใช่ตัวอนิเมชันเสีย จะได้ไล่ถูกจุด
        /// </summary>
        private void ReportState(bool airborne, bool walking, float horizontal, float vertical)
        {
            string state = airborne ? "ลอย" : walking ? "เดิน" : "ยืน";
            if (state == lastReportedState) return;

            lastReportedState = state;

            string ground = player != null
                ? (player.IsGrounded ? "แตะพื้น" : "ไม่แตะพื้น")
                : "ไม่มี NetworkPlayer2D";

            Debug.Log(
                $"[CharacterAnimator2D] {name} -> {state} | "
                + $"เร็วแนวนอน {horizontal:F2} (เกณฑ์ {walkThreshold}) | "
                + $"เร็วแนวดิ่ง {vertical:F2} | {ground}", this);
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;

            // เกลี่ยความเร็วก่อนใช้ อย่าเชื่อค่าของเฟรมเดียว
            //
            // ฟิสิกส์ขยับตัวละคร 50 ครั้งต่อวินาที แต่จอวาด 60-144 เฟรม
            // เฟรมที่ไม่มีการก้าวของฟิสิกส์ ระยะที่ขยับจะเป็นศูนย์พอดี
            // ถ้าเชื่อค่าดิบจะสลับไปมาระหว่าง "เดิน" กับ "ยืน" หลายสิบครั้งต่อวินาที
            // มุมเอียงถูกดึงกลับเป็นศูนย์ตลอด จนดูเหมือนไม่มีอนิเมชันเลย
            // ยิ่งจอเฟรมสูงยิ่งอาการหนัก
            float instantHorizontal = Mathf.Abs(delta.x) / dt;
            float instantVertical = Mathf.Abs(delta.y) / dt;

            // ค่าคงที่ 12 คือความไวในการตาม ประมาณ 80 มิลลิวินาทีก็เข้าที่
            // เร็วพอให้ตอบสนองทันตอนเริ่มเดินและหยุดเดิน
            float follow = 1f - Mathf.Exp(-dt * 12f);
            smoothedHorizontal = Mathf.Lerp(smoothedHorizontal, instantHorizontal, follow);
            smoothedVertical = Mathf.Lerp(smoothedVertical, instantVertical, follow);

            float horizontalSpeed = smoothedHorizontal;
            float verticalSpeed = smoothedVertical;

            // ถามตัวคุมการเดินตรง ๆ ว่าเท้าแตะพื้นอยู่ไหม แม่นกว่าเดาจากความเร็ว
            //
            // เดิมเดาจากความเร็วแนวดิ่งอย่างเดียว ซึ่งเด้งเกินเกณฑ์ได้ง่ายมาก
            // ตอนเดินบนพื้น เพราะฟิสิกส์ดันตัวขึ้นลงเล็กน้อยทุกก้าว
            // พอถูกตัดสินว่าลอยอยู่ มุมเอียงจะถูกบังคับเป็นศูนย์ ตัวเลยไม่โยก
            //
            // เก็บทางเดาไว้เป็นทางสำรอง เผื่อเอา component นี้ไปใช้กับของอื่น
            // ที่ไม่มี NetworkPlayer2D อยู่ด้วย
            bool airborne = player != null
                ? !player.IsGrounded
                : verticalSpeed > airThreshold;

            bool walking = !airborne && horizontalSpeed > walkThreshold;

            if (logStateChanges) ReportState(airborne, walking, horizontalSpeed, verticalSpeed);

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
