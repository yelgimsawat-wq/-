#if METAVC_NGO
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

        private SpellPower power;

        private void Awake()
        {
            power = GetComponent<SpellPower>();

            // ต้องจองสิทธิ์ตั้งแต่ Awake เพราะ Start ของ SpellPower จะไปเปิดไมค์
            // ถ้าบอกช้ากว่านั้นจะกลายเป็นเปิดไมค์ซ้อนกันสองระบบแล้วพังทั้งคู่
            power.UseExternalSource();
        }

        private void OnEnable()
        {
            if (audioInput == null)
                audioInput = FindFirstObjectByType<VcAudioInput>();

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
    }
}
#endif
