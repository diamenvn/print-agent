# Print Agent (C# / .NET 8)

App nền chạy trên **Windows**, mở web server ở `localhost` để web client của bạn
gọi tới, sau đó **in trực tiếp ra máy in** — không hiện hộp thoại in, chọn được
**máy in** và **khổ giấy A4 / A5** (và A3, A6, Letter, Legal, Custom).

In được: **PDF, HTML, XML (kèm XSLT), ảnh (PNG/JPG/GIF), text** — nhờ engine
**WebView2** (Edge/Chromium) render và in im lặng.

---

## 1. Yêu cầu

- **Windows 10 (bản mới) hoặc Windows 11**
- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- **WebView2 Runtime** — thường đã có sẵn trên Win11 / Win10 cập nhật.
  Nếu thiếu, tải Evergreen Runtime:
  https://developer.microsoft.com/microsoft-edge/webview2/

> Lưu ý: dự án chỉ build/chạy được trên **Windows** (dùng WinForms + WebView2 +
> System.Drawing.Printing). Không build trên macOS/Linux.

---

## 2. Chạy thử

Từ thư mục `PrintAgent`:

```bash
dotnet restore
dotnet run
```

Agent chạy tại `http://127.0.0.1:9100`.
Mở trình duyệt vào địa chỉ đó → có sẵn **trang client demo** để test in A4/A5,
in HTML, và upload PDF/ảnh/XML để in.

Kiểm tra nhanh:

```bash
curl http://127.0.0.1:9100/health
curl http://127.0.0.1:9100/printers
```

---

## 3. API

### `GET /health`
Kiểm tra agent sống.

### `GET /printers`
Trả về máy in mặc định và danh sách máy in đã cài:
```json
{ "defaultPrinter": "HP LaserJet", "printers": ["HP LaserJet", "Microsoft Print to PDF"] }
```

### `POST /print`
Gửi lệnh in. Body JSON — đưa nội dung theo **một** trong các cách:
`url`, `contentBase64`, hoặc `content`.

| Trường          | Kiểu    | Ý nghĩa |
|-----------------|---------|---------|
| `type`          | string  | `pdf` \| `html` \| `xml` \| `image` \| `text` \| `url` \| `auto` |
| `url`           | string  | In thẳng từ URL (http/https/file) |
| `contentBase64` | string  | File nhị phân (PDF/ảnh) mã hóa Base64 |
| `content`       | string  | Nội dung text: HTML / XML / text |
| `xslt`          | string  | (tùy chọn) biến đổi XML → HTML |
| `fileName`      | string  | Gợi ý đuôi file, vd `hoadon.pdf` |
| `paperSize`     | string  | `A4` \| `A5` \| `A3` \| `A6` \| `Letter` \| `Legal` \| `Custom` |
| `widthMm`/`heightMm` | number | Kích thước khi `Custom` |
| `marginMm`      | number  | Lề (mm), mặc định 10 |
| `orientation`   | string  | `portrait` \| `landscape` |
| `printer`       | string  | Tên máy in; bỏ trống = mặc định |
| `copies`        | number  | Số bản, mặc định 1 |
| `printBackground` | bool  | In nền, mặc định true |

**Ví dụ — in HTML khổ A5:**
```bash
curl -X POST http://127.0.0.1:9100/print \
  -H "Content-Type: application/json" \
  -d '{"type":"html","paperSize":"A5","content":"<h1>Xin chào</h1>"}'
```

**Ví dụ — in PDF (Base64) ra máy in cụ thể, khổ A4, 2 bản:**
```json
{
  "type": "pdf",
  "paperSize": "A4",
  "printer": "HP LaserJet",
  "copies": 2,
  "fileName": "hoadon.pdf",
  "contentBase64": "JVBERi0xLjQK..."
}
```

**Ví dụ gọi từ web client (JavaScript):**
```js
await fetch('http://127.0.0.1:9100/print', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ type: 'html', paperSize: 'A5', content: '<h1>...</h1>' })
});
```

---

## 4. Bảo mật

- Agent chỉ nghe ở `127.0.0.1` (không lộ ra mạng LAN).
- Bật token: đặt biến môi trường trước khi chạy, client phải gửi header
  `X-Print-Token`:
  ```bat
  set PRINT_AGENT_TOKEN=matkhau-bi-mat
  dotnet run
  ```
- Đổi cổng/URL: `set PRINT_AGENT_URL=http://127.0.0.1:8888`

### Mixed content (client chạy HTTPS)
Nếu web client của bạn chạy `https://`, trình duyệt sẽ **chặn** gọi tới
`http://127.0.0.1`. Cách xử lý:
- Chạy client qua `http://` (nếu là app nội bộ), **hoặc**
- Cấp chứng chỉ SSL cho `localhost`/`127.0.0.1` và cho agent nghe `https`.

---

## 5. Đóng gói & tự chạy cùng Windows

### Publish 1 thư mục chạy độc lập
```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```
Chạy: `publish\PrintAgent.exe`

### Tự khởi động khi đăng nhập (khuyên dùng)
Vì WebView2 và việc in cần **phiên desktop của người dùng**, nên chạy agent
như **ứng dụng khởi động** (Startup / Task Scheduler khi logon), **không** nên
chạy như Windows Service (session 0 dễ lỗi in/WebView2).

Cách nhanh: tạo shortcut tới `PrintAgent.exe` bỏ vào thư mục
`shell:startup` (mở Run → gõ `shell:startup`).

---

## 6. Hạn chế & mở rộng

- **Word/Excel (.docx/.xlsx)** không được WebView2 render trực tiếp. Hai hướng:
  1. Chuyển sang PDF trước (LibreOffice headless / thư viện) rồi gửi PDF.
  2. Thêm nhánh dùng verb `printto` của Windows cho các định dạng có app xử lý.
- Muốn in **giữ đúng khay giấy A5 riêng**: WebView2 chọn kích thước giấy;
  nếu cần chỉ định **khay (PaperSource)**, có thể bổ sung nhánh in bằng
  `System.Drawing.Printing` cho PDF/ảnh. Nói mình nếu bạn cần.
