using System.Collections;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// เส้นที่ผู้เล่นวาด แสดงค้างไว้บนแผนที่ให้ทุกคนเห็นแล้วค่อยจางหาย
    ///
    /// ไม่ใช่ NetworkObject โดยตั้งใจ ทุกเครื่องสร้างของตัวเองจากชุดพิกัดเดียวกัน
    /// ที่ ClientRpc ส่งมา จึงเห็นตรงกันอยู่แล้วโดยไม่ต้องซิงก์ทีละเฟรม
    /// วิธีนี้เบากว่าการ Spawn NetworkObject มาก และไม่กินโควตา object ของ Netcode
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SpellStrokeView : MonoBehaviour
    {
        private LineRenderer line;
        private static Material sharedLineMaterial;

        /// <summary>สร้างเส้นหนึ่งเส้นที่จะจางหายไปเอง</summary>
        public static SpellStrokeView Spawn(Vector2[] points, Color color, float width, float lifetime)
        {
            if (points == null || points.Length < 2) return null;

            var holder = new GameObject("SpellStroke");
            var view = holder.AddComponent<SpellStrokeView>();
            view.Setup(points, color, width, lifetime);
            return view;
        }

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
        }

        private void Setup(Vector2[] points, Color color, float width, float lifetime)
        {
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = SharedLineMaterial;
            line.startColor = color;
            line.endColor = color;

            // เส้นเวทควรอยู่หน้าฉากและตัวละคร เหมือนกับวงเวท
            line.sortingOrder = 400;

            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));

            StartCoroutine(FadeOut(color, lifetime));
        }

        private static Material SharedLineMaterial
        {
            get
            {
                // ใช้วัสดุร่วมกันทุกเส้น ไม่งั้นร่ายรัว ๆ จะสร้างวัสดุใหม่ทิ้งไว้เรื่อย ๆ
                if (sharedLineMaterial == null)
                    sharedLineMaterial = new Material(Shader.Find("Sprites/Default"));
                return sharedLineMaterial;
            }
        }

        private IEnumerator FadeOut(Color color, float lifetime)
        {
            float elapsed = 0f;
            float holdTime = lifetime * 0.4f;
            float fadeTime = Mathf.Max(0.01f, lifetime - holdTime);

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;

                float alpha = elapsed <= holdTime
                    ? 1f
                    : 1f - (elapsed - holdTime) / fadeTime;

                alpha = Mathf.Clamp01(alpha);

                var faded = new Color(color.r, color.g, color.b, alpha);
                line.startColor = faded;
                line.endColor = faded;

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
