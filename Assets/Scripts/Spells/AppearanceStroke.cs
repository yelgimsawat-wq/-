using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// เส้นหนึ่งเส้นของตัวละคร พร้อมสีและความหนาของตัวเอง
    ///
    /// เดิมเก็บแค่พิกัดจุด แล้วใช้สีกับความหนาชุดเดียวกันทั้งตัว
    /// พอผู้เล่นอยากเปลี่ยนสีกลางคัน ข้อมูลเดิมรองรับไม่ได้เลย
    /// เพราะไม่มีที่ให้เก็บว่าเส้นไหนสีอะไร
    ///
    /// เก็บเป็น struct เพราะเป็นข้อมูลล้วน ไม่มีพฤติกรรม และถูกคัดลอกไปมาบ่อย
    /// </summary>
    public struct AppearanceStroke
    {
        public Vector2[] Points;
        public Color Color;

        /// <summary>ความหนาเทียบกับความกว้างภาพ อยู่ในช่วง MinThickness ถึง MaxThickness</summary>
        public float Thickness;

        /// <summary>
        /// ช่วงความหนาที่ยอมให้เลือก
        ///
        /// บางกว่า 0.004 จะจางจนแทบมองไม่เห็นบนภาพ 256 พิกเซล
        /// หนากว่า 0.05 จะกลายเป็นก้อนจนวาดรายละเอียดไม่ได้
        /// และเป็นช่วงที่เข้ารหัสลงหนึ่งไบต์ได้พอดีโดยตายังแยกขั้นไม่ออก
        /// </summary>
        public const float MinThickness = 0.004f;
        public const float MaxThickness = 0.05f;

        public AppearanceStroke(Vector2[] points, Color color, float thickness)
        {
            Points = points;
            Color = color;
            Thickness = Mathf.Clamp(thickness, MinThickness, MaxThickness);
        }

        public bool IsValid => Points != null && Points.Length >= 2;

        /// <summary>บีบความหนาลงหนึ่งไบต์สำหรับเก็บและส่งข้ามเน็ต</summary>
        public byte ThicknessToByte()
        {
            float t = Mathf.InverseLerp(MinThickness, MaxThickness, Thickness);
            return (byte)Mathf.RoundToInt(t * 255f);
        }

        public static float ThicknessFromByte(byte value)
        {
            return Mathf.Lerp(MinThickness, MaxThickness, value / 255f);
        }
    }
}
