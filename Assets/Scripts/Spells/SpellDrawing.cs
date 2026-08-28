using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MagicDrawing
{
    /// <summary>
    /// รับการวาดเวท แล้วพาผู้เล่นผ่าน 3 ขั้นก่อนเวทจะออก
    ///
    ///   ว่าง  --ลากเมาส์-->  กำลังเขียน  --Space-->  กำลังเล็ง  --Space-->  ยิง
    ///                            ^                        |
    ///                            +---------Esc------------+
    ///
    /// ทำไมต้องสองจังหวะ:
    /// - จังหวะแรกยืนยันว่า "เขียนคาถาเสร็จแล้ว" ผู้เล่นจึงขีดได้หลายขีดโดยไม่ยิงออกไปก่อน
    ///   ซึ่งจำเป็นสำหรับเวทลมที่ต้องขีดหลายขีด
    /// - จังหวะที่สองยืนยัน "ทิศที่จะยิง" เล็งด้วยการเลื่อนเมาส์
    ///
    /// ระหว่างเขียนและเล็ง ตัวละครจะเดินไม่ได้ ทำให้การร่ายเวทมีต้นทุน
    /// ต้องเลือกจังหวะ ไม่ใช่ร่ายไปวิ่งไป
    ///
    /// ทำงานเฉพาะเครื่องเจ้าของตัวละคร ยังไม่ส่งอะไรข้ามเน็ตจนกว่าจะยิงจริง
    /// </summary>
    [RequireComponent(typeof(SpellCaster))]
    public class SpellDrawing : MonoBehaviour
    {
        private enum CastPhase
        {
            Idle,       // ยังไม่ได้เขียนอะไร เดินได้
            Composing,  // กำลังเขียนคาถา เดินไม่ได้
            Aiming,     // คาถาผ่านแล้ว กำลังเลือกทิศ เดินไม่ได้
        }

        [Header("กล้อง")]
        [Tooltip("กล้องที่ใช้แปลงพิกัดจอเป็นพิกัดโลก ปล่อยว่าง = ใช้ Camera.main")]
        [SerializeField] private Camera drawCamera;

        [Header("การเก็บจุด")]
        [Tooltip("ระยะห่างขั้นต่ำระหว่างจุด (หน่วยโลก) ยิ่งมากยิ่งประหยัดข้อมูล แต่เส้นจะหยาบ")]
        [SerializeField] private float minPointDistance = 0.12f;

        [Tooltip("เพดานจำนวนจุดต่อหนึ่งขีด กันคนลากวนไม่เลิก")]
        [SerializeField] private int maxPointsPerStroke = 512;

        [Tooltip("ขีดที่สั้นกว่านี้ถือว่าเผลอจิ้ม ทิ้งไปเลย")]
        [SerializeField] private float minStrokeLength = 0.3f;

        [Tooltip("เขียนได้มากสุดกี่ขีดต่อหนึ่งคาถา")]
        [SerializeField] private int maxStrokes = 12;

        [Header("กฎการตัดสินธาตุ")]
        [Tooltip("ต้องขีดตรง ๆ กี่ขีดถึงจะได้เวทลม")]
        [SerializeField] private int windMinStrokes = 4;

        [Tooltip("เกณฑ์ความแม่นยำของรูปทรง ตั้งต่ำไว้เพื่อให้เห็นเค้าโครงก็นับ")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumScore = 0.55f;

        [Tooltip("ระยะผ่อนผันของการ \"วาดทับตัวเอง\" ยิ่งมากยิ่งกลายเป็นโล่ง่าย "
                 + "ถ้ารู้สึกว่ายิงใกล้ ๆ ตัวแล้วกลายเป็นโล่บ่อย ให้ลดค่านี้ลง")]
        [SerializeField] private float shieldMargin = 0.4f;

        [Header("ปุ่ม")]
        [SerializeField] private Key confirmKey = Key.Space;
        [SerializeField] private Key cancelKey = Key.Escape;

        [Header("การแสดงผล")]
        [SerializeField] private float strokeWidth = 0.1f;
        [SerializeField] private float aimArrowLength = 2.5f;
        [SerializeField] private bool showOnScreenHint = true;

        [Tooltip("พิมพ์ผลการตรวจลง Console เอาไว้จูนค่า")]
        [SerializeField] private bool logRecognition = true;

        [Header("ส่งเส้นให้อีกฝ่ายเห็นสด ๆ")]
        [Tooltip("ส่งทุกกี่วินาที ยิ่งถี่ยิ่งลื่นแต่กินแบนด์วิดท์มากขึ้น")]
        [SerializeField] private float liveSyncInterval = 0.1f;

        private float nextSyncTime;

        private SpellCaster caster;
        private NetworkPlayer2D player;
        private SpellPower voicePower;

        private CastPhase phase = CastPhase.Idle;

        private readonly List<Vector2[]> strokes = new List<Vector2[]>();
        private readonly List<Vector2> currentStroke = new List<Vector2>();
        private readonly List<LineRenderer> strokeLines = new List<LineRenderer>();

        private LineRenderer activeLine;
        private LineRenderer aimLine;
        private bool isPressing;

        private SpriteRenderer drawSpark;
        private static Sprite sparkSprite;


        /// <summary>
        /// กำลังเขียนคาถาหรือเล็งอยู่หรือเปล่า
        /// ระบบเสียงพูดใช้ค่านี้ตัดสินว่าจะเปิดไมค์ส่งให้เพื่อนฟังตอนไหน
        /// </summary>
        public bool IsCasting => phase != CastPhase.Idle;

        private SpellCastResult pendingSpell;
        private Vector2 aimDirection = Vector2.right;
        private string statusMessage = "";

        private void Awake()
        {
            caster = GetComponent<SpellCaster>();
            player = GetComponent<NetworkPlayer2D>();
            voicePower = GetComponent<SpellPower>();
            if (drawCamera == null) drawCamera = Camera.main;
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

            HandleCancel();

            if (phase == CastPhase.Aiming) UpdateAiming();
            else UpdateDrawing();

            UpdateMovementLock();
        }

        // ---------- ขั้นเขียนคาถา ----------

        private void UpdateDrawing()
        {
            if (TryGetPointer(out Vector2 screen, out bool pressedNow, out bool held, out bool releasedNow))
            {
                if (pressedNow && !IsPointerOverUI())
                {
                    BeginStroke(screen);
                }
                else if (held && isPressing)
                {
                    AppendPoint(screen);
                }
                else if (releasedNow && isPressing)
                {
                    EndStroke();
                }
            }
            else if (isPressing)
            {
                // อุปกรณ์หลุดกลางคัน เช่น ถอดเมาส์ ให้จบขีดไปเลยไม่ให้ค้าง
                EndStroke();
            }

            if (phase == CastPhase.Composing && !isPressing && WasConfirmPressed())
                ConfirmSpell();
        }

        private void BeginStroke(Vector2 screen)
        {
            if (strokes.Count >= maxStrokes)
            {
                statusMessage = $"เขียนได้มากสุด {maxStrokes} ขีด กด Space ยืนยันหรือ Esc ล้าง";
                return;
            }

            // เริ่มจับความดังตั้งแต่ขีดแรก ผู้เล่นจึงตะโกนตอนไหนของการวาดก็ได้
            if (phase == CastPhase.Idle && voicePower != null) voicePower.StartCapture();

            isPressing = true;
            phase = CastPhase.Composing;

            currentStroke.Clear();
            currentStroke.Add(ScreenToWorld(screen));

            activeLine = CreateLine(Color.white, strokeWidth);
            strokeLines.Add(activeLine);
            RedrawActiveLine();

            SpellAudio.Play(SpellSound.StrokeStart, transform.position);
        }

        private void AppendPoint(Vector2 screen)
        {
            if (currentStroke.Count >= maxPointsPerStroke) return;

            Vector2 world = ScreenToWorld(screen);

            // เก็บเฉพาะจุดที่ห่างจากจุดก่อนหน้าพอสมควร
            // ถ้าเก็บทุกเฟรมจะได้จุดเป็นพันตอนลากช้า ๆ เปลืองทั้งแรมและแบนด์วิดท์
            if (Vector2.Distance(currentStroke[currentStroke.Count - 1], world) < minPointDistance) return;

            currentStroke.Add(world);
            RedrawActiveLine();

            // ส่งให้อีกฝ่ายเห็นเป็นระยะ ไม่ส่งทุกจุดที่เพิ่ม
            // ถ้าส่งทุกจุดจะเป็นสิบครั้งต่อวินาทีต่อคน กินแบนด์วิดท์เกินจำเป็น
            // ทั้งที่ตาคนแยกความต่างระดับนั้นไม่ออกอยู่แล้ว
            if (Time.time >= nextSyncTime)
            {
                nextSyncTime = Time.time + liveSyncInterval;
                caster.SyncStroke(strokes.Count, currentStroke.ToArray());
            }
        }

        private void EndStroke()
        {
            isPressing = false;
            HideSpark();

            Vector2[] stroke = currentStroke.ToArray();
            currentStroke.Clear();

            if (stroke.Length < 2 || StrokeLength(stroke) < minStrokeLength)
            {
                // ขีดสั้นเกินไป เก็บเส้นที่วาดไว้ทิ้งด้วย ไม่ให้ค้างเป็นจุดเล็ก ๆ บนจอ
                RemoveLine(activeLine);
                activeLine = null;

                if (strokes.Count == 0) ResetToIdle();
                return;
            }

            // ส่งครั้งสุดท้ายให้ครบเส้น ไม่งั้นอีกฝ่ายจะเห็นขีดขาดตรงปลาย
            // เพราะการส่งเป็นระยะอาจตัดจบก่อนถึงจุดสุดท้าย
            caster.SyncStroke(strokes.Count, stroke);

            strokes.Add(stroke);
            activeLine = null;
            statusMessage = $"เขียนแล้ว {strokes.Count} ขีด — กด Space ยืนยัน";

            SpellAudio.Play(SpellSound.StrokeEnd, transform.position);
        }

        /// <summary>จังหวะยืนยันที่ 1: ปิดคาถาแล้วตัดสินว่าได้ธาตุอะไร</summary>
        private void ConfirmSpell()
        {
            SpellCastResult result = SpellRecognizer.Evaluate(strokes, windMinStrokes, minimumScore);
            pendingSpell = result;

            if (logRecognition)
            {
                Debug.Log($"[SpellDrawing] {strokes.Count} ขีด | รูปทรง {result.ShapeName ?? "ไม่รู้จัก"} "
                          + $"| แม่นยำ {result.Score:P0} | "
                          + (result.Success ? $"ได้เวท{result.Element.ToThai()}" : $"ล้มเหลว: {result.FailReason}"));
            }

            if (!result.Success)
            {
                statusMessage = "ร่ายไม่ติด: " + result.FailReason;
                SpellAudio.Play(SpellSound.Reject, transform.position);
                ClearStrokes();
                ResetToIdle();
                return;
            }

            SpellAudio.Play(SpellSound.Confirm, transform.position, result.Element);

            // วาดทับตัวเอง = ขอโล่ ไม่ใช่ขอยิง
            // ข้ามขั้นเล็งไปเลยเพราะโล่ไม่มีทิศ ตำแหน่งที่วาดคือคำสั่งอยู่แล้ว
            if (IsDrawnOverSelf())
            {
                caster.RequestShield(result.Element);

                if (logRecognition)
                    Debug.Log($"[SpellDrawing] วาดทับตัวเอง -> กางโล่{result.Element.ToThai()}");

                statusMessage = $"กางโล่{result.Element.ToThai()}";
                ClearStrokes();
                ResetToIdle();
                return;
            }

            phase = CastPhase.Aiming;
            aimDirection = DefaultAimDirection();
            TintStrokes(result.Element.ToColor());
            statusMessage = $"เวท{result.Element.ToThai()}พร้อม — เลื่อนเมาส์เล็ง แล้วกด Space ยิง";
        }

        /// <summary>
        /// ตัวละครอยู่ในกรอบของสิ่งที่วาดไหม
        ///
        /// ใช้กรอบสี่เหลี่ยมที่ครอบทุกขีดรวมกัน แทนการวัดระยะจากจุดกึ่งกลาง
        /// เพราะผู้เล่นอาจวาดวงใหญ่ครอบตัวแบบไม่ได้อยู่กึ่งกลางพอดี
        /// หรือขีดหลายขีดพาดผ่านตัว ซึ่งทั้งสองแบบก็ควรนับว่า "ทับตัวเอง"
        /// </summary>
        private bool IsDrawnOverSelf()
        {
            if (strokes.Count == 0) return false;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (Vector2[] stroke in strokes)
            {
                foreach (Vector2 point in stroke)
                {
                    if (point.x < minX) minX = point.x;
                    if (point.x > maxX) maxX = point.x;
                    if (point.y < minY) minY = point.y;
                    if (point.y > maxY) maxY = point.y;
                }
            }

            Vector2 self = transform.position;

            return self.x >= minX - shieldMargin && self.x <= maxX + shieldMargin
                && self.y >= minY - shieldMargin && self.y <= maxY + shieldMargin;
        }

        // ---------- ขั้นเล็งทิศ ----------

        private void UpdateAiming()
        {
            if (TryGetPointer(out Vector2 screen, out bool pressedNow, out _, out _))
            {
                Vector2 world = ScreenToWorld(screen);
                Vector2 toPointer = world - (Vector2)transform.position;

                // เมาส์ทับตัวละครพอดีจะหาทิศไม่ได้ คงทิศเดิมไว้
                if (toPointer.sqrMagnitude > 0.0001f) aimDirection = toPointer.normalized;

                if (pressedNow && !IsPointerOverUI())
                {
                    FireSpell();
                    return;
                }
            }

            DrawAimArrow();

            if (WasConfirmPressed()) FireSpell();
        }

        /// <summary>จังหวะยืนยันที่ 2: ยิงจริง ตรงนี้เท่านั้นที่ข้อมูลถูกส่งข้ามเน็ต</summary>
        private void FireSpell()
        {
            Vector2[] main = LongestStroke();
            if (main != null)
                caster.RequestCast(main, pendingSpell.Element, aimDirection);

            if (logRecognition)
            {
                Debug.Log($"[SpellDrawing] ยิงเวท{pendingSpell.Element.ToThai()} "
                          + $"ทิศ ({aimDirection.x:F2}, {aimDirection.y:F2})");
            }

            statusMessage = $"ยิงเวท{pendingSpell.Element.ToThai()}!";
            ClearStrokes();
            ResetToIdle();
        }

        private Vector2 DefaultAimDirection()
        {
            // เริ่มจากทิศที่ตัวละครหันอยู่ ผู้เล่นจึงยิงตรงหน้าได้โดยไม่ต้องขยับเมาส์
            float facing = player != null ? player.Facing : 1f;
            return new Vector2(facing, 0f);
        }

        // ---------- สถานะและการล้าง ----------

        private void HandleCancel()
        {
            if (phase == CastPhase.Idle) return;

            if (WasCancelPressed())
            {
                statusMessage = "ยกเลิกแล้ว";
                ClearStrokes();
                ResetToIdle();
                return;
            }

            // กดปุ่มเดินระหว่างร่าย = เปลี่ยนใจ ขอเดินแทน
            //
            // ข้อนี้สำคัญกว่าที่คิด ถ้าไม่มี ผู้เล่นที่เขียนคาถาแล้วไม่รู้ว่าต้องกด
            // Space ยืนยัน จะติดอยู่ในสถานะที่เดินไม่ได้ถาวร แล้วดูเหมือนเกมค้าง
            // ทั้งที่จริงแค่รอปุ่มอยู่ ยอมให้เสียคาถาที่เขียนไว้ดีกว่าขยับไม่ได้เลย
            if (WasMoveKeyPressed())
            {
                statusMessage = "ยกเลิกคาถาเพื่อเดิน";
                ClearStrokes();
                ResetToIdle();
            }
        }

        private bool WasMoveKeyPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            return keyboard.aKey.wasPressedThisFrame
                || keyboard.dKey.wasPressedThisFrame
                || keyboard.leftArrowKey.wasPressedThisFrame
                || keyboard.rightArrowKey.wasPressedThisFrame;
        }

        private void ResetToIdle()
        {
            phase = CastPhase.Idle;
            isPressing = false;
            currentStroke.Clear();
            activeLine = null;
            HideAimArrow();
            HideSpark();

            // หยุดจับความดัง แต่ค่าที่จับได้ยังอ่านได้ เพราะ SpellCaster อ่านทีหลัง
            if (voicePower != null) voicePower.StopCapture();
        }

        /// <summary>
        /// ล็อกการเดินตลอดตั้งแต่เริ่มเขียนจนยิงเสร็จ
        /// ตั้งทุกเฟรมเพราะสถานะอาจเปลี่ยนกลางเฟรม และตัวละครอาจเกิดทีหลัง
        /// </summary>
        private void UpdateMovementLock()
        {
            if (player != null) player.MovementLocked = phase != CastPhase.Idle;
        }

        private void OnDestroy()
        {
            // สร้างเองก็ต้องเก็บเอง ไม่งั้นเปลี่ยนซีนแล้วจุดค้างอยู่กลางจอ
            if (drawSpark != null) Destroy(drawSpark.gameObject);
        }

        private void OnDisable()
        {
            // ตายหรือถูกปิดกลางคันแล้วปล่อยล็อกค้างไว้ = เดินไม่ได้ตลอดกาล
            if (player != null) player.MovementLocked = false;
            ClearStrokes();
            ResetToIdle();
        }

        // ---------- อ่านอินพุต ----------

        private bool WasConfirmPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[confirmKey].wasPressedThisFrame;
        }

        private bool WasCancelPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[cancelKey].wasPressedThisFrame) return true;

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
        }

        /// <summary>อ่านนิ้วก่อน ถ้าไม่มีค่อยใช้เมาส์ คืน false เมื่อไม่มีอุปกรณ์ชี้เลย</summary>
        private bool TryGetPointer(out Vector2 screenPosition, out bool pressedNow, out bool held, out bool releasedNow)
        {
            screenPosition = default;
            pressedNow = held = releasedNow = false;

            Touchscreen touch = Touchscreen.current;
            if (touch != null)
            {
                var press = touch.primaryTouch.press;

                // ใช้นิ้วเฉพาะตอนแตะอยู่จริงหรือเพิ่งปล่อย ถ้าไม่มีนิ้วบนจอต้องตกไปใช้เมาส์
                // ไม่งั้นเครื่องที่มีทั้งจอสัมผัสและเมาส์จะใช้เมาส์ไม่ได้เลย
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

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            // เกม 2D วาดกันที่ระนาบ z = 0 จึงต้องบอกกล้องว่าห่างจากตัวมันเท่าไร
            float depth = Mathf.Abs(drawCamera.transform.position.z);
            Vector3 world = drawCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, depth));
            return new Vector2(world.x, world.y);
        }

        // ---------- เส้นที่เห็นบนจอ ----------

        private LineRenderer CreateLine(Color color, float width)
        {
            var holder = new GameObject("SpellDrawStroke");
            var line = holder.AddComponent<LineRenderer>();

            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = SharedMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 600;

            return line;
        }

        private static Material sharedMaterial;

        private static Material SharedMaterial
        {
            get
            {
                // ใช้วัสดุร่วมกันทุกเส้น ไม่งั้นเขียนหลายขีดจะสร้างวัสดุใหม่ทิ้งไว้เรื่อย ๆ
                if (sharedMaterial == null)
                    sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                return sharedMaterial;
            }
        }

        private void RedrawActiveLine()
        {
            if (activeLine == null) return;

            activeLine.positionCount = currentStroke.Count;
            for (int i = 0; i < currentStroke.Count; i++)
                activeLine.SetPosition(i, new Vector3(currentStroke[i].x, currentStroke[i].y, 0f));

            if (currentStroke.Count > 0)
                ShowSpark(currentStroke[currentStroke.Count - 1]);
        }

        // ---------- ประกายไฟที่ปลายปากกา ----------

        /// <summary>
        /// จุดเรืองแสงวิ่งตามปลายเส้นตอนกำลังลาก
        ///
        /// ทำให้รู้สึกว่ากำลัง "เขียนเวท" ไม่ใช่แค่ลากเส้นในโปรแกรมวาดรูป
        /// และช่วยให้ตาจับตำแหน่งปัจจุบันได้ง่ายตอนลากเร็ว ๆ
        /// </summary>
        private void ShowSpark(Vector2 position)
        {
            if (drawSpark == null)
            {
                var holder = new GameObject("SpellDrawSpark");
                drawSpark = holder.AddComponent<SpriteRenderer>();
                drawSpark.sprite = SparkSprite;
                drawSpark.material = SharedMaterial;
                drawSpark.sortingOrder = 650;
            }

            drawSpark.enabled = true;
            drawSpark.transform.position = new Vector3(position.x, position.y, 0f);

            // เต้นเบา ๆ ให้ดูมีชีวิต ไม่ใช่จุดนิ่ง ๆ
            float pulse = 0.85f + Mathf.Sin(Time.time * 14f) * 0.15f;
            drawSpark.transform.localScale = Vector3.one * pulse;
        }

        private void HideSpark()
        {
            if (drawSpark != null) drawSpark.enabled = false;
        }

        /// <summary>จุดนุ่ม ๆ สว่างตรงกลางจางที่ขอบ สร้างครั้งเดียวใช้ร่วมกันทุกตัว</summary>
        private static Sprite SparkSprite
        {
            get
            {
                if (sparkSprite != null) return sparkSprite;

                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                float center = (size - 1) * 0.5f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float radius = Mathf.Sqrt(dx * dx + dy * dy) / center;
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2.2f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                // หารสี่ทำให้จุดกว้างประมาณ 0.25 หน่วย พอดีกับความหนาของเส้น
                sparkSprite = Sprite.Create(
                    texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size * 4f);

                return sparkSprite;
            }
        }

        private void TintStrokes(Color color)
        {
            foreach (LineRenderer line in strokeLines)
            {
                if (line == null) continue;
                line.startColor = color;
                line.endColor = color;
            }
        }

        private void RemoveLine(LineRenderer line)
        {
            if (line == null) return;
            strokeLines.Remove(line);
            Destroy(line.gameObject);
        }

        private void ClearStrokes()
        {
            foreach (LineRenderer line in strokeLines)
                if (line != null) Destroy(line.gameObject);

            strokeLines.Clear();
            strokes.Clear();
            currentStroke.Clear();
            activeLine = null;

            // ล้างของที่ค้างบนจอคนอื่นด้วย ไม่งั้นเส้นที่เรายกเลิกไปแล้ว
            // จะยังลอยอยู่บนจอคู่ต่อสู้ตลอดเกม
            if (caster != null) caster.ClearSyncedStrokes();
        }

        private void DrawAimArrow()
        {
            if (aimLine == null)
                aimLine = CreateLine(pendingSpell.Element.ToColor(), strokeWidth * 1.4f);

            Vector3 from = transform.position;
            Vector3 to = from + (Vector3)(aimDirection * aimArrowLength);

            aimLine.positionCount = 2;
            aimLine.SetPosition(0, new Vector3(from.x, from.y, 0f));
            aimLine.SetPosition(1, new Vector3(to.x, to.y, 0f));
        }

        private void HideAimArrow()
        {
            if (aimLine == null) return;
            Destroy(aimLine.gameObject);
            aimLine = null;
        }

        private Vector2[] LongestStroke()
        {
            Vector2[] best = null;
            float bestLength = -1f;

            foreach (Vector2[] stroke in strokes)
            {
                float length = StrokeLength(stroke);
                if (length > bestLength)
                {
                    bestLength = length;
                    best = stroke;
                }
            }
            return best;
        }

        private static float StrokeLength(Vector2[] stroke)
        {
            if (stroke == null || stroke.Length < 2) return 0f;

            float total = 0f;
            for (int i = 1; i < stroke.Length; i++)
                total += Vector2.Distance(stroke[i - 1], stroke[i]);
            return total;
        }

        // ---------- คำใบ้บนจอ ----------

        private void OnGUI()
        {
            if (!showOnScreenHint) return;
            if (caster == null || !caster.IsOwner) return;

            string hint;
            switch (phase)
            {
                case CastPhase.Composing:
                    hint = $"เขียนคาถา {strokes.Count} ขีด  |  Space = ยืนยัน  |  Esc = ล้าง";
                    break;
                case CastPhase.Aiming:
                    hint = $"เวท{pendingSpell.Element.ToThai()}  |  เลื่อนเมาส์เล็ง  |  Space หรือคลิก = ยิง  |  Esc = ยกเลิก";
                    break;
                default:
                    hint = "A/D เดิน  |  ลากเมาส์เขียนคาถา  |  วงกลม=น้ำ สามเหลี่ยม=ไฟ สี่เหลี่ยม=ดิน ขีด 4 ขีด=ลม"
                           + "\nวาดข้าง ๆ ตัว = ยิงออกไป  |  วาดทับตัวเอง = กางโล่ธาตุนั้น";
                    break;
            }

            var area = new Rect(10, Screen.height - 70, Screen.width - 20, 60);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(hint);
            if (!string.IsNullOrEmpty(statusMessage)) GUILayout.Label(statusMessage);
            GUILayout.EndArea();

            // ระหว่างที่เดินไม่ได้ ต้องบอกให้ชัดว่าเกมรออะไรอยู่และออกยังไง
            // แถบล่างอย่างเดียวมองข้ามได้ง่าย โดยเฉพาะตอน Game view เล็ก
            if (phase == CastPhase.Idle) return;

            string banner = phase == CastPhase.Aiming
                ? $"เล็งด้วยเมาส์ แล้วกด Space หรือคลิกเพื่อยิงเวท{pendingSpell.Element.ToThai()}"
                : "กด Space เพื่อยืนยันคาถา";

            var bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };

            var bannerRect = new Rect(Screen.width * 0.5f - 260f, 90f, 520f, 64f);
            GUI.Box(bannerRect, banner + "\n(กด A หรือ D เพื่อยกเลิกแล้วเดินต่อ)", bannerStyle);
        }
    }
}
