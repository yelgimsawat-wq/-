using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// วัดความดังเสียงจากไมโครโฟน แล้วแปลงเป็นความแรงเวท 0-1
    ///
    /// ตะโกนดัง = เวทแรง ยิ่งใหญ่ ยิ่งเจ็บ
    ///
    /// ใช้ Microphone API ที่ติดมากับ Unity ไม่ต้องลงไลบรารีเพิ่ม
    /// อ่านค่าเฉพาะเครื่องเจ้าของตัวละคร แล้วส่งค่าไปพร้อมคำสั่งร่ายเวท
    /// (ไมค์ของเราไม่ได้ยินเสียงเพื่อน ค่าจึงต้องเดินทางไปกับคำสั่ง ไม่ใช่
    /// ให้ทุกเครื่องอ่านไมค์ตัวเองแล้วหวังว่าจะตรงกัน)
    ///
    /// หมายเหตุ: นี่คือการ "วัดความดัง" ไม่ใช่ "ส่งเสียงคุยกัน"
    /// ถ้าอยากให้ได้ยินเสียงกันจริง ๆ ต้องใช้ระบบ voice chat แยกต่างหาก
    /// </summary>
    public class SpellPower : MonoBehaviour
    {
        [Header("การอ่านไมค์")]
        [Tooltip("ตัวคูณความไว ยิ่งมากยิ่งถึงเต็มหลอดง่าย พูดปกติก็แรงแล้ว")]
        [SerializeField] private float sensitivity = 12f;

        [Tooltip("ความหนืดของหลอด ยิ่งมากยิ่งตอบสนองไว แต่กระตุกกว่า")]
        [SerializeField] private float smoothing = 10f;

        [Tooltip("เสียงต่ำกว่านี้ถือว่าเงียบ กันเสียงลมและเสียงพัดลมคอมพิวเตอร์")]
        [SerializeField] private float noiseFloor = 0.015f;

        [Tooltip("ใช้เมื่อเครื่องไม่มีไมค์หรือเปิดไม่ได้ เกมจะได้เล่นต่อได้")]
        [Range(0f, 1f)]
        [SerializeField] private float fallbackPower = 0.5f;

        [Header("หน้าจอ")]
        [Tooltip("แสดงหลอดความดังให้เจ้าของตัวละครเห็น")]
        [SerializeField] private bool showMeter = true;

        private AudioClip micClip;
        private string micDevice;
        private float[] sampleBuffer;
        private float smoothedPower;
        private bool micReady;

        private SpellCaster caster;

        /// <summary>ความแรงตอนนี้ 0 = เงียบ, 1 = ดังเต็มที่</summary>
        public float CurrentPower => micReady ? smoothedPower : fallbackPower;

        /// <summary>เปิดไมค์ได้หรือเปล่า เอาไว้บอกผู้เล่นบนจอ</summary>
        public bool MicrophoneReady => micReady;

        private void Awake()
        {
            caster = GetComponent<SpellCaster>();
        }

        private void Start()
        {
            // ตัวละครของคนอื่นไม่ต้องเปิดไมค์ เปลืองเปล่า ๆ และอาจชนกันเองด้วย
            if (caster != null && !caster.IsOwner) return;

            StartMicrophone();
        }

        private void StartMicrophone()
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning(
                    "[SpellPower] ไม่พบไมโครโฟน จะใช้ความแรงคงที่แทน "
                    + $"({fallbackPower:P0}) เกมยังเล่นได้ตามปกติ");
                return;
            }

            micDevice = Microphone.devices[0];

            // อัดวนในคลิปสั้น ๆ 1 วินาที พอสำหรับดูความดังปัจจุบัน
            // และไม่กินหน่วยความจำเหมือนอัดเก็บไว้ยาว ๆ
            micClip = Microphone.Start(micDevice, true, 1, 44100);

            if (micClip == null)
            {
                Debug.LogWarning("[SpellPower] เปิดไมโครโฟนไม่สำเร็จ จะใช้ความแรงคงที่แทน");
                return;
            }

            sampleBuffer = new float[1024];
            micReady = true;
            Debug.Log($"[SpellPower] ใช้ไมโครโฟน: {micDevice}");
        }

        private void Update()
        {
            if (!micReady) return;

            float loudness = ReadLoudness();
            float target = Mathf.Clamp01((loudness - noiseFloor) * sensitivity);

            // ค่าดิบจากไมค์กระโดดแรงมากทุกเฟรม ต้องหน่วงก่อนไม่งั้นหลอดจะสั่น
            smoothedPower = Mathf.Lerp(smoothedPower, target, smoothing * Time.deltaTime);
        }

        /// <summary>
        /// ความดังแบบ RMS ของช่วงเสียงล่าสุด
        /// ใช้ RMS แทนค่าสูงสุด เพราะค่าสูงสุดกระโดดตามเสียงป๊อกแป๊กเดียว
        /// ส่วน RMS สะท้อนพลังเสียงโดยรวมซึ่งตรงกับความรู้สึก "ดัง" มากกว่า
        /// </summary>
        private float ReadLoudness()
        {
            int position = Microphone.GetPosition(micDevice) - sampleBuffer.Length;
            if (position < 0) return 0f;

            micClip.GetData(sampleBuffer, position);

            float sum = 0f;
            foreach (float sample in sampleBuffer)
                sum += sample * sample;

            return Mathf.Sqrt(sum / sampleBuffer.Length);
        }

        private void OnDestroy()
        {
            // ไม่ปิดไมค์ = ไฟไมค์ค้างติดหลังออกจากเกม ผู้เล่นจะตกใจ
            if (micReady && !string.IsNullOrEmpty(micDevice))
                Microphone.End(micDevice);
        }

        private void OnGUI()
        {
            if (!showMeter) return;
            if (caster == null || !caster.IsOwner) return;

            var box = new Rect(Screen.width - 190f, 16f, 174f, 54f);
            GUI.Box(box, GUIContent.none);

            string label = micReady
                ? $"เสียง {CurrentPower:P0}"
                : "ไม่มีไมค์ (ใช้ค่าคงที่)";

            GUI.Label(new Rect(box.x + 8f, box.y + 4f, box.width - 16f, 20f), label);

            // หลอดพื้นหลัง
            var barBack = new Rect(box.x + 8f, box.y + 28f, box.width - 16f, 14f);
            GUI.Box(barBack, GUIContent.none);

            // หลอดจริงยาวตามความดัง
            var barFill = new Rect(barBack.x, barBack.y, barBack.width * CurrentPower, barBack.height);
            Color previous = GUI.color;
            GUI.color = Color.Lerp(Color.cyan, Color.red, CurrentPower);
            GUI.Box(barFill, GUIContent.none);
            GUI.color = previous;
        }
    }
}
