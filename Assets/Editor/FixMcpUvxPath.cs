using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
/// ตาม PathResolverService.GetUvxPath() ข้อความนั้นเกิดได้เฉพาะตอน EditorPrefs
/// key MCPForUnity.UvxPath ว่าง เพราะโค้ดจะคืนคำว่า "uvx" ดื้อ ๆ ไปให้ cmd รัน
///
/// หมายเหตุสองข้อที่ทำให้เวอร์ชันก่อน ๆ ไม่ผ่าน:
/// 1. Unity แคช EditorPrefs ในหน่วยความจำและเขียนลงรีจิสทรีตอนปิดโปรแกรม
///    ค่าที่เห็นในรีจิสทรีจึงอาจเป็นของ instance อื่น ต้องเขียนทับทุกครั้ง
///    ไม่ใช่เขียนเฉพาะตอนที่อ่านได้ว่าง
/// 2. Environment.GetFolderPath(SpecialFolder.*) บน Mono ของ Unity คืนค่าว่าง
///    ได้ ต้องอ่าน APPDATA / USERPROFILE จากตัวแปรสภาพแวดล้อมตรง ๆ แทน
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

        var tried = new List<string>();
        string found = FindUvx(tried);

        if (verbose)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[FixMcpUvxPath] รายงานผล");
            sb.AppendLine("  ค่าที่ Unity อ่านได้: " + (string.IsNullOrEmpty(current) ? "(ว่าง)" : current));
            sb.AppendLine("  หา uvx เจอที่: " + (string.IsNullOrEmpty(found) ? "(ไม่เจอ)" : found));
            sb.AppendLine("  APPDATA = " + Describe(GetEnv("APPDATA")));
            sb.AppendLine("  USERPROFILE = " + Describe(GetEnv("USERPROFILE")));
            sb.AppendLine("  ที่ค้นไปแล้ว " + tried.Count + " ที่:");
            foreach (string t in tried) sb.AppendLine("    " + t);
            Debug.Log(sb.ToString());
        }

        if (string.IsNullOrEmpty(found))
        {
            // ถ้าค่าเดิมยังใช้ได้ก็ปล่อยไว้ ไม่ไปลบของที่ทำงานอยู่
            if (!string.IsNullOrEmpty(current) && File.Exists(current)) return;

            Debug.LogWarning(
                "[FixMcpUvxPath] หา uvx ไม่เจอ สั่ง Tools > MCP > ตั้ง path ของ uvx ใหม่ "
                + "เพื่อดูว่าค้นที่ไหนไปบ้าง"
            );
            return;
        }

        if (current == found)
        {
            if (verbose) Debug.Log("[FixMcpUvxPath] ค่าถูกต้องอยู่แล้ว กด Start Server ได้เลย");
            return;
        }

        EditorPrefs.SetString(PrefKey, found);

        // อ่านกลับมายืนยันว่าเขียนติดจริง ไม่ใช่แค่คิดไปเอง
        string after = EditorPrefs.GetString(PrefKey, string.Empty);
        if (after == found)
            Debug.Log("[FixMcpUvxPath] ตั้ง path ของ uvx แล้ว: " + after
                      + "\nกด Start Server ในหน้าต่าง MCP for Unity ได้เลย");
        else
            Debug.LogError("[FixMcpUvxPath] เขียนค่าไม่ติด อ่านกลับมาได้: " + Describe(after));
    }

    private static string Describe(string value)
    {
        return string.IsNullOrEmpty(value) ? "(ว่าง)" : value;
    }

    private static string GetEnv(string name)
    {
        try
        {
            return Environment.GetEnvironmentVariable(name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// หาโฟลเดอร์ AppData\Roaming จาก Unity เอง เผื่อตัวแปรสภาพแวดล้อมว่าง
    /// persistentDataPath บน Windows อยู่ใต้ AppData/LocalLow ของผู้ใช้เสมอ
    /// จึงไต่ขึ้นไปหาคำว่า AppData แล้วต่อด้วย Roaming
    /// </summary>
    private static string GetRoamingFromUnity()
    {
        try
        {
            string dir = Application.persistentDataPath;
            while (!string.IsNullOrEmpty(dir))
            {
                string name = Path.GetFileName(dir);
                if (string.Equals(name, "AppData", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(dir, "Roaming");

                string parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
        }
        catch (Exception)
        {
            // ไม่ต้องทำอะไร ปล่อยให้ทางอื่นลองต่อ
        }

        return null;
    }

    private static string FindUvx(List<string> tried)
    {
        bool windows = Application.platform == RuntimePlatform.WindowsEditor;
        string exe = windows ? "uvx.exe" : "uvx";

        foreach (string dir in CandidateDirectories())
        {
            if (string.IsNullOrEmpty(dir)) continue;

            string candidate;
            try
            {
                candidate = Path.Combine(dir.Trim().Trim('"'), exe);
            }
            catch (ArgumentException)
            {
                // บาง entry ใน PATH มีอักขระที่ใช้ในชื่อพาธไม่ได้ ข้ามไป
                continue;
            }

            bool exists = File.Exists(candidate);
            tried.Add((exists ? "[เจอ] " : "[ไม่มี] ") + candidate);
            if (exists) return candidate;
        }

        return null;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        // อ่านจากตัวแปรสภาพแวดล้อมตรง ๆ เพราะ Environment.GetFolderPath
        // บน Mono ของ Unity คืนค่าว่างได้
        string home = GetEnv("USERPROFILE");
        if (string.IsNullOrEmpty(home)) home = GetEnv("HOME");

        string appData = GetEnv("APPDATA");
        if (string.IsNullOrEmpty(appData) && !string.IsNullOrEmpty(home))
            appData = Path.Combine(home, "AppData", "Roaming");
        if (string.IsNullOrEmpty(appData))
            appData = GetRoamingFromUnity();

        if (!string.IsNullOrEmpty(appData))
        {
            // ตัวที่ทดสอบแล้วว่ารันเซิร์ฟเวอร์ได้จริงบนเครื่องนี้มาก่อน
            yield return Path.Combine(appData, "Python", "Python314", "Scripts");
            yield return Path.Combine(appData, "npm");

            // เผื่อ Python เวอร์ชันอื่น
            string pythonRoot = Path.Combine(appData, "Python");
            if (Directory.Exists(pythonRoot))
            {
                string[] subdirs;
                try { subdirs = Directory.GetDirectories(pythonRoot); }
                catch (Exception) { subdirs = new string[0]; }

                foreach (string sub in subdirs)
                    yield return Path.Combine(sub, "Scripts");
            }
        }

        if (!string.IsNullOrEmpty(home))
        {
            // ที่ตัวติดตั้งทางการของ astral ลงให้
            yield return Path.Combine(home, ".local", "bin");
            yield return Path.Combine(home, ".cargo", "bin");
        }

        // macOS / Linux
        yield return "/opt/homebrew/bin";
        yield return "/usr/local/bin";

        // สุดท้ายค่อยไล่ตาม PATH เผื่อ Unity ได้ค่าใหม่มาแล้ว
        string path = GetEnv("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (string dir in path.Split(Path.PathSeparator))
                yield return dir;
        }
    }
}
