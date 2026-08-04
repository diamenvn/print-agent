using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PrintAgent;

/// <summary>
/// Engine in dùng WebView2 (Edge/Chromium) chạy ẩn trên một STA thread riêng.
/// Render được PDF, HTML, XML, ảnh, text... rồi in IM LẶNG ra máy in chỉ định
/// với khổ giấy tùy chọn (A4/A5/...). Các job được xử lý tuần tự.
/// </summary>
public sealed class PrintEngine : IDisposable
{
    private Form _form = null!;
    private WebView2 _web = null!;
    private CoreWebView2Environment _env = null!;
    private Thread _uiThread = null!;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Khởi động thread UI ẩn và WebView2. Chờ tới khi sẵn sàng.</summary>
    public Task InitializeAsync()
    {
        _uiThread = new Thread(UiThreadMain)
        {
            IsBackground = true,
            Name = "PrintAgent-WebView2-UI"
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        return _ready.Task;
    }

    private void UiThreadMain()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        _form = new Form
        {
            // Ẩn hoàn toàn: trong suốt, ngoài màn hình, không hiện taskbar.
            Opacity = 0,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-4000, -4000),
            Width = 1200,
            Height = 1600,
        };
        _web = new WebView2 { Dock = DockStyle.Fill };
        _form.Controls.Add(_web);

        _form.Shown += async (_, _) =>
        {
            try
            {
                var userData = Path.Combine(Path.GetTempPath(), "PrintAgentWV2");
                _env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
                await _web.EnsureCoreWebView2Async(_env);
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _ready.TrySetResult();
            }
            catch (Exception ex)
            {
                _ready.TrySetException(ex);
            }
        };

        Application.Run(_form); // vòng lặp message giữ WebView2 sống
    }

    /// <summary>In một tài liệu (đã ở dạng Uri) theo tham số cho trước.</summary>
    public async Task PrintAsync(Uri uri, PrintOptions opt)
    {
        await _lock.WaitAsync();
        try
        {
            await InvokeAsync(async () =>
            {
                // --- Điều hướng và chờ tải xong ---
                var navTcs = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                void OnNav(object? s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _web.CoreWebView2.NavigationCompleted -= OnNav;
                    navTcs.TrySetResult(e.IsSuccess);
                }

                _web.CoreWebView2.NavigationCompleted += OnNav;
                _web.CoreWebView2.Navigate(uri.ToString());

                var ok = await navTcs.Task.WaitAsync(TimeSpan.FromSeconds(60));
                if (!ok)
                    throw new InvalidOperationException($"Không tải được nội dung: {uri}");

                // Cho trình xem PDF / nội dung phức tạp thời gian render nốt.
                await Task.Delay(uri.AbsolutePath.EndsWith(".pdf",
                    StringComparison.OrdinalIgnoreCase) ? 800 : 300);

                // --- Cấu hình in ---
                var ps = _env.CreatePrintSettings();
                ps.ShouldPrintBackgrounds = opt.PrintBackground;
                ps.ShouldPrintHeaderAndFooter = false;
                if (!string.IsNullOrWhiteSpace(opt.Printer))
                    ps.PrinterName = opt.Printer;            // rỗng = máy in mặc định

                // Ép dùng khổ giấy TÙY CHỈNH đúng như ta gửi (tránh máy in
                // tự lấy khổ mặc định rồi xoay ngang nội dung).
                ps.MediaSize = CoreWebView2PrintMediaSize.Custom;
                ps.PageWidth = opt.WidthInch;                // khổ giấy (inch)
                ps.PageHeight = opt.HeightInch;
                ps.MarginTop = ps.MarginBottom = opt.MarginInch;
                ps.MarginLeft = ps.MarginRight = opt.MarginInch;
                ps.Orientation = opt.Landscape
                    ? CoreWebView2PrintOrientation.Landscape
                    : CoreWebView2PrintOrientation.Portrait;
                ps.ScaleFactor = opt.Scale;                  // fit to page (tỉ lệ)
                ps.Copies = opt.Copies;

                // --- In im lặng ---
                var status = await _web.CoreWebView2.PrintAsync(ps);
                if (status != CoreWebView2PrintStatus.Succeeded)
                    throw new InvalidOperationException(
                        $"In thất bại (status = {status}). " +
                        "Kiểm tra tên máy in và trạng thái máy in.");

                // Rời khỏi trang để giải phóng file tạm.
                _web.CoreWebView2.Navigate("about:blank");
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Chạy một hàm async trên UI thread của WebView2 và chờ hoàn tất.</summary>
    private Task InvokeAsync(Func<Task> func)
    {
        var tcs = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _form.BeginInvoke(new Action(async () =>
        {
            try { await func(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        }));

        return tcs.Task;
    }

    public void Dispose()
    {
        try { _form?.BeginInvoke(new Action(() => Application.ExitThread())); }
        catch { /* ignore */ }
    }
}
