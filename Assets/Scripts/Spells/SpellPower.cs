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

        [Tooltip("ปรับความไวตามไมค์ให้เอง เปิดไว้แล้วไม่ต้องจูน sensitivity เอง")]
        [SerializeField] private bool autoCalibrate = true;

        [Tooltip("ความดังต่ำสุดที่ยอมให้ถือว่าเป็นเสียงเต็มหลอด "
                 + "กันไมค์ที่เงียบมากจนหายใจแล้วหลอดเต็ม")]
        [SerializeField] private float minLoudSpan = 0.035f;

        [Tooltip("ค่าสูงสุดที่จำไว้จะลดลงกี่ส่วนต่อวินาที "
                 + "ต้องลดบ้างไม่งั้นตะโกนแรงครั้งเดียวแล้วหลอดจะตื้อไปทั้งเกม")]
        [SerializeField] private float peakDecayPerSecond = 0.08f;

        // ความดังสูงสุดที่เคยได้ยินจากไมค์ตัวนี้ ใช้เป็นตัวหารให้หลอดเต็มพอดี
        private float observedLoudest;

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
            // ต้องอัปเดตหลอดก่อนออกจากเมธอด ไม่งั้นตอนไม่มีไมค์หลอดจะค้าง
            // ไม่ขึ้นข้อความบอกว่าไม่มีไมค์เลย ผู้เล่นจะนึกว่าระบบพัง
            if (!HasAudioSource)
            {
                PushMeter();
                return;
            }

            // เสียงมาจากสองทางได้ ไมค์ที่เราเปิดเอง หรือระบบ voice chat ป้อนมาให้
            float loudness = externalSource ? rawLoudness : ReadLoudness();
            float target = ToPower(loudness);

            // ค่าดิบจากไมค์กระโดดแรงมากทุกเฟรม ต้องหน่วงก่อนไม่งั้นหลอดจะสั่น
            smoothedPower = Mathf.Lerp(smoothedPower, target, smoothing * Time.deltaTime);

            if (capturing && smoothedPower > peakPower) peakPower = smoothedPower;

            PushMeter();
        }


        /// <summary>
        /// แปลงความดังดิบเป็นค่า 0..1
        ///
        /// โหมดปรับเอง: จำความดังสูงสุดที่เคยได้ยินจากไมค์ตัวนี้ แล้วหารด้วยค่านั้น
        /// ตะโกนสุดเสียงจึงเต็มหลอดเสมอ ไม่ว่าไมค์จะเกนสูงหรือต่ำ
        ///
        /// ที่ต้องทำแบบนี้เพราะค่าดิบจากไมค์แต่ละตัวต่างกันหลายเท่า
        /// ตั้งตัวคูณตายตัวไว้ค่าเดียว ไมค์เกนต่ำจะตะโกนยังไงก็ไม่ถึงครึ่ง
        /// ส่วนไมค์เกนสูงแค่หายใจก็เต็มหลอดแล้ว
        ///
        /// ค่าสูงสุดที่จำไว้ค่อย ๆ ลดลง ไม่งั้นเผลอตะโกนแรงครั้งเดียว
        /// (หรือมีเสียงดังผ่านมา) แล้วหลอดจะตื้อไปทั้งเกม
        /// </summary>
        private float ToPower(float loudness)
        {
            if (!autoCalibrate)
                return Mathf.Clamp01((loudness - noiseFloor) * sensitivity);

            if (loudness > observedLoudest) observedLoudest = loudness;

            observedLoudest = Mathf.Max(
                noiseFloor + minLoudSpan,
                observedLoudest - observedLoudest * peakDecayPerSecond * Time.deltaTime);

            float span = Mathf.Max(minLoudSpan, observedLoudest - noiseFloor);
            return Mathf.Clamp01((loudness - noiseFloor) / span);
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

        /// <summary>
        /// ส่งความดังไปให้หลอดวัดบน Canvas
        ///
        /// ย้ายจาก OnGUI ที่โชว์เป็นเปอร์เซ็นต์ มาเป็นหลอดแนวตั้งที่สูงตามเสียง
        /// ตัวเลขเปอร์เซ็นต์อ่านยากระหว่างเล่น ต้องละสายตาจากเกมมาอ่าน
        /// แต่หลอดสีดูปราดเดียวรู้ว่าดังพอหรือยัง
        ///
        /// ตอนกำลังเขียนคาถาส่งค่าสูงสุดที่จับได้ ไม่ใช่ค่าปัจจุบัน
        /// เพราะค่าสูงสุดคือค่าที่จะกลายเป็นความแรงเวทจริง ถ้าโชว์ค่าปัจจุบัน
        /// ผู้เล่นจะไม่รู้เลยว่าที่ตะโกนไปเมื่อกี้ติดหรือเปล่า
        /// </summary>
        private void PushMeter()
        {
            VoiceMeter meter = VoiceMeter.Instance;
            if (meter == null) return;

            if (!showMeter || caster == null || !caster.IsOwner) return;

            // โชว์ค่าสด ไม่ใช่ค่าสูงสุดค้างไว้ ไม่งั้นหลอดจะค้างสูงจนดูไม่ออก
            // ว่าตอนนี้พูดดังแค่ไหน ส่วนค่าสูงสุดไปโชว์เป็นขีดแยกต่างหาก
            meter.SetLevel(CurrentPower, HasAudioSource);
            meter.SetPeak(capturing ? peakPower : 0f, capturing);
        }
    }
}
