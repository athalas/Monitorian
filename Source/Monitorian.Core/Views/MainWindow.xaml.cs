using System;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using Monitorian.Core.Models;
using Monitorian.Core.ViewModels;
using ScreenFrame.Movers;

namespace Monitorian.Core.Views;

public partial class MainWindow : Window
{
   private const int HotkeyId = 0x7248;
	private const uint WM_HOTKEY = 0x0312;
	private const int WH_KEYBOARD_LL = 13;
	private const int WM_KEYDOWN = 0x0100;
	private const int WM_SYSKEYDOWN = 0x0104;
	private const int VK_CONTROL = 0x11;
	private const int VK_MENU = 0x12;

	private readonly StickWindowMover _mover;
   private readonly AppControllerCore _controller;
	private readonly IntPtr _windowHandle;
	private readonly HwndSource _hwndSource;
  private readonly LowLevelKeyboardProc _keyboardProc;
	private readonly IntPtr _keyboardHookHandle;
	public MainWindowViewModel ViewModel => (MainWindowViewModel)this.DataContext;

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool UnhookWindowsHookEx(IntPtr hhk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr GetModuleHandle(string lpModuleName);

	private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential)]
	private struct KBDLLHOOKSTRUCT
	{
		public uint vkCode;
		public uint scanCode;
		public uint flags;
		public uint time;
		public IntPtr dwExtraInfo;
	}

	public MainWindow(AppControllerCore controller)
	{
     _controller = controller ?? throw new ArgumentNullException(nameof(controller));
		LanguageService.Switch();

		InitializeComponent();

       this.DataContext = new MainWindowViewModel(_controller);

		_windowHandle = new WindowInteropHelper(this).EnsureHandle();
		_hwndSource = HwndSource.FromHwnd(_windowHandle);
		_hwndSource?.AddHook(WndProc);
     RegisterHotKey(_windowHandle, HotkeyId, MonitorSleepShortcut.Modifiers, (uint)KeyInterop.VirtualKeyFromKey(MonitorSleepShortcut.HotkeyKey));

		_keyboardProc = KeyboardHookCallback;
		_keyboardHookHandle = SetKeyboardHook(_keyboardProc);

       _mover = new StickWindowMover(this, _controller.NotifyIconContainer.NotifyIcon)
		{
			KeepsDistance = true
		};
		_mover.ForegroundWindowChanged += OnDeactivated;

       _controller.WindowPainter.Add(this);
		_controller.WindowPainter.ThemeChanged += (_, _) =>
		{
			ViewModel.MonitorsView.Refresh();
		};
		//controller.WindowPainter.AccentColorChanged += (_, _) =>
		//{
		//};
	}

	protected override void OnClosed(EventArgs e)
	{
      if (_keyboardHookHandle != IntPtr.Zero)
			UnhookWindowsHookEx(_keyboardHookHandle);

		UnregisterHotKey(_windowHandle, HotkeyId);
		_hwndSource?.RemoveHook(WndProc);

		base.OnClosed(e);
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
		{
          ViewModel.ToggleAllSleepMode();
			handled = true;
		}

		return IntPtr.Zero;
	}

	private IntPtr SetKeyboardHook(LowLevelKeyboardProc proc)
	{
		using var process = Process.GetCurrentProcess();
		using var module = process.MainModule;
		var moduleHandle = GetModuleHandle(module?.ModuleName);

		return SetWindowsHookEx(WH_KEYBOARD_LL, proc, moduleHandle, 0);
	}

	private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
	{
		if (nCode >= 0 && (wParam.ToInt32() is WM_KEYDOWN or WM_SYSKEYDOWN))
		{
			var keyData = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
			if (!IsGlobalSleepShortcut(keyData.vkCode))
			{
              Dispatcher.BeginInvoke(new Action(() =>
				{
					if (_controller.TryRestoreAllMonitorsOnAnyKey())
                       ViewModel.IsAllSleepModeEnabled = false;
				}));
			}
		}

		return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
	}

	private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState != MouseButtonState.Pressed)
			return;

		try
		{
			DragMove();
		}
		catch (InvalidOperationException)
		{
			// Ignore cases where the window cannot be dragged yet.
		}
	}

	private static bool IsGlobalSleepShortcut(uint virtualKey)
	{
		if (virtualKey != (uint)KeyInterop.VirtualKeyFromKey(MonitorSleepShortcut.HotkeyKey))
			return false;

		return IsKeyDown(VK_CONTROL) && IsKeyDown(VK_MENU);
	}

	private static bool IsKeyDown(int virtualKey)
	{
		return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
	}

	#region Show/Hide

	internal Point? CursorLocation
	{
		get => _mover.CursorLocation;
		set => _mover.CursorLocation = value;
	}

	public bool IsForeground => _mover.IsForeground();

	public void ShowForeground()
	{
		try
		{
			this.Topmost = true;

			// When a window is deactivated, a focused element will lose focus and usually,
			// no element will have focus until the window is activated again and the last focused
			// element automatically gets focus back. Therefore, in usual case, no focused element
			// exists before Window.Show method is called. However, it is possible to set focus on
			// an element during window is not active and such focused element is found here.
			// The issue is that such focused element will lose focus because the element which had
			// focus before the window was deactivated will restore focus even though any other
			// element has focus. To prevent this unintended change of focus, it is necessary to
			// set focus back on the element which has focus before Window.Show method is called.
			var currentFocusedElement = FocusManager.GetFocusedElement(this);

			base.Show();

			if (currentFocusedElement is not null)
			{
				var restoredFocusedElement = FocusManager.GetFocusedElement(this);
				if (restoredFocusedElement != currentFocusedElement)
					FocusManager.SetFocusedElement(this, currentFocusedElement);
			}

			// Set time to prevent hiding procedure from being triggered.
			_preventionTime = DateTimeOffset.Now + TimeSpan.FromSeconds(0.1);
		}
		catch (ArgumentException ex) when ((uint)ex.HResult is 0x80070057)
		{
			// Window.Show method can cause ArgumentException when internally calling
			// CompositionTarget.SetRootVisual method.
		}
		finally
		{
			this.Topmost = false;
		}
	}

	public void ShowUnnoticed()
	{
		var width = this.Width;
		var height = this.Height;
		var sizeToContent = this.SizeToContent;
		try
		{
			// Set window size as small as possible to make it almost unnoticed.
			this.Width = 1;
			this.Height = 1;
			this.SizeToContent = SizeToContent.Manual;

			base.Show();
			this.Hide();
		}
		finally
		{
			// Restore window size.
			this.Width = width;
			this.Height = height;
			this.SizeToContent = sizeToContent;
		}
	}

	public bool CanBeShown => (_preventionTime < DateTimeOffset.Now);
	private DateTimeOffset _preventionTime;

	private void OnDeactivated(object sender, EventArgs e)
	{
		ProceedHide();
	}

	protected override void OnDeactivated(EventArgs e)
	{
		base.OnDeactivated(e);

		ProceedHide();
	}

	private void ProceedHide()
	{
		if (this.Visibility is not Visibility.Visible)
			return;

		// Compare time to prevent hiding procedure from repeating.
		if (_preventionTime > DateTimeOffset.Now)
			return;

		ViewModel.Deactivate();

		// Set time to prevent this window from being shown unintentionally.
		_preventionTime = DateTimeOffset.Now + TimeSpan.FromSeconds(0.2);

		ClearHide();
	}

	public async void ClearHide()
	{
		// Clear focus.
		FocusManager.SetFocusedElement(this, null);

		// Wait for this window to be refreshed before being hidden.
		await Task.Delay(TimeSpan.FromSeconds(0.1));

		this.Hide();
	}

	#endregion
}

internal sealed class MonitorBlackoutWindow : Window
{
	public MonitorBlackoutWindow(Rect monitorRect)
	{
		if (monitorRect.IsEmpty || (monitorRect.Width <= 0D) || (monitorRect.Height <= 0D))
			throw new ArgumentException("The monitor rectangle must be valid.", nameof(monitorRect));

		Left = monitorRect.Left;
		Top = monitorRect.Top;
		Width = monitorRect.Width;
		Height = monitorRect.Height;
		WindowStyle = WindowStyle.None;
		ResizeMode = ResizeMode.NoResize;
		ShowInTaskbar = false;
		ShowActivated = false;
		Topmost = true;
		Background = Brushes.Black;
		Content = new Border
		{
			Background = Brushes.Black
		};
	}
}