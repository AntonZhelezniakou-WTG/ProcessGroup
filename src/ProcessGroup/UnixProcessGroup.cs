using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ProcessGroup;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("freebsd")]
sealed class UnixProcessGroup : IProcessGroupImpl
{
	const int SIGTERM = 15;
	const int EPERM = 1;
	const int ESRCH = 3;

	readonly List<Process> _processes = [];
	int _pgid;

	public Process StartAndAdd(ProcessStartInfo startInfo)
	{
		var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start process.");

		int pid = process.Id;

		if (_pgid == 0)
			_pgid = pid;

		int result = setpgid(pid, _pgid);
		if (result != 0)
		{
			int err = Marshal.GetLastPInvokeError();
			if (err is not (ESRCH or EPERM))
				throw new InvalidOperationException(
					$"setpgid({pid}, {_pgid}) failed with errno {err}.");
		}

		_processes.Add(process);
		return process;
	}

	public void Add(Process process)
	{
		int pid = process.Id;

		if (_pgid == 0)
			_pgid = pid;

		_ = setpgid(pid, _pgid);
		_processes.Add(process);
	}

	public void TerminateAll()
	{
		if (_pgid != 0)
			_ = kill(-_pgid, SIGTERM);
	}

	public void Dispose()
	{
		TerminateAll();

		foreach (var process in _processes)
		{
			try
			{
				if (!process.HasExited && !process.WaitForExit(2000))
					process.Kill(entireProcessTree: true);
			}
			catch (InvalidOperationException)
			{
			}
		}
	}

	[DllImport("libc", SetLastError = true)]
	static extern int setpgid(int pid, int pgid);

	[DllImport("libc", SetLastError = true)]
	static extern int kill(int pid, int sig);
}
