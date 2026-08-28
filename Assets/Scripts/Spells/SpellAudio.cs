using System.Collections.Generic;
using UnityEngine;

namespace MagicDrawing
{
    public enum SpellSound
    {
        Cast,          // ยิงเวทออกไป
        Shield,        // กางโล่
        DrawLoop,      // เสียงต่อเนื่องระหว่างลากเส้น (วนลูป)
        StrokeStart,   // เริ่มลากขีดใหม่
        StrokeEnd,     // ปล่อยมือจบขีด
        Confirm,       // เขียนคาถาเสร็จ ระบบอ่านออกว่าเป็นธาตุอะไร
        Reject,        // เขียนแล้วอ่านไม่ออก ร่ายไม่ติด
        Manifest,      // วงเวทปรากฏ
        Hit,           // โดนเวทเต็ม ๆ
        Blocked,       // โล่กันไว้ได้
        ShieldBreak,   // โล่แตกเพราะโดนธาตุที่แก้ได้
        Jump,
        Death,
    }

    /// <summary>
    /// เสียงเอฟเฟกต์ของเกม สังเคราะห์ด้วยโค้ดทั้งหมด
    ///
    /// ทำแบบเดียวกับภาพในโปรเจกต์นี้ คือปั้นขึ้นมาเองแทนการไปหาไฟล์มาก่อน
    /// ได้เสียงที่ใช้ได้ทันทีโดยไม่ต้องกังวลเรื่องลิขสิทธิ์หรือขนาดไฟล์
    /// และเปลี่ยนโทนของแต่ละธาตุได้ด้วยการแก้ตัวเลขบรรทัดเดียว
    ///
    /// อยากใช้ไฟล์เสียงจริงเมื่อไร ใส่ลงช่อง clips ใน SpellAudioLibrary
    /// ระบบจะใช้ไฟล์แทนทันที ไม่ต้องแก้โค้ดที่เรียกใช้เลย
    ///
    /// เสียงถูกสร้างครั้งเดียวแล้วเก็บไว้ใช้ซ้ำ ไม่ได้สังเคราะห์ใหม่ทุกครั้งที่ยิง
    /// </summary>
    public static class SpellAudio
    {
        private const int SampleRate = 44100;

        private static readonly Dictionary<int, AudioClip> cache = new Dictionary<int, AudioClip>();

        /// <summary>ไฟล์เสียงจริงที่ผู้ใช้ใส่มาทับ ถ้ามีจะถูกใช้ก่อนเสียงสังเคราะห์</summary>
        private static readonly Dictionary<SpellSound, AudioClip> overrides =
            new Dictionary<SpellSound, AudioClip>();

        [Range(0f, 1f)]
        private static float masterVolume = 0.5f;

        public static float MasterVolume
        {
            get => masterVolume;
            set => masterVolume = Mathf.Clamp01(value);
        }

        /// <summary>ให้ SpellAudioLibrary เอาไฟล์เสียงจริงมาลงทะเบียนทับ</summary>
        public static void SetOverride(SpellSound sound, AudioClip clip)
        {
            if (clip == null) overrides.Remove(sound);
            else overrides[sound] = clip;
        }

        /// <summary>
        /// เล่นเสียงหนึ่งครั้งที่ตำแหน่งในโลก
        /// element ใช้กำหนดโทนเสียง ธาตุต่างกันจะได้ยินต่างกันโดยไม่ต้องมองจอ
        /// </summary>
        public static void Play(SpellSound sound, Vector3 position, SpellElement element = SpellElement.Wind)
        {
            AudioClip clip = Resolve(sound, element);
            if (clip == null) return;

            // PlayClipAtPoint สร้าง AudioSource ชั่วคราวแล้วเก็บกวาดให้เอง
            // เหมาะกับเสียงสั้น ๆ ที่เล่นแล้วจบ ไม่ต้องจัดการ object pool
            AudioSource.PlayClipAtPoint(clip, position, masterVolume);
        }

        private static AudioClip Resolve(SpellSound sound, SpellElement element)
        {
            if (overrides.TryGetValue(sound, out AudioClip custom) && custom != null)
                return custom;

            // ธาตุมีผลกับโทนเฉพาะเสียงที่เกี่ยวกับเวท เสียงกระโดดไม่ต้องแยกธาตุ
            bool elementMatters =
                sound == SpellSound.Cast
                || sound == SpellSound.Shield
                || sound == SpellSound.Confirm
                || sound == SpellSound.Manifest;
            int key = (int)sound * 100 + (elementMatters ? (int)element : 0);

            if (cache.TryGetValue(key, out AudioClip cached) && cached != null)
                return cached;

            AudioClip generated = Synthesize(sound, element);
            cache[key] = generated;
            return generated;
        }

