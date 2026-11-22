using System.Windows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Monitorian.Core.ViewModels;

namespace Monitorian.Test;

[TestClass]
public class SleepModePolicyTest
{
	private static readonly Rect PrimaryRect = new(0, 0, 1920, 1080);
	private static readonly Rect SecondaryRect = new(1920, 0, 1920, 1080);

	[TestMethod]
	public void IsSameMonitorRect_ReturnsTrueOnlyForMatchingRects()
	{
		Assert.IsTrue(MonitorViewModel.IsSameMonitorRect(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1080)));
		Assert.IsTrue(MonitorViewModel.IsSameMonitorRect(new Rect(1920, 0, 1920, 1080), new System.Drawing.Rectangle(1920, 0, 1920, 1080)));
		Assert.IsFalse(MonitorViewModel.IsSameMonitorRect(new Rect(1921, 0, 1920, 1080), new Rect(0, 0, 1920, 1080)));
		Assert.IsFalse(MonitorViewModel.IsSameMonitorRect(new Rect(0, 0, 1920, 1081), new Rect(0, 0, 1920, 1080)));
		Assert.IsFalse(MonitorViewModel.IsSameMonitorRect(Rect.Empty, new Rect(0, 0, 1920, 1080)));
		Assert.IsFalse(MonitorViewModel.IsSameMonitorRect(new Rect(0, 0, 1920, 1080), Rect.Empty));
		Assert.IsFalse(MonitorViewModel.IsSameMonitorRect(new Rect(0, 0, 1920, 1080), System.Drawing.Rectangle.Empty));
	}

	[TestMethod]
	public void IsPrimaryMonitorSleepAllowed_ReturnsTrueOnlyForAwakeNonPrimaryWindowMonitor()
	{
		Assert.IsTrue(MonitorViewModel.IsPrimaryMonitorSleepAllowed(SecondaryRect, isWindowMonitorAsleep: false, PrimaryRect));
		Assert.IsFalse(MonitorViewModel.IsPrimaryMonitorSleepAllowed(PrimaryRect, isWindowMonitorAsleep: false, PrimaryRect)); // Window on the primary monitor
		Assert.IsFalse(MonitorViewModel.IsPrimaryMonitorSleepAllowed(SecondaryRect, isWindowMonitorAsleep: true, PrimaryRect)); // Window monitor asleep
		Assert.IsFalse(MonitorViewModel.IsPrimaryMonitorSleepAllowed(Rect.Empty, isWindowMonitorAsleep: false, PrimaryRect));
		Assert.IsFalse(MonitorViewModel.IsPrimaryMonitorSleepAllowed(SecondaryRect, isWindowMonitorAsleep: false, Rect.Empty));
	}

	[TestMethod]
	public void IsSleepModeToggleEnabledCore_EnablesPrimaryOnlyWhenAllowed()
	{
		Assert.IsTrue(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: true, isPrimaryMonitor: false, isPrimaryMonitorSleepAllowed: false));
		Assert.IsTrue(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: true, isPrimaryMonitor: true, isPrimaryMonitorSleepAllowed: true));
		Assert.IsFalse(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: true, isPrimaryMonitor: true, isPrimaryMonitorSleepAllowed: false));
		Assert.IsFalse(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: false, isPrimaryMonitor: false, isPrimaryMonitorSleepAllowed: false));
		Assert.IsFalse(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: false, isPrimaryMonitor: true, isPrimaryMonitorSleepAllowed: true));
	}
}
