using System.Diagnostics;

namespace ProcessGroup;

interface IProcessGroupImpl : IDisposable
{
	Process StartAndAdd(ProcessStartInfo startInfo);
	void Add(Process process);
	void TerminateAll();
}