        /// <summary>ความถี่พื้นฐานของแต่ละธาตุ ไล่จากต่ำ (ดิน) ไปสูง (ลม)</summary>
        private static float BaseFrequency(SpellElement element)
        {
            switch (element)
            {
                case SpellElement.Earth: return 165f;
                case SpellElement.Fire:  return 262f;
                case SpellElement.Water: return 392f;
                default:                 return 587f;   // ลม
            }
        }

        private static AudioClip Synthesize(SpellSound sound, SpellElement element)
        {
            switch (sound)
            {
                case SpellSound.Cast:
                    // เสียงกวาดขึ้นเร็ว ๆ ให้ความรู้สึกว่ามีอะไรพุ่งออกไป
                    return BuildTone($"Cast_{element}", 0.28f,
                        BaseFrequency(element), BaseFrequency(element) * 2.2f, 0.35f);

                case SpellSound.Shield:
                    // กวาดลงและยาวกว่า ให้ความรู้สึกหนักแน่นแบบตั้งรับ
                    return BuildTone($"Shield_{element}", 0.5f,
                        BaseFrequency(element) * 1.6f, BaseFrequency(element) * 0.9f, 0.15f);

                case SpellSound.DrawLoop:
                    return BuildDrawLoop();

                case SpellSound.StrokeStart:
                    // สั้นและเบามาก เพราะดังทุกครั้งที่แตะเมาส์ ถ้าเด่นไปจะน่ารำคาญเร็ว
                    return BuildTone("StrokeStart", 0.05f, 900f, 1200f, 0.1f);

                case SpellSound.StrokeEnd:
                    return BuildTone("StrokeEnd", 0.07f, 1200f, 800f, 0.1f);

                case SpellSound.Confirm:
                    // กวาดขึ้นสองเท่าตัว ให้ความรู้สึกว่าคาถาติดแล้ว พลังกำลังรวมตัว
                    return BuildTone($"Confirm_{element}", 0.35f,
                        BaseFrequency(element), BaseFrequency(element) * 3f, 0.12f);

                case SpellSound.Reject:
                    // กวาดลงต่ำ ฟังแล้วรู้ทันทีว่าไม่ผ่าน ไม่ต้องอ่านข้อความ
                    return BuildTone("Reject", 0.25f, 300f, 120f, 0.25f);

                case SpellSound.Manifest:
                    // ยาวและนุ่ม เป็นเสียงพื้นหลังตอนวงเวทค่อย ๆ ปรากฏ
                    return BuildTone($"Manifest_{element}", 0.6f,
                        BaseFrequency(element) * 0.75f, BaseFrequency(element) * 1.5f, 0.08f);

                case SpellSound.Hit:
                    return BuildNoise("Hit", 0.22f, 0.6f, 220f);

                case SpellSound.Blocked:
                    // เบาและทึบ บอกว่าโล่รับไว้ได้ ไม่ใช่โดนเต็ม ๆ
                    return BuildNoise("Blocked", 0.14f, 0.25f, 900f);

                case SpellSound.ShieldBreak:
                    return BuildNoise("ShieldBreak", 0.45f, 0.75f, 120f);

                case SpellSound.Jump:
                    return BuildTone("Jump", 0.12f, 320f, 620f, 0.2f);

                default:
                    return BuildTone("Death", 0.7f, 330f, 70f, 0.3f);
            }
        }

        /// <summary>
        /// เสียงโทนที่กวาดความถี่จาก from ไป to
        ///
        /// สะสมเฟสทีละตัวอย่างแทนการคำนวณจากเวลาตรง ๆ
        /// ถ้าใช้ sin(2*pi*f*t) ตอนที่ f เปลี่ยนไปเรื่อย ๆ คลื่นจะขาดเป็นช่วง ๆ
        /// แล้วได้ยินเป็นเสียงแตกแทนที่จะเป็นการกวาดที่ลื่นไหล
        /// </summary>
        private static AudioClip BuildTone(string name, float duration, float from, float to, float noiseMix)
        {
            int count = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[count];

            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float frequency = Mathf.Lerp(from, to, t);

                phase += 2f * Mathf.PI * frequency / SampleRate;
                float wave = Mathf.Sin(phase);

                // ผสมเสียงซ่าเล็กน้อยให้ฟังดูมีเนื้อ ไม่ใช่เสียงบี๊บเปล่า ๆ
                float noise = Random.Range(-1f, 1f) * noiseMix;

                samples[i] = (wave + noise) * Envelope(t) * 0.6f;
            }

