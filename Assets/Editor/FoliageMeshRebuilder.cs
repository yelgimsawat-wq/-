using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// สั่งให้ Foliage2D สร้างเมชใหม่ทุกชิ้นในฉากรวดเดียว
///
/// ปัญหาที่แก้: Foliage2D เก็บเมชไว้ในหน่วยความจำ ไม่ได้เซฟลงไฟล์ พอเปิดโปรเจกต์ใหม่
/// MeshRenderer จึงมีอยู่แต่ไม่มีเมชให้วาด ผลคือแมพล่องหนทั้งแมพโดยไม่มี error
/// ตัวมันสร้างเมชใหม่ตอนที่ Editor ของมันถูกเรียกทำงาน ซึ่งเกิดตอนเลือกวัตถุเท่านั้น
/// ถ้ามีของ 500 ชิ้นก็ต้องคลิกทีละชิ้น ซึ่งทำจริงไม่ไหว
///
/// เรียกผ่าน reflection แทนการอ้างคลาสตรง ๆ เพราะ Foliage2D เป็นแอสเซ็ตของคนอื่น
/// ถ้าวันหนึ่งลบออกจากโปรเจกต์ ไฟล์นี้จะยังคอมไพล์ผ่าน ไม่พังทั้งโปรเจกต์ตาม
/// </summary>
public static class FoliageMeshRebuilder
{
    /// <summary>
    /// Foliage2D มีสองคลาสที่สร้างเมชเอง ชื่อเมธอดเหมือนกันแต่คนละคลาส
    /// ถ้าทำแค่ตัวแรกจะเหลืออีกครึ่งแมพที่ยังล่องหน
    /// </summary>
    private static readonly string[] TypeNames =
    {
        "Foliage.Foliage2D, Assembly-CSharp",
        "Foliage.Foliage2D_Sprite, Assembly-CSharp",
    };

    private const string MethodName = "RebuildMesh";

    [MenuItem("Tools/เกมวาดวงเวท/สร้างเมช Foliage2D ใหม่ทั้งหมด", priority = 21)]
    public static void RebuildAll()
    {
        int rebuilt = 0;
        int failed = 0;
        int total = 0;
        string firstError = null;
        var missing = new System.Collections.Generic.List<string>();

        try
        {
            foreach (string typeName in TypeNames)
            {
                Type type = Type.GetType(typeName);
                if (type == null)
                {
                    missing.Add(typeName.Split(',')[0]);
                    continue;
                }

                MethodInfo method = type.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    Debug.LogError($"[FoliageMeshRebuilder] คลาส {type.FullName} ไม่มีเมธอด {MethodName} "
                                   + "แอสเซ็ตอาจเปลี่ยนเวอร์ชัน");
                    continue;
                }

                var components = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
                total += components.Length;

                for (int i = 0; i < components.Length; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "สร้างเมช Foliage2D ใหม่",
                            $"{type.Name}  {i + 1} / {components.Length}  {components[i].name}",
                            (float)i / Mathf.Max(1, components.Length)))
                    {
                        return;
                    }

                    try
                    {
                        method.Invoke(components[i], null);
                        rebuilt++;
                    }
                    catch (Exception e)
                    {
                        // ชิ้นเดียวพังไม่ควรทำให้ที่เหลือไม่ได้สร้าง
                        // เช่นชิ้นที่วัสดุหายหรือไม่มีพื้นผิว
                        failed++;
                        if (firstError == null)
                            firstError = components[i].name + ": " + (e.InnerException ?? e).Message;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (missing.Count == TypeNames.Length)
        {
            Debug.LogWarning("[FoliageMeshRebuilder] ไม่มี Foliage2D ในโปรเจกต์นี้ ไม่ต้องทำอะไร");
            return;
        }

        // เมชอยู่ในฉาก ต้องบอก Unity ว่าฉากเปลี่ยนแล้ว ไม่งั้นปิดไปโดยไม่เซฟจะหายอีก
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        var report = new System.Text.StringBuilder();
        report.AppendLine($"[FoliageMeshRebuilder] สร้างเมชใหม่ {rebuilt} ชิ้น จากทั้งหมด {total}");

        if (failed > 0)
            report.AppendLine($"ล้มเหลว {failed} ชิ้น ตัวอย่างแรก: {firstError}");

        report.Append("อย่าลืมเซฟฉาก (Ctrl+S) ไม่งั้นเปิดใหม่แล้วเมชจะหายอีก");

        Debug.Log(report.ToString());
    }
}
