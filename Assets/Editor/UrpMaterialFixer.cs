using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// แปลงวัสดุที่ทำมาสำหรับ Built-in Render Pipeline ให้ใช้กับ URP ได้
///
/// ปัญหาที่แก้: แอสเซ็ตเก่า ๆ (เช่น Foliage2D) ใช้เชเดอร์อย่าง Unlit/Transparent
/// ซึ่งไม่มีแท็ก RenderPipeline = UniversalPipeline ตัววาดภาพ 2D ของ URP
/// จะข้ามวัตถุพวกนั้นไปเลย ไม่ใช่วาดผิดสีแต่ไม่วาดเลย ผลคือของหายทั้งแมพ
/// โดยไม่มี error ให้เห็นสักบรรทัด
///
/// Unity มีตัวแปลงของตัวเองอยู่ที่ Window > Rendering > Render Pipeline Converter
/// แต่ตัวนั้นแปลงวัสดุ 3D เป็นหลัก และต้องกดผ่านหน้าต่างหลายขั้น
/// ตัวนี้ตรงไปตรงมากว่าสำหรับเกม 2D คือเปลี่ยนไปใช้เชเดอร์ 2D ของ URP ทั้งหมด
///
/// เก็บพื้นผิวและสีเดิมไว้ครบ เพราะทั้งสองฝั่งใช้ชื่อช่องเดียวกัน (_MainTex, _Color)
/// </summary>
public static class UrpMaterialFixer
{
    /// <summary>เชเดอร์ปลายทาง เป็นตัว 2D ของ URP ที่ไม่รับแสง เหมาะกับภาพที่วาดสีมาแล้ว</summary>
    private const string TargetShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    /// <summary>
    /// เชเดอร์ของ Built-in ที่รู้ว่าต้องแปลง
    ///
    /// จำกัดเฉพาะตระกูล Unlit เพราะแปลงแล้วได้ผลตรงกับของเดิม
    /// ตระกูลที่รับแสง (Standard, Diffuse) แปลงตรง ๆ ไม่ได้ หน้าตาจะเพี้ยน
    /// ปล่อยไว้ให้คนตัดสินใจเองดีกว่าแปลงมั่วแล้วพัง
    /// </summary>
    private static readonly HashSet<string> Convertible = new HashSet<string>
    {
        "Unlit/Texture",
        "Unlit/Transparent",
        "Unlit/Transparent Cutout",
        "Unlit/Color",
        "Sprites/Default",
        "Sprites/Diffuse",
    };

    [MenuItem("Tools/เกมวาดวงเวท/แปลงวัสดุเก่าให้เข้ากับ URP", priority = 20)]
    public static void ConvertAll()
    {
        Shader target = Shader.Find(TargetShaderName);
        if (target == null)
        {
            Debug.LogError($"[UrpMaterialFixer] หาเชเดอร์ {TargetShaderName} ไม่เจอ โปรเจกต์นี้อาจไม่ได้ใช้ URP");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material");

        int converted = 0;
        int skipped = 0;
        var skippedShaders = new Dictionary<string, int>();

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (EditorUtility.DisplayCancelableProgressBar(
                        "แปลงวัสดุให้เข้ากับ URP",
                        $"{i + 1} / {guids.Length}  {path}",
                        (float)i / guids.Length))
                {
                    break;
                }

                // ไฟล์ในแพ็กเกจแก้ไม่ได้ และไม่ควรแก้ด้วย
                if (!path.StartsWith("Assets/")) continue;

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) continue;

                string shaderName = material.shader.name;
                if (shaderName == TargetShaderName) continue;

                if (!Convertible.Contains(shaderName))
                {
                    // เชเดอร์ที่รองรับ URP อยู่แล้วไม่ต้องนับว่าข้าม
                    if (!shaderName.StartsWith("Universal Render Pipeline/"))
                    {
                        skipped++;
                        if (!skippedShaders.ContainsKey(shaderName)) skippedShaders[shaderName] = 0;
                        skippedShaders[shaderName]++;
                    }
                    continue;
                }

                Convert(material, target);
                converted++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var report = new System.Text.StringBuilder();
        report.AppendLine($"[UrpMaterialFixer] แปลงแล้ว {converted} วัสดุ จากทั้งหมด {guids.Length}");

        if (skipped > 0)
        {
            report.AppendLine($"ข้ามไป {skipped} วัสดุ เพราะเป็นเชเดอร์ที่แปลงตรง ๆ แล้วหน้าตาจะเพี้ยน:");
            foreach (var kv in skippedShaders)
                report.AppendLine($"   {kv.Key}  ({kv.Value} วัสดุ)");
        }

        Debug.Log(report.ToString());
    }

    /// <summary>
    /// เปลี่ยนเชเดอร์แล้วเอาพื้นผิวกับสีเดิมกลับใส่
    ///
    /// ต้องอ่านค่าเก็บไว้ก่อนเปลี่ยน เพราะการเปลี่ยนเชเดอร์ทำให้ช่องที่เชเดอร์ใหม่
    /// ไม่รู้จักถูกล้างทิ้ง ถ้าอ่านทีหลังจะได้ค่าว่าง แล้วแมพจะกลายเป็นสีขาวล้วน
    /// </summary>
    private static void Convert(Material material, Shader target)
    {
        Texture mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

        material.shader = target;

        if (mainTexture != null && material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", mainTexture);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        EditorUtility.SetDirty(material);
    }
}
