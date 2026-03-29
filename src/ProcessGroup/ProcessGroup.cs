using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessGroup;

public sealed class ProcessGroup : IDisposable
{
	readonly IProcessGroupImpl _impl;
	bool _disposed;

	public ProcessGroup()
	{
		if (OperatingSystem.IsWindows())
			_impl = new WindowsJobObject();
		else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
			_impl = new UnixProcessGroup();
		else
			throw new PlatformNotSupportedException(
				$"ProcessGroup is not supported on {RuntimeInformation.OSDescription}.");
	}

	public Process Start(ProcessStartInfo startInfo)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(startInfo);
		return _impl.StartAndAdd(startInfo);
	}

	public void Add(Process process)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(process);
		_impl.Add(process);
	}

	public void TerminateAll()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_impl.TerminateAll();
	}

	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;
		_impl.Dispose();
	}
}
