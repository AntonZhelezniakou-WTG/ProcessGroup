using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ProcessGroup;

[SupportedOSPlatform("windows")]
sealed class WindowsJobObject : IProcessGroupImpl
{
	readonly nint _jobHandle;

	public WindowsJobObject()
	{
		_jobHandle = CreateJobObjectW(nint.Zero, null);
		if (_jobHandle == nint.Zero)
			throw new Win32Exception(Marshal.GetLastWin32Error());

		var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
		{
			BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
			{
				LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
			},
		};

		int len = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
		if (!SetInformationJobObject(_jobHandle, JobObjectInfoClass.ExtendedLimitInformation, ref info, len))
			throw new Win32Exception(Marshal.GetLastWin32Error());
	}

	public Process StartAndAdd(ProcessStartInfo startInfo)
	{
		var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start process.");
		AssignToJob(process);
		return process;
	}

	public void Add(Process process) => AssignToJob(process);

	public void TerminateAll() => TerminateJobObject(_jobHandle, 1);

	public void Dispose()
	{
		if (_jobHandle != nint.Zero)
			CloseHandle(_jobHandle);
	}

	void AssignToJob(Process process)
	{
		if (!AssignProcessToJobObject(_jobHandle, process.Handle))
			throw new Win32Exception(Marshal.GetLastWin32Error());
	}

	const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

	enum JobObjectInfoClass
	{
		ExtendedLimitInformation = 9,
	}

	[StructLayout(LayoutKind.Sequential)]
	struct JOBOBJECT_BASIC_LIMIT_INFORMATION
	{
		public long PerProcessUserTimeLimit;
		public long PerJobUserTimeLimit;
		public uint LimitFlags;
		public nuint MinimumWorkingSetSize;
		public nuint MaximumWorkingSetSize;
		public uint ActiveProcessLimit;
		public nint Affinity;
		public uint PriorityClass;
		public uint SchedulingClass;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct IO_COUNTERS
	{
		public ulong ReadOperationCount;
		public ulong WriteOperationCount;
		public ulong OtherOperationCount;
		public ulong ReadTransferCount;
		public ulong WriteTransferCount;
		public ulong OtherTransferCount;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
	{
		public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
		public IO_COUNTERS IoInfo;
		public nuint ProcessMemoryLimit;
		public nuint JobMemoryLimit;
		public nuint PeakProcessMemoryUsed;
		public nuint PeakJobMemoryUsed;
	}

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	static extern nint CreateJobObjectW(nint lpJobAttributes, string? lpName);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool SetInformationJobObject(
		nint hJob,
		JobObjectInfoClass infoClass,
		ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info,
		int cbInfoLength);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool TerminateJobObject(nint hJob, uint uExitCode);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool CloseHandle(nint hObject);
}
