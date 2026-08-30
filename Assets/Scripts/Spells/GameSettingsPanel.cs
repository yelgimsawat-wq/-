using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// ตั้งค่าระหว่างเล่น เปิดจากเมนู Esc
    ///
    /// มีเฉพาะของที่อยากเปลี่ยนกลางเกมจริง ๆ คือภาษาและระดับเสียง
    /// ส่วนการเลือกไมค์อยู่ในเมนูหลักก่อนเข้าห้อง เพราะการสลับไมค์กลางเกม
    /// ต้องปิดแล้วเปิดอุปกรณ์ใหม่ ซึ่งทำให้เสียงขาดหายกลางการต่อสู้
    /// </summary>
    public class GameSettingsPanel : MonoBehaviour
    {
        private const string VolumeKey = "MagicDrawing.Volume";

        [Header("ภาษา")]
        [SerializeField] private Button languageButton;
        [SerializeField] private Text languageValue;

        [Header("เสียง")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValue;

        private void Awake()
        {
            // ตั้งเสียงตั้งแต่เริ่ม ไม่ต้องรอให้เปิดหน้านี้ก่อน
            // ไม่งั้นผู้เล่นที่หรี่เสียงไว้จะโดนเสียงดังใส่ทุกครั้งที่เปิดเกม
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        }

        private void OnEnable()
        {
            if (languageButton != null) languageButton.onClick.AddListener(ToggleLanguage);

            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.SetValueWithoutNotify(AudioListener.volume);
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }

            GameLanguage.Changed += RefreshLabels;
            RefreshLabels();
        }

        private void OnDisable()
        {
            if (languageButton != null) languageButton.onClick.RemoveListener(ToggleLanguage);
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetVolume);

            GameLanguage.Changed -= RefreshLabels;
        }

        private void ToggleLanguage()
        {
            GameLanguage.Toggle();
            RefreshLabels();
        }

        private void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);

            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
            PlayerPrefs.Save();

            RefreshLabels();
        }

        private void RefreshLabels()
        {
            // ชื่อภาษาเขียนด้วยภาษานั้นเอง จะได้อ่านออกแม้ตอนนี้อ่านอีกภาษาไม่ออก
            if (languageValue != null) languageValue.text = GameLanguage.CurrentName;

            if (volumeValue != null)
                volumeValue.text = Mathf.RoundToInt(AudioListener.volume * 100f) + " %";
        }
    }
}
