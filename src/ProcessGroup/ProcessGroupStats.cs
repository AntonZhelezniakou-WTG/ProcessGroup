namespace ProcessGroup;

public readonly record struct ProcessGroupStats(
	int ActiveProcessCount,
	TimeSpan TotalCpuTime,
	long PeakMemoryBytes);
