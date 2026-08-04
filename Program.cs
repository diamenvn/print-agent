using System.Drawing.Printing;
using PrintAgent;

// ============================================================================
//  PRINT AGENT — web server localhost, in trực tiếp PDF/HTML/XML/ảnh/text.
// ============================================================================

const string DefaultUrl = "http://127.0.0.1:9100";
var listenUrl = Environment.GetEnvironmentVariable("PRINT_AGENT_URL") ?? DefaultUrl;

// Token bảo mật (tùy chọn). Nếu đặt biến môi trường PRINT_AGENT_TOKEN,
// client phải gửi header  X-Print-Token: <token>.
var requiredToken = Environment.GetEnvironmentVariable("PRINT_AGENT_TOKEN");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Cho phép body lớn (file PDF/ảnh base64 có thể nặng). Mặc định Kestrel ~28MB.
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 512L * 1024 * 1024; // 512 MB
});

// Cho phép mọi origin gọi tới (agent chỉ nghe ở localhost).
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

// Khởi động engine WebView2 (ẩn) trước khi phục vụ request.
var engine = new PrintEngine();
try
{
    await engine.InitializeAsync();
    app.Logger.LogInformation("WebView2 engine đã sẵn sàng.");
}
catch (Exception ex)
{
    app.Logger.LogError(ex,
        "Không khởi tạo được WebView2. Hãy cài 'WebView2 Runtime' " +
        "(https://developer.microsoft.com/microsoft-edge/webview2/).");
    return;
}

// ----- Middleware kiểm tra token (nếu bật) -----
app.Use(async (ctx, next) =>
{
    if (!string.IsNullOrEmpty(requiredToken) &&
        ctx.Request.Path.StartsWithSegments("/print"))
    {
        var token = ctx.Request.Headers["X-Print-Token"].ToString();
        if (token != requiredToken)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Token không hợp lệ." });
            return;
        }
    }
    await next();
});

// ----- Endpoints -----

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "PrintAgent",
    time = DateTime.Now
}));

app.MapGet("/printers", (bool? all) =>
{
    var infos = PrinterService.GetPrinters();
    var def = infos.FirstOrDefault(p => p.IsDefault)?.Name ?? "";

    // Mặc định chỉ trả về máy in VẬT LÝ. Thêm ?all=true để lấy cả máy in ảo.
    var visible = (all == true) ? infos : infos.Where(p => p.Physical).ToList();

    return Results.Ok(new
    {
        defaultPrinter = def,
        printers = visible.Select(p => new
        {
            name = p.Name,
            port = p.Port,
            physical = p.Physical,
            offline = p.Offline,
            isDefault = p.IsDefault
        })
    });
});

app.MapPost("/print", async (PrintRequest req) =>
{
    try
    {
        var uri = await ContentResolver.ResolveAsync(req);
        var opts = PrintSettingsFactory.Build(req);
        await engine.PrintAsync(uri, opts);
        return Results.Ok(new
        {
            success = true,
            printer = opts.Printer ?? new PrinterSettings().PrinterName,
            paperSize = req.PaperSize ?? "A4"
        });
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { success = false, error = "ContentBase64 không hợp lệ." });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
    }
});

// Phục vụ trang client demo (wwwroot/index.html) tại "/".
app.UseDefaultFiles();
app.UseStaticFiles();

app.Logger.LogInformation("Print Agent đang chạy tại {Url}", listenUrl);
app.Run(listenUrl);
