using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using Monitorian.Core.Models;
using Monitorian.Core.Views;

namespace Monitorian.Core.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
	private readonly AppControllerCore _controller;
	public SettingsCore Settings => _controller.Settings;

	public MainWindowViewModel(AppControllerCore controller)
	{
		_controller = controller ?? throw new ArgumentNullException(nameof(controller));
		_controller.ScanningChanged += OnScanningChanged;
		Settings.PropertyChanged += OnSettingsChanged;
		_controller.Monitors.CollectionChanged += OnMonitorsCollectionChanged;
		HookMonitorSleepHandlers(_controller.Monitors);

		RefreshProfileNames();
		Settings.BrightnessProfiles.CollectionChanged += (_, _) => RefreshProfileNames();
	}

	public ObservableCollection<string> ProfileNames { get; } = new();
	public bool HasProfiles => ProfileNames.Count > 0;

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

	public ICommand NewProfileCommand => _newProfileCommand ??= new RelayCommand(CreateNewProfile, () => true);
	private RelayCommand _newProfileCommand;

	public ICommand DeleteProfileCommand => _deleteProfileCommand ??= new RelayCommand(DeleteProfile, () => !string.IsNullOrWhiteSpace(SelectedProfileName));
	private RelayCommand _deleteProfileCommand;

	private void RefreshProfileNames()
	{
		ProfileNames.Clear();
		foreach (var kv in Settings.BrightnessProfiles)
			ProfileNames.Add(kv.Key);
       OnPropertyChanged(nameof(HasProfiles));
		_loadProfileCommand?.RaiseCanExecuteChanged();
		_deleteProfileCommand?.RaiseCanExecuteChanged();
	}

	private void LoadProfile()
	{
		if (string.IsNullOrWhiteSpace(SelectedProfileName))
			return;

		if (!Settings.BrightnessProfiles.TryGetValue(SelectedProfileName, out var profile))
			return;

      foreach (var kv in profile.Values)
		{
			var monitor = _controller.Monitors.FirstOrDefault(x => string.Equals(x.DeviceInstanceId, kv.Key, StringComparison.OrdinalIgnoreCase));
			if (monitor is not null)
			{
				var brightness = Math.Max(0, Math.Min(100, kv.Value));
				monitor.SetBrightness(brightness);
			}
		}

      foreach (var kv in profile.SleepModeEnabledStates ?? Enumerable.Empty<KeyValuePair<string, bool>>())
		{
			var monitor = _controller.Monitors.FirstOrDefault(x => string.Equals(x.DeviceInstanceId, kv.Key, StringComparison.OrdinalIgnoreCase));
			// The monitor where the main window is cannot sleep.
			if (monitor is not null && _controller.IsSleepAllowed(monitor))
				monitor.IsSleepModeEnabled = kv.Value;
		}
	}

	private void SaveProfile()
	{
		var name = SelectedProfileName;
		if (string.IsNullOrWhiteSpace(name))
		{
			name = AskForProfileName();
			if (string.IsNullOrWhiteSpace(name))
				return;
		}

		SaveProfile(name);
	}

	private void CreateNewProfile()
	{
		var name = AskForProfileName();
		if (string.IsNullOrWhiteSpace(name))
			return;

		SaveProfile(name);
	}

	private void SaveProfile(string name)
	{

		if (Settings.BrightnessProfiles.ContainsKey(name))
		{
			var result = MessageBox.Show($"Profile '{name}' already exists. Overwrite?", "Confirm overwrite", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
		}

     var values = new Dictionary<string, int>();
		var sleepModeStates = new Dictionary<string, bool>();
		foreach (var monitor in _controller.Monitors)
     {
			values[monitor.DeviceInstanceId] = monitor.Brightness;
			sleepModeStates[monitor.DeviceInstanceId] = monitor.IsSleepModeEnabled;
		}

      Settings.BrightnessProfiles.Add(name, new BrightnessProfile(name, values)
		{
			SleepModeEnabledStates = sleepModeStates
		});
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

	private string AskForProfileName()
	{
      var dialog = new ProfileNameDialog("Enter profile name", "Profile name:");
		return dialog.ShowDialog() is true ? dialog.ResponseText : null;
	}

    public static string RestoreAllMonitorsShortcutText => Monitorian.Core.Views.MonitorSleepShortcut.DisplayText;

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

	public void ToggleAllSleepMode() => IsAllSleepModeEnabled = !(IsAllSleepModeEnabled ?? false);

	private void RefreshAllSleepModeState() => OnPropertyChanged(nameof(IsAllSleepModeEnabled));
	private ListCollectionView _monitorsView;

	private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
			case NotifyCollectionChangedAction.Remove:
				OnPropertyChanged(nameof(IsMonitorsEmpty));

				if (MonitorsView.CurrentItem is null && !MonitorsView.Cast<MonitorViewModel>().Any(x => x.IsSelected))
				{
					var monitor = MonitorsView.Cast<MonitorViewModel>().FirstOrDefault(x => ReferenceEquals(x, _controller.SelectedMonitor));
					monitor?.IsSelected = true;
				}
				break;
		}
	}

	public bool IsMonitorsEmpty => MonitorsView.IsEmpty;

	public bool? IsAllSleepModeEnabled
	{
		get
		{
			// The monitor where the main window is cannot sleep and so it is out of scope.
			var monitors = _controller.Monitors.Where(x => _controller.IsSleepAllowed(x)).ToArray();
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
		_controller.RefreshMonitorSleepToggleStates();
	}

	private void OnMonitorPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MonitorViewModel.IsSleepModeEnabled))
		{
			OnPropertyChanged(nameof(IsAllSleepModeEnabled));
			_controller.RefreshMonitorSleepToggleStates();
		}
	}

	private void HookMonitorSleepHandlers(IEnumerable<MonitorViewModel> monitors)
	{
		foreach (var monitor in monitors)
			monitor.PropertyChanged += OnMonitorPropertyChanged;
	}

	private void UnhookMonitorSleepHandlers(IEnumerable<MonitorViewModel> monitors = null)
	{
		monitors ??= _controller.Monitors;

		foreach (var monitor in monitors)
			monitor.PropertyChanged -= OnMonitorPropertyChanged;
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

	private sealed class ProfileNameDialog : Window
	{
		private readonly TextBox _textBox;
		public string ResponseText => _textBox.Text;

		public ProfileNameDialog(string title, string prompt)
		{
			Title = title;
			Width = 360;
			Height = 140;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			ResizeMode = ResizeMode.NoResize;
			WindowStyle = WindowStyle.ToolWindow;

			var grid = new Grid { Margin = new Thickness(8) };
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			grid.Children.Add(new TextBlock { Text = prompt });

			_textBox = new TextBox { Margin = new Thickness(0, 8, 0, 8) };
			Grid.SetRow(_textBox, 1);
			grid.Children.Add(_textBox);

			var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			Grid.SetRow(buttons, 2);
			var ok = new Button { Width = 80, Margin = new Thickness(4, 0, 4, 0), IsDefault = true, Content = "OK" };
			ok.Click += (_, __) => { DialogResult = true; Close(); };
			var cancel = new Button { Width = 80, Margin = new Thickness(4, 0, 4, 0), IsCancel = true, Content = "Cancel" };
			buttons.Children.Add(ok);
			buttons.Children.Add(cancel);
			grid.Children.Add(buttons);

			Content = grid;
		}
	}
}