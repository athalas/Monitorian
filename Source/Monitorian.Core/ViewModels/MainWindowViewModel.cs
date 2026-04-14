using System;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

using Monitorian.Core.Models;
using Monitorian.Core.Views;

namespace Monitorian.Core.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
	private readonly AppControllerCore _controller;
	public SettingsCore Settings => _controller.Settings;

	public MainWindowViewModel(AppControllerCore controller)
	{
		this._controller = controller ?? throw new ArgumentNullException(nameof(controller));
		this._controller.ScanningChanged += OnScanningChanged;
		Settings.PropertyChanged += OnSettingsChanged;
       _controller.Monitors.CollectionChanged += OnMonitorsCollectionChanged;
		HookMonitorSleepHandlers(_controller.Monitors);
	}

	public static string RestoreAllMonitorsShortcutText => MonitorSleepShortcut.DisplayText;

	public ListCollectionView MonitorsView
	{
		get
		{
			if (_monitorsView is null)
			{
				_monitorsView = new ListCollectionView(_controller.Monitors);
				if (Settings.SortsArrangement)
				{
					_monitorsView.SortDescriptions.Add(new SortDescription(nameof(MonitorViewModel.MonitorTopLeft), ListSortDirection.Ascending));
					_monitorsView.IsLiveSorting = true;
					_monitorsView.LiveSortingProperties.Add(nameof(MonitorViewModel.MonitorTopLeft));
				}
				_monitorsView.SortDescriptions.Add(new SortDescription(nameof(MonitorViewModel.DisplayIndex), ListSortDirection.Ascending));
				_monitorsView.SortDescriptions.Add(new SortDescription(nameof(MonitorViewModel.MonitorIndex), ListSortDirection.Ascending));
				_monitorsView.Filter = x => ((MonitorViewModel)x).IsTarget;
				_monitorsView.IsLiveFiltering = true;
				_monitorsView.LiveFilteringProperties.Add(nameof(MonitorViewModel.IsTarget));

				((INotifyCollectionChanged)_monitorsView).CollectionChanged += OnCollectionChanged;
			}
			return _monitorsView;
		}
	}

	public void ToggleAllSleepMode()
	{
		IsAllSleepModeEnabled = !(IsAllSleepModeEnabled ?? false);
	}

 private void RefreshAllSleepModeState() => OnPropertyChanged(nameof(IsAllSleepModeEnabled));
	private ListCollectionView _monitorsView;

	private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			//case NotifyCollectionChangedAction.Reset:
			case NotifyCollectionChangedAction.Add:
			case NotifyCollectionChangedAction.Remove:
				OnPropertyChanged(nameof(IsMonitorsEmpty));

				if (MonitorsView.CurrentItem is null)
				{
					// CollectionView.CurrentItem is automatically synchronized with SelectedItem
					// when the target is an ItemsControl. However, this synchronization is not
					// always fast enough to check if any item is currently selected.
					if (!MonitorsView.Cast<MonitorViewModel>().Any(x => x.IsSelected))
					{
						var monitor = MonitorsView.Cast<MonitorViewModel>()
							.FirstOrDefault(x => ReferenceEquals(x, _controller.SelectedMonitor));
						monitor?.IsSelected = true;
					}
				}
				break;
		}
	}

	public bool IsMonitorsEmpty => MonitorsView.IsEmpty;

	public bool? IsAllSleepModeEnabled
	{
		get
		{
			var monitors = _controller.Monitors.ToArray();
			if (monitors.Length == 0)
				return false;

			var enabledCount = monitors.Count(x => x.IsSleepModeEnabled);
			return enabledCount switch
			{
				0 => false,
				_ when enabledCount == monitors.Length => true,
				_ => null
			};
		}
		set
		{
			if (value is true)
			{
				_controller.SetAllMonitorsSleepMode(true);
               RefreshAllSleepModeState();
			}
			else if (value is false)
			{
				_controller.RestoreAllMonitors();
               RefreshAllSleepModeState();
			}
		}
	}

	private void OnScanningChanged(object sender, bool e)
	{
		IsScanning = e;
		OnPropertyChanged(nameof(IsScanning));
	}

	private void OnMonitorsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
			case NotifyCollectionChangedAction.Replace:
				HookMonitorSleepHandlers(e.NewItems.Cast<MonitorViewModel>());
				if (e.OldItems is not null)
					UnhookMonitorSleepHandlers(e.OldItems.Cast<MonitorViewModel>());
				break;

			case NotifyCollectionChangedAction.Remove:
				if (e.OldItems is not null)
					UnhookMonitorSleepHandlers(e.OldItems.Cast<MonitorViewModel>());
				break;

			case NotifyCollectionChangedAction.Reset:
				UnhookMonitorSleepHandlers();
				HookMonitorSleepHandlers(_controller.Monitors);
				break;
		}

		OnPropertyChanged(nameof(IsAllSleepModeEnabled));
	}

	private void OnMonitorPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MonitorViewModel.IsSleepModeEnabled))
			OnPropertyChanged(nameof(IsAllSleepModeEnabled));
	}

	private void HookMonitorSleepHandlers(System.Collections.Generic.IEnumerable<MonitorViewModel> monitors)
	{
		foreach (var monitor in monitors)
		{
			monitor.PropertyChanged += OnMonitorPropertyChanged;
		}
	}

	private void UnhookMonitorSleepHandlers(System.Collections.Generic.IEnumerable<MonitorViewModel> monitors = null)
	{
		monitors ??= _controller.Monitors;

		foreach (var monitor in monitors)
		{
			monitor.PropertyChanged -= OnMonitorPropertyChanged;
		}
	}

	public bool IsScanning { get; private set; }

	private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(Settings.SortsArrangement):
				var description = new SortDescription(nameof(MonitorViewModel.MonitorTopLeft), ListSortDirection.Ascending);
				int index = MonitorsView.SortDescriptions.IndexOf(description);

				switch (Settings.SortsArrangement, index)
				{
					case (true, < 0):
						MonitorsView.SortDescriptions.Insert(0, description);
						MonitorsView.IsLiveSorting = true;
						MonitorsView.LiveSortingProperties.Add(description.PropertyName);
						break;

					case (false, >= 0):
						MonitorsView.SortDescriptions.RemoveAt(index);
						MonitorsView.IsLiveSorting = false;
						MonitorsView.LiveSortingProperties.Remove(description.PropertyName);
						break;
				}

				MonitorsView.Refresh();
				break;
		}
	}

	internal void Deactivate()
	{
		var monitor = MonitorsView.Cast<MonitorViewModel>().FirstOrDefault(x => x.IsSelectedByKey);
		monitor?.IsByKey = false;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_controller.ScanningChanged -= OnScanningChanged;
			Settings.PropertyChanged -= OnSettingsChanged;
			_controller.Monitors.CollectionChanged -= OnMonitorsCollectionChanged;
			UnhookMonitorSleepHandlers();

			if (_monitorsView is not null)
				((INotifyCollectionChanged)_monitorsView).CollectionChanged -= OnCollectionChanged;
		}

		base.Dispose(disposing);
	}
}