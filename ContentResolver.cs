using System.Text;
using System.Xml;
using System.Xml.Xsl;

namespace PrintAgent;

/// <summary>
/// Chuyển nội dung yêu cầu (URL / base64 / text) thành một Uri mà WebView2 có thể mở.
/// Mọi file tạm được ghi vào thư mục tạm và tự dọn theo tuổi đời.
/// </summary>
public static class ContentResolver
{
    private static readonly string TempDir =
        Path.Combine(Path.GetTempPath(), "PrintAgentJobs");

    public static async Task<Uri> ResolveAsync(PrintRequest r)
    {
        Directory.CreateDirectory(TempDir);
        CleanupOld();

        // 1) In thẳng từ URL
        if (!string.IsNullOrWhiteSpace(r.Url))
            return new Uri(r.Url);

        var type = (r.Type ?? "auto").Trim().ToLowerInvariant();
        var id = Guid.NewGuid().ToString("N");

        // 2) XML + XSLT -> HTML
        if (type == "xml" && !string.IsNullOrWhiteSpace(r.Xslt) &&
            !string.IsNullOrWhiteSpace(r.Content))
        {
            var html = XsltTransform(r.Content!, r.Xslt!);
            var pf = Path.Combine(TempDir, id + ".html");
            await File.WriteAllTextAsync(pf, html, new UTF8Encoding(false));
            return new Uri(pf);
        }

        // 3) File nhị phân (PDF, ảnh...) qua Base64
        if (!string.IsNullOrWhiteSpace(r.ContentBase64))
        {
            var bytes = Convert.FromBase64String(CleanBase64(r.ContentBase64!));
            var ext = ExtFor(type, r.FileName, bytes);
            var pf = Path.Combine(TempDir, id + ext);
            await File.WriteAllBytesAsync(pf, bytes);
            return new Uri(pf);
        }

        // 4) Nội dung text thuần: HTML / XML / text
        var raw = r.Content ?? string.Empty;
        string ext2;
        switch (type)
        {
            case "text":
                // Bọc trong <pre> để in giữ nguyên xuống dòng
                raw = "<!doctype html><meta charset='utf-8'>" +
                      "<pre style=\"font:12pt/1.4 Consolas,monospace;white-space:pre-wrap;" +
                      "word-break:break-word;margin:0\">" +
                      System.Net.WebUtility.HtmlEncode(raw) + "</pre>";
                ext2 = ".html";
                break;
            case "xml":
                ext2 = ".xml";
                break;
            case "html":
            default:
                // auto: nếu trông giống XML thì để .xml, còn lại .html
                ext2 = (type == "auto" && LooksLikeXml(raw)) ? ".xml" : ".html";
                break;
        }

        var file = Path.Combine(TempDir, id + ext2);
        await File.WriteAllTextAsync(file, raw, new UTF8Encoding(false));
        return new Uri(file);
    }

    private static string ExtFor(string type, string? fileName, byte[] bytes)
    {
        // Ưu tiên đuôi file gợi ý
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var e = Path.GetExtension(fileName);
            if (!string.IsNullOrWhiteSpace(e)) return e.ToLowerInvariant();
        }

        // Đoán theo magic bytes
        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
                return ".pdf"; // %PDF
            if (bytes[0] == 0x89 && bytes[1] == 0x50) return ".png";
            if (bytes[0] == 0xFF && bytes[1] == 0xD8) return ".jpg";
            if (bytes[0] == 0x47 && bytes[1] == 0x49) return ".gif";
        }

        return type switch
        {
            "pdf" => ".pdf",
            "image" => ".png",
            "html" => ".html",
            "xml" => ".xml",
            _ => ".pdf"
        };
    }

    private static string XsltTransform(string xml, string xslt)
    {
        var transform = new XslCompiledTransform();
        using (var xsltReader = XmlReader.Create(new StringReader(xslt)))
            transform.Load(xsltReader);

        using var xmlReader = XmlReader.Create(new StringReader(xml));
        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, transform.OutputSettings))
            transform.Transform(xmlReader, writer);
        return sb.ToString();
    }

    private static bool LooksLikeXml(string s)
    {
        s = s.TrimStart();
        return s.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanBase64(string s)
    {
        // Bỏ tiền tố data URI nếu có: "data:application/pdf;base64,...."
        var idx = s.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) s = s[(idx + 7)..];
        return s.Trim();
    }

    private static void CleanupOld()
    {
        try
        {
            var cutoff = DateTime.Now.AddHours(-2);
            foreach (var f in Directory.EnumerateFiles(TempDir))
                if (File.GetCreationTime(f) < cutoff)
                    File.Delete(f);
        }
        catch { /* bỏ qua lỗi dọn dẹp */ }
    }
}
