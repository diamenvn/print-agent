namespace PrintAgent;

/// <summary>
/// Yêu cầu in gửi từ web client xuống agent.
/// Chỉ cần đưa nội dung theo MỘT trong các cách: Url, ContentBase64, hoặc Content (raw).
/// </summary>
public class PrintRequest
{
    /// <summary>pdf | html | xml | image | text | url | auto (mặc định auto).</summary>
    public string? Type { get; set; }

    /// <summary>In từ URL trực tiếp (http/https/file). Ưu tiên cao nhất.</summary>
    public string? Url { get; set; }

    /// <summary>Nội dung file nhị phân (PDF, ảnh...) mã hóa Base64.</summary>
    public string? ContentBase64 { get; set; }

    /// <summary>Nội dung dạng text thuần: HTML, XML, hoặc text.</summary>
    public string? Content { get; set; }

    /// <summary>XSLT (tùy chọn) để biến đổi XML -> HTML trước khi in.</summary>
    public string? Xslt { get; set; }

    /// <summary>Tên file gợi ý để đoán phần mở rộng (vd "hoadon.pdf").</summary>
    public string? FileName { get; set; }

    // ----- Cấu hình in -----

    /// <summary>A4 | A5 | A3 | Letter | Legal | Custom. Mặc định A4.</summary>
    public string? PaperSize { get; set; }

    /// <summary>Chiều rộng (mm) khi PaperSize = Custom.</summary>
    public double? WidthMm { get; set; }

    /// <summary>Chiều cao (mm) khi PaperSize = Custom.</summary>
    public double? HeightMm { get; set; }

    /// <summary>Lề (mm). Mặc định ~10mm.</summary>
    public double? MarginMm { get; set; }

    /// <summary>portrait | landscape. Mặc định portrait.</summary>
    public string? Orientation { get; set; }

    /// <summary>Tên máy in. Bỏ trống = máy in mặc định của Windows.</summary>
    public string? Printer { get; set; }

    /// <summary>Số bản in. Mặc định 1.</summary>
    public int? Copies { get; set; }

    /// <summary>In màu nền/hình nền. Mặc định true.</summary>
    public bool? PrintBackground { get; set; }

    /// <summary>Tỉ lệ in theo phần trăm (fit to page). 100 = giữ nguyên. Mặc định 100.</summary>
    public double? Scale { get; set; }
}

/// <summary>Tham số in đã chuẩn hóa (đơn vị inch) truyền xuống engine.</summary>
public record PrintOptions
{
    public double WidthInch { get; init; }
    public double HeightInch { get; init; }
    public double MarginInch { get; init; } = 0.4;
    public bool Landscape { get; init; }
    public string? Printer { get; init; }
    public int Copies { get; init; } = 1;
    public bool PrintBackground { get; init; } = true;
    public double Scale { get; init; } = 1.0;   // hệ số cho ScaleFactor (0.1–2.0)
}
