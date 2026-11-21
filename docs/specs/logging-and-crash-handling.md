# Logging and Crash Handling

One subject: asynchronous file logging, crash-time log recovery, runtime dump collection, the out-of-process crash supervisor, and the client log counters.

## Overview

Every launcher starts through `CrashSupervisor.Run` (`Shoal/CrashHandling/CrashSupervisor.cs:23`).
The supervisor runs in a parent process and spawns the application as a child process with `--crash-watched`.
This isolates crash recovery from managed heap corruption, stack overflows, and native faults in OpenGL, Jolt, or audio drivers.

Bypass conditions:

- The child process already carries `--crash-watched`.
- The user passes `--no-crash-handler`.
- A debugger is attached (`Debugger.IsAttached`).
- `CrashHandler.Enabled` is `false` in `config/logging.toml`.

## Steam compatibility

The supervisor keeps the parent process alive until the child exits so Steam tracks the full process tree.
The child inherits all environment variables and CLI arguments verbatim.
The supervisor never initializes graphics or OpenGL so the Steam Overlay hooks the child window directly.
Termination signals (`SIGINT`, `SIGTERM`) forward to the child.
On Windows, a `WindowsJobObject` with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` prevents orphaned children.

## File logging and flush guarantees

`AppEngine` replaces third-party file logging with `AsyncFileLoggerProvider` (`Shoal/Logging/AsyncFileLoggerProvider.cs:10`).

- Normal entries (`Trace`, `Debug`, `Information`) enqueue onto a non-blocking queue.
- A background `LogWriter` thread writes to `<LogsRoot>/<yyyy-MM-dd_HHmm-ss>.log` and `<LogsRoot>/latest.log` (`AppPaths.LogsRoot`).
- The writer flushes at least every `FlushIntervalMs` (default `200` ms).
- Entries at or above `SyncFlushMinLevel` (default `Error`) synchronously drain and flush the writer before `Log` returns.
- `AppEngine.FlushLogs` flushes buffered entries on `AppEngine.Dispose`, `ValidateContainerOrDie`, `ProcessExit`, and `UnhandledException`.
- Old timestamped log files prune to `MaxLogFiles` on startup.

## Ring buffer and crash recovery

When supervised, `AsyncFileLoggerProvider` mirrors every formatted UTF-8 log entry into a file-backed `MemoryMappedFile` (`LogRingBuffer`, `Shoal/Logging/LogRingBuffer.cs:8`).
The buffer tracks two monotonic byte offsets: `WriteSequence` and `FlushedSequence`.
The OS kernel retains mapped pages even on instantaneous native crashes (`SIGSEGV`, `AccessViolationException`, `StackOverflowException`, `Environment.FailFast`).

All crash artifacts write to `AppPaths.CrashesRoot` (`<DataRoot>/crashes`), matching the configured application data directory (`--user-data`, `--portable`, local user app data, or server working directory).

When the child exits with a non-zero exit code (and not via supervisor-requested shutdown):

1. The supervisor reads `FlushedSequence..WriteSequence` from the ring buffer and appends those bytes to both the session log and `<LogsRoot>/latest.log`.
2. The supervisor decodes the exit code via `CrashExitDecoder` (Windows NTSTATUS codes and Unix signals).
3. The supervisor loads `CrashSessionMetadata` (engine state, GPU vendor/renderer/version, loaded modules, faulting thread, and unhandled exception text).
4. `CrashReportWriter` writes `<CrashesRoot>/crash_<yyyy-MM-dd_HHmm-ss>.log` combining system info, metadata, exception details, captured `stderr` tail, dump path, and session log.
5. `CrashReportWriter` appends a crash footer to both `<LogsRoot>/<timestamp>.log` and `<LogsRoot>/latest.log`.
6. Old crash reports and dumps prune to `MaxCrashReports` and `MaxCrashDumps`.
7. Temporary `.session_*` files are deleted.

## Runtime crash dumps

When `CrashHandler.CollectDumps` is `true`, the supervisor sets these environment variables on the child process before launch:

- `DOTNET_DbgEnableMiniDump=1`
- `DOTNET_DbgMiniDumpType` (`1` = `Mini`, `2` = `WithHeap`, `3` = `Triage`, `4` = `Full`)
- `DOTNET_DbgMiniDumpName` (`crashes/crash_<timestamp>.dmp`)
- `DOTNET_EnableCrashReport=1`

`AppDomain.CurrentDomain.UnhandledException` logs and flushes without swallowing termination, so the .NET runtime still writes a dump on unhandled managed exceptions as well as native crashes.

## Crash alert dialog

Headless hosts (`CrashHandlerOptions.Headless = true`, such as `WaywardBeyond.Server.Launcher`) print the crash report path to `stderr` and exit.

Non-headless hosts display a native OS window (`CrashAlertDialog`, `Shoal/CrashHandling/UI/CrashAlertDialog.cs:8`) that does not touch OpenGL:

- Windows uses a native Win32 window (`Win32CrashAlertDialog`).
- Linux uses native GTK3 via `libgtk-3.so.0` (`GtkCrashAlertDialog`) on both Wayland and X11, falling back to `stderr` when no display or GTK3 library is present.
- The dialog shows the fault summary, selectable crash log and dump paths, a scrollable monospace log tail (`AlertTailLines` and `AlertTailMaxChars`), an **Open Folder** button, and a **Close** button.
- When `CrashHandlerOptions.OnSendReportAsync` is configured (as in `WaywardBeyond.Client.Launcher`), the dialog also shows optional **Contact** (max 30 chars) and **Description** (max 500 chars) inputs plus a **Send Report** button. Clicking **Send Report** works with or without user text.

## Client log counters

The client keeps a running total of warning, error, and critical log entries. `LogCounters` (`WaywardBeyond.Client/Diagnostics/LogCounters.cs:9`) subscribes to `LogListener.NewLog` and increments thread-safe counters. `GetSnapshot` returns the current `LogCountersSnapshot`.

Two surfaces read the snapshot:

- `LogCountersOverlay` (`WaywardBeyond.Client/UI/LogCountersOverlay.cs:10`) adds a `LOGS` block to the F3 debug overlay. It shows all three totals.
- `LogCountersBadge` (`WaywardBeyond.Client/UI/Layers/LogCountersBadge.cs:9`) draws non-zero totals in the bottom-right corner. The client registers it only in `DEBUG` builds (`WaywardBeyond.Client/Injector.cs:282`).

The badge sits left of the control hints, so it does not cover the hint pills.

## Configuration

All tunables live in `logging.toml`, resolved from `AppPaths.ConfigRoot` first and falling back to shipped defaults. See [config-schemas](config-schemas.md#loggingtoml-per-app).

## Source of truth

- `Shoal/Logging/LoggingConfig.cs`
- `Shoal/Logging/CrashHandlerConfig.cs`
- `Shoal/Logging/LogRingBuffer.cs`
- `Shoal/Logging/AsyncFileLoggerProvider.cs`
- `Shoal/AppEngine.cs`
- `Shoal/CrashHandling/CrashSupervisor.cs`
- `Shoal/CrashHandling/CrashReportWriter.cs`
- `Shoal/CrashHandling/CrashExitDecoder.cs`
- `Shoal/CrashHandling/UI/CrashAlertDialog.cs`
- `Shoal/CrashHandling/UI/Win32CrashAlertDialog.cs`
- `Shoal/CrashHandling/UI/GtkCrashAlertDialog.cs`
- `WaywardBeyond.Data/Webhooks/FeedbackWebhook.cs`
- `WaywardBeyond.Client/Diagnostics/LogCounters.cs`
- `WaywardBeyond.Client/UI/LogCountersOverlay.cs`
- `WaywardBeyond.Client/UI/Layers/LogCountersBadge.cs`
- `WaywardBeyond.Client.Launcher/Application.cs`
- `WaywardBeyond.Server.Launcher/Program.cs`

## Tests that pin this

- `Swordfish.Tests/LoggingAndCrashHandlingTests.cs`
- `Swordfish.Tests/LogCountersTests.cs`
- `Swordfish.Tests/FeedbackWebhookTests.cs`
- `Swordfish.Tests/OverlayViewTests.cs`
