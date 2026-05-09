using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

using Monitorian.Core.Collections;
using Monitorian.Core.Common;
using Monitorian.Core.Helper;
using Monitorian.Core.Views;

namespace Monitorian.Core.Models;

/// <summary>
/// Settings
/// </summary>
[DataContract]
public class SettingsCore : BindableBase
{
	#region Settings (persistent)

	/// <summary>
	/// Whether to use large elements
	/// </summary>
	[DataMember]
	public bool UsesLargeElements
	{
		get => _usesLargeElements;
		set => SetProperty(ref _usesLargeElements, value);
	}

    private bool _usesLargeElements = true; // Default

	/// <summary>
	/// Whether to use accent color for brightness
	/// </summary>
	[DataMember]
	public bool UsesAccentColor
	{
		get => _usesAccentColor;
		set => SetProperty(ref _usesAccentColor, value);
	}
	private bool _usesAccentColor;

	/// <summary>
	/// Whether to show adjusted brightness
	/// </summary>
	[DataMember]
	public bool ShowsAdjusted
	{
		get => _showsAdjusted;
		set => SetProperty(ref _showsAdjusted, value);
	}
	private bool _showsAdjusted = true; // default

	/// <summary>
	/// Whether to sort by monitor arrangement
	/// </summary>
	[DataMember]
	public bool SortsArrangement
	{
		get => _sortsArrangement;
		set => SetProperty(ref _sortsArrangement, value);
	}
	private bool _sortsArrangement = true; // default

	/// <summary>
	/// Whether to defer change until stopped
	/// </summary>
	[DataMember]
	public bool DefersChange
	{
		get => _defersChange;
		set => SetProperty(ref _defersChange, value);
	}
	private bool _defersChange;

	/// <summary>
	/// Whether to adjust SDR content brightness
	/// </summary>
	[DataMember]
	public bool AdjustsSdrContent
	{
		get => _adjustsSdrContent;
		set => SetProperty(ref _adjustsSdrContent, value);
	}
	private bool _adjustsSdrContent;

	/// <summary>
	/// Whether to invert scroll direction
	/// </summary>
	/// <remarks>
	/// This value is a set of flags.
	/// </remarks>
	public ScrollInput InvertsScrollDirection
	{
		get => _invertsScrollDirection ??= (ScrollInput)0b_1110; // default
		set => SetProperty(ref _invertsScrollDirection, value);
	}
	[DataMember(Name = nameof(InvertsScrollDirection))]
	private ScrollInput? _invertsScrollDirection;

	/// <summary>
	/// Whether to enable moving in unison
	/// </summary>
	[DataMember]
	public bool EnablesUnison
	{
		get => _enablesUnison;
		set => SetProperty(ref _enablesUnison, value);
	}
	private bool _enablesUnison;

	/// <summary>
	/// Whether to enable changing adjustable range
	/// </summary>
	[DataMember]
	public bool EnablesRange
	{
		get => _enablesRange;
		set => SetProperty(ref _enablesRange, value);
	}
	private bool _enablesRange;

	/// <summary>
	/// Whether to enable changing contrast
	/// </summary>
	[DataMember]
	public bool EnablesContrast
	{
		get => _enablesContrast;
		set => SetProperty(ref _enablesContrast, value);
	}
	private bool _enablesContrast;

	/// <summary>
	/// Whether to enable showing monitor identity
	/// </summary>
	[DataMember]
	public bool EnablesIdentity
	{
		get => _enablesIdentity;
		set => SetProperty(ref _enablesIdentity, value);
	}
	private bool _enablesIdentity = true;

	/// <summary>
	/// Monitor customizations by user
	/// </summary>
	[DataMember]
	public ObservableKeyedList<string, MonitorCustomizationItem> MonitorCustomizations
	{
		get => _monitorCustomizations ??= new ObservableKeyedList<string, MonitorCustomizationItem>();
		protected set => _monitorCustomizations = value;
	}
	private ObservableKeyedList<string, MonitorCustomizationItem> _monitorCustomizations;

	/// <summary>
	/// Brightness profiles saved by user
	/// </summary>
	public ObservableKeyedList<string, BrightnessProfile> BrightnessProfiles
	{
		get => _brightnessProfiles ??= new ObservableKeyedList<string, BrightnessProfile>();
		protected set => _brightnessProfiles = value;
	}
	private ObservableKeyedList<string, BrightnessProfile> _brightnessProfiles;

	/// <summary>
	/// Device Instance ID of selected monitor
	/// </summary>
	[DataMember]
	public string SelectedDeviceInstanceId
	{
		get => _selectedDeviceInstanceId;
		set => SetProperty(ref _selectedDeviceInstanceId, value);
	}
	private string _selectedDeviceInstanceId;

	/// <summary>
	/// Whether to record operations to log
	/// </summary>
	[DataMember]
	public bool RecordsOperationLog
	{
		get => _recordsOperationLog;
		set => SetProperty(ref _recordsOperationLog, value);
	}
	private bool _recordsOperationLog;

