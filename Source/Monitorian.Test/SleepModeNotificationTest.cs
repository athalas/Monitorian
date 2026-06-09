using System;
using System.Collections.ObjectModel;
using System.Windows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Monitorian.Core;
using Monitorian.Core.Models;
using Monitorian.Core.Models.Monitor;
using Monitorian.Core.ViewModels;

namespace Monitorian.Test;

[TestClass]
public class SleepModeNotificationTest
{
	[TestMethod]
	public void IsPrimaryMonitorRect_ReturnsTrueOnlyForThePrimaryScreenBounds()
	{
		Assert.IsTrue(MonitorViewModel.IsPrimaryMonitorRect(new Rect(0, 0, 1920, 1080), new System.Drawing.Rectangle(0, 0, 1920, 1080)));
		Assert.IsFalse(MonitorViewModel.IsPrimaryMonitorRect(new Rect(1921, 0, 1920, 1080), new System.Drawing.Rectangle(0, 0, 1920, 1080)));
		Assert.IsFalse(MonitorViewModel.IsPrimaryMonitorRect(Rect.Empty, new System.Drawing.Rectangle(0, 0, 1920, 1080)));
	}

	[TestMethod]
	public void IsSleepModeEnabled_RaisesPropertyChangedForTheBoundProperty()
	{
		var app = new Application();
		try
		{
			var controller = new AppControllerCore(new AppKeeper(), new SettingsCore());
			var viewModel = new MonitorViewModel(controller, new FakeMonitor());
			string raisedProperty = null;

			viewModel.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(MonitorViewModel.IsSleepModeEnabled))
					raisedProperty = e.PropertyName;
			};

			viewModel.IsSleepModeEnabled = true;

			Assert.AreEqual(nameof(MonitorViewModel.IsSleepModeEnabled), raisedProperty);
		}
		finally
		{
			Application.Current?.Shutdown();
		}
	}

	private sealed class FakeMonitor : IMonitor
	{
		public string DeviceInstanceId => "DISPLAY\\FAKE\\1";
		public string Description => "Fake monitor";
		public byte DisplayIndex => 1;
		public byte MonitorIndex => 1;
		public Rect MonitorRect => new Rect(0, 0, 1920, 1080);
		public ConnectionType Connection => ConnectionType.DisplayPort;
		public bool IsInternal => false;
		public bool IsReachable => true;
		public bool IsBrightnessSupported => true;
		public bool IsContrastSupported => false;
		public bool IsPowerModeSupported => true;

		public int Brightness => 50;
		public int BrightnessSystemAdjusted => 50;
		public int Contrast => 0;

		public AccessResult UpdateBrightness(int value = -1) => AccessResult.Succeeded;
		public AccessResult SetBrightness(int brightness) => AccessResult.Succeeded;
		public AccessResult UpdateContrast() => AccessResult.Succeeded;
		public AccessResult SetContrast(int contrast) => AccessResult.Succeeded;
		public (AccessResult result, ValueData data) GetValue(byte code) => (AccessResult.Succeeded, new ValueData(1, new byte[] { 1, 2, 3, 4 }));
		public (AccessResult result, ValueData data) SetValue(byte code, int value) => (AccessResult.Succeeded, new ValueData((byte)value, new byte[] { (byte)value }));
		public void Dispose() { }
	}
}
