using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// เปิด/ปิด define METAVC_NGO ให้อัตโนมัติตามว่ามี MetaVoiceChat อยู่ในโปรเจกต์ไหม
///
/// ทำไมต้องมี: ตัวเชื่อม Netcode ของ MetaVoiceChat ถูกครอบด้วย #if METAVC_NGO
/// ทั้งไฟล์ ถ้าไม่เปิด define มันจะไม่ถูกคอมไพล์เลย แล้วเสียงจะไม่ทำงาน
/// โดยไม่มี error ให้เห็น (ผู้เขียนแพ็กเกจออกแบบไว้แบบนี้เพราะ NGO ไม่มี
/// define symbol ประจำตัวเหมือนไลบรารีอื่น)
///
/// และถ้าวันหนึ่งลบโฟลเดอร์ MetaVoiceChat ทิ้ง define จะถูกถอดให้เอง
/// ไม่งั้นจะเหลือ define ค้างที่ชี้ไปยังโค้ดที่ไม่มีอยู่แล้ว
/// </summary>
[InitializeOnLoad]
public static class EnsureMetaVoiceChatDefine
{
    private const string Symbol = "METAVC_NGO";
    private const string PackageFolder = "Assets/ThirdParty/MetaVoiceChat";

    static EnsureMetaVoiceChatDefine()
    {
        bool shouldBeOn = Directory.Exists(PackageFolder);

        // ตั้งให้ครบทุกแพลตฟอร์มที่โปรเจกต์นี้จะ build จริง
        // ถ้าตั้งแค่ Standalone แล้ววันหนึ่งไป build Android เสียงจะหายเฉย ๆ
        foreach (NamedBuildTarget target in new[]
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.WebGL,
        })
        {
            Apply(target, shouldBeOn);
        }
    }

    private static void Apply(NamedBuildTarget target, bool shouldBeOn)
    {
        string current;
        try
        {
            current = PlayerSettings.GetScriptingDefineSymbols(target);
        }
        catch (System.Exception)
        {
            // บางแพลตฟอร์มอาจไม่ได้ติดตั้งโมดูลไว้ ข้ามไปเงียบ ๆ
            return;
        }

        var symbols = new List<string>(
            current.Split(';', System.StringSplitOptions.RemoveEmptyEntries));

        bool isOn = symbols.Contains(Symbol);
        if (isOn == shouldBeOn) return;

        if (shouldBeOn) symbols.Add(Symbol);
        else symbols.Remove(Symbol);

        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));

        Debug.Log(shouldBeOn
            ? $"[MetaVoiceChat] เปิด define {Symbol} ให้ {target.TargetName} แล้ว ระบบเสียงพร้อมใช้"
            : $"[MetaVoiceChat] ถอด define {Symbol} ออกจาก {target.TargetName} เพราะไม่พบแพ็กเกจแล้ว");
    }
}
