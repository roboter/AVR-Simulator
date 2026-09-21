using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace AVR_Simulator
{
	// http://stackoverflow.com/a/718505/1474139
	[SuppressUnmanagedCodeSecurity]
	public static class ConsoleManager
	{
		private const string Kernel32_DllName = "kernel32.dll";

		[DllImport(Kernel32_DllName)]
		private static extern bool AllocConsole();

		[DllImport(Kernel32_DllName)]
		private static extern bool FreeConsole();

		[DllImport(Kernel32_DllName)]
		private static extern IntPtr GetConsoleWindow();

		[DllImport(Kernel32_DllName)]
		private static extern int GetConsoleOutputCP();

		public static bool HasConsole
		{
			get { return GetConsoleWindow() != IntPtr.Zero; }
		}

		/// <summary>
		/// Creates a new console instance if the process is not attached to a console already.
		/// </summary>
		public static void Show()
		{
			//#if DEBUG
			if (!HasConsole)
			{
				AllocConsole();
				InvalidateOutAndError();
			}
			//#endif
		}

		/// <summary>
		/// If the process has a console attached to it, it will be detached and no longer visible. Writing to the System.Console is still possible, but no output will be shown.
		/// </summary>
		public static void Hide()
		{
			//#if DEBUG
			if (HasConsole)
			{
				SetOutAndErrorNull();
				FreeConsole();
			}
			//#endif
		}

		public static void Toggle()
		{
			if (HasConsole)
			{
				Hide();
			}
			else
			{
				Show();
			}
		}

		static void InvalidateOutAndError()
		{
			// In .NET 5+, reopening the standard streams is sufficient —
			// the private reflection trick used in .NET Framework no longer works.
			Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
			Console.SetError(new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
		}

		static void SetOutAndErrorNull()
		{
			Console.SetOut(TextWriter.Null);
			Console.SetError(TextWriter.Null);
		}
	}
}