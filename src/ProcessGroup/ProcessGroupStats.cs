namespace ProcessGroups;

public readonly record struct ProcessGroupStats(
	int ActiveProcessCount,
	TimeSpan TotalCpuTime,
	long PeakMemoryBytes);
