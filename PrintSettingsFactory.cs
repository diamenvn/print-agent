namespace PrintAgent;

/// <summary>Chuyển PrintRequest -> PrintOptions (đơn vị inch cho WebView2).</summary>
public static class PrintSettingsFactory
{
    public static PrintOptions Build(PrintRequest r)
    {
        var (w, h) = (r.PaperSize?.Trim().ToUpperInvariant()) switch
        {
            "A5"     => (5.83, 8.27),
            "A4"     => (8.27, 11.69),
            "A3"     => (11.69, 16.54),
            "A6"     => (4.13, 5.83),
            "LETTER" => (8.5, 11.0),
            "LEGAL"  => (8.5, 14.0),
            "CUSTOM" => (Mm(r.WidthMm ?? 210), Mm(r.HeightMm ?? 297)),
            _        => (8.27, 11.69) // Mặc định A4
        };

        return new PrintOptions
        {
            WidthInch = w,
            HeightInch = h,
            Landscape = string.Equals(r.Orientation, "landscape",
                                      StringComparison.OrdinalIgnoreCase),
            Printer = string.IsNullOrWhiteSpace(r.Printer) ? null : r.Printer.Trim(),
            Copies = r.Copies is > 0 ? r.Copies.Value : 1,
            MarginInch = r.MarginMm.HasValue ? Mm(r.MarginMm.Value) : Mm(10),
            PrintBackground = r.PrintBackground ?? true,
        };
    }

    private static double Mm(double mm) => mm / 25.4;
}
