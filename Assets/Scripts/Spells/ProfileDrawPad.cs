using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// กระดานวาดตัวละครในหน้าเมนู
    ///
    /// วาดก่อนเข้าห้อง ข้อมูลจึงพร้อมตั้งแต่ก่อนต่อเน็ต ไม่ต้องส่งอะไรกลางเกม
    ///
    /// อ่านเมาส์ตรง ๆ แทนการใช้ IDragHandler เพราะโค้ดที่เหลือในโปรเจกต์
    /// อ่าน Mouse.current อยู่แล้ว ทำเหมือนกันหมดจะได้ไม่มีสองมาตรฐาน
    /// และไม่ต้องพึ่ง EventSystem ที่อาจถูกลบทิ้งโดยไม่ตั้งใจ
    ///
    /// พิกัดที่เก็บเป็น 0..1 เทียบกับกรอบวาด ไม่ใช่พิกเซล
    /// ย่อขยายกรอบทีหลังรูปจึงไม่เพี้ยน
    /// </summary>
    public class ProfileDrawPad : MonoBehaviour
    {
        [Header("ของที่ต้องผูก")]
        [Tooltip("กรอบสี่เหลี่ยมที่ใช้วาด")]
        [SerializeField] private RectTransform drawArea;

        [Tooltip("ที่แสดงภาพที่วาด")]
        [SerializeField] private RawImage preview;

        [Tooltip("ข้อความบอกว่าตัวใหญ่พอหรือยัง")]
        [SerializeField] private Text sizeHint;

        [Header("การวาด")]
        [Tooltip("ระยะห่างขั้นต่ำระหว่างจุด (0..1) ยิ่งมากยิ่งเก็บจุดน้อย")]
        [SerializeField] private float minPointDistance = 0.03f;

        [Tooltip("สีเส้นตัวละคร")]
        [SerializeField] private Color inkColor = Color.white;

        private readonly List<Vector2[]> strokes = new List<Vector2[]>();
        private readonly List<Vector2> currentStroke = new List<Vector2>();

        private Texture2D previewTexture;
        private bool isDrawing;

        /// <summary>ชุดเส้นที่วาดไว้ตอนนี้</summary>
        public IReadOnlyList<Vector2[]> Strokes => strokes;

        /// <summary>วาดครบเงื่อนไขแล้วหรือยัง (มีเส้นและตัวใหญ่พอ)</summary>
        public bool IsValid => strokes.Count > 0 && PlayerProfile.IsBigEnough(strokes);

        private void Start()
        {
            // โหลดตัวละครที่เคยวาดไว้ขึ้นมา จะได้ไม่ต้องวาดใหม่ทุกครั้งที่เปิดเกม
            strokes.Clear();
            strokes.AddRange(PlayerProfile.LoadAppearance());
            Redraw();
        }

        private void OnDestroy()
        {
            if (previewTexture != null) Destroy(previewTexture);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || drawArea == null) return;

            Vector2 screen = mouse.position.ReadValue();
            bool inside = TryGetNormalized(screen, out Vector2 normalized);

            if (mouse.leftButton.wasPressedThisFrame && inside)
            {
                BeginStroke(normalized);
            }
            else if (mouse.leftButton.isPressed && isDrawing)
            {
                // ลากออกนอกกรอบแล้วยังลากต่อได้ แค่หนีบพิกัดไว้ในกรอบ
                // ถ้าตัดจบทันทีที่ออกนอกกรอบ จะวาดขอบ ๆ ยากมาก
                AppendPoint(new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y)));
            }
            else if (mouse.leftButton.wasReleasedThisFrame && isDrawing)
            {
                EndStroke();
            }
        }

        private bool TryGetNormalized(Vector2 screenPoint, out Vector2 normalized)
        {
            normalized = default;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    drawArea, screenPoint, null, out Vector2 local))
            {
                return false;
            }

            Rect rect = drawArea.rect;
            normalized = new Vector2(
                (local.x - rect.xMin) / rect.width,
                (local.y - rect.yMin) / rect.height);

            return normalized.x >= 0f && normalized.x <= 1f
                && normalized.y >= 0f && normalized.y <= 1f;
        }

        private void BeginStroke(Vector2 point)
        {
            if (strokes.Count >= PlayerProfile.MaxStrokes)
            {
                if (sizeHint != null)
                    sizeHint.text = $"วาดได้มากสุด {PlayerProfile.MaxStrokes} เส้น กดลบเส้นล่าสุดก่อน";
                return;
            }

            isDrawing = true;
            currentStroke.Clear();
            currentStroke.Add(point);
        }

        private void AppendPoint(Vector2 point)
        {
            if (currentStroke.Count >= PlayerProfile.MaxPointsPerStroke) return;
            if (Vector2.Distance(currentStroke[currentStroke.Count - 1], point) < minPointDistance) return;

            currentStroke.Add(point);
            Redraw();
        }

        private void EndStroke()
        {
            isDrawing = false;

            if (currentStroke.Count >= 2) strokes.Add(currentStroke.ToArray());
            currentStroke.Clear();

            Redraw();
        }

        // ---------- ปุ่ม ----------

        public void UndoLastStroke()
        {
            if (strokes.Count == 0) return;

            strokes.RemoveAt(strokes.Count - 1);
            Redraw();
        }

        public void ClearAll()
        {
            strokes.Clear();
            currentStroke.Clear();
            Redraw();
        }

        /// <summary>บันทึกลงเครื่อง คืน false ถ้ายังไม่ผ่านเงื่อนไขขนาด</summary>
        public bool Save()
        {
            if (!IsValid) return false;

            PlayerProfile.SaveAppearance(strokes);
            return true;
        }

        // ---------- แสดงผล ----------

        private void Redraw()
        {
            var all = new List<Vector2[]>(strokes);
            if (currentStroke.Count >= 2) all.Add(currentStroke.ToArray());

            if (previewTexture != null) Destroy(previewTexture);
            previewTexture = AppearanceRenderer.BakeTexture(all, inkColor);

            if (preview != null) preview.texture = previewTexture;

            UpdateSizeHint(all);
        }

        /// <summary>
        /// บอกความคืบหน้าของขนาดเป็นเปอร์เซ็นต์ ไม่ใช่แค่ผ่าน/ไม่ผ่าน
        /// ผู้เล่นจะได้รู้ว่าต้องวาดใหญ่ขึ้นอีกเท่าไร ไม่ใช่เดาไปเรื่อย ๆ
        /// </summary>
        private void UpdateSizeHint(List<Vector2[]> all)
        {
            if (sizeHint == null) return;

            if (all.Count == 0)
            {
                sizeHint.text = "ลากเมาส์ในกรอบเพื่อวาดตัวละคร";
                sizeHint.color = new Color(0.75f, 0.78f, 0.85f);
                return;
            }

            float size = PlayerProfile.MeasureSize(all);
            float required = PlayerProfile.MinimumSizeRatio;

            if (size >= required)
            {
                sizeHint.text = "ขนาดผ่านแล้ว";
                sizeHint.color = new Color(0.45f, 0.9f, 0.5f);
            }
            else
            {
                sizeHint.text = $"ตัวเล็กไป — วาดให้ใหญ่ขึ้น ({size / required:P0})";
                sizeHint.color = new Color(1f, 0.55f, 0.4f);
            }
        }
    }
}
