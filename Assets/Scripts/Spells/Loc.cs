using System.Collections.Generic;

namespace MagicDrawing
{
    /// <summary>
    /// ตารางแปลข้อความ ไทยกับอังกฤษ
    ///
    /// เก็บไว้ที่เดียวทั้งเกม เพิ่มภาษาใหม่ทำได้โดยเติมช่องในตารางนี้
    /// ไม่ต้องไล่แก้ทุกไฟล์ที่มีข้อความ
    ///
    /// ใช้กุญแจเป็นภาษาอังกฤษแบบสั้น ๆ ไม่ใช้ข้อความไทยเป็นกุญแจ
    /// เพราะถ้าใช้ข้อความเป็นกุญแจ พอแก้คำไทยนิดเดียวคำแปลอังกฤษจะหลุดทันที
    ///
    /// ข้อความที่ยังไม่มีในตารางจะคืนกุญแจกลับไป จะได้เห็นชัดว่าลืมแปลตัวไหน
    /// แทนที่จะขึ้นเป็นช่องว่างแล้วไม่มีใครสังเกต
    /// </summary>
    public static class Loc
    {
        private struct Entry
        {
            public readonly string Thai;
            public readonly string English;

            public Entry(string thai, string english)
            {
                Thai = thai;
                English = english;
            }
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";

            if (!table.TryGetValue(key, out Entry entry)) return key;

            return GameLanguage.Current == Language.Thai ? entry.Thai : entry.English;
        }

        /// <summary>แปลแล้วเสียบค่าลงไป เช่นจำนวนขีดหรือชื่อธาตุ</summary>
        public static string Get(string key, object value)
        {
            string template = Get(key);

            try
            {
                return string.Format(template, value);
            }
            catch (System.FormatException)
            {
                return template;
            }
        }