            return CreateClip(name, samples);
        }

        /// <summary>เสียงซ่ากรองความถี่สูงออก ใช้กับเสียงกระแทกและเสียงแตก</summary>
        private static AudioClip BuildNoise(string name, float duration, float bodyMix, float toneFrequency)
        {
            int count = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[count];

            float phase = 0f;
            float smoothed = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;

                // กรองแบบง่ายที่สุด คือถัวเฉลี่ยกับค่าก่อนหน้า ตัดความแหลมออก
                // เสียงซ่าดิบ ๆ จะบาดหูเกินไปสำหรับเสียงกระแทก
                smoothed = Mathf.Lerp(smoothed, Random.Range(-1f, 1f), 0.35f);

                phase += 2f * Mathf.PI * toneFrequency / SampleRate;
                float body = Mathf.Sin(phase) * bodyMix;

                samples[i] = (smoothed + body) * Envelope(t) * 0.6f;
            }

            return CreateClip(name, samples);
        }

        /// <summary>
        /// เสียงต่อเนื่องระหว่างลากเส้น คล้ายเสียงดินสอขูดกระดาษผสมเสียงฮัมเบา ๆ
        ///
        /// ต่างจากเสียงอื่นตรงที่ไม่มีซองเสียง เพราะเสียงนี้ถูกเล่นวนลูป
        /// ถ้าใส่ซองจะได้ยินเป็นจังหวะดัง-เบาซ้ำ ๆ ทุกครั้งที่วนกลับมาต้นคลิป
        ///
        /// ความยาวคลิปคำนวณให้พอดีกับจำนวนรอบเต็มของเสียงฮัม
        /// รอยต่อตอนวนกลับจึงไม่มีเสียงป๊อก
        /// </summary>
        private static AudioClip BuildDrawLoop()
        {
            const float humFrequency = 110f;
            const int cycles = 55;                 // 0.5 วินาทีพอดีที่ 110 Hz

            int count = Mathf.RoundToInt(SampleRate * cycles / humFrequency);
            var samples = new float[count];

            float smoothed = 0f;

            for (int i = 0; i < count; i++)
            {
                // เสียงฮัมคำนวณจากดัชนีโดยตรง ไม่ใช่การสะสมเฟส
                // เพื่อให้ตัวอย่างสุดท้ายต่อกับตัวแรกได้พอดีเป๊ะ
                float hum = Mathf.Sin(2f * Mathf.PI * humFrequency * i / SampleRate) * 0.25f;

                smoothed = Mathf.Lerp(smoothed, Random.Range(-1f, 1f), 0.25f);

                samples[i] = (hum + smoothed * 0.35f) * 0.35f;
            }

            return CreateClip("DrawLoop", samples);
        }

        /// <summary>
        /// ขอคลิปไปเล่นเองผ่าน AudioSource ของผู้เรียก
        /// ใช้กับเสียงที่ต้องวนลูปหรือคุมความดัง/ระดับเสียงระหว่างเล่น
        /// ซึ่ง PlayClipAtPoint ทำไม่ได้เพราะมันเล่นแล้วปล่อยจนจบ
        /// </summary>
        public static AudioClip GetClip(SpellSound sound, SpellElement element = SpellElement.Wind)
        {
            return Resolve(sound, element);
        }

        /// <summary>
        /// ซองเสียง ดังขึ้นเร็วมากแล้วค่อย ๆ เบาลง
        /// ถ้าไม่มีซอง เสียงจะเริ่มและจบแบบตัดกึก แล้วได้ยินเป็นเสียงป๊อกทุกครั้ง
        /// </summary>
        private static float Envelope(float t)
        {
            const float attack = 0.02f;

            if (t < attack) return t / attack;
            return Mathf.Pow(1f - (t - attack) / (1f - attack), 2f);
        }

        private static AudioClip CreateClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
