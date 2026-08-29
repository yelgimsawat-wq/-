using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// หน้าตั้งค่าไมโครโฟน เลือกไมค์และทดสอบว่าได้ยินจริงไหม
    ///
    /// มีเพราะ "ไมค์ไม่ติด" เป็นปัญหาที่หาสาเหตุเองไม่ได้เลยจากในเกม
    /// ผู้เล่นเห็นแค่ว่าเวทไม่ออก แต่ไม่รู้ว่าเพราะพูดเบาไป เลือกไมค์ผิดตัว
    /// หรือ Windows ไม่ให้สิทธิ์ หน้านี้แยกสามอย่างนั้นออกจากกันให้เห็นชัด
    ///
    /// เปิดไมค์เองเฉพาะตอนหน้านี้เปิดอยู่ แล้วปิดทันทีที่ปิดหน้า
    /// ถ้าค้างไว้จะไปแย่งไมค์กับระบบเสียงตอนเล่นจริง
    /// </summary>
    public class MicTestPanel : MonoBehaviour
    {
        [Header("ของที่ต้องผูก")]
        [SerializeField] private Dropdown deviceDropdown;

        [Tooltip("หลอดแสดงความดังตอนทดสอบ ต้องตั้ง Image Type เป็น Filled")]
        [SerializeField] private Image levelFill;

        [SerializeField] private Text statusLabel;

        [Header("เกณฑ์")]
        [Tooltip("ดังเกินค่านี้ถือว่าได้ยินเสียงแล้ว")]
        [SerializeField] private float hearThreshold = 0.02f;

        [Tooltip("เปิดหน้านี้ค้างไว้กี่วินาทีโดยไม่ได้ยินอะไรเลย ถึงจะเตือน")]
        [SerializeField] private float silenceWarnSeconds = 3f;

        private AudioClip clip;
        private string device;
        private float[] buffer;
        private float shownLevel;
        private float silentFor;
        private bool everHeard;

        private readonly List<string> deviceValues = new List<string>();

        private void OnEnable()
        {
            buffer = new float[1024];
            BuildDeviceList();
            StartListening();
        }

        private void OnDisable()
        {
            StopListening();

            if (deviceDropdown != null)
                deviceDropdown.onValueChanged.RemoveListener(HandleDeviceChanged);
        }

        /// <summary>
        /// เติมรายชื่อไมค์ลงกล่องเลือก
        /// ตัวเลือกแรกคือค่าเริ่มต้นของระบบ เผื่อคนไม่อยากเลือกเอง
        /// </summary>
        private void BuildDeviceList()
        {
            if (deviceDropdown == null) return;

            deviceDropdown.onValueChanged.RemoveListener(HandleDeviceChanged);

            deviceValues.Clear();
            var labels = new List<string>();

            deviceValues.Add("");
            labels.Add(MicSettings.SystemDefaultLabel);

            foreach (string name in Microphone.devices)
            {
                deviceValues.Add(name);
                labels.Add(name);
            }

            deviceDropdown.ClearOptions();
            deviceDropdown.AddOptions(labels);

            // เลือกให้ตรงกับที่บันทึกไว้ ไม่งั้นเปิดหน้ามาทีไรก็เด้งกลับตัวแรกทุกที
            string saved = MicSettings.SelectedDevice ?? "";
            int index = deviceValues.IndexOf(saved);
            deviceDropdown.SetValueWithoutNotify(index >= 0 ? index : 0);

            deviceDropdown.onValueChanged.AddListener(HandleDeviceChanged);
        }

        private void HandleDeviceChanged(int index)
        {
            if (index < 0 || index >= deviceValues.Count) return;

            MicSettings.Select(deviceValues[index]);

            // เปิดใหม่ด้วยตัวที่เพิ่งเลือก จะได้ทดสอบตัวนั้นทันที
            StopListening();
            StartListening();
        }

        private void StartListening()
        {
            everHeard = false;
            silentFor = 0f;
            shownLevel = 0f;

            if (Microphone.devices.Length == 0)
            {
                Report("ไม่พบไมโครโฟนในเครื่องนี้\nตรวจว่าเสียบไมค์แล้ว และเปิดสิทธิ์ไมโครโฟนใน Windows",
                    warning: true);
                return;
            }

            device = MicSettings.SelectedDevice;
            clip = Microphone.Start(device, true, 1, 44100);

            if (clip == null)
            {
                Report("เปิดไมโครโฟนไม่สำเร็จ\nอาจถูกโปรแกรมอื่นใช้อยู่ ลองปิดโปรแกรมที่ใช้ไมค์แล้วลองใหม่",
                    warning: true);
                return;
            }

            Report("พูดอะไรก็ได้ แล้วดูว่าหลอดขยับไหม");
        }

        private void StopListening()
        {
            if (clip == null) return;

            Microphone.End(device);
            clip = null;
        }

        private void Update()
        {
            if (clip == null) return;

            float loudness = ReadLoudness();

            // เกลี่ยก่อนแสดง ไม่งั้นหลอดสั่นตามคลื่นเสียงทุกเฟรมจนดูไม่ออก
            shownLevel = Mathf.Lerp(shownLevel, Mathf.Clamp01(loudness * 12f),
                1f - Mathf.Exp(-Time.deltaTime * 15f));

            if (levelFill != null)
            {
                levelFill.fillAmount = shownLevel;
                levelFill.color = Color.Lerp(
                    new Color(0.35f, 0.85f, 0.40f), new Color(1f, 0.30f, 0.25f), shownLevel);
            }

            if (loudness > hearThreshold)
            {
                everHeard = true;
                silentFor = 0f;
                Report("ได้ยินเสียงแล้ว ไมค์ใช้งานได้", good: true);
                return;
            }

            if (everHeard) return;

            // เงียบมาตั้งแต่เปิดหน้า แปลว่าน่าจะเลือกผิดตัวหรือไมค์ถูกปิดเสียง
            silentFor += Time.deltaTime;
            if (silentFor >= silenceWarnSeconds)
            {
                Report("ยังไม่ได้ยินอะไรเลย\nลองเลือกไมค์ตัวอื่นในช่องด้านบน หรือเช็คว่าไมค์ถูกปิดเสียงอยู่ไหม",
                    warning: true);
            }
        }

        /// <summary>ความดังแบบ RMS ของช่วงเสียงล่าสุด</summary>
        private float ReadLoudness()
        {
            int position = Microphone.GetPosition(device) - buffer.Length;
            if (position < 0) return 0f;

            clip.GetData(buffer, position);

            float sum = 0f;
            foreach (float sample in buffer)
                sum += sample * sample;

            return Mathf.Sqrt(sum / buffer.Length);
        }

        private void Report(string message, bool warning = false, bool good = false)
        {
            if (statusLabel == null) return;
            if (statusLabel.text == message) return;

            statusLabel.text = message;
            statusLabel.color = good
                ? new Color(0.25f, 0.65f, 0.30f)
                : warning ? new Color(0.85f, 0.35f, 0.20f)
                : new Color(0.20f, 0.22f, 0.28f);
        }
    }
}