        private static readonly Dictionary<string, Entry> table = new Dictionary<string, Entry>
        {
            // ---------- ธาตุ ----------
            ["element.water"] = new Entry("น้ำ", "Water"),
            ["element.fire"] = new Entry("ไฟ", "Fire"),
            ["element.earth"] = new Entry("ดิน", "Earth"),
            ["element.wind"] = new Entry("ลม", "Wind"),

            // ---------- คำใบ้ระหว่างเล่น ----------
            ["hint.idle"] = new Entry(
                "A/D เดิน  |  W กระโดด  |  ลากเมาส์เขียนคาถา\nวงกลม=น้ำ  สามเหลี่ยม=ไฟ  สี่เหลี่ยม=ดิน  ขีด 4 ขีด=ลม",
                "A/D move  |  W jump  |  Drag mouse to draw a spell\nCircle=Water  Triangle=Fire  Square=Earth  4 slashes=Wind"),

            ["hint.composing"] = new Entry(
                "เขียนคาถา {0} ขีด  |  Space = ยืนยัน  |  Esc = ล้าง",
                "Drawing: {0} strokes  |  Space = confirm  |  Esc = clear"),

            ["hint.aiming"] = new Entry(
                "เวท{0} พร้อมแล้ว  |  เลื่อนเมาส์เล็ง  |  พูดให้ดังถึงเส้นขาว แล้วหยุดพูด",
                "{0} spell ready  |  Move mouse to aim  |  Speak past the white line, then stop"),

            // ---------- ผลการต่อสู้ ----------
            ["match.win"] = new Entry("คุณชนะ!", "You win!"),
            ["match.lose"] = new Entry("คุณแพ้", "You lose"),
            ["match.draw"] = new Entry("เสมอ — ไม่มีใครรอด", "Draw — nobody survived"),
            ["match.howToLeave"] = new Entry(
                "   —   กดออกจากห้องเพื่อกลับหน้าเมนู",
                "   —   Leave the room to return to the menu"),
            ["match.waiting"] = new Entry(
                "รอเพื่อนอีก {0} คนถึงจะเริ่มนับแพ้ชนะ   —   ระหว่างนี้ซ้อมวาดเวทได้ตามปกติ",
                "Waiting for {0} more player(s) before scoring starts   —   feel free to practise"),
            ["match.eliminated"] = new Entry(
                "คุณตกรอบแล้ว — กด Tab เปลี่ยนคนที่ดู   (เหลือ {0} คน)",
                "You are out — press Tab to change who you watch   ({0} left)"),
            ["match.alive"] = new Entry("เหลือ {0} คน", "{0} players left"),

            // ---------- เมนูหยุดเกม ----------
            ["pause.title"] = new Entry("หยุดเกม", "Paused"),
            ["pause.resume"] = new Entry("เล่นต่อ", "Resume"),
            ["pause.settings"] = new Entry("ตั้งค่า", "Settings"),
            ["pause.tutorial"] = new Entry("วิธีเล่น", "How to play"),
            ["pause.leave"] = new Entry("ออกจากห้อง", "Leave room"),
            ["pause.back"] = new Entry("ย้อนกลับ", "Back"),
            ["pause.openHint"] = new Entry("กด Esc เพื่อเปิดเมนู", "Press Esc for menu"),

            // ---------- ตั้งค่า ----------
            ["settings.title"] = new Entry("ตั้งค่า", "Settings"),
            ["settings.language"] = new Entry("ภาษา", "Language"),
            ["settings.volume"] = new Entry("ระดับเสียง", "Volume"),
            ["settings.micDevice"] = new Entry("ไมโครโฟน", "Microphone"),
            ["settings.fireThreshold"] = new Entry("ความดังที่ต้องใช้ยิงเวท", "Loudness needed to cast"),

            // ---------- วิธีเล่น ----------
            ["tut.title"] = new Entry("วิธีเล่น", "How to play"),
            ["tut.next"] = new Entry("ถัดไป", "Next"),
            ["tut.prev"] = new Entry("ก่อนหน้า", "Back"),
            ["tut.page"] = new Entry("หน้า {0}", "Page {0}"),

            ["tut.draw.title"] = new Entry("1. วาดคาถาด้วยเมาส์", "1. Draw your spell"),
            ["tut.draw.body"] = new Entry(
                "กดเมาส์ค้างแล้วลากบนจอเพื่อวาด\nรูปที่วาดจะกลายเป็นธาตุของเวท",
                "Hold the mouse button and drag to draw.\nThe shape you draw decides the element."),

            ["tut.shapes.title"] = new Entry("2. รูปไหนได้ธาตุอะไร", "2. Shapes and elements"),
            ["tut.shapes.body"] = new Entry(
                "วงกลม = น้ำ     สามเหลี่ยม = ไฟ\nสี่เหลี่ยม = ดิน     ขีดตรง 4 ขีด = ลม",
                "Circle = Water     Triangle = Fire\nSquare = Earth     4 slashes = Wind"),

            ["tut.shield.title"] = new Entry("3. วาดทับตัวเอง = โล่", "3. Draw on yourself = shield"),
            ["tut.shield.body"] = new Entry(
                "ถ้าวาดครอบตัวละครของตัวเอง เวทจะกลายเป็นโล่แทนการยิง\nโล่กันเวทได้เกือบหมด ยกเว้นธาตุที่แก้กันได้",
                "Draw around your own character and the spell becomes a shield\ninstead of a projectile. Shields block almost everything\nexcept the element that counters them."),

            ["tut.fire.title"] = new Entry("4. ยิงเวทออกไป", "4. Casting at the enemy"),
            ["tut.fire.body"] = new Entry(
                "วาดข้าง ๆ ตัว แล้วกด Space เพื่อยืนยันคาถา\nเลื่อนเมาส์เล็งทิศ แล้วพูดให้ดังถึงเส้นขาวบนหลอด\nพอหยุดพูด เวทจะยิงออกไปเอง ยิ่งดังยิ่งแรง",
                "Draw beside your character, then press Space to confirm.\nMove the mouse to aim, then speak until the bar passes\nthe white line. Stop speaking and the spell fires.\nLouder voice means a stronger spell."),

            ["tut.counter.title"] = new Entry("5. ธาตุแก้กัน", "5. Elements counter each other"),
            ["tut.counter.body"] = new Entry(
                "น้ำ ชนะ ไฟ  •  ไฟ ชนะ ลม\nลม ชนะ ดิน  •  ดิน ชนะ น้ำ\n\nยิงถูกธาตุที่แก้โล่ได้ โล่จะแตกและเจ็บกว่าเดิม",
                "Water beats Fire  •  Fire beats Wind\nWind beats Earth  •  Earth beats Water\n\nHit a shield with its counter and the shield breaks."),
        };

        /// <summary>ชื่อธาตุตามภาษาที่เลือกอยู่</summary>
        public static string ElementName(SpellElement element)
        {
            switch (element)
            {
                case SpellElement.Water: return Get("element.water");
                case SpellElement.Fire: return Get("element.fire");
                case SpellElement.Earth: return Get("element.earth");
                default: return Get("element.wind");
            }
        }
    }
}
