using System.Runtime.InteropServices;
using System.Text;
using Swordfish.Library.Util;

namespace Shoal.CrashHandling.UI;

/// <summary>
///     Native Win32 crash alert window with scrollable log tail and optional report submission inputs.
/// </summary>
internal static class Win32CrashAlertDialog
{
    private const uint WS_OVERLAPPED = 0x00000000;
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_SYSMENU = 0x00080000;
    private const uint WS_MINIMIZEBOX = 0x00020000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_CHILD = 0x40000000;
    private const uint WS_VSCROLL = 0x00200000;
    private const uint WS_HSCROLL = 0x00100000;
    private const uint WS_TABSTOP = 0x00010000;
    private const uint WS_EX_CLIENTEDGE = 0x00000200;

    private const uint ES_MULTILINE = 0x0004;
    private const uint ES_AUTOVSCROLL = 0x0040;
    private const uint ES_AUTOHSCROLL = 0x0080;
    private const uint ES_READONLY = 0x0800;
    private const uint ES_WANTRETURN = 0x1000;

    private const uint BS_PUSHBUTTON = 0x0000;
    private const uint BS_DEFPUSHBUTTON = 0x0001;

    private const uint WM_DESTROY = 0x0002;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_SETFONT = 0x0030;
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_APP = 0x8000;
    private const uint WM_REPORT_DONE = WM_APP + 1;

    private const uint EM_SETLIMITTEXT = 0x00C5;
    private const int SW_SHOWNORMAL = 1;
    private const int CW_USEDEFAULT = unchecked((int)0x80000000);
    private const int COLOR_BTNFACE = 15;
    private const int IDC_ARROW = 32512;

