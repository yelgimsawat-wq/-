using System;
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

        [Header("เส้นที่วาดค้างไว้บนแผนที่")]
        [Tooltip("แสดงเส้นที่ผู้เล่นเขียนให้ทุกคนเห็นด้วย ปิดได้ถ้าอยากเห็นแค่วงเวท")]
        [SerializeField] private bool showDrawnStroke = true;

        [SerializeField] private float strokeLifetime = 1.2f;
        [SerializeField] private float strokeWidth = 0.12f;

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

            CastSpellServerRpc(packed, (byte)element, aim);
        }

        /// <summary>
        /// Server รับคำสั่งแล้วกระจายต่อ
        /// ไม่ตรวจซ้ำว่ารูปทรงตรงกับธาตุไหม เพราะให้เครื่องผู้เล่นเป็นคนตัดสิน
        /// ผลคือถ้ามีคนแก้เกมก็ส่งธาตุอะไรมาก็ได้ ยอมรับได้สำหรับเกมเล่นกับเพื่อน
        /// ถ้าวันหนึ่งต้องกันโกง ให้ย้ายการเรียก SpellRecognizer.Evaluate มาทำตรงนี้
        /// </summary>
        [ServerRpc]
        private void CastSpellServerRpc(Vector2[] points, byte elementId, Vector2 direction)
        {
            if (points == null || points.Length < 2 || points.Length > NetworkPointCount) return;

            CastSpellClientRpc(points, elementId, direction);
        }

        /// <summary>ทุกเครื่องรวมทั้งคนร่ายเอง แสดงผลให้ตรงกัน</summary>
        [ClientRpc]
        private void CastSpellClientRpc(Vector2[] points, byte elementId, Vector2 direction)
        {
            SpellElement element = SpellElementExtensions.FromNetworkId(elementId);
            PlaySpell(points, element, direction);
        }

        private void PlaySpell(Vector2[] points, SpellElement element, Vector2 direction)
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
            }

            SpellProjectile projectilePrefab = visual != null && visual.projectilePrefab != null
                ? visual.projectilePrefab
                : fallbackProjectilePrefab;

            if (projectilePrefab != null)
            {
                SpellProjectile projectile = Instantiate(projectilePrefab, origin, rotation);
                projectile.Launch(aim, element);
            }
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
