using System.Diagnostics;

namespace ProcessGroup;

interface IProcessGroupImpl : IDisposable, IAsyncDisposable
{
	Process StartAndAdd(ProcessStartInfo startInfo);
	void Add(Process process);
	void TerminateAll();
	ProcessGroupStats GetStats();
}
