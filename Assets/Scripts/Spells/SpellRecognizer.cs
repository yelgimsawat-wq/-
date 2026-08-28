using System.Collections.Generic;
using UnityEngine;

namespace MagicDrawing
{
    public struct SpellCastResult
    {
        public bool Success;
        public SpellElement Element;
        public float Score;
        public string ShapeName;

        /// <summary>ข้อความบอกผู้เล่นว่าทำไมร่ายไม่ติด ว่างถ้าสำเร็จ</summary>
        public string FailReason;
    }

    /// <summary>
    /// ตัดสินว่าชุดเส้นที่วาดออกมาเป็นเวทอะไร
    ///
    /// กฎใหม่ (แก้จากเวอร์ชันแรกที่ธาตุลมออกง่ายเกินไป):
    ///
    /// เดิม: อะไรที่ดูไม่ออก = ลม  ผลคือวาดมั่วก็ได้เวท ลมจึงออกบ่อยที่สุด
    ///       ทั้งที่ควรเป็นธาตุที่ตั้งใจทำถึงจะได้
    ///
    /// ใหม่: - ลม ต้อง "ขีดสั้น ๆ หลายขีด" อย่างน้อย 4 ขีด และต้องเป็นขีดตรง ๆ จริง
    ///       - รูปทรง (วงกลม/สามเหลี่ยม/สี่เหลี่ยม) ใช้เกณฑ์หลวมลง เห็นเค้าโครงก็นับ
    ///       - วาดมั่วจนไม่เข้าพวกอะไรเลย = ร่ายไม่ติด ต้องวาดใหม่ ไม่ได้เวทลมฟรี ๆ
    /// </summary>
    public static class SpellRecognizer
    {
        /// <summary>
        /// ประเมินชุดเส้นทั้งหมดที่ผู้เล่นวาดค้างไว้
        /// </summary>
        /// <param name="strokes">แต่ละสมาชิกคือหนึ่งเส้นที่ลากจนปล่อยมือ</param>
        /// <param name="windMinStrokes">ต้องขีดกี่ขีดถึงจะนับเป็นเวทลม</param>
        /// <param name="minimumScore">เกณฑ์ความแม่นยำของรูปทรง</param>
        public static SpellCastResult Evaluate(
            IReadOnlyList<Vector2[]> strokes,
            int windMinStrokes,
            float minimumScore)
        {
            var result = new SpellCastResult { Success = false, Score = 0f };

            if (strokes == null || strokes.Count == 0)
            {
                result.FailReason = "ยังไม่ได้วาดอะไรเลย";
                return result;
            }

            // ---- ตรวจเวทลมก่อน เพราะเป็นกฎที่เฉพาะเจาะจงกว่า ----
            int slashCount = CountSlashes(strokes);
            if (slashCount >= windMinStrokes)
            {
                result.Success = true;
                result.Element = SpellElement.Wind;
                result.ShapeName = "Slashes";
                // ยิ่งขีดเกินเกณฑ์มากยิ่งมั่นใจ แต่ไม่ให้เกิน 1
                result.Score = Mathf.Clamp01((float)slashCount / windMinStrokes);
                return result;
            }

            // ---- ไม่ใช่ลม ก็ไปดูว่าเป็นรูปทรงอะไร ----
            // ใช้เส้นที่ยาวที่สุดเป็นตัวตัดสิน เผื่อผู้เล่นเผลอขีดเล็ก ๆ ปนมา
            Vector2[] main = LongestStroke(strokes);

            if (main == null || main.Length < 2)
            {
                result.FailReason = "เส้นสั้นเกินไป";
                return result;
            }

            RecognitionResult shape = DollarOneRecognizer.Recognize(main, SpellShapeLibrary.Templates);
            result.Score = shape.Score;
            result.ShapeName = shape.HasMatch ? shape.Name : null;

            if (!shape.HasMatch || shape.Score < minimumScore)
            {
                result.FailReason = slashCount > 0
                    ? $"ขีดยังไม่ครบ {windMinStrokes} ขีด และรูปทรงก็ไม่ชัดพอ"
                    : "ไม่เข้าพวกรูปทรงไหนเลย ลองวาดให้ชัดขึ้น";
                return result;
            }

            SpellElement? element = SpellShapeLibrary.ShapeToElement(shape.Name);
            if (element == null)
            {
                result.FailReason = "รูปทรงนี้ยังไม่มีเวทผูกไว้";
                return result;
            }

            result.Success = true;
            result.Element = element.Value;
            return result;
        }

        /// <summary>
        /// นับว่ามีกี่เส้นที่เข้าข่าย "ขีด" คือลากตรง ๆ ไม่ใช่วนเป็นรูป
        ///
        /// วัดด้วยความตรง = ระยะจากหัวถึงหางหารด้วยความยาวเส้นจริง
        /// เส้นตรงเป๊ะได้ 1.0 ส่วนวงกลมที่ลากกลับมาบรรจบจะเข้าใกล้ 0
        /// </summary>
        private static int CountSlashes(IReadOnlyList<Vector2[]> strokes)
        {
            const float straightnessThreshold = 0.72f;

            int count = 0;
            foreach (Vector2[] stroke in strokes)
            {
                if (stroke == null || stroke.Length < 2) continue;

                float pathLength = PathLength(stroke);
                if (pathLength <= Mathf.Epsilon) continue;

                float endToEnd = Vector2.Distance(stroke[0], stroke[stroke.Length - 1]);
                if (endToEnd / pathLength >= straightnessThreshold) count++;
            }
            return count;
        }

        private static Vector2[] LongestStroke(IReadOnlyList<Vector2[]> strokes)
        {
            Vector2[] best = null;
            float bestLength = -1f;

            foreach (Vector2[] stroke in strokes)
            {
                if (stroke == null || stroke.Length < 2) continue;

                float length = PathLength(stroke);
                if (length > bestLength)
                {
                    bestLength = length;
                    best = stroke;
                }
            }
            return best;
        }

        private static float PathLength(Vector2[] stroke)
        {
            float total = 0f;
            for (int i = 1; i < stroke.Length; i++)
                total += Vector2.Distance(stroke[i - 1], stroke[i]);
            return total;
        }
    }
}
