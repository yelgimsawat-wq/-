using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ตัวกลางระหว่างการวาดกับเครือข่าย ทำให้ทุกเครื่องเห็นเวทตรงกัน
    ///
    /// เส้นทางข้อมูล:
    ///   เจ้าของตัวละครยืนยันทิศ -> RequestCast -> ServerRpc -> ClientRpc -> ทุกเครื่องแสดงผล
    ///
    /// จุดที่ต้องระวังเรื่องขนาดข้อมูล: RPC ของ Netcode มีเพดานขนาดข้อความ
    /// ถ้าส่งจุดดิบไปทั้งหมดตอนคนลากยาว ๆ อาจทะลุเพดานจนหลุดทั้งก้อน
    /// เราจึงบีบให้เหลือจำนวนคงที่ก่อนส่งเสมอ (NetworkPointCount)
    /// รูปทรงยังคงเดิมเพราะเป็นการเกลี่ยจุดใหม่ ไม่ใช่ตัดจุดท้ายทิ้ง
    ///
    /// ส่งเฉพาะขีดที่ยาวที่สุดขีดเดียว ไม่ได้ส่งทุกขีด เพราะขีดที่เหลือเป็นแค่
    /// ขั้นตอนการเขียนคาถาของคนร่าย คนอื่นเห็นผลลัพธ์ก็พอ
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class SpellCaster : NetworkBehaviour
    {
        /// <summary>
        /// จำนวนจุดที่ส่งข้ามเน็ตต่อการร่ายหนึ่งครั้ง
        /// 32 จุด = 256 ไบต์ พอให้เห็นรูปทรงชัดและเบามากสำหรับ RPC
        /// </summary>
        public const int NetworkPointCount = 32;

        [Serializable]
        public class ElementVisual
        {
            public SpellElement element;

            [Tooltip("Prefab วงเวทที่จะโผล่ตรงทิศที่เล็ง")]
            public MagicCircle circlePrefab;

            [Tooltip("Prefab ลูกเวทที่พุ่งออกไป ปล่อยว่างได้ถ้ายังไม่มี")]
            public SpellProjectile projectilePrefab;

            [Tooltip("Prefab โล่ธาตุ ปล่อยว่างได้ถ้ายังไม่มี")]
            public SpellShield shieldPrefab;
        }

        [Header("ตำแหน่งที่เวทจะออก")]
        [Tooltip("ระยะห่างจากตัวละครไปตามทิศที่เล็ง")]
        [SerializeField] private float castDistance = 1.2f;

        [Header("ภาพประจำแต่ละธาตุ")]
        [SerializeField] private ElementVisual[] elementVisuals;

        [Tooltip("วงเวทสำรอง ใช้เมื่อธาตุนั้นยังไม่ได้ใส่ของตัวเอง")]
        [SerializeField] private MagicCircle fallbackCirclePrefab;

        [Tooltip("ลูกเวทสำรอง ใช้เมื่อธาตุนั้นยังไม่ได้ใส่ของตัวเอง")]
        [SerializeField] private SpellProjectile fallbackProjectilePrefab;

        [Tooltip("โล่สำรอง ใช้เมื่อธาตุนั้นยังไม่ได้ใส่ของตัวเอง")]
        [SerializeField] private SpellShield fallbackShieldPrefab;

        [Header("ขนาดและอายุของโล่")]
        [Tooltip("ขนาดโล่เทียบกับตัวละคร 1 = เท่าตัว, 2 = สองเท่า "
                 + "ปรับตรงนี้ได้เลยแม้ยังไม่มี Prefab โล่")]
        [SerializeField] private float shieldScale = 1.8f;

        [Tooltip("โล่อยู่กี่วินาทีก่อนจางหาย")]
        [SerializeField] private float shieldDuration = 4f;

        [Header("เส้นที่วาดค้างไว้บนแผนที่")]
        [Tooltip("แสดงเส้นที่ผู้เล่นเขียนให้ทุกคนเห็นด้วย ปิดได้ถ้าอยากเห็นแค่วงเวท")]
        [SerializeField] private bool showDrawnStroke = true;

        [SerializeField] private float strokeLifetime = 1.2f;
        [SerializeField] private float strokeWidth = 0.12f;

        [Header("เส้นที่อีกฝ่ายกำลังวาด")]
        [Tooltip("ให้เห็นแบบเรียลไทม์ว่าคู่ต่อสู้กำลังเขียนคาถาอะไรอยู่")]
        [SerializeField] private bool showRemoteDrawing = true;

        [SerializeField] private float remoteStrokeWidth = 0.09f;

        [Header("ความแรงของเวท")]
        [Tooltip("ดาเมจตอนตะโกนเบาสุด")]
        [SerializeField] private int minDamage = 8;

        [Tooltip("ดาเมจตอนตะโกนดังสุด")]
        [SerializeField] private int maxDamage = 30;

        [Tooltip("ขนาดลูกเวทตอนเบาสุดถึงดังสุด ใช้ให้เห็นความแรงด้วยตา")]
        [SerializeField] private Vector2 projectileScaleRange = new Vector2(0.7f, 1.6f);

        [Header("กันร่ายรัว")]
        [Tooltip("เวลาพักขั้นต่ำระหว่างการร่ายสองครั้ง (วินาที)")]
        [SerializeField] private float castCooldown = 0.35f;

        private float nextAllowedCastTime;

        /// <summary>ยังร่ายไม่ได้เพราะติดคูลดาวน์อยู่</summary>
        public bool IsOnCooldown => Time.time < nextAllowedCastTime;

        /// <summary>
        /// เรียกจากเครื่องเจ้าของตัวละครหลังยืนยันทิศแล้ว
        /// จุดที่ส่งเข้ามาเป็นพิกัดโลก จะถูกบีบให้เหลือจำนวนคงที่ก่อนส่งต่อ
        /// </summary>
        public void RequestCast(Vector2[] worldPoints, SpellElement element, Vector2 direction)
        {
            if (!IsOwner) return;
            if (worldPoints == null || worldPoints.Length < 2) return;
            if (IsOnCooldown) return;

            nextAllowedCastTime = Time.time + castCooldown;

            Vector2[] packed = DollarOneRecognizer.Resample(worldPoints, NetworkPointCount);
            if (packed == null) return;

            // ทิศต้องยาว 1 หน่วยเสมอ ไม่งั้นความเร็วลูกเวทจะเพี้ยนตามระยะเมาส์
            Vector2 aim = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

            CastSpellServerRpc(packed, (byte)element, aim, SampleVoicePower());
        }

        /// <summary>
        /// อ่านความดังเสียงตอนนี้แล้วแปลงเป็น 0-255 เพื่อส่งข้ามเน็ต
        ///
        /// ส่งเป็น byte เพราะความละเอียดระดับ 1/255 มากพอสำหรับความแรงเวท
        /// และกินแค่ไบต์เดียวเทียบกับ float ที่กินสี่
        ///
        /// อ่านค่าที่เครื่องคนร่ายแล้วส่งไป ไม่ใช่ให้ทุกเครื่องอ่านไมค์ตัวเอง
        /// เพราะไมค์ของเราไม่ได้ยินเสียงเพื่อน ค่าจึงต้องเดินทางไปพร้อมคำสั่งร่าย
        /// </summary>
        private byte SampleVoicePower()
        {
            var power = GetComponent<SpellPower>();

            // ใช้ค่าสูงสุดตลอดช่วงที่เขียนคาถา ไม่ใช่ค่า ณ วินาทีที่กดยิง
            // เพราะผู้เล่นตะโกนตอนวาดแล้วมักเงียบไปแล้วตอนกดยืนยัน
            float value = power != null ? power.PeakPower : 0f;
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        /// <summary>
        /// Server รับคำสั่งแล้วกระจายต่อ
        /// ไม่ตรวจซ้ำว่ารูปทรงตรงกับธาตุไหม เพราะให้เครื่องผู้เล่นเป็นคนตัดสิน
        /// ผลคือถ้ามีคนแก้เกมก็ส่งธาตุอะไรมาก็ได้ ยอมรับได้สำหรับเกมเล่นกับเพื่อน
        /// ถ้าวันหนึ่งต้องกันโกง ให้ย้ายการเรียก SpellRecognizer.Evaluate มาทำตรงนี้
        /// </summary>
        [ServerRpc]
        private void CastSpellServerRpc(Vector2[] points, byte elementId, Vector2 direction, byte power)
        {
            if (points == null || points.Length < 2 || points.Length > NetworkPointCount) return;

            CastSpellClientRpc(points, elementId, direction, power);
        }

        /// <summary>ทุกเครื่องรวมทั้งคนร่ายเอง แสดงผลให้ตรงกัน</summary>
        [ClientRpc]
        private void CastSpellClientRpc(Vector2[] points, byte elementId, Vector2 direction, byte power)
        {
            SpellElement element = SpellElementExtensions.FromNetworkId(elementId);
            PlaySpell(points, element, direction, power / 255f);
        }

        private void PlaySpell(Vector2[] points, SpellElement element, Vector2 direction, float power)
        {
            if (showDrawnStroke && points != null && points.Length >= 2)
                SpellStrokeView.Spawn(points, element.ToColor(), strokeWidth, strokeLifetime);

            Vector2 aim = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector3 origin = transform.position + (Vector3)(aim * castDistance);

            // หันวงเวทและลูกเวทไปตามทิศที่เล็ง
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            ElementVisual visual = FindVisual(element);

            MagicCircle circlePrefab = visual != null && visual.circlePrefab != null
                ? visual.circlePrefab
                : fallbackCirclePrefab;

            if (circlePrefab != null)
            {
                MagicCircle circle = Instantiate(circlePrefab, origin, rotation);
                circle.Play(element);
                SpellAudio.Play(SpellSound.Manifest, origin, element);
            }

            SpellProjectile projectilePrefab = visual != null && visual.projectilePrefab != null
                ? visual.projectilePrefab
                : fallbackProjectilePrefab;

            // ไม่มี prefab ก็ยังต้องยิงออกไปได้ ไม่งั้นเวทจะเงียบหายโดยไม่มี error
            SpellProjectile projectile = projectilePrefab != null
                ? Instantiate(projectilePrefab, origin, rotation)
                : CreateFallbackProjectile(origin, rotation);

            // ตะโกนดังขึ้น ลูกใหญ่ขึ้นและเจ็บขึ้น เห็นความแรงได้ด้วยตาไม่ต้องอ่านตัวเลข
            projectile.transform.localScale =
                Vector3.one * Mathf.Lerp(projectileScaleRange.x, projectileScaleRange.y, power);

            int damage = Mathf.RoundToInt(Mathf.Lerp(minDamage, maxDamage, power));

            // ให้เฉพาะ Server เท่านั้นที่คิดดาเมจ เครื่องผู้เล่นสร้างลูกเวทเหมือนกัน
            // แต่เป็นภาพล้วน ๆ ถ้าปล่อยให้ทุกเครื่องคิด ยิงทีเดียวจะโดนหลายครั้ง
            projectile.Launch(aim, element, IsServer, damage, transform);

            SpellAudio.Play(SpellSound.Cast, origin, element);
        }

        /// <summary>
        /// ขอกางโล่ธาตุรอบตัวเอง เรียกจากเครื่องเจ้าของตัวละคร
        /// ไม่ต้องส่งจุดที่วาดไปด้วย เพราะโล่ไม่ได้ใช้รูปทรง ใช้แค่ธาตุ
        /// ประหยัดข้อมูลกว่าการยิงมาก
        /// </summary>
        public void RequestShield(SpellElement element)
        {
            if (!IsOwner) return;
            if (IsOnCooldown) return;

            nextAllowedCastTime = Time.time + castCooldown;
            CastShieldServerRpc((byte)element);
        }

        [ServerRpc]
        private void CastShieldServerRpc(byte elementId)
        {
            CastShieldClientRpc(elementId);
        }

        [ClientRpc]
        private void CastShieldClientRpc(byte elementId)
        {
            PlayShield(SpellElementExtensions.FromNetworkId(elementId));
        }

        private void PlayShield(SpellElement element)
        {
            // กางโล่ใหม่ทับของเดิม ต้องเก็บอันเก่าก่อน ไม่งั้นจะซ้อนกันหลายชั้น
            // แล้วสีของธาตุจะปนกันจนดูไม่ออกว่ากำลังใช้โล่อะไรอยู่
            SpellShield existing = SpellShield.FindActiveOn(transform);
            if (existing != null) Destroy(existing.gameObject);

            ElementVisual visual = FindVisual(element);

            SpellShield prefab = visual != null && visual.shieldPrefab != null
                ? visual.shieldPrefab
                : fallbackShieldPrefab;

            SpellShield shield;
            if (prefab != null)
            {
                shield = Instantiate(prefab, transform.position, Quaternion.identity);
                shield.AttachTo(transform);
            }
            else
            {
                shield = SpellShield.CreateFallback(transform);
            }

            shield.Configure(shieldScale, shieldDuration);
            shield.Play(element);

            SpellAudio.Play(SpellSound.Shield, transform.position, element);
        }

        // ---------- เส้นที่อีกฝ่ายกำลังวาด ----------

        /// <summary>
        /// จำนวนจุดต่อหนึ่งขีดตอนส่งสด ๆ ระหว่างวาด
        /// น้อยกว่าตอนร่ายจริงเพราะส่งถี่มาก (หลายครั้งต่อวินาที) แค่ให้เห็นเค้าโครง
        /// ว่าอีกฝ่ายกำลังเขียนอะไรก็พอ ไม่ต้องละเอียดเท่าตอนตรวจรูปทรง
        /// </summary>
        public const int LiveSyncPointCount = 16;

        private readonly Dictionary<int, LineRenderer> remoteStrokes = new Dictionary<int, LineRenderer>();
        private static Material remoteStrokeMaterial;

        /// <summary>ส่งขีดที่กำลังวาดให้คนอื่นเห็น เรียกจากเครื่องเจ้าของ</summary>
        public void SyncStroke(int strokeIndex, Vector2[] points)
        {
            if (!IsOwner || !showRemoteDrawing) return;
            if (points == null || points.Length < 2) return;
            if (strokeIndex < 0 || strokeIndex > byte.MaxValue) return;

            Vector2[] packed = DollarOneRecognizer.Resample(points, LiveSyncPointCount);
            if (packed == null) return;

            SyncStrokeServerRpc(packed, (byte)strokeIndex);
        }

        /// <summary>ล้างเส้นที่ค้างอยู่บนจอคนอื่น เรียกตอนร่ายเสร็จหรือยกเลิก</summary>
        public void ClearSyncedStrokes()
        {
            if (!IsOwner) return;
            ClearStrokesServerRpc();
        }

        [ServerRpc]
        private void SyncStrokeServerRpc(Vector2[] points, byte strokeIndex)
        {
            if (points == null || points.Length < 2 || points.Length > LiveSyncPointCount) return;
            SyncStrokeClientRpc(points, strokeIndex);
        }

        [ClientRpc]
        private void SyncStrokeClientRpc(Vector2[] points, byte strokeIndex)
        {
            // เจ้าของเห็นเส้นตัวเองจาก SpellDrawing อยู่แล้ว วาดซ้ำจะได้เส้นทับกันหนา ๆ
            if (IsOwner) return;
            DrawRemoteStroke(strokeIndex, points);
        }

        [ServerRpc]
        private void ClearStrokesServerRpc()
        {
            ClearStrokesClientRpc();
        }

        [ClientRpc]
        private void ClearStrokesClientRpc()
        {
            if (IsOwner) return;
            ClearRemoteStrokes();
        }

        private void DrawRemoteStroke(int strokeIndex, Vector2[] points)
        {
            if (!remoteStrokes.TryGetValue(strokeIndex, out LineRenderer line) || line == null)
            {
                var holder = new GameObject($"RemoteStroke_{strokeIndex}");
                line = holder.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.widthMultiplier = remoteStrokeWidth;
                line.numCapVertices = 4;
                line.numCornerVertices = 4;
                line.material = RemoteStrokeMaterial;
                line.sortingOrder = 550;

                // สีจาง ๆ เพื่อให้แยกออกว่าเป็นเส้นของอีกฝ่าย ยังเขียนไม่เสร็จ
                var color = new Color(1f, 1f, 1f, 0.55f);
                line.startColor = color;
                line.endColor = color;

                remoteStrokes[strokeIndex] = line;
            }

            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
        }

        private void ClearRemoteStrokes()
        {
            foreach (LineRenderer line in remoteStrokes.Values)
                if (line != null) Destroy(line.gameObject);

            remoteStrokes.Clear();
        }

        private static Material RemoteStrokeMaterial
        {
            get
            {
                // Sprites/Default เข้ากับ URP ที่โปรเจกต์นี้ใช้
                // อย่าเผลอไปใช้เชเดอร์ของ Built-in RP เด็ดขาด มันจะไม่ขึ้นเลย
                if (remoteStrokeMaterial == null)
                    remoteStrokeMaterial = new Material(Shader.Find("Sprites/Default"));
                return remoteStrokeMaterial;
            }
        }

        public override void OnNetworkDespawn()
        {
            // ออกจากเกมแล้วเส้นต้องไม่ค้างบนจอคนอื่น
            ClearRemoteStrokes();
            base.OnNetworkDespawn();
        }

        private static bool warnedAboutMissingProjectile;

        /// <summary>
        /// สร้างลูกเวทเองเมื่อไม่มี Prefab ให้ใช้ พร้อมเตือนหนึ่งครั้งว่ากำลังใช้ของสำรอง
        /// เตือนแค่ครั้งเดียวเพราะยิงรัว ๆ แล้วเตือนทุกนัดจะท่วม Console จนอ่านอย่างอื่นไม่ออก
        /// </summary>
        private SpellProjectile CreateFallbackProjectile(Vector3 origin, Quaternion rotation)
        {
            if (!warnedAboutMissingProjectile)
            {
                warnedAboutMissingProjectile = true;
                Debug.LogWarning(
                    "[SpellCaster] ไม่มี Prefab ลูกเวท กำลังใช้ลูกเวทที่สร้างด้วยโค้ดแทน\n"
                    + "ถ้าอยากใช้อาร์ตของตัวเอง สั่ง Tools > เกมวาดวงเวท > ติดตั้งฉากอัตโนมัติ "
                    + "แล้วใส่ Prefab ในช่อง Element Visuals ของ Spell Caster");
            }

            return SpellProjectile.CreateFallback(origin, rotation);
        }

        private ElementVisual FindVisual(SpellElement element)
        {
            if (elementVisuals == null) return null;

            foreach (ElementVisual visual in elementVisuals)
                if (visual != null && visual.element == element) return visual;

            return null;
        }
    }
}
