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

        private bool externalSource;
        private float rawLoudness;

        private bool capturing;
        private bool hasCaptured;
        private float peakPower;

        /// <summary>
        /// ความดังสูงสุดตลอดช่วงที่เขียนคาถา
        ///
        /// ต้องใช้ค่าสูงสุด ไม่ใช่ค่า ณ วินาทีที่กดยิง เพราะผู้เล่นตะโกนตอนวาด
        /// แล้วมักจะเงียบไปแล้วตอนกดยืนยัน ถ้าอ่านตอนยิงจะได้ดาเมจต่ำทุกครั้ง
        /// ทั้งที่ตะโกนสุดเสียง
        ///
        /// ถ้ายังไม่เคยเริ่มจับ จะคืนค่าปัจจุบันแทน
        /// </summary>
        public float PeakPower => hasCaptured ? peakPower : CurrentPower;

        /// <summary>เริ่มจับความดัง เรียกตอนผู้เล่นเริ่มเขียนคาถา</summary>
        public void StartCapture()
        {
            capturing = true;
            hasCaptured = true;
            peakPower = 0f;
        }

        /// <summary>หยุดจับ เรียกตอนยิงหรือยกเลิก ค่าที่จับได้ยังอ่านได้อยู่</summary>
        public void StopCapture()
        {
            capturing = false;
        }

        /// <summary>ความแรงตอนนี้ 0 = เงียบ, 1 = ดังเต็มที่</summary>
        public float CurrentPower => HasAudioSource ? smoothedPower : fallbackPower;

        /// <summary>มีแหล่งเสียงให้อ่านไหม ไม่ว่าจะจากไมค์เองหรือจากระบบ voice chat</summary>
        public bool HasAudioSource => micReady || externalSource;

        /// <summary>
        /// บอกว่าจะมีคนป้อนเสียงให้ ไม่ต้องเปิดไมค์เอง
        ///
        /// ต้องเรียกใน Awake ของตัวป้อน เพราะ Awake ของทุก component ทำงานก่อน
        /// Start ทั้งหมด ค่านี้จึงถูกตั้งทันก่อนที่ Start ตรงนี้จะไปเปิดไมค์
        ///
        /// จำเป็นมาก เพราะถ้าทั้ง voice chat และตัวนี้เปิดไมค์ตัวเดียวกันพร้อมกัน
        /// จะแย่งอุปกรณ์กันจนพังทั้งคู่
        /// </summary>
        public void UseExternalSource()
        {
            externalSource = true;
        }

        /// <summary>
        /// รับตัวอย่างเสียงดิบจากระบบ voice chat มาคิดความดัง
        /// ใช้สตรีมเดียวกับที่ส่งให้เพื่อนได้ยิน ความแรงเวทจึงตรงกับเสียงที่พูดจริง
        /// </summary>
        public void FeedExternalSamples(float[] samples)
        {
            if (samples == null || samples.Length == 0) return;

            float sum = 0f;
            foreach (float sample in samples)
                sum += sample * sample;

            rawLoudness = Mathf.Sqrt(sum / samples.Length);
        }

        private void Awake()
        {
            caster = GetComponent<SpellCaster>();
        }

        private void Start()
        {
            // ตัวละครของคนอื่นไม่ต้องเปิดไมค์ เปลืองเปล่า ๆ และอาจชนกันเองด้วย
            if (caster != null && !caster.IsOwner) return;

            // มีระบบ voice chat ป้อนเสียงให้แล้ว ห้ามเปิดไมค์ซ้ำ จะแย่งอุปกรณ์กัน
            if (externalSource) return;

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
            if (!HasAudioSource) return;

            // เสียงมาจากสองทางได้ ไมค์ที่เราเปิดเอง หรือระบบ voice chat ป้อนมาให้
            float loudness = externalSource ? rawLoudness : ReadLoudness();
            float target = Mathf.Clamp01((loudness - noiseFloor) * sensitivity);

            // ค่าดิบจากไมค์กระโดดแรงมากทุกเฟรม ต้องหน่วงก่อนไม่งั้นหลอดจะสั่น
            smoothedPower = Mathf.Lerp(smoothedPower, target, smoothing * Time.deltaTime);

            if (capturing && smoothedPower > peakPower) peakPower = smoothedPower;
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

            string label = HasAudioSource
                ? (externalSource ? $"เสียง {CurrentPower:P0} (voice chat)" : $"เสียง {CurrentPower:P0}")
                : "ไม่มีไมค์ (ใช้ค่าคงที่)";

            GUI.Label(new Rect(box.x + 8f, box.y + 4f, box.width - 16f, 20f), label);

            // หลอดพื้นหลัง
            var barBack = new Rect(box.x + 8f, box.y + 28f, box.width - 16f, 14f);
            GUI.Box(barBack, GUIContent.none);

            Color previous = GUI.color;

            // หลอดจริงยาวตามความดังตอนนี้
            var barFill = new Rect(barBack.x, barBack.y, barBack.width * CurrentPower, barBack.height);
            GUI.color = Color.Lerp(Color.cyan, Color.red, CurrentPower);
            GUI.Box(barFill, GUIContent.none);

            // ขีดค้างที่ค่าสูงสุดตอนกำลังเขียนคาถา
            // ผู้เล่นต้องเห็นว่าตะโกนไปแล้วได้เท่าไร เพราะนั่นคือค่าที่จะกลายเป็นดาเมจ
            // ถ้าเห็นแค่ค่าปัจจุบันจะไม่รู้เลยว่าที่ตะโกนไปเมื่อกี้ติดหรือเปล่า
            if (capturing && peakPower > 0.01f)
            {
                var peakMark = new Rect(
                    barBack.x + barBack.width * peakPower - 1f, barBack.y - 2f, 2f, barBack.height + 4f);
                GUI.color = Color.yellow;
                GUI.Box(peakMark, GUIContent.none);
            }

            GUI.color = previous;
        }
    }
}