    private const int ID_BTN_OPEN_FOLDER = 1001;
    private const int ID_BTN_SEND_REPORT = 1002;
    private const int ID_BTN_CLOSE = 1003;

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    ///     Shows the Win32 crash alert window and blocks until the user closes it.
    /// </summary>
    public static bool TryShow(CrashReportContext report, CrashHandlerOptions options)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var state = new DialogState(report, options);
        return state.Run();
    }

    private sealed class DialogState(CrashReportContext report, CrashHandlerOptions options)
    {
        private readonly CrashReportContext _report = report;
        private readonly CrashHandlerOptions _options = options;
        private readonly bool _canSend = options.OnSendReportAsync != null;
        private readonly CancellationTokenSource _cts = new();

        private WndProcDelegate? _wndProc;
        private IntPtr _hwnd;
        private IntPtr _contactEdit;
        private IntPtr _descriptionEdit;
        private IntPtr _statusLabel;
        private IntPtr _sendButton;
        private IntPtr _uiFont;
        private IntPtr _monoFont;
        private volatile bool _sending;
        private volatile bool _closed;
        private string _pendingStatus = string.Empty;
        private bool _pendingSuccess;

        public bool Run()
        {
            string className = $"ShoalCrashDialog_{Guid.NewGuid():N}";
            IntPtr hInstance = GetModuleHandleW(null);
            _wndProc = WindowProc;

            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = hInstance,
                hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW),
                hbrBackground = (IntPtr)(COLOR_BTNFACE + 1),
                lpszClassName = className,
            };

            if (RegisterClassExW(ref wc) == 0)
            {
                return false;
            }

            try
            {
                int width = 760;
                int height = _canSend ? 640 : 470;

                _hwnd = CreateWindowExW(
                    0,
                    className,
                    $"{_report.AppName} — Crash Report",
                    WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
                    CW_USEDEFAULT,
                    CW_USEDEFAULT,
                    width,
                    height,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    hInstance,
                    IntPtr.Zero
                );

                if (_hwnd == IntPtr.Zero)
                {
                    return false;
                }

                _uiFont = CreateFontW(-14, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
                _monoFont = CreateFontW(-13, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Consolas");

                BuildControls(hInstance);
                ShowWindow(_hwnd, SW_SHOWNORMAL);
                UpdateWindow(_hwnd);

                while (GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) > 0)
                {
                    if (!IsDialogMessageW(_hwnd, ref msg))
                    {
                        TranslateMessage(ref msg);
                        DispatchMessageW(ref msg);
                    }
                }

                return true;
            }
            finally
            {
                _closed = true;
                _cts.Cancel();
                _cts.Dispose();
                if (_uiFont != IntPtr.Zero)
                {
                    DeleteObject(_uiFont);
                }

                if (_monoFont != IntPtr.Zero)
                {
                    DeleteObject(_monoFont);
                }

                UnregisterClassW(className, hInstance);
            }
        }

        private void BuildControls(IntPtr hInstance)
        {
            int margin = 16;
            int contentWidth = 712;
            int y = 14;

            string headline = $"{_report.AppName} crashed unexpectedly ({_report.FaultSummary}).";
            CreateLabel(hInstance, headline, margin, y, contentWidth, 22);
            y += 24;

            string pathText = !string.IsNullOrWhiteSpace(_report.DumpPath)
                ? $"Crash log: {_report.ReportPath}\r\nCrash dump: {_report.DumpPath}"
                : $"Crash log: {_report.ReportPath}";

            IntPtr pathBox = CreateWindowExW(
                0,
                "EDIT",
                pathText,
                WS_CHILD | WS_VISIBLE | ES_MULTILINE | ES_READONLY | ES_AUTOHSCROLL,
                margin,
                y,
                contentWidth,
                !string.IsNullOrWhiteSpace(_report.DumpPath) ? 38 : 20,
                _hwnd,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero
            );
            SetFont(pathBox, _uiFont);
            y += (!string.IsNullOrWhiteSpace(_report.DumpPath) ? 44 : 26);

            CreateLabel(hInstance, "Log tail (truncated):", margin, y, contentWidth, 18);
            y += 20;

            string logText = _report.LogTail.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
            IntPtr logView = CreateWindowExW(
                WS_EX_CLIENTEDGE,
                "EDIT",
                logText,
                WS_CHILD | WS_VISIBLE | WS_VSCROLL | WS_HSCROLL | ES_MULTILINE | ES_READONLY | ES_AUTOVSCROLL | ES_AUTOHSCROLL,
                margin,
                y,
                contentWidth,
                250,
                _hwnd,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero
            );
            SetFont(logView, _monoFont);
            y += 260;

            if (_canSend)
            {
                CreateLabel(hInstance, "Contact (optional — Discord/email, max 30 chars):", margin, y, contentWidth, 18);
                y += 20;

                _contactEdit = CreateWindowExW(
                    WS_EX_CLIENTEDGE,
                    "EDIT",
                    string.Empty,
                    WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL,
                    margin,
                    y,
                    contentWidth,
                    24,
                    _hwnd,
                    IntPtr.Zero,
                    hInstance,
                    IntPtr.Zero
                );
                SetFont(_contactEdit, _uiFont);
                SendMessageW(_contactEdit, EM_SETLIMITTEXT, (IntPtr)CrashAlertDialog.MAX_CONTACT_LENGTH, IntPtr.Zero);
                y += 32;

                CreateLabel(hInstance, "What were you doing? (optional, max 500 chars):", margin, y, contentWidth, 18);
                y += 20;

                _descriptionEdit = CreateWindowExW(
                    WS_EX_CLIENTEDGE,
                    "EDIT",
                    string.Empty,
                    WS_CHILD | WS_VISIBLE | WS_TABSTOP | WS_VSCROLL | ES_MULTILINE | ES_AUTOVSCROLL | ES_WANTRETURN,
                    margin,
                    y,
                    contentWidth,
                    68,
                    _hwnd,
                    IntPtr.Zero,
                    hInstance,
                    IntPtr.Zero
                );
                SetFont(_descriptionEdit, _uiFont);
                SendMessageW(_descriptionEdit, EM_SETLIMITTEXT, (IntPtr)CrashAlertDialog.MAX_DESCRIPTION_LENGTH, IntPtr.Zero);
                y += 76;

                _statusLabel = CreateLabel(hInstance, string.Empty, margin, y, contentWidth, 20);
                y += 24;
            }

            int buttonY = y + 4;
            IntPtr openBtn = CreateButton(hInstance, "Open Folder", ID_BTN_OPEN_FOLDER, margin, buttonY, 120, 30, BS_PUSHBUTTON);
            SetFont(openBtn, _uiFont);

            if (_canSend)
            {
                _sendButton = CreateButton(hInstance, "Send Report", ID_BTN_SEND_REPORT, margin + contentWidth - 230, buttonY, 120, 30, BS_DEFPUSHBUTTON);
                SetFont(_sendButton, _uiFont);
            }

            IntPtr closeBtn = CreateButton(hInstance, "Close", ID_BTN_CLOSE, margin + contentWidth - 100, buttonY, 100, 30, _canSend ? BS_PUSHBUTTON : BS_DEFPUSHBUTTON);
            SetFont(closeBtn, _uiFont);
        }

        private IntPtr CreateLabel(IntPtr hInstance, string text, int x, int y, int width, int height)
        {
            IntPtr handle = CreateWindowExW(
                0,
                "STATIC",
                text,
                WS_CHILD | WS_VISIBLE,
                x,
                y,
                width,
                height,
                _hwnd,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero
            );
            SetFont(handle, _uiFont);
            return handle;
        }

        private IntPtr CreateButton(IntPtr hInstance, string text, int id, int x, int y, int width, int height, uint style)
        {
            return CreateWindowExW(
                0,
                "BUTTON",
                text,
                WS_CHILD | WS_VISIBLE | WS_TABSTOP | style,
                x,
                y,
                width,
                height,
                _hwnd,
                (IntPtr)id,
                hInstance,
                IntPtr.Zero
            );
        }

        private IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_COMMAND:
                    int notifyCode = (int)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (notifyCode == 0)
                    {
                        int commandId = (int)(wParam.ToInt64() & 0xFFFF);
                        HandleCommand(commandId);
                    }

                    return IntPtr.Zero;

                case WM_REPORT_DONE:
                    _sending = false;
                    if (_closed)
                    {
                        return IntPtr.Zero;
                    }

                    if (_statusLabel != IntPtr.Zero)
                    {
                        SetWindowTextW(_statusLabel, _pendingStatus);
                    }

                    if (_sendButton != IntPtr.Zero)
                    {
                        EnableWindow(_sendButton, !_pendingSuccess);
                        SetWindowTextW(_sendButton, _pendingSuccess ? "Sent" : "Send Report");
                    }

                    return IntPtr.Zero;

                case WM_CLOSE:
                    DestroyWindow(hwnd);
                    return IntPtr.Zero;

                case WM_DESTROY:
                    PostQuitMessage(0);
                    return IntPtr.Zero;
            }

            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        private void HandleCommand(int commandId)
        {
            switch (commandId)
            {
                case ID_BTN_OPEN_FOLDER:
                    CrashAlertDialog.OpenCrashFolder(_report.CrashDirectory);
                    break;

                case ID_BTN_SEND_REPORT:
                    BeginSendReport();
                    break;

                case ID_BTN_CLOSE:
                    DestroyWindow(_hwnd);
                    break;
            }
        }

        private void BeginSendReport()
        {
            if (_sending || _options.OnSendReportAsync == null)
            {
                return;
            }

            _sending = true;
            if (_sendButton != IntPtr.Zero)
            {
                EnableWindow(_sendButton, false);
                SetWindowTextW(_sendButton, "Sending...");
            }

            if (_statusLabel != IntPtr.Zero)
            {
                SetWindowTextW(_statusLabel, "Sending crash report...");
            }

            string? contact = CrashAlertDialog.SanitizeContact(ReadWindowText(_contactEdit));
            string? description = CrashAlertDialog.SanitizeDescription(ReadWindowText(_descriptionEdit));
            var submission = new CrashReportSubmission(_report, description, contact);
            CancellationToken token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    Result<string> result = await _options.OnSendReportAsync(submission, token).ConfigureAwait(false);
                    _pendingSuccess = result.Success;
                    _pendingStatus = result.Success
                        ? (string.IsNullOrWhiteSpace(result.Value) ? "Crash report sent." : $"Crash report sent ({result.Value}).")
                        : $"Failed to send report: {result.Message}";
                }
                catch (Exception ex)
                {
                    _pendingSuccess = false;
                    _pendingStatus = $"Failed to send report: {ex.Message}";
                }

                if (!_closed && _hwnd != IntPtr.Zero)
                {
                    PostMessageW(_hwnd, WM_REPORT_DONE, IntPtr.Zero, IntPtr.Zero);
                }
            });
        }

        private static string ReadWindowText(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return string.Empty;
            }

            int length = GetWindowTextLengthW(hwnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(length + 1);
            GetWindowTextW(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static void SetFont(IntPtr hwnd, IntPtr font)
        {
            if (hwnd != IntPtr.Zero && font != IntPtr.Zero)
            {
                SendMessageW(hwnd, WM_SETFONT, font, (IntPtr)1);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam
    );

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool IsDialogMessageW(IntPtr hDlg, ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(
        int cHeight,
        int cWidth,
        int cEscapement,
        int cOrientation,
        int cWeight,
        uint bItalic,
        uint bUnderline,
        uint bStrikeOut,
        uint iCharSet,
        uint iOutPrecision,
        uint iClipPrecision,
        uint iQuality,
        uint iPitchAndFamily,
        string pszFaceName
    );

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);
}
