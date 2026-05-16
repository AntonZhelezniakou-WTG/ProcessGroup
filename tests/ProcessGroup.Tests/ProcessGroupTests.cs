using System.Diagnostics;

namespace ProcessGroups.Tests;

public class ProcessGroupTests
{
	[Test]
	public void CreateAndDispose_DoesNotThrow()
	{
		using var group = new ProcessGroup();
	}

	[Test]
	public void DoubleDispose_DoesNotThrow()
	{
		var group = new ProcessGroup();
		group.Dispose();
		group.Dispose();
	}

	[Test]
	public void Start_ReturnsRunningProcess()
	{
		using var group = new ProcessGroup();

		var process = group.Start(LongRunningProcess());

		Assert.That(process.HasExited, Is.False);
	}

	[Test]
	public void Dispose_TerminatesStartedProcess()
	{
		Process process;
		using (var group = new ProcessGroup())
		{
			process = group.Start(LongRunningProcess());
			Assert.That(process.HasExited, Is.False);
		}

		Assert.That(process.WaitForExit(5_000), Is.True);
		Assert.That(process.HasExited, Is.True);
	}

	[Test]
	public void Dispose_TerminatesMultipleProcesses()
	{
		Process p1, p2;
		using (var group = new ProcessGroup())
		{
			p1 = group.Start(LongRunningProcess());
			p2 = group.Start(LongRunningProcess());
		}

		Assert.That(p1.WaitForExit(5_000), Is.True);
		Assert.That(p2.WaitForExit(5_000), Is.True);
	}

	[Test]
	public void TerminateAll_KillsAllProcesses()
	{
		using var group = new ProcessGroup();
		var p1 = group.Start(LongRunningProcess());
		var p2 = group.Start(LongRunningProcess());

		group.TerminateAll();

		Assert.That(p1.WaitForExit(5_000), Is.True);
		Assert.That(p2.WaitForExit(5_000), Is.True);
	}

	[Test]
	public void Add_ExistingProcess_IsTerminatedOnDispose()
	{
		var external = Process.Start(LongRunningProcess())!;
		try
		{
			Process started;
			using (var group = new ProcessGroup())
			{
				started = group.Start(LongRunningProcess());
				group.Add(external);
			}

			Assert.That(started.WaitForExit(5_000), Is.True);
			Assert.That(external.WaitForExit(5_000), Is.True);
		}
		finally
		{
			if (!external.HasExited)
			{
				external.Kill(entireProcessTree: true);
				external.WaitForExit();
			}
		}
	}

	[Test]
	public void Start_AfterDispose_ThrowsObjectDisposedException()
	{
		var group = new ProcessGroup();
		group.Dispose();

		Assert.Throws<ObjectDisposedException>(() => group.Start(LongRunningProcess()));
	}

	[Test]
	public void Add_AfterDispose_ThrowsObjectDisposedException()
	{
		var group = new ProcessGroup();
		group.Dispose();

		using var process = Process.Start(LongRunningProcess())!;
		try
		{
			Assert.Throws<ObjectDisposedException>(() => group.Add(process));
		}
		finally
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
				process.WaitForExit();
			}
		}
	}

	[Test]
	public void TerminateAll_AfterDispose_ThrowsObjectDisposedException()
	{
		var group = new ProcessGroup();
		group.Dispose();

		Assert.Throws<ObjectDisposedException>(() => group.TerminateAll());
	}

	[Test]
	public void Add_NullProcess_ThrowsArgumentNullException()
	{
		using var group = new ProcessGroup();

		Assert.Throws<ArgumentNullException>(() => group.Add(null!));
	}

	[Test]
	public void Start_NullStartInfo_ThrowsArgumentNullException()
	{
		using var group = new ProcessGroup();

		Assert.Throws<ArgumentNullException>(() => group.Start(null!));
	}

	[Test]
	public void Start_WithCancelledToken_Throws()
	{
		using var group = new ProcessGroup();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		Assert.Throws<OperationCanceledException>(() => group.Start(LongRunningProcess(), cts.Token));
	}

	[Test]
	public void Start_CancellationAfterStart_KillsProcess()
	{
		using var group = new ProcessGroup();
		using var cts = new CancellationTokenSource();

		var process = group.Start(LongRunningProcess(), cts.Token);
		Assert.That(process.HasExited, Is.False);

		cts.Cancel();

		Assert.That(process.WaitForExit(5_000), Is.True);
	}

	[Test]
	public async Task DisposeAsync_TerminatesStartedProcess()
	{
		Process process;
		await using (var group = new ProcessGroup())
		{
			process = group.Start(LongRunningProcess());
			Assert.That(process.HasExited, Is.False);
		}

		Assert.That(process.WaitForExit(5_000), Is.True);
	}

	[Test]
	public async Task DisposeAsync_DoubleCall_DoesNotThrow()
	{
		var group = new ProcessGroup();
		await group.DisposeAsync();
		await group.DisposeAsync();
	}

	[Test]
	public void GetStats_OnEmptyGroup_ReturnsZeros()
	{
		using var group = new ProcessGroup();

		var stats = group.GetStats();

		Assert.That(stats.ActiveProcessCount, Is.Zero);
	}

	[Test]
	public void GetStats_WithActiveProcess_ReturnsAtLeastOne()
	{
		using var group = new ProcessGroup();
		group.Start(LongRunningProcess());

		var stats = group.GetStats();

		Assert.That(stats.ActiveProcessCount, Is.GreaterThanOrEqualTo(1));
	}

	[Test]
	public void GetStats_AfterDispose_Throws()
	{
		var group = new ProcessGroup();
		group.Dispose();

		Assert.Throws<ObjectDisposedException>(() => group.GetStats());
	}

	static ProcessStartInfo LongRunningProcess()
		=> OperatingSystem.IsWindows()
			? new ProcessStartInfo("ping", ["-n", "9999", "127.0.0.1"]) {
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
			}
			: new ProcessStartInfo("sleep", ["9999"]) {
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
			};
}
