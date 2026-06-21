// Source - https://stackoverflow.com/a/9164742
// Posted by Alexander Yezutov, modified by community. See post 'Timeline' for change history
// Retrieved 2026-06-21, License - CC BY-SA 3.0

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WaywardBeyond.Server.Core.Interop.Windows;

public class Job : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr a, string? lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, UInt32 cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private IntPtr _handle;
    private bool _disposed;

    public Job()
    {
        _handle = CreateJobObject(IntPtr.Zero, null);

        var info = new JobObjectBasicLimitInformation
        {
            LimitFlags = 0x2000,
        };

        var extendedInfo = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = info,
        };

        int length = Marshal.SizeOf(typeof(JobObjectExtendedLimitInformation));
        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

        if (!SetInformationJobObject(_handle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
        {
            throw new Exception($"Unable to set information.  Error: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Close()
    {
        CloseHandle(_handle);
        _handle = IntPtr.Zero;
    }

    public bool AddProcess(IntPtr processHandle)
    {
        return AssignProcessToJobObject(_handle, processHandle);
    }

    public bool AddProcess(int processId)
    {
        return AddProcess(Process.GetProcessById(processId).Handle);
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct IOCounters
    {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }


    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IOCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9,
    }

}