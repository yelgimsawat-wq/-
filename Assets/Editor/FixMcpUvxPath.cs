using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// บังคับตั้ง path เต็มของ uvx ให้ MCP for Unity
///
/// ปัญหาที่แก้: MCP for Unity เรียก uvx ผ่าน cmd โดยพึ่ง PATH ของ process
/// แต่ Unity รับ PATH ต่อมาจาก Unity Hub ซึ่งรับต่อจาก explorer.exe ที่จำค่าไว้
/// ตั้งแต่บูตเครื่อง ติดตั้ง uv ทีหลังจึงมองไม่เห็นจนกว่าจะรีบูต
/// อาการ: Console ขึ้น "'uvx' is not recognized as an internal or external command"
///
/// ตาม PathResolverService.GetUvxPath() ข้อความนั้นเกิดได้เฉพาะตอนที่
/// EditorPrefs key MCPForUnity.UvxPath ว่าง เพราะโค้ดจะคืนคำว่า "uvx" ดื้อ ๆ
/// ไปให้ cmd รัน สคริปต์นี้จึงเขียนค่าลงไปตรง ๆ ทุกครั้งที่โหลด domain
/// ไม่ใช่เขียนเฉพาะตอนว่าง เพื่อกันกรณีค่าในรีจิสทรีกับค่าที่ process
/// มองเห็นไม่ตรงกัน
/// </summary>
[InitializeOnLoad]
public static class FixMcpUvxPath
{
    private const string PrefKey = "MCPForUnity.UvxPath";

    static FixMcpUvxPath()
    {
        Apply(verbose: false);
    }

    [MenuItem("Tools/MCP/ตั้ง path ของ uvx ใหม่")]
    public static void ApplyFromMenu()
    {
        Apply(verbose: true);
    }

    private static void Apply(bool verbose)
    {
        string current = EditorPrefs.GetString(PrefKey, string.Empty);
        string found = FindUvx();

        if (verbose)
        {
            Debug.Log(
                "[FixMcpUvxPath] ค่าที่ Unity อ่านได้ตอนนี้: "
                + (string.IsNullOrEmpty(current) ? "(ว่าง)" : current)
                + "\nที่หาเจอในเครื่อง: "
                + (string.IsNullOrEmpty(found) ? "(ไม่เจอ)" : found)
            );
        }

        if (string.IsNullOrEmpty(found))
        {
            // ถ้าค่าเดิมยังใช้ได้ก็ปล่อยไว้ ไม่ไปลบของที่ทำงานอยู่
            if (!string.IsNullOrEmpty(current) && File.Exists(current)) return;

            Debug.LogWarning(
                "[FixMcpUvxPath] หา uvx ไม่เจอในเครื่องนี้ "
                + "ติดตั้ง uv แล้วสั่ง Tools > MCP > ตั้ง path ของ uvx ใหม่ อีกครั้ง"
            );
            return;
        }

        if (current == found)
        {
            if (verbose) Debug.Log("[FixMcpUvxPath] ค่าถูกต้องอยู่แล้ว ไม่ต้องแก้");
            return;
        }

        EditorPrefs.SetString(PrefKey, found);

        // อ่านกลับมายืนยันว่าเขียนติดจริง ไม่ใช่แค่คิดไปเอง
        string after = EditorPrefs.GetString(PrefKey, string.Empty);
        if (after == found)
            Debug.Log("[FixMcpUvxPath] ตั้ง path ของ uvx แล้ว: " + after + "\nกด Start Server ในหน้าต่าง MCP for Unity ได้เลย");
        else
            Debug.LogError("[FixMcpUvxPath] เขียนค่าไม่ติด อ่านกลับมาได้: " + after);
    }

    private static string FindUvx()
    {
        string exe = Application.platform == RuntimePlatform.WindowsEditor ? "uvx.exe" : "uvx";

        foreach (string dir in CandidateDirectories())
        {
            if (string.IsNullOrEmpty(dir)) continue;

            string candidate;
            try
            {
                candidate = Path.Combine(dir.Trim('"'), exe);
            }
            catch (ArgumentException)
            {
                // บาง entry ใน PATH มีอักขระที่ใช้ในชื่อพาธไม่ได้ ข้ามไป
                continue;
            }

            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // ตัวที่ทดสอบแล้วว่ารันเซิร์ฟเวอร์ได้จริงบนเครื่องนี้มาก่อน
        if (!string.IsNullOrEmpty(appData))
        {
            yield return Path.Combine(appData, "Python", "Python314", "Scripts");
            yield return Path.Combine(appData, "npm");
        }

        // ที่ตัวติดตั้งทางการของ astral ลงให้
        yield return Path.Combine(home, ".local", "bin");
        yield return Path.Combine(home, ".cargo", "bin");

        // macOS / Linux
        yield return "/opt/homebrew/bin";
        yield return "/usr/local/bin";

        // สุดท้ายค่อยไล่ตาม PATH เผื่อ Unity ได้ค่าใหม่มาแล้ว
        string path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (string dir in path.Split(Path.PathSeparator))
                yield return dir;
        }
    }
}
