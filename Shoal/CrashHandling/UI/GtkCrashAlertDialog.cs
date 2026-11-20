using System.Runtime.InteropServices;
using Swordfish.Library.Util;

namespace Shoal.CrashHandling.UI;

/// <summary>
///     Native GTK3 crash alert window for Linux (X11 and Wayland) with scrollable log tail and optional report inputs.
/// </summary>
internal static class GtkCrashAlertDialog
{
    private const string LIB_GTK = "libgtk-3.so.0";
    private const string LIB_GOBJECT = "libgobject-2.0.so.0";
    private const string LIB_GLIB = "libglib-2.0.so.0";

    private const int GTK_ORIENTATION_HORIZONTAL = 0;
    private const int GTK_ORIENTATION_VERTICAL = 1;
    private const int GTK_WINDOW_TOPLEVEL = 0;
    private const int GTK_POLICY_AUTOMATIC = 1;
    private const int GTK_SHADOW_IN = 1;
    private const int GTK_WRAP_WORD_CHAR = 3;

    private delegate void SignalCallback(IntPtr widget, IntPtr userData);
    private delegate bool IdleCallback(IntPtr userData);

    /// <summary>
    ///     Shows the GTK3 crash alert window if a Linux graphical display and libgtk-3 are available.
    /// </summary>
    public static bool TryShow(CrashReportContext report, CrashHandlerOptions options)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return false;
        }

        if (!NativeLibrary.TryLoad(LIB_GTK, out IntPtr gtkHandle))
        {
            return false;
        }

        NativeLibrary.Free(gtkHandle);

        if (!gtk_init_check(IntPtr.Zero, IntPtr.Zero))
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
        private readonly List<Delegate> _pinnedCallbacks = [];
        private readonly CancellationTokenSource _cts = new();

        private IntPtr _window;
        private IntPtr _contactEntry;
        private IntPtr _descriptionBuffer;
        private IntPtr _statusLabel;
        private IntPtr _sendButton;
        private volatile bool _sending;
        private volatile bool _closed;

        public bool Run()
        {
            try
            {
                _window = gtk_window_new(GTK_WINDOW_TOPLEVEL);
                gtk_window_set_title(_window, $"{_report.AppName} — Crash Report");
                gtk_window_set_default_size(_window, 760, _canSend ? 640 : 460);
                gtk_container_set_border_width(_window, 16);

                IntPtr rootBox = gtk_box_new(GTK_ORIENTATION_VERTICAL, 8);
                gtk_container_add(_window, rootBox);

                IntPtr header = gtk_label_new($"{_report.AppName} crashed unexpectedly ({_report.FaultSummary}).");
                gtk_label_set_xalign(header, 0f);
                gtk_label_set_line_wrap(header, true);
                gtk_box_pack_start(rootBox, header, expand: false, fill: true, padding: 0);

                string pathSummary = !string.IsNullOrWhiteSpace(_report.DumpPath)
                    ? $"Crash log: {_report.ReportPath}\nCrash dump: {_report.DumpPath}"
                    : $"Crash log: {_report.ReportPath}";
                IntPtr pathLabel = gtk_label_new(pathSummary);
                gtk_label_set_xalign(pathLabel, 0f);
                gtk_label_set_selectable(pathLabel, true);
                gtk_label_set_line_wrap(pathLabel, true);
                gtk_box_pack_start(rootBox, pathLabel, expand: false, fill: true, padding: 0);

                IntPtr tailLabel = gtk_label_new("Log tail (truncated):");
                gtk_label_set_xalign(tailLabel, 0f);
                gtk_box_pack_start(rootBox, tailLabel, expand: false, fill: true, padding: 0);

                IntPtr scrolledLog = gtk_scrolled_window_new(IntPtr.Zero, IntPtr.Zero);
                gtk_scrolled_window_set_policy(scrolledLog, GTK_POLICY_AUTOMATIC, GTK_POLICY_AUTOMATIC);
                gtk_scrolled_window_set_shadow_type(scrolledLog, GTK_SHADOW_IN);
                gtk_widget_set_size_request(scrolledLog, -1, 240);

                IntPtr logView = gtk_text_view_new();
                gtk_text_view_set_editable(logView, false);
                gtk_text_view_set_monospace(logView, true);
                IntPtr logBuffer = gtk_text_view_get_buffer(logView);
                gtk_text_buffer_set_text(logBuffer, _report.LogTail, -1);

                gtk_container_add(scrolledLog, logView);
                gtk_box_pack_start(rootBox, scrolledLog, expand: true, fill: true, padding: 0);

                if (_canSend)
                {
                    IntPtr contactLabel = gtk_label_new("Contact (optional — Discord/email, max 30 chars):");
                    gtk_label_set_xalign(contactLabel, 0f);
                    gtk_box_pack_start(rootBox, contactLabel, expand: false, fill: true, padding: 0);

                    _contactEntry = gtk_entry_new();
                    gtk_entry_set_max_length(_contactEntry, CrashAlertDialog.MAX_CONTACT_LENGTH);
                    gtk_box_pack_start(rootBox, _contactEntry, expand: false, fill: true, padding: 0);

                    IntPtr descLabel = gtk_label_new("What were you doing? (optional, max 500 chars):");
                    gtk_label_set_xalign(descLabel, 0f);
                    gtk_box_pack_start(rootBox, descLabel, expand: false, fill: true, padding: 0);

                    IntPtr scrolledDesc = gtk_scrolled_window_new(IntPtr.Zero, IntPtr.Zero);
                    gtk_scrolled_window_set_policy(scrolledDesc, GTK_POLICY_AUTOMATIC, GTK_POLICY_AUTOMATIC);
                    gtk_scrolled_window_set_shadow_type(scrolledDesc, GTK_SHADOW_IN);
                    gtk_widget_set_size_request(scrolledDesc, -1, 72);

                    IntPtr descView = gtk_text_view_new();
                    gtk_text_view_set_wrap_mode(descView, GTK_WRAP_WORD_CHAR);
                    _descriptionBuffer = gtk_text_view_get_buffer(descView);

                    gtk_container_add(scrolledDesc, descView);
                    gtk_box_pack_start(rootBox, scrolledDesc, expand: false, fill: true, padding: 0);

                    _statusLabel = gtk_label_new(string.Empty);
                    gtk_label_set_xalign(_statusLabel, 0f);
                    gtk_box_pack_start(rootBox, _statusLabel, expand: false, fill: true, padding: 0);
                }

                IntPtr buttonRow = gtk_box_new(GTK_ORIENTATION_HORIZONTAL, 8);
                gtk_box_pack_start(rootBox, buttonRow, expand: false, fill: true, padding: 4);

                IntPtr openButton = gtk_button_new_with_label("Open Folder");
                ConnectSignal(openButton, "clicked", (_, _) => CrashAlertDialog.OpenCrashFolder(_report.CrashDirectory));
                gtk_box_pack_start(buttonRow, openButton, expand: false, fill: false, padding: 0);

                IntPtr closeButton = gtk_button_new_with_label("Close");
                ConnectSignal(closeButton, "clicked", (_, _) => gtk_widget_destroy(_window));
                gtk_box_pack_end(buttonRow, closeButton, expand: false, fill: false, padding: 0);

                if (_canSend)
                {
                    _sendButton = gtk_button_new_with_label("Send Report");
                    ConnectSignal(_sendButton, "clicked", (_, _) => BeginSendReport());
                    gtk_box_pack_end(buttonRow, _sendButton, expand: false, fill: false, padding: 0);
                }

                ConnectSignal(_window, "destroy", (_, _) => gtk_main_quit());

                gtk_widget_show_all(_window);
                gtk_main();
                return true;
            }
            finally
            {
                _closed = true;
                _cts.Cancel();
                _cts.Dispose();
                lock (_pinnedCallbacks)
                {
                    _pinnedCallbacks.Clear();
                }
            }
        }

        private void BeginSendReport()
        {
            if (_sending || _closed || _options.OnSendReportAsync == null)
            {
                return;
            }

            _sending = true;
            gtk_widget_set_sensitive(_sendButton, false);
            gtk_button_set_label(_sendButton, "Sending...");
            gtk_label_set_text(_statusLabel, "Sending crash report...");

            string? contact = CrashAlertDialog.SanitizeContact(ReadEntryText(_contactEntry));
            string? description = CrashAlertDialog.SanitizeDescription(ReadBufferText(_descriptionBuffer));
            var submission = new CrashReportSubmission(_report, description, contact);
            CancellationToken token = _cts.Token;

            _ = Task.Run(async () =>
            {
                bool success;
                string statusText;

                try
                {
                    Result<string> result = await _options.OnSendReportAsync(submission, token).ConfigureAwait(false);
                    success = result.Success;
                    statusText = result.Success
                        ? (string.IsNullOrWhiteSpace(result.Value) ? "Crash report sent." : $"Crash report sent ({result.Value}).")
                        : $"Failed to send report: {result.Message}";
                }
                catch (Exception ex)
                {
                    success = false;
                    statusText = $"Failed to send report: {ex.Message}";
                }

                if (_closed)
                {
                    _sending = false;
                    return;
                }

                IdleCallback? idle = null;
                idle = _ =>
                {
                    _sending = false;
                    if (!_closed)
                    {
                        gtk_label_set_text(_statusLabel, statusText);
                        gtk_widget_set_sensitive(_sendButton, !success);
                        gtk_button_set_label(_sendButton, success ? "Sent" : "Send Report");
                    }

                    lock (_pinnedCallbacks)
                    {
                        if (idle != null)
                        {
                            _pinnedCallbacks.Remove(idle);
                        }
                    }

                    return false;
                };

                lock (_pinnedCallbacks)
                {
                    if (_closed)
                    {
                        _sending = false;
                        return;
                    }

                    _pinnedCallbacks.Add(idle);
                }

                g_idle_add(idle, IntPtr.Zero);
            });
        }

        private void ConnectSignal(IntPtr instance, string signalName, SignalCallback callback)
        {
            lock (_pinnedCallbacks)
            {
                _pinnedCallbacks.Add(callback);
            }

            g_signal_connect_data(instance, signalName, callback, IntPtr.Zero, IntPtr.Zero, 0);
        }

        private static string ReadEntryText(IntPtr entry)
        {
            if (entry == IntPtr.Zero)
            {
                return string.Empty;
            }

            IntPtr ptr = gtk_entry_get_text(entry);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) ?? string.Empty : string.Empty;
        }

        private static string ReadBufferText(IntPtr buffer)
        {
            if (buffer == IntPtr.Zero)
            {
                return string.Empty;
            }

            var start = new GtkTextIter();
            var end = new GtkTextIter();
            gtk_text_buffer_get_start_iter(buffer, ref start);
            gtk_text_buffer_get_end_iter(buffer, ref end);
            IntPtr textPtr = gtk_text_buffer_get_text(buffer, ref start, ref end, includeHiddenChars: false);
            if (textPtr == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUTF8(textPtr) ?? string.Empty;
            }
            finally
            {
                g_free(textPtr);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct GtkTextIter
    {
        private fixed long _opaque[16];
    }

    [DllImport(LIB_GTK)]
    private static extern bool gtk_init_check(IntPtr argc, IntPtr argv);

    [DllImport(LIB_GTK)]
    private static extern void gtk_main();

    [DllImport(LIB_GTK)]
    private static extern void gtk_main_quit();

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_window_new(int type);

    [DllImport(LIB_GTK, CharSet = CharSet.Ansi)]
    private static extern void gtk_window_set_title(IntPtr window, [MarshalAs(UnmanagedType.LPUTF8Str)] string title);

    [DllImport(LIB_GTK)]
    private static extern void gtk_window_set_default_size(IntPtr window, int width, int height);

    [DllImport(LIB_GTK)]
    private static extern void gtk_container_set_border_width(IntPtr container, uint borderWidth);

    [DllImport(LIB_GTK)]
    private static extern void gtk_container_add(IntPtr container, IntPtr widget);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_box_new(int orientation, int spacing);

    [DllImport(LIB_GTK)]
    private static extern void gtk_box_pack_start(IntPtr box, IntPtr child, bool expand, bool fill, uint padding);

    [DllImport(LIB_GTK)]
    private static extern void gtk_box_pack_end(IntPtr box, IntPtr child, bool expand, bool fill, uint padding);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_label_new([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(LIB_GTK)]
    private static extern void gtk_label_set_text(IntPtr label, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(LIB_GTK)]
    private static extern void gtk_label_set_xalign(IntPtr label, float xalign);

    [DllImport(LIB_GTK)]
    private static extern void gtk_label_set_selectable(IntPtr label, bool setting);

    [DllImport(LIB_GTK)]
    private static extern void gtk_label_set_line_wrap(IntPtr label, bool wrap);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_scrolled_window_new(IntPtr hadjustment, IntPtr vadjustment);

    [DllImport(LIB_GTK)]
    private static extern void gtk_scrolled_window_set_policy(IntPtr scrolledWindow, int hscrollbarPolicy, int vscrollbarPolicy);

    [DllImport(LIB_GTK)]
    private static extern void gtk_scrolled_window_set_shadow_type(IntPtr scrolledWindow, int type);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_text_view_new();

    [DllImport(LIB_GTK)]
    private static extern void gtk_text_view_set_editable(IntPtr textView, bool setting);

    [DllImport(LIB_GTK)]
    private static extern void gtk_text_view_set_monospace(IntPtr textView, bool monospace);

    [DllImport(LIB_GTK)]
    private static extern void gtk_text_view_set_wrap_mode(IntPtr textView, int wrapMode);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_text_view_get_buffer(IntPtr textView);

    [DllImport(LIB_GTK)]
    private static extern void gtk_text_buffer_set_text(IntPtr buffer, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, int len);

    [DllImport(LIB_GTK)]
    private static extern void gtk_text_buffer_get_start_iter(IntPtr buffer, ref GtkTextIter iter);

    [DllImport(LIB_GTK)]
    private static extern void gtk_text_buffer_get_end_iter(IntPtr buffer, ref GtkTextIter iter);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_text_buffer_get_text(IntPtr buffer, ref GtkTextIter start, ref GtkTextIter end, bool includeHiddenChars);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_entry_new();

    [DllImport(LIB_GTK)]
    private static extern void gtk_entry_set_max_length(IntPtr entry, int max);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_entry_get_text(IntPtr entry);

    [DllImport(LIB_GTK)]
    private static extern IntPtr gtk_button_new_with_label([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

    [DllImport(LIB_GTK)]
    private static extern void gtk_button_set_label(IntPtr button, [MarshalAs(UnmanagedType.LPUTF8Str)] string label);

    [DllImport(LIB_GTK)]
    private static extern void gtk_widget_set_size_request(IntPtr widget, int width, int height);

    [DllImport(LIB_GTK)]
    private static extern void gtk_widget_set_sensitive(IntPtr widget, bool sensitive);

    [DllImport(LIB_GTK)]
    private static extern void gtk_widget_show_all(IntPtr widget);

    [DllImport(LIB_GTK)]
    private static extern void gtk_widget_destroy(IntPtr widget);

    [DllImport(LIB_GOBJECT)]
    private static extern ulong g_signal_connect_data(
        IntPtr instance,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string detailedSignal,
        SignalCallback cHandler,
        IntPtr data,
        IntPtr destroyData,
        int connectFlags
    );

    [DllImport(LIB_GLIB)]
    private static extern uint g_idle_add(IdleCallback function, IntPtr data);

    [DllImport(LIB_GLIB)]
    private static extern void g_free(IntPtr mem);
}
