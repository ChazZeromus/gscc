using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace TestCompile
{
	class TaskBar : IDisposable
	{
		IntPtr Hwnd;
		[DllImport("taskbar.dll", CharSet = CharSet.Auto)]
		private static extern bool Init();
		[DllImport("taskbar.dll", CharSet = CharSet.Auto)]
		private static extern void Unload();
		[DllImport("taskbar.dll", CharSet = CharSet.Auto, EntryPoint = "IsAvailable")]
		private static extern bool _IsAvailable();
		[DllImport("taskbar.dll", CharSet = CharSet.Auto)]
		private static extern uint SetProgressValue_DWORD(IntPtr hwnd, uint completed, uint total);
		[DllImport("taskbar.dll", CharSet = CharSet.Auto)]
		private static extern uint SetProgressState(IntPtr hwnd, StateFlags flags);
		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern bool FlashWindowEx(IntPtr lpFWI);

		public enum FlashFlags : uint
		{
			All = 0x3,
			Caption = 0x1,
			Stop = 0x0,
			Timer = 0x4,
			TimerNoForeground = 0xC,
			Tray = 0x2
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private struct FlashWindowInfo
		{
			public uint cbSize;
			public IntPtr hwnd;
			public FlashFlags dwFlags;
			public uint uCount;
			public uint dwTimeout;
		}

		public void Flash(FlashFlags flags, int count, int timeout = 0)
		{
			FlashWindowInfo fwi = new FlashWindowInfo();
			fwi.cbSize = (uint)Marshal.SizeOf(fwi);
			fwi.dwFlags = flags;
			fwi.uCount = (uint)count;
			fwi.dwTimeout = (uint)timeout;
			fwi.hwnd = Hwnd;
			IntPtr ptr = Marshal.AllocHGlobal((int)fwi.cbSize);
			Marshal.StructureToPtr(fwi, ptr, false);
			FlashWindowEx(ptr);
			Marshal.FreeHGlobal(ptr);
		}

		public void StopFlash()
		{
			Flash(FlashFlags.Stop, 0);
		}

		public void Dispose()
		{
			Unload();
		}

		public TaskBar(IntPtr hwnd)
		{
			Hwnd = hwnd;
			Init();
		}

		public enum StateFlags : uint
		{
			None = 0x0,
			Indeterminate = 0x1,
			Normal = 0x2,
			Error = 0x4,
			Paused = 0x8
		}

		public static Boolean IsAvailable()
		{
			return (Boolean)TaskBar._IsAvailable();
		}

		public void SetState(StateFlags flags)
		{
			SetProgressState(Hwnd, flags);
		}

		public void SetValue(uint completed, uint total)
		{
			SetProgressValue_DWORD(Hwnd, completed, total);
		}

		public void SetValue(int completed, int total)
		{
			SetValue((uint)completed, (uint)total);
		}
	}
}