	/// <summary>	
	/// Whether to enable DDC/CI for sleep mode
	/// </summary>
	[DataMember]
	public bool EnablesSleepModeByDdcCi
	{
        get => _enablesSleepModeByDdcCi ??= true;
		set => SetProperty(ref _enablesSleepModeByDdcCi, value);
	}
	private bool? _enablesSleepModeByDdcCi;

	/// <summary>
	/// Whether to enable brightness zero for sleep mode
	/// </summary>
	[DataMember]
	public bool EnablesSleepModeByBrightnessZero
	{
       get => _enablesSleepModeByBrightnessZero ??= true;
		set => SetProperty(ref _enablesSleepModeByBrightnessZero, value);
	}
	private bool? _enablesSleepModeByBrightnessZero;

	/// <summary>
	/// Whether to enable black overlay for sleep mode
	/// </summary>
	[DataMember]
	public bool EnablesSleepModeByBlackOverlay
	{
     get => _enablesSleepModeByBlackOverlay ??= true;
		set => SetProperty(ref _enablesSleepModeByBlackOverlay, value);
	}
	private bool? _enablesSleepModeByBlackOverlay;

	#endregion

	protected Type[] KnownTypes { get; set; }

	private const string SettingsFileName = "settings.xml";

	protected string FileName
	{
		get => _fileName;
		set
		{
			if (!string.IsNullOrWhiteSpace(value))
				_fileName = value;
		}
	}
	private string _fileName = SettingsFileName;
	private const string BrightnessProfilesFileName = "brightness-profiles.json";

    public SettingsCore()
	{
        // BrightnessProfiles are stored separately as JSON.
		KnownTypes = Array.Empty<Type>();
	}

	private Throttle _save;

	protected internal virtual async Task InitiateAsync()
	{
		await Task.Run(() => Load(this));
		await Task.Run(LoadBrightnessProfiles);

		_save = new Throttle(
			TimeSpan.FromMilliseconds(100),
			() => Save(this));
		var saveBrightnessProfiles = new Throttle(
			TimeSpan.FromMilliseconds(100),
			SaveBrightnessProfiles);

		MonitorCustomizations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(MonitorCustomizations));
     BrightnessProfiles.CollectionChanged += async (_, _) => await saveBrightnessProfiles.PushAsync();
		PropertyChanged += async (_, _) => await _save.PushAsync();
	}

	private void LoadBrightnessProfiles()
	{
		try
		{
			if (!AppDataService.Exists(BrightnessProfilesFileName, out _))
				return;

			var json = AppDataService.ReadAsync(BrightnessProfilesFileName).GetAwaiter().GetResult();
			if (string.IsNullOrWhiteSpace(json))
				return;

			using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
			var serializer = new DataContractJsonSerializer(typeof(List<BrightnessProfile>));
			if (serializer.ReadObject(ms) is not List<BrightnessProfile> loaded)
				return;

			BrightnessProfiles.Clear();
			foreach (var profile in loaded.Where(x => !string.IsNullOrWhiteSpace(x?.Name)))
				BrightnessProfiles.Add(profile.Name, profile);
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Failed to load brightness profiles from JSON." + Environment.NewLine + ex);
		}
	}

	private void SaveBrightnessProfiles()
	{
		try
		{
			var profiles = BrightnessProfiles.Select(x => x.Value).ToList();
			using var ms = new MemoryStream();
			var serializer = new DataContractJsonSerializer(typeof(List<BrightnessProfile>));
			serializer.WriteObject(ms, profiles);
			var json = Encoding.UTF8.GetString(ms.ToArray());
			AppDataService.Write(BrightnessProfilesFileName, append: false, delete: true, json);
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Failed to save brightness profiles to JSON." + Environment.NewLine + ex);
		}
	}

	#region Load/Save

	private void Load<T>(T instance) where T : class
	{
		try
		{
			AppDataService.Load(instance, FileName, KnownTypes);
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Failed to load settings from AppData." + Environment.NewLine
				+ ex);
		}
	}

	private void Save<T>(T instance) where T : class
	{
		try
		{
			AppDataService.Save(instance, FileName, KnownTypes);
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Failed to save settings to AppData." + Environment.NewLine
				+ ex);
		}
	}

	#endregion
}

[DataContract]
public class MonitorCustomizationItem
{
	[DataMember]
	public string Name { get; private set; }

	[DataMember(Name = "Unison")]
	public bool IsUnison { get; private set; }

	[DataMember]
	public byte Lowest { get; private set; } = 0;

	[DataMember]
	public byte Highest { get; private set; } = 100;

	public MonitorCustomizationItem(string name, bool isUnison, byte lowest, byte highest)
	{
		this.Name = name;
		this.IsUnison = isUnison;
		this.Lowest = lowest;
		this.Highest = highest;
	}

	internal bool IsValid
	{
		get => (Lowest < Highest) && (Highest <= 100);
	}

	internal bool IsDefault
	{
		get => (Name is null)
			&& (IsUnison == default)
			&& (Lowest, Highest) is (0, 100);
	}
}