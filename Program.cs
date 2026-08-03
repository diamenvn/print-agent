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

app.MapGet("/printers", () =>
{
    var printers = new List<string>();
    foreach (string p in PrinterSettings.InstalledPrinters) printers.Add(p);
    var def = new PrinterSettings().PrinterName;
    return Results.Ok(new { defaultPrinter = def, printers });
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
