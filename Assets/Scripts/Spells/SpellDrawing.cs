using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MagicDrawing
{
    /// <summary>
    /// รับการวาดเส้นจากเมาส์หรือนิ้ว แล้วส่งต่อให้ SpellCaster ร่ายเวท
    ///
    /// ทำงานเฉพาะเครื่องของเจ้าของตัวละครเท่านั้น เส้นที่เห็นระหว่างลากเป็นแค่
    /// ภาพชั่วคราวในเครื่องเรา ยังไม่ส่งข้ามเน็ต จะส่งตอนปล่อยมือทีเดียว
    /// ตามข้อกำหนดข้อ 3.4 (วาดเสร็จแล้วค่อยส่ง)
    ///
    /// โปรเจกต์นี้ตั้ง Active Input Handling เป็น Input System แบบใหม่
    /// จึงอ่านค่าจาก Mouse.current / Touchscreen.current ไม่ใช่ Input.mousePosition
    /// </summary>
    [RequireComponent(typeof(SpellCaster))]
    public class SpellDrawing : MonoBehaviour
    {
        [Header("เส้นที่เห็นระหว่างวาด")]
        [Tooltip("LineRenderer สำหรับพรีวิว ปล่อยว่างได้ เดี๋ยวสร้างให้เอง")]
        [SerializeField] private LineRenderer previewLine;

        [Tooltip("กล้องที่ใช้แปลงพิกัดจอเป็นพิกัดโลก ปล่อยว่าง = ใช้ Camera.main")]
        [SerializeField] private Camera drawCamera;

        [Header("การเก็บจุด")]
        [Tooltip("ระยะห่างขั้นต่ำระหว่างจุด (หน่วยโลก) ยิ่งมากยิ่งประหยัดข้อมูล แต่เส้นจะหยาบ")]
        [SerializeField] private float minPointDistance = 0.12f;

        [Tooltip("เพดานจำนวนจุดกันคนลากวนไม่เลิกจนกินหน่วยความจำ")]
        [SerializeField] private int maxPoints = 512;

        [Tooltip("เส้นสั้นกว่านี้ถือว่าเผลอจิ้ม ไม่นับเป็นการร่ายเวท")]
        [SerializeField] private float minStrokeLength = 0.5f;

        [Header("การตัดสินธาตุ")]
        [Tooltip("ความแม่นยำขั้นต่ำที่จะนับว่าเป็นรูปทรงนั้นจริง ต่ำกว่านี้เป็นเวทลม")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumScore = 0.70f;

        [Tooltip("พิมพ์ผลการตรวจรูปทรงลง Console เอาไว้จูนค่าความแม่นยำ")]
        [SerializeField] private bool logRecognition = true;

        private SpellCaster caster;
        private readonly List<Vector2> points = new List<Vector2>();
        private bool isDrawing;

        private void Awake()
        {
            caster = GetComponent<SpellCaster>();
            if (drawCamera == null) drawCamera = Camera.main;
            EnsurePreviewLine();
        }

        private void Update()
        {
            // ตัวละครของคนอื่นไม่รับปุ่ม ไม่งั้นวาดทีเดียวร่ายพร้อมกันทุกตัว
            if (caster == null || !caster.IsOwner) return;

            if (drawCamera == null)
            {
                drawCamera = Camera.main;
                if (drawCamera == null) return;
            }

            if (TryGetPointer(out Vector2 screenPosition, out bool pressedNow, out bool held, out bool releasedNow))
            {
                if (pressedNow && !IsPointerOverUI())
                {
                    BeginStroke(screenPosition);
                }
                else if (held && isDrawing)
                {
                    AppendPoint(screenPosition);
                }
                else if (releasedNow && isDrawing)
                {
                    EndStroke();
                }
            }
            else if (isDrawing)
            {
                // อุปกรณ์หลุดกลางคัน เช่น ถอดเมาส์ ให้จบเส้นไปเลยไม่ให้ค้าง
                EndStroke();
            }
        }

        /// <summary>
        /// อ่านตำแหน่งและสถานะการกดจากนิ้วก่อน ถ้าไม่มีค่อยใช้เมาส์
        /// คืน false เมื่อไม่มีอุปกรณ์ชี้ตำแหน่งเลย
        /// </summary>
        private bool TryGetPointer(out Vector2 screenPosition, out bool pressedNow, out bool held, out bool releasedNow)
        {
            screenPosition = default;
            pressedNow = held = releasedNow = false;

            Touchscreen touch = Touchscreen.current;
            if (touch != null)
            {
                var press = touch.primaryTouch.press;

                // ใช้นิ้วเฉพาะตอนที่แตะอยู่จริงหรือเพิ่งปล่อย ถ้าไม่มีนิ้วบนจอ
                // ต้องตกไปใช้เมาส์ ไม่งั้นเครื่องที่มีทั้งจอสัมผัสและเมาส์จะใช้เมาส์ไม่ได้เลย
                if (press.isPressed || press.wasReleasedThisFrame)
                {
                    screenPosition = touch.primaryTouch.position.ReadValue();
                    pressedNow = press.wasPressedThisFrame;
                    held = press.isPressed;
                    releasedNow = press.wasReleasedThisFrame;
                    return true;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                pressedNow = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
                releasedNow = mouse.leftButton.wasReleasedThisFrame;
                return true;
            }

            return false;
        }

        /// <summary>กันไม่ให้การกดปุ่มบนหน้าจอ UI กลายเป็นการเริ่มวาดเวท</summary>
        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void BeginStroke(Vector2 screenPosition)
        {
            isDrawing = true;
            points.Clear();
            points.Add(ScreenToWorld(screenPosition));
            RedrawPreview();
        }

        private void AppendPoint(Vector2 screenPosition)
        {
            if (points.Count >= maxPoints) return;

            Vector2 world = ScreenToWorld(screenPosition);

            // ข้อกำหนด 3.1: เก็บเฉพาะจุดที่ห่างจากจุดก่อนหน้าพอสมควร
            // ถ้าเก็บทุกเฟรมจะได้จุดเป็นพันตอนลากช้า ๆ เปลืองทั้งแรมและแบนด์วิดท์
            if (Vector2.Distance(points[points.Count - 1], world) < minPointDistance) return;

            points.Add(world);
            RedrawPreview();
        }

        private void EndStroke()
        {
            isDrawing = false;

            Vector2[] stroke = points.ToArray();
            points.Clear();
            ClearPreview();

            if (stroke.Length < 2 || StrokeLength(stroke) < minStrokeLength) return;

            RecognitionResult result = DollarOneRecognizer.Recognize(stroke, SpellShapeLibrary.Templates);
            SpellElement element = SpellShapeLibrary.ToElement(result, minimumScore);

            if (logRecognition)
            {
                string shape = result.HasMatch ? result.Name : "ไม่รู้จัก";
                Debug.Log($"[SpellDrawing] วาดได้ {shape} ความแม่นยำ {result.Score:P0} -> เวท{element.ToThai()}");
            }

            caster.RequestCast(stroke, element);
        }

        private static float StrokeLength(Vector2[] stroke)
        {
            float total = 0f;
            for (int i = 1; i < stroke.Length; i++)
                total += Vector2.Distance(stroke[i - 1], stroke[i]);
            return total;
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            // เกม 2D วาดกันที่ระนาบ z = 0 จึงต้องบอกกล้องว่าห่างจากตัวมันเท่าไร
            float depth = Mathf.Abs(drawCamera.transform.position.z);
            Vector3 world = drawCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, depth));
            return new Vector2(world.x, world.y);
        }

        private void EnsurePreviewLine()
        {
            if (previewLine != null) return;

            var holder = new GameObject("SpellPreviewLine");
            holder.transform.SetParent(null);   // ไม่ผูกกับตัวละคร เส้นจะได้ไม่ขยับตามตอนเดิน

            previewLine = holder.AddComponent<LineRenderer>();
            previewLine.useWorldSpace = true;
            previewLine.positionCount = 0;
            previewLine.widthMultiplier = 0.12f;
            previewLine.numCapVertices = 4;
            previewLine.material = new Material(Shader.Find("Sprites/Default"));
            previewLine.startColor = Color.white;
            previewLine.endColor = Color.white;
            previewLine.sortingOrder = 100;
        }

        private void RedrawPreview()
        {
            if (previewLine == null) return;

            previewLine.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                previewLine.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
        }

        private void ClearPreview()
        {
            if (previewLine != null) previewLine.positionCount = 0;
        }

        private void OnDestroy()
        {
            // สร้างเองก็ต้องเก็บกวาดเอง ไม่งั้นเปลี่ยนฉากแล้วเส้นค้าง
            if (previewLine != null && previewLine.gameObject.name == "SpellPreviewLine")
                Destroy(previewLine.gameObject);
        }
    }
}
