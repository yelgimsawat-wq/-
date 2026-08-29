#if METAVC_NGO
using MetaVoiceChat;
using MetaVoiceChat.Input;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// สะพานเชื่อมระหว่างระบบ voice chat กับความแรงเวท
    ///
    /// นี่คือส่วน "ปรับแต่งให้ระดับความแรงของเวทขึ้นอยู่กับเสียงที่พูด"
    /// ดึงตัวอย่างเสียงจากสตรีมเดียวกับที่ส่งให้เพื่อนได้ยิน
    /// ตะโกนดังแค่ไหน เพื่อนได้ยินเท่านั้น และเวทก็แรงเท่านั้น ตรงกันเสมอ
    ///
    /// ทั้งไฟล์ถูกครอบด้วย #if METAVC_NGO ถ้าวันหนึ่งลบ MetaVoiceChat ออก
    /// ไฟล์นี้จะหายไปจากการคอมไพล์เอง ไม่ทำให้โปรเจกต์พัง
    /// (define ตัวนี้ EnsureMetaVoiceChatDefine เปิด/ปิดให้อัตโนมัติ)
    ///
    /// วิธีใช้: ใส่ component นี้ไว้บน GameObject เดียวกับ SpellPower
    /// แล้วลาก VcAudioInput ของผู้เล่นมาใส่ ถ้าปล่อยว่างจะไปค้นหาให้เอง
    /// </summary>
    [RequireComponent(typeof(SpellPower))]
    public class VoiceChatPowerBridge : MonoBehaviour
    {
        [Tooltip("ตัวรับเสียงของ voice chat ปล่อยว่าง = ค้นหาในฉากให้เอง")]
        [SerializeField] private VcAudioInput audioInput;

        [Tooltip("เปิดไมค์เฉพาะตอนกำลังร่ายเวท ปิดตัวเลือกนี้ = พูดได้ตลอดเวลา")]
        [SerializeField] private bool onlyTransmitWhileCasting = true;

        private SpellPower power;
        private SpellDrawing drawing;
        private MetaVc metaVc;

        private void Awake()
        {
            power = GetComponent<SpellPower>();
            drawing = GetComponent<SpellDrawing>();
            metaVc = GetComponent<MetaVc>();

            // ต้องจองสิทธิ์ตั้งแต่ Awake เพราะ Start ของ SpellPower จะไปเปิดไมค์
            // ถ้าบอกช้ากว่านั้นจะกลายเป็นเปิดไมค์ซ้อนกันสองระบบแล้วพังทั้งคู่
            power.UseExternalSource();
        }

        private void OnEnable()
        {
            if (audioInput == null)
                audioInput = GetComponentInChildren<VcAudioInput>(true);

            // ห้ามใช้ FindFirstObjectByType หาจากทั้งฉาก
            //
            // ในห้องมีตัวละครหลายตัว แต่ละตัวมีไมค์ของตัวเอง การค้นทั้งฉาก
            // จะคว้าตัวไหนก็ได้ที่เจอก่อน สะพานของเราอาจไปเกาะไมค์ของคนอื่น
            // แล้วความแรงเวทของเราจะขึ้นกับเสียงเพื่อนแทนเสียงตัวเอง

            if (audioInput == null)
            {
                Debug.LogWarning(
                    "[VoiceChatPowerBridge] หา VcAudioInput ไม่เจอ "
                    + "ความแรงเวทจะใช้ค่าคงที่แทน\n"
                    + "ตรวจว่าใส่ MetaVc และ VcMicAudioInput ไว้ในฉากแล้วหรือยัง",
                    this);
                return;
            }

            audioInput.OnFrameReady += HandleFrameReady;
        }

        private void OnDisable()
        {
            // ไม่ถอดออก = event ค้างชี้มาที่ object ที่ถูกทำลายแล้ว
            if (audioInput != null) audioInput.OnFrameReady -= HandleFrameReady;
        }

        private void HandleFrameReady(int index, float[] samples)
        {
            power.FeedExternalSamples(samples);
        }

        /// <summary>
        /// เปิดไมค์เฉพาะตอนกำลังร่ายเวท
        ///
        /// ทำให้เพื่อนได้ยินเสียงเราตอนร่ายเวทพอดี ซึ่งเป็นจังหวะที่ต้องการ
        /// และแก้ปัญหาที่ตามมาเองด้วย: ถ้าเปิดไมค์ตลอดเวลา เสียงหายใจ
        /// เสียงพัดลม เสียงคีย์บอร์ด จะดังกวนกันทั้งเกม
        ///
        /// ปิดตัวเลือกนี้ใน Inspector ถ้าอยากให้คุยกันได้ตลอด
        /// </summary>
        private void Update()
        {
            if (!onlyTransmitWhileCasting) return;
            if (metaVc == null || drawing == null) return;

            bool shouldTransmit = drawing.IsCasting;

            // เขียนทับทุกเฟรมไม่ได้ ค่านี้เป็น reactive property ที่ยิง event ทุกครั้งที่ตั้ง
            if (metaVc.isInputMuted.Value == shouldTransmit)
                metaVc.isInputMuted.Value = !shouldTransmit;
        }
    }
}
#endif
