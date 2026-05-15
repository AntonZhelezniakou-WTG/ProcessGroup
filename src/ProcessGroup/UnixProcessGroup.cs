using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static ProcessGroup.Libc;

namespace ProcessGroup;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("freebsd")]
sealed class UnixProcessGroup : IProcessGroupImpl
{
	readonly List<Process> _processes = [];
	int _pgid;

	public Process StartAndAdd(ProcessStartInfo startInfo)
	{
		var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start process.");

		var pid = process.Id;

		if (_pgid == 0)
			_pgid = pid;

		_processes.Add(process);

		var result = setpgid(pid, _pgid);
		if (result != 0)
		{
			var err = Marshal.GetLastPInvokeError();
			if (err is not (ESRCH or EPERM or EACCES))
				throw new InvalidOperationException(
					$"setpgid({pid}, {_pgid}) failed with errno {err}.");
		}

		return process;
	}

	public void Add(Process process)
	{
		var pid = process.Id;

		if (_pgid == 0)
			_pgid = pid;

		var result = setpgid(pid, _pgid);
		if (result != 0)
		{
			var err = Marshal.GetLastPInvokeError();
			if (err is not (ESRCH or EPERM or EACCES))
				throw new InvalidOperationException(
					$"setpgid({pid}, {_pgid}) failed with errno {err}.");
		}

		_processes.Add(process);
	}

	public void TerminateAll()
	{
		if (_pgid != 0)
			_ = kill(-_pgid, SIGTERM);
	}

	public ProcessGroupStats GetStats()
	{
		var active = 0;
		var cpu = TimeSpan.Zero;
		long peakMem = 0;

		foreach (var process in _processes)
		{
			try
			{
				if (process.HasExited)
					continue;

				process.Refresh();
				active++;
				cpu += process.TotalProcessorTime;
				peakMem = Math.Max(peakMem, process.PeakWorkingSet64);
			}
			catch (InvalidOperationException)
			{
			}
		}

		return new ProcessGroupStats(active, cpu, peakMem);
	}

	public void Dispose()
	{
		TerminateAll();

		var start = Stopwatch.GetTimestamp();
		var timeout = TimeSpan.FromSeconds(2);
		foreach (var process in _processes)
		{
			try
			{
				var remaining = timeout - Stopwatch.GetElapsedTime(start);
				var remainingMs = remaining > TimeSpan.Zero ? (int)remaining.TotalMilliseconds : 0;
				if (!process.HasExited && !process.WaitForExit(remainingMs))
					process.Kill(entireProcessTree: true);
			}
			catch (InvalidOperationException)
			{
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		TerminateAll();

		var start = Stopwatch.GetTimestamp();
		var timeout = TimeSpan.FromSeconds(2);
		foreach (var process in _processes)
		{
			try
			{
				if (process.HasExited)
					continue;

				var remaining = timeout - Stopwatch.GetElapsedTime(start);
				if (remaining > TimeSpan.Zero)
				{
					using var cts = new CancellationTokenSource(remaining);
					try
					{
						await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
					}
				}

				if (!process.HasExited)
					process.Kill(entireProcessTree: true);
			}
			catch (InvalidOperationException)
			{
			}
		}
	}
}
