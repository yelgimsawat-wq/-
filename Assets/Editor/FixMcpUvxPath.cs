using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ตั้งค่า path ของ uvx ให้ MCP for Unity อัตโนมัติ
///
/// ปัญหาที่แก้: MCP for Unity เรียก uvx ผ่าน cmd โดยพึ่ง PATH ของ process
/// แต่ Unity รับ PATH ต่อมาจากโปรแกรมแม่ (Unity Hub > explorer.exe) ซึ่งจำค่า
/// ตั้งแต่ตอนเปิดเครื่อง ถ้าติดตั้ง uv ทีหลัง Unity จะมองไม่เห็นจนกว่าจะรีบูต
/// อาการคือ Console ขึ้น "'uvx' is not recognized as an internal or external command"
///
/// วิธีแก้: เขียน path เต็มลง EditorPrefs key ที่ package อ่าน
/// (MCPForUnity.UvxPath) ทำให้ข้าม PATH ไปเลย
///
/// สคริปต์นี้ไม่ทับค่าที่ตั้งไว้เองแล้ว และเงียบถ้าหา uvx ไม่เจอ
/// ถ้าไม่ต้องการแล้วลบไฟล์นี้ทิ้งได้ ค่าที่ตั้งไว้ยังอยู่
/// </summary>
[InitializeOnLoad]
public static class FixMcpUvxPath
{
    private const string PrefKey = "MCPForUnity.UvxPath";

    static FixMcpUvxPath()
    {
        // เคารพค่าที่ตั้งไว้แล้ว ตราบใดที่ไฟล์ยังอยู่จริง
        string existing = EditorPrefs.GetString(PrefKey, string.Empty);
        if (!string.IsNullOrEmpty(existing) && File.Exists(existing))
            return;

        string found = FindUvx();
        if (string.IsNullOrEmpty(found))
        {
            // หาไม่เจอ ปล่อยให้ package จัดการต่อตามปกติ ไม่ต้องรบกวนด้วย warning
            return;
        }

        EditorPrefs.SetString(PrefKey, found);
        Debug.Log("[FixMcpUvxPath] ตั้ง path ของ uvx ให้ MCP for Unity แล้ว: " + found);
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
                candidate = Path.Combine(dir, exe);
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

    private static string[] CandidateDirectories()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var dirs = new System.Collections.Generic.List<string>
        {
            // ที่ uv ลงเวลาใช้ npm (เครื่องนี้อยู่ตรงนี้)
            appData != null ? Path.Combine(appData, "npm") : null,
            // ที่ตัวติดตั้งทางการของ astral ลงให้
            Path.Combine(home, ".local", "bin"),
            Path.Combine(home, ".cargo", "bin"),
            // macOS / Linux
            "/opt/homebrew/bin",
            "/usr/local/bin",
        };

        // สุดท้ายค่อยไล่ตาม PATH เผื่อ Unity ได้ค่าใหม่มาแล้ว
        string path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
            dirs.AddRange(path.Split(Path.PathSeparator));

        return dirs.ToArray();
    }
}
