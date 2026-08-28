using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// เอฟเฟกต์อนุภาคแบบพุ่งกระจาย
    ///
    /// ไม่ใช้ ParticleSystem ของ Unity โดยตั้งใจ เพราะการตั้งค่า ParticleSystem
    /// ด้วยโค้ดต้องแตะ module ย่อยเป็นสิบตัวและอ่านยากมาก ส่วนเอฟเฟกต์ที่เกมนี้
    /// ต้องการคือ "สไปรต์ไม่กี่ตัวพุ่งออกแล้วจางหาย" ซึ่งเขียนเองสั้นกว่าและ
    /// คุมได้ตรงกว่า
    ///
    /// ภาพอนุภาคมาจาก Kenney (CC0) ดูรายละเอียดที่ Assets/Art/Kenney/LICENSE.md
    /// ถ้าไม่ได้ผูกภาพไว้ ระบบจะเงียบ ๆ ไม่ทำอะไร ไม่พัง
    /// </summary>
    public static class SpellVfx
    {
        private static readonly Dictionary<SpellElement, Sprite> elementSprites =
            new Dictionary<SpellElement, Sprite>();

        private static Sprite genericSprite;
        private static Material sharedMaterial;

        /// <summary>ให้ SpellVfxLibrary เอาภาพมาลงทะเบียน</summary>
        public static void Register(SpellElement element, Sprite sprite)
        {
            if (sprite == null) elementSprites.Remove(element);
            else elementSprites[element] = sprite;
        }

        public static void RegisterGeneric(Sprite sprite)
        {
            genericSprite = sprite;
        }

        /// <summary>
        /// พุ่งอนุภาคกระจายออกจากจุดหนึ่ง
        /// </summary>
        /// <param name="element">ใช้เลือกภาพและสี</param>
        /// <param name="spread">1 = กระจายรอบทิศ, ต่ำกว่านั้น = พุ่งไปทางเดียว</param>
        public static void Burst(
            SpellElement element,
            Vector3 position,
            int count = 8,
            float speed = 4f,
            float lifetime = 0.5f,
            float size = 0.5f,
            Vector2 direction = default,
            float spread = 1f)
        {
            Sprite sprite = Resolve(element);
            if (sprite == null) return;

            Color color = element.ToColor();
            bool aimed = direction.sqrMagnitude > 0.0001f && spread < 1f;
            float baseAngle = aimed ? Mathf.Atan2(direction.y, direction.x) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = aimed
                    ? baseAngle + Random.Range(-Mathf.PI, Mathf.PI) * spread
                    : Random.Range(0f, Mathf.PI * 2f);

                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                               * speed * Random.Range(0.6f, 1.3f);

                SpawnOne(sprite, position, color, velocity, lifetime * Random.Range(0.75f, 1.25f),
                    size * Random.Range(0.7f, 1.3f));
            }
        }

        private static Sprite Resolve(SpellElement element)
        {
            if (elementSprites.TryGetValue(element, out Sprite sprite) && sprite != null)
                return sprite;

            return genericSprite;
        }

        private static void SpawnOne(
            Sprite sprite, Vector3 position, Color color, Vector2 velocity, float lifetime, float size)
        {
            var go = new GameObject("SpellVfxParticle");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            // หมุนสุ่มให้แต่ละตัวไม่เหมือนกัน ไม่งั้นดูเป็นภาพเดิมซ้ำ ๆ
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.material = SharedMaterial;
            renderer.sortingOrder = 480;

            go.AddComponent<SpellVfxParticle>().Play(velocity, lifetime);
        }

        private static Material SharedMaterial
        {
            get
            {
                // Sprites/Default เข้ากับ URP ที่โปรเจกต์นี้ใช้
                // เชเดอร์ของ Built-in RP จะไม่ขึ้นเลย เคยพลาดมาแล้วกับวงเวท
                if (sharedMaterial == null)
                    sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                return sharedMaterial;
            }
        }
    }

    /// <summary>อนุภาคหนึ่งตัว ลอยออกไปพร้อมจางและหดตัวแล้วหายไปเอง</summary>
    public class SpellVfxParticle : MonoBehaviour
    {
        private SpriteRenderer view;

        public void Play(Vector2 velocity, float lifetime)
        {
            view = GetComponent<SpriteRenderer>();
            StartCoroutine(Animate(velocity, lifetime));
        }

        private IEnumerator Animate(Vector2 velocity, float lifetime)
        {
            Color start = view.color;
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                // ชะลอลงเรื่อย ๆ แทนความเร็วคงที่ ดูเหมือนมีแรงต้านอากาศ
                transform.position += (Vector3)(velocity * (1f - t) * Time.deltaTime);

                // จางแบบยกกำลัง ค้างสว่างช่วงแรกแล้วหายเร็วตอนท้าย
                view.color = new Color(start.r, start.g, start.b, start.a * (1f - t * t));
                transform.localScale = startScale * Mathf.Lerp(1f, 0.4f, t);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
