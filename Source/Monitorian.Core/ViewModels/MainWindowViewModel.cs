using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

using Monitorian.Core.Models;
using Monitorian.Core.Views;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace Monitorian.Core.ViewModels;

internal class RelayCommand : System.Windows.Input.ICommand
{
	private readonly Action<object> _execute;
	private readonly Func<object, bool> _canExecute;

	public event EventHandler CanExecuteChanged;

	public RelayCommand(Action execute, Func<bool> canExecute = null)
	{
		_execute = _ => execute();
		_canExecute = _ => canExecute?.Invoke() ?? true;
	}

	public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute ?? (_ => true);
	}

	public bool CanExecute(object parameter) => _canExecute(parameter);

	public void Execute(object parameter) => _execute(parameter);

	public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

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

		// Initialize profile names from settings
		RefreshProfileNames();

		// Refresh when settings change profiles
		Settings.BrightnessProfiles.CollectionChanged += (_, _) => RefreshProfileNames();
	}

	public ObservableCollection<string> ProfileNames { get; } = new();

	private string _selectedProfileName;
	public string SelectedProfileName
	{
		get => _selectedProfileName;
        set
		{
			if (SetProperty(ref _selectedProfileName, value))
			{
				_loadProfileCommand?.RaiseCanExecuteChanged();
				_deleteProfileCommand?.RaiseCanExecuteChanged();
			}
		}
	}

	public ICommand LoadProfileCommand => _loadProfileCommand ??= new RelayCommand(LoadProfile, () => !string.IsNullOrWhiteSpace(SelectedProfileName));
	private RelayCommand _loadProfileCommand;

	public ICommand SaveProfileCommand => _saveProfileCommand ??= new RelayCommand(SaveProfile, () => true);
	private RelayCommand _saveProfileCommand;

	public ICommand DeleteProfileCommand => _deleteProfileCommand ??= new RelayCommand(DeleteProfile, () => !string.IsNullOrWhiteSpace(SelectedProfileName));
	private RelayCommand _deleteProfileCommand;

	private void RefreshProfileNames()
	{
		ProfileNames.Clear();
		foreach (var kv in Settings.BrightnessProfiles)
		{
			ProfileNames.Add(kv.Key);
		}
	}

	private void LoadProfile()
	{
		if (string.IsNullOrWhiteSpace(SelectedProfileName))
			return;

		if (!Settings.BrightnessProfiles.TryGetValue(SelectedProfileName, out var profile))
			return;

		// Apply profile values to monitors
		foreach (var kv in profile.Values)
		{
			var monitor = _controller.Monitors.FirstOrDefault(x => string.Equals(x.DeviceInstanceId, kv.Key, StringComparison.OrdinalIgnoreCase));
			if (monitor is not null)
			{
				// Clamp and set brightness
				int b = Math.Max(0, Math.Min(100, kv.Value));
				monitor.SetBrightness(b);
			}
		}
	}

	private void SaveProfile()
	{
        // Prompt for name if none selected
		var name = SelectedProfileName;
		if (string.IsNullOrWhiteSpace(name))
		{
            var prompt = new Views.InputDialog("Enter profile name", "Profile name:");
			if (prompt.ShowDialog() is not true)
				return;
			name = prompt.ResponseText;
		}

		if (string.IsNullOrWhiteSpace(name))
			return;

		// If exists, confirm overwrite
		if (Settings.BrightnessProfiles.ContainsKey(name))
		{
			var result = MessageBox.Show($"Profile '{name}' already exists. Overwrite?", "Confirm overwrite", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
		}

		var values = new Dictionary<string, int>();
		foreach (var m in _controller.Monitors)
			values[m.DeviceInstanceId] = m.Brightness;

		var profile = new BrightnessProfile(name, values);
		Settings.BrightnessProfiles.Add(name, profile);
		RefreshProfileNames();
		SelectedProfileName = name;
	}

	private void DeleteProfile()
	{
		var name = SelectedProfileName;
		if (string.IsNullOrWhiteSpace(name))
			return;

		var result = MessageBox.Show($"Delete profile '{name}'?", "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
		if (result != MessageBoxResult.Yes)
			return;

		Settings.BrightnessProfiles.Remove(name);
		RefreshProfileNames();
		SelectedProfileName = null;
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