using System.Drawing.Printing;
using System.Management;

namespace PrintAgent;

/// <summary>Thông tin một máy in.</summary>
public record PrinterInfo(
    string Name,
    string Port,
    bool Physical,
    bool Offline,
    bool IsDefault);

/// <summary>
/// Liệt kê máy in qua WMI (Win32_Printer) để phân biệt máy in VẬT LÝ
/// (USB, mạng, LPT...) với máy in ẢO (Print to PDF, XPS, Fax, OneNote...).
/// </summary>
public static class PrinterService
{
    // Cổng của máy in ảo / phần mềm.
    private static readonly string[] VirtualPorts =
        { "PORTPROMPT:", "NUL:", "FILE:", "SHRFAX:", "XPSPORT:" };

    // Từ khóa trong TÊN hoặc CỔNG của máy in ảo.
    private static readonly string[] VirtualKeywords =
        { "onenote", "print to pdf", "xps", "fax", "pdfcreator", "adobe pdf",
          "foxit", "cutepdf", "microsoft print", "pdf24", "bullzip", "dopdf",
          "send to onenote", "microsoft shared fax" };

    public static List<PrinterInfo> GetPrinters()
    {
        var defaultName = SafeDefault();

        try
        {
            var list = new List<PrinterInfo>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PortName, WorkOffline FROM Win32_Printer");

            foreach (ManagementObject mo in searcher.Get())
            {
                var name = (mo["Name"] as string) ?? "";
                var port = (mo["PortName"] as string) ?? "";
                var offline = (mo["WorkOffline"] as bool?) ?? false;

                list.Add(new PrinterInfo(
                    Name: name,
                    Port: port,
                    Physical: IsPhysical(name, port),
                    Offline: offline,
                    IsDefault: string.Equals(name, defaultName, StringComparison.OrdinalIgnoreCase)));
            }

            if (list.Count > 0) return list;
        }
        catch
        {
            // WMI lỗi → rơi xuống cách liệt kê cơ bản bên dưới.
        }

        // Fallback: không có thông tin cổng, đoán theo tên.
        var fallback = new List<PrinterInfo>();
        foreach (string p in PrinterSettings.InstalledPrinters)
            fallback.Add(new PrinterInfo(
                p, "", IsPhysical(p, ""), false,
                string.Equals(p, defaultName, StringComparison.OrdinalIgnoreCase)));
        return fallback;
    }

    private static bool IsPhysical(string name, string port)
    {
        var n = name.ToLowerInvariant();
        var p = port.ToLowerInvariant();

        // Cổng ảo rõ ràng
        foreach (var vp in VirtualPorts)
            if (port.Equals(vp, StringComparison.OrdinalIgnoreCase)) return false;

        // Tên/cổng chứa từ khóa ảo
        foreach (var kw in VirtualKeywords)
            if (n.Contains(kw) || p.Contains(kw)) return false;

        // Cổng vật lý điển hình: USB, WSD, LPT, COM, IP mạng, TCP...
        if (p.StartsWith("usb") || p.StartsWith("wsd") || p.StartsWith("lpt") ||
            p.StartsWith("com") || p.StartsWith("ip_") || p.StartsWith("tcp") ||
            p.StartsWith("\\\\") || System.Text.RegularExpressions.Regex.IsMatch(p, @"^\d+\.\d+\.\d+\.\d+"))
            return true;

        // Cổng trống (fallback) mà tên không chứa từ khóa ảo → coi là vật lý.
        if (string.IsNullOrEmpty(p)) return true;

        // Còn lại: mặc định coi là vật lý (an toàn hơn là ẩn nhầm).
        return true;
    }

    private static string SafeDefault()
    {
        try { return new PrinterSettings().PrinterName; }
        catch { return ""; }
    }
}
