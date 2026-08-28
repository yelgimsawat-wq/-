using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ธาตุเวทมนตร์ 4 ธาตุ
    ///
    /// ตัวเลขที่กำกับไว้สำคัญมาก ห้ามสลับหรือแทรกกลางภายหลัง เพราะค่านี้ถูกส่ง
    /// ข้ามเน็ตเป็นตัวเลข ถ้าสองเครื่องตีความไม่ตรงกันจะร่ายคนละเวท
    /// ถ้าจะเพิ่มธาตุใหม่ให้ต่อท้ายเท่านั้น
    /// </summary>
    public enum SpellElement : byte
    {
        Water = 0,
        Fire  = 1,
        Earth = 2,
        Wind  = 3,
    }

    public static class SpellElementExtensions
    {
        public static string ToThai(this SpellElement element)
        {
            switch (element)
            {
                case SpellElement.Water: return "น้ำ";
                case SpellElement.Fire:  return "ไฟ";
                case SpellElement.Earth: return "ดิน";
                default:                 return "ลม";
            }
        }

        /// <summary>สีประจำธาตุ ใช้กับเส้นที่วาดและวงเวทตอนที่ยังไม่มีอาร์ตจริง</summary>
        public static Color ToColor(this SpellElement element)
        {
            switch (element)
            {
                case SpellElement.Water: return new Color(0.30f, 0.65f, 1.00f);
                case SpellElement.Fire:  return new Color(1.00f, 0.45f, 0.15f);
                case SpellElement.Earth: return new Color(0.65f, 0.50f, 0.25f);
                default:                 return new Color(0.70f, 1.00f, 0.85f);
            }
        }

        /// <summary>
        /// ธาตุนี้ชนะธาตุอะไร เป็นวงจรปิด 4 ธาตุ ไม่มีธาตุไหนได้เปรียบกว่าเพื่อน
        ///
        ///   น้ำ ชนะ ไฟ  (ดับไฟ)
        ///   ไฟ ชนะ ลม  (ไฟลามตามลม)
        ///   ลม ชนะ ดิน (พัดดินจนกร่อน)
        ///   ดิน ชนะ น้ำ (ดินดูดซับน้ำ)
        ///
        /// ออกแบบเป็นวงจรเพื่อไม่ให้มีธาตุที่แข็งที่สุด ผู้เล่นจึงต้องอ่านว่า
        /// อีกฝ่ายกางโล่อะไรแล้วเลือกธาตุตอบ ไม่ใช่ร่ายธาตุเดิมซ้ำ ๆ
        /// </summary>
        public static SpellElement Beats(this SpellElement element)
        {
            switch (element)
            {
                case SpellElement.Water: return SpellElement.Fire;
                case SpellElement.Fire:  return SpellElement.Wind;
                case SpellElement.Wind:  return SpellElement.Earth;
                default:                 return SpellElement.Water;   // ดิน ชนะ น้ำ
            }
        }

        /// <summary>เวทที่ยิงมาเอาชนะโล่ที่กางอยู่ได้ไหม</summary>
        public static bool CountersShield(this SpellElement attacker, SpellElement shield)
        {
            return attacker.Beats() == shield;
        }

        /// <summary>ธาตุอะไรที่ใช้แก้โล่ธาตุนี้ได้ เอาไว้บอกผู้เล่นบนจอ</summary>
        public static SpellElement CounterFor(SpellElement shield)
        {
            foreach (SpellElement candidate in new[]
            {
                SpellElement.Water, SpellElement.Fire, SpellElement.Earth, SpellElement.Wind
            })
            {
                if (candidate.Beats() == shield) return candidate;
            }
            return SpellElement.Wind;
        }

        /// <summary>กันค่าที่ส่งข้ามเน็ตมาเพี้ยนหรือมาจากเวอร์ชันที่ไม่ตรงกัน</summary>
        public static SpellElement FromNetworkId(byte id)
        {
            switch (id)
            {
                case 0: return SpellElement.Water;
                case 1: return SpellElement.Fire;
                case 2: return SpellElement.Earth;
                case 3: return SpellElement.Wind;
                default: return SpellElement.Wind;
            }
        }
    }
}
