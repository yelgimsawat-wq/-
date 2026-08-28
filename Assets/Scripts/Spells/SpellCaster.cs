using System;
using Unity.Netcode;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ตัวกลางระหว่างการวาดกับเครือข่าย ทำให้ทุกเครื่องเห็นวงเวทตรงกัน
    ///
    /// เส้นทางข้อมูลตามข้อกำหนดข้อ 3.4:
    ///   เจ้าของตัวละครวาดเสร็จ -> RequestCast -> ServerRpc -> ClientRpc -> ทุกเครื่องแสดงผล
    ///
    /// จุดที่ต้องระวังเรื่องขนาดข้อมูล: RPC ของ Netcode มีเพดานขนาดข้อความ
    /// ถ้าส่งจุดดิบไปทั้งหมดตอนคนลากยาว ๆ อาจทะลุเพดานจนหลุดทั้งก้อน
    /// เราจึงบีบให้เหลือจำนวนคงที่ก่อนส่งเสมอ (NetworkPointCount)
    /// รูปทรงยังคงเดิมเพราะเป็นการเกลี่ยจุดใหม่ ไม่ใช่ตัดจุดท้ายทิ้ง
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

            [Tooltip("Prefab วงเวทที่จะโผล่หน้าตัวละคร")]
            public MagicCircle circlePrefab;

            [Tooltip("เอฟเฟกต์ตอนร่าย เช่น ParticleSystem ปล่อยว่างได้")]
            public GameObject castEffectPrefab;
        }

        [Header("ตำแหน่งที่วงเวทจะโผล่")]
        [Tooltip("จุดหน้าตัวละคร ปล่อยว่าง = ใช้ตัวละครเองแล้วเลื่อนตาม castOffset")]
        [SerializeField] private Transform castOrigin;

        [Tooltip("ระยะเลื่อนจากตัวละคร ใช้เมื่อไม่ได้กำหนด castOrigin")]
        [SerializeField] private Vector2 castOffset = new Vector2(0f, 1.2f);

        [Header("ภาพประจำแต่ละธาตุ")]
        [SerializeField] private ElementVisual[] elementVisuals;

        [Tooltip("ใช้เมื่อธาตุนั้นยังไม่ได้ใส่ prefab เอาไว้กันลืม")]
        [SerializeField] private MagicCircle fallbackCirclePrefab;

        [Header("เส้นที่วาดค้างไว้บนแผนที่")]
        [Tooltip("แสดงเส้นที่ผู้เล่นวาดให้ทุกคนเห็นด้วย ปิดได้ถ้าอยากเห็นแค่วงเวท")]
        [SerializeField] private bool showDrawnStroke = true;

        [SerializeField] private float strokeLifetime = 1.2f;
        [SerializeField] private float strokeWidth = 0.12f;

        [Header("กันร่ายรัว")]
        [Tooltip("เวลาพักขั้นต่ำระหว่างการร่ายสองครั้ง (วินาที)")]
        [SerializeField] private float castCooldown = 0.35f;

        private float nextAllowedCastTime;

        /// <summary>
        /// เรียกจากเครื่องเจ้าของตัวละครหลังวาดเสร็จ
        /// จุดที่ส่งเข้ามาเป็นพิกัดโลก จะถูกบีบให้เหลือจำนวนคงที่ก่อนส่งต่อ
        /// </summary>
        public void RequestCast(Vector2[] worldPoints, SpellElement element)
        {
            if (!IsOwner) return;
            if (worldPoints == null || worldPoints.Length < 2) return;

            if (Time.time < nextAllowedCastTime) return;
            nextAllowedCastTime = Time.time + castCooldown;

            Vector2[] packed = DollarOneRecognizer.Resample(worldPoints, NetworkPointCount);
            if (packed == null) return;

            CastSpellServerRpc(packed, (byte)element);
        }

        /// <summary>
        /// Server รับคำสั่งแล้วกระจายต่อ
        /// ไม่ตรวจซ้ำว่ารูปทรงตรงกับธาตุไหม เพราะข้อกำหนดให้เครื่องผู้เล่นเป็นคนตัดสิน
        /// ผลคือถ้ามีคนแก้เกมก็ส่งธาตุอะไรมาก็ได้ ยอมรับได้สำหรับเกมเล่นกับเพื่อน
        /// ถ้าวันหนึ่งต้องกันโกง ให้ย้ายการเรียก Recognize มาทำตรงนี้แทน
        /// </summary>
        [ServerRpc]
        private void CastSpellServerRpc(Vector2[] points, byte elementId)
        {
            if (points == null || points.Length < 2 || points.Length > NetworkPointCount) return;

            CastSpellClientRpc(points, elementId);
        }

        /// <summary>ทุกเครื่องรวมทั้งคนร่ายเอง วาดเส้นและวงเวทให้ตรงกัน</summary>
        [ClientRpc]
        private void CastSpellClientRpc(Vector2[] points, byte elementId)
        {
            SpellElement element = SpellElementExtensions.FromNetworkId(elementId);
            PlaySpell(points, element);
        }

        private void PlaySpell(Vector2[] points, SpellElement element)
        {
            if (showDrawnStroke && points != null && points.Length >= 2)
                SpellStrokeView.Spawn(points, element.ToColor(), strokeWidth, strokeLifetime);

            Vector3 origin = GetCastPosition();
            ElementVisual visual = FindVisual(element);

            MagicCircle prefab = visual != null && visual.circlePrefab != null
                ? visual.circlePrefab
                : fallbackCirclePrefab;

            if (prefab != null)
            {
                MagicCircle circle = Instantiate(prefab, origin, Quaternion.identity);
                circle.Play(element);
            }

            if (visual != null && visual.castEffectPrefab != null)
            {
                GameObject effect = Instantiate(visual.castEffectPrefab, origin, Quaternion.identity);
                // เอฟเฟกต์ส่วนใหญ่จบในตัวมันเอง ตั้งเวลาเก็บกวาดกันลืมไว้ด้วย
                Destroy(effect, 5f);
            }
        }

        private Vector3 GetCastPosition()
        {
            if (castOrigin != null) return castOrigin.position;
            return transform.position + new Vector3(castOffset.x, castOffset.y, 0f);
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
