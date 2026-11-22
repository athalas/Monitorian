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
	public void IsMonitorSleepAllowed_ReturnsFalseOnlyForTheWindowMonitor()
	{
		Assert.IsTrue(MonitorViewModel.IsMonitorSleepAllowed(PrimaryRect, SecondaryRect)); // Window on another monitor
		Assert.IsTrue(MonitorViewModel.IsMonitorSleepAllowed(SecondaryRect, PrimaryRect)); // Window on another monitor
		Assert.IsFalse(MonitorViewModel.IsMonitorSleepAllowed(PrimaryRect, PrimaryRect)); // Window on this monitor
		Assert.IsFalse(MonitorViewModel.IsMonitorSleepAllowed(SecondaryRect, SecondaryRect)); // Window on this monitor
		Assert.IsTrue(MonitorViewModel.IsMonitorSleepAllowed(PrimaryRect, Rect.Empty)); // Window location unknown
		Assert.IsTrue(MonitorViewModel.IsMonitorSleepAllowed(Rect.Empty, PrimaryRect)); // Monitor location unknown
	}

	[TestMethod]
	public void IsSleepModeToggleEnabledCore_EnablesOnlyWhenSupportedAndSleepAllowed()
	{
		Assert.IsTrue(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: true, isSleepAllowed: true));
		Assert.IsFalse(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: true, isSleepAllowed: false));
		Assert.IsFalse(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: false, isSleepAllowed: true));
		Assert.IsFalse(MonitorViewModel.IsSleepModeToggleEnabledCore(isSleepModeSupported: false, isSleepAllowed: false));
	}
}
