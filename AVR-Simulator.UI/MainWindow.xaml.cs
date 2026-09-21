using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace AVR_Simulator
{
	public partial class MainWindow : Window
	{
		// ── Recent files ────────────────────────────────────────────────────
		private const int MaxRecentFiles = 8;
		private static readonly string RecentFilesPath =
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			             "AVR Simulator", "recent.json");
		private List<string> RecentFiles = new();

		// ── Currently loaded HEX path ────────────────────────────────────────
		private string? CurrentHexPath;

		// ── Pin row descriptor ──────────────────────────────────────────────
		// Each row is just: pin name label + LED ellipse (+ invisible toggle for input)
		private class PinRow
		{
			public AVRInterpreter.GPIOPin Pin    { get; init; } = null!;
			public Ellipse                Led    { get; init; } = null!;
			public DropShadowEffect       Glow   { get; init; } = null!;
			public ToggleButton           Toggle { get; init; } = null!; // invisible hitbox for INPUT
			public bool                   LastVal { get; set; } = false;
		}

		private class DisassemblyLine
		{
			public int Address { get; init; }
			public int WordLength { get; init; }
			public string AddressText { get; init; } = string.Empty;
			public string OpcodeText { get; init; } = string.Empty;
			public string HexBytesText { get; init; } = string.Empty;
			public string Mnemonic { get; init; } = string.Empty;
			public string TooltipText { get; init; } = string.Empty;
			public bool IsCurrent { get; set; }
			public bool IsLinked { get; set; }
		}

		private class HexByteCell
		{
			public int ByteAddress { get; init; }
			public int WordAddress => ByteAddress / 2;
			public string Text { get; init; } = string.Empty;
			public string TooltipText { get; init; } = string.Empty;
			public bool IsCurrent { get; set; }
			public bool IsLinked { get; set; }
		}

		private class HexRow
		{
			public int StartByteAddress { get; init; }
			public string AddressText { get; init; } = string.Empty;
			public List<HexByteCell> Bytes { get; init; } = new();
		}

		// ── Brushes ─────────────────────────────────────────────────────────
		private static readonly SolidColorBrush BrushHigh   = new(Color.FromRgb(0xA6, 0xE3, 0xA1));
		private static readonly SolidColorBrush BrushLow    = new(Color.FromRgb(0x45, 0x47, 0x5A));
		private static readonly SolidColorBrush BrushInHigh = new(Color.FromRgb(0x89, 0xB4, 0xFA)); // blue for input HIGH
		private static readonly Color GlowGreen  = Color.FromRgb(0xA6, 0xE3, 0xA1);
		private static readonly Color GlowBlue   = Color.FromRgb(0x89, 0xB4, 0xFA);

		// ── State ───────────────────────────────────────────────────────────
		private BackgroundWorker?     Worker;
		private Atmega328Interpreter? Interpreter;
		private readonly object       InterpreterLock = new();
		private volatile bool         IsEmulationRunning;
		private DispatcherTimer?      ADCTimer;
		private DateTime              StartTime;
		private List<PinRow>          AllPinRows = new();
		private List<DisassemblyLine> DisassemblyLines = new();
		private List<HexRow>          HexRows = new();
		private Dictionary<int, DisassemblyLine> DisassemblyByAddress = new();
		private DisassemblyLine?      CurrentDisassemblyLine;
		private HexRow?               CurrentHexRow;
		private List<HexByteCell>     CurrentHexBytes = new();
		private readonly List<DisassemblyLine> LinkedDisassemblyLines = new();
		private readonly List<HexByteCell>     LinkedHexBytes = new();

		// ── Built-in Blink.hex path ──────────────────────────────────────────
		private static string BlinkHexPath =>
			Path.Combine(AppContext.BaseDirectory, "Blink.hex");

		public MainWindow()
		{
			InitializeComponent();

			// Ctrl+O → Load
			CommandBindings.Add(new CommandBinding(ApplicationCommands.Open,
				(s, e) => MenuLoad_Click(s, e)));
			InputBindings.Add(new KeyBinding(ApplicationCommands.Open,
				Key.O, ModifierKeys.Control));

			LoadRecentFiles();

			ADCTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
			ADCTimer.Tick += ADCTimer_Tick;
			ADCTimer.Start();
			StartTime = DateTime.Now;
		}

		// ── Window events ───────────────────────────────────────────────────
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (File.Exists(BlinkHexPath))
				StartEmulation(BlinkHexPath);
			else
				SetStatus("No project loaded — use File › Load HEX File");
		}

		private void Window_Closing(object sender, CancelEventArgs e) => StopEmulation();

		// ── Menu handlers ────────────────────────────────────────────────────
		private void MenuLoad_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new Microsoft.Win32.OpenFileDialog
			{
				Title            = "Load Intel HEX File",
				Filter           = "Intel HEX files (*.hex)|*.hex|All files (*.*)|*.*",
				DefaultExt       = ".hex",
				RestoreDirectory = true,
			};
			if (dlg.ShowDialog() != true) return;
			LoadHex(dlg.FileName);
		}

		private void MenuBlink_Click(object sender, RoutedEventArgs e)
		{
			if (!File.Exists(BlinkHexPath))
			{
				MessageBox.Show($"Blink.hex not found at:\n{BlinkHexPath}",
					"Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			LoadHex(BlinkHexPath);
		}

		private void MenuClose_Click(object sender, RoutedEventArgs e)
		{
			StopEmulation();
			ClearPortPanels();
			ClearDisassembly();
			CurrentHexPath = null;
			Title = "AVR Simulator";
			MenuClose.IsEnabled = false;
			SetStatus("Project closed — use File › Load HEX File to open one");
		}

		private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

		private void RunButton_Click(object sender, RoutedEventArgs e)
		{
			if (Interpreter == null)
				return;

			IsEmulationRunning = true;
			SetStatus(CurrentHexPath != null ? $"Running: {CurrentHexPath}" : "Running");
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			if (Interpreter == null)
				return;

			IsEmulationRunning = false;
			SetStatus(CurrentHexPath != null ? $"Stopped: {CurrentHexPath}" : "Stopped");
		}

		private void StepButton_Click(object sender, RoutedEventArgs e)
		{
			if (Interpreter == null)
				return;

			IsEmulationRunning = false;

			lock (InterpreterLock)
			{
				Interpreter.Execute();
			}

			RefreshDisassemblyHighlight(forceScroll: true);
			RefreshPinRows();
			SetStatus(CurrentHexPath != null ? $"Step: {CurrentHexPath}" : "Step");
		}

		// ── Load / Stop helpers ──────────────────────────────────────────────
		private void LoadHex(string path)
		{
			StopEmulation();
			ClearPortPanels();
			CurrentHexPath = path;
			StartEmulation(path);
			AddRecentFile(path);
			Title = $"AVR Simulator — {Path.GetFileName(path)}";
			MenuClose.IsEnabled = true;
			SetStatus($"▶  Running: {path}");
		}

		private void StartEmulation(string hexPath)
		{
			IsEmulationRunning = true;
			Worker = new BackgroundWorker { WorkerSupportsCancellation = true };
			Worker.DoWork += Worker_DoWork;
			Worker.RunWorkerAsync(hexPath);
		}

		private void StopEmulation()
		{
			IsEmulationRunning = false;
			if (Worker is { IsBusy: true })
				Worker.CancelAsync();
			Interpreter = null;
		}

		// ── Background worker ───────────────────────────────────────────────
		private void Worker_DoWork(object? sender, DoWorkEventArgs e)
		{
			string hexPath = e.Argument as string ?? string.Empty;

			Interpreter = new Atmega328Interpreter();

			if (File.Exists(hexPath))
				Interpreter.Load(IntelHEX.Parse(hexPath));

			Dispatcher.Invoke(() =>
			{
				BuildPortPanels();
				BuildDisassembly();
			});

			for (; !Worker!.CancellationPending;)
			{
				if (IsEmulationRunning)
				{
					lock (InterpreterLock)
					{
						Interpreter.Execute();
					}
				}
				else
				{
					Thread.Sleep(1);
				}
			}
		}

		// ── ADC / DAC / port refresh timer ─────────────────────────────────
		private void ADCTimer_Tick(object? sender, EventArgs e)
		{
			if (Interpreter == null) return;

			// ADC
			double analogValue;
			if (SineWaveCheckBox.IsChecked == true)
			{
				double t    = (DateTime.Now - StartTime).TotalSeconds;
				double freq = FreqSlider.Value;
				double amp  = AmpSlider.Value;
				analogValue = 2.5 + amp * Math.Sin(2 * Math.PI * freq * t);
			}
			else
			{
				analogValue = VoltageSlider.Value;
			}

			if (Interpreter.ADCUnit != null)
			{
				Interpreter.ADCUnit.AnalogInput = analogValue;
				CurrentValueText.Text = $"Current ADC Input: {analogValue:F2} V";
			}

			// DAC
			if (Interpreter.DACUnit != null)
				DACOutputText.Text = $"DAC Voltage: {Interpreter.DACUnit.Voltage:F2} V  (0x{Interpreter.DACUnit.OutputValue:X2})";

			// GPIO LEDs
			RefreshPinRows();

			// Code window
			RefreshDisassemblyHighlight();
		}

		// ── Clear port panels ────────────────────────────────────────────────
		private void ClearPortPanels()
		{
			AllPinRows.Clear();
			PortBPanel.ItemsSource = null;
			PortCPanel.ItemsSource = null;
			PortDPanel.ItemsSource = null;
		}

		private void ClearDisassembly()
		{
			DisassemblyLines.Clear();
			HexRows.Clear();
			DisassemblyByAddress.Clear();
			CurrentDisassemblyLine = null;
			CurrentHexRow = null;
			CurrentHexBytes.Clear();
			ClearLinkedHighlight();
			DisassemblyList.ItemsSource = null;
			HexList.ItemsSource = null;
			CurrentInstructionText.Text = "PC: ----";
		}

		// ── Status bar helper ────────────────────────────────────────────────
		private void SetStatus(string text) =>
			Dispatcher.InvokeAsync(() => StatusText.Text = text);

		// ── Recent files ─────────────────────────────────────────────────────
		private void LoadRecentFiles()
		{
			try
			{
				if (File.Exists(RecentFilesPath))
					RecentFiles = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentFilesPath)) ?? new();
			}
			catch { RecentFiles = new(); }
			RebuildRecentMenu();
		}

		private void SaveRecentFiles()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(RecentFilesPath)!);
				File.WriteAllText(RecentFilesPath, JsonSerializer.Serialize(RecentFiles));
			}
			catch { /* non-critical */ }
		}

		private void AddRecentFile(string path)
		{
			RecentFiles.Remove(path);
			RecentFiles.Insert(0, path);
			if (RecentFiles.Count > MaxRecentFiles)
				RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
			SaveRecentFiles();
			RebuildRecentMenu();
		}

		private void RebuildRecentMenu()
		{
			MenuRecent.Items.Clear();
			MenuRecent.IsEnabled = RecentFiles.Count > 0;

			foreach (string path in RecentFiles)
			{
				var item = new MenuItem
				{
					Header  = Path.GetFileName(path),
					ToolTip = path,
					Style   = (Style)FindResource("DarkMenuItem"),
				};
				var captured = path;
				item.Click += (s, e) => LoadHex(captured);
				MenuRecent.Items.Add(item);
			}

			if (RecentFiles.Count > 0)
			{
				MenuRecent.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)) });
				var clearItem = new MenuItem { Header = "Clear Recent Files", Style = (Style)FindResource("DarkMenuItem") };
				clearItem.Click += (s, e) => { RecentFiles.Clear(); SaveRecentFiles(); RebuildRecentMenu(); };
				MenuRecent.Items.Add(clearItem);
			}
		}

		// ── Build port panels ────────────────────────────────────────────────
		private void BuildPortPanels()
		{
			if (Interpreter == null) return;
			AllPinRows.Clear();

			var pb = Interpreter.PORTB;
			AddPinRows(PortBPanel, new[]
			{
				(pb.PB0,"PB0"),(pb.PB1,"PB1"),(pb.PB2,"PB2"),(pb.PB3,"PB3"),
				(pb.PB4,"PB4"),(pb.PB5,"PB5"),(pb.PB6,"PB6"),(pb.PB7,"PB7"),
			});

			var pc = Interpreter.PORTC;
			AddPinRows(PortCPanel, new[]
			{
				(pc.PC0,"PC0"),(pc.PC1,"PC1"),(pc.PC2,"PC2"),(pc.PC3,"PC3"),
				(pc.PC4,"PC4"),(pc.PC5,"PC5"),(pc.PC6,"PC6"),
			});

			var pd = Interpreter.PORTD;
			AddPinRows(PortDPanel, new[]
			{
				(pd.PD0,"PD0"),(pd.PD1,"PD1"),(pd.PD2,"PD2"),(pd.PD3,"PD3"),
				(pd.PD4,"PD4"),(pd.PD5,"PD5"),(pd.PD6,"PD6"),(pd.PD7,"PD7"),
			});
		}

		// ── Code / disassembly window ──────────────────────────────────────
		private void BuildDisassembly()
		{
			if (Interpreter == null)
			{
				ClearDisassembly();
				return;
			}

			DisassemblyLines = new List<DisassemblyLine>();
			HexRows = new List<HexRow>();
			DisassemblyByAddress = new Dictionary<int, DisassemblyLine>();
			int lineCount = GetDisassemblyLineCount(Interpreter);

			for (int address = 0; address < lineCount;)
			{
				string text = Interpreter.Disassemble(address);
				string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				string opcode = parts.Length > 1 && IsHexWord(parts[1])
					? $"{parts[0]} {parts[1]}"
					: parts.FirstOrDefault() ?? string.Empty;
				string mnemonic = parts.Length > 1 && IsHexWord(parts[1])
					? string.Join(" ", parts.Skip(2))
					: string.Join(" ", parts.Skip(1));
				int wordLength = Math.Min(Interpreter.GetInstructionWordLength(address), lineCount - address);
				string hexBytes = FormatInstructionBytes(Interpreter, address, wordLength);

				var line = new DisassemblyLine
				{
					Address = address,
					WordLength = wordLength,
					AddressText = string.Format("{0:X4}", address),
					OpcodeText = opcode,
					HexBytesText = hexBytes,
					Mnemonic = mnemonic,
					TooltipText = $"{address:X4}  {opcode}  {mnemonic}",
				};

				DisassemblyLines.Add(line);

				for (int offset = 0; offset < wordLength; offset++)
					DisassemblyByAddress[address + offset] = line;

				address += wordLength;
			}

			DisassemblyList.ItemsSource = DisassemblyLines;
			BuildHexRows(Interpreter, lineCount);
			HexList.ItemsSource = HexRows;
			RefreshDisassemblyHighlight(forceScroll: true);
		}

		private void BuildHexRows(AVRInterpreter interpreter, int wordCount)
		{
			const int bytesPerRow = 16;
			int byteCount = wordCount * 2;

			for (int rowStart = 0; rowStart < byteCount; rowStart += bytesPerRow)
			{
				var row = new HexRow
				{
					StartByteAddress = rowStart,
					AddressText = string.Format("{0:X4}", rowStart),
				};

				for (int byteAddress = rowStart; byteAddress < Math.Min(rowStart + bytesPerRow, byteCount); byteAddress++)
				{
					int wordAddress = byteAddress / 2;
					ushort word = interpreter.Flash[wordAddress];
					byte value = (byte)((byteAddress % 2 == 0) ? (word & 0xFF) : (word >> 8));
					string tooltip = DisassemblyByAddress.TryGetValue(wordAddress, out DisassemblyLine? line)
						? line.TooltipText
						: string.Empty;

					row.Bytes.Add(new HexByteCell
					{
						ByteAddress = byteAddress,
						Text = string.Format("{0:X2}", value),
						TooltipText = tooltip,
					});
				}

				HexRows.Add(row);
			}
		}

		private static string FormatInstructionBytes(AVRInterpreter interpreter, int address, int wordLength)
		{
			var bytes = new List<string>();

			for (int offset = 0; offset < wordLength; offset++)
			{
				ushort word = interpreter.Flash[address + offset];
				bytes.Add(string.Format("{0:X2}", word & 0xFF));
				bytes.Add(string.Format("{0:X2}", word >> 8));
			}

			return string.Join(" ", bytes);
		}

		private static bool IsHexWord(string text)
		{
			return text.Length == 4 && text.All(Uri.IsHexDigit);
		}

		private static int GetDisassemblyLineCount(AVRInterpreter interpreter)
		{
			int lastNonZero = Array.FindLastIndex(interpreter.Flash, word => word != 0);
			int visibleTail = Math.Max(lastNonZero + 8, 32);
			return Math.Min(Math.Max(visibleTail, 0), interpreter.Flash.Length);
		}

		private void RefreshDisassemblyHighlight(bool forceScroll = false)
		{
			if (Interpreter == null || DisassemblyLines.Count == 0)
				return;

			int address = Interpreter.LastExecutedPC;
			if (!DisassemblyByAddress.TryGetValue(address, out DisassemblyLine? line))
				return;

			if (!forceScroll && ReferenceEquals(line, CurrentDisassemblyLine))
				return;

			if (CurrentDisassemblyLine != null)
				CurrentDisassemblyLine.IsCurrent = false;

			foreach (HexByteCell byteCell in CurrentHexBytes)
				byteCell.IsCurrent = false;

			line.IsCurrent = true;
			CurrentHexBytes = GetHexBytesForInstruction(line).ToList();
			foreach (HexByteCell byteCell in CurrentHexBytes)
				byteCell.IsCurrent = true;

			CurrentHexRow = HexRows.FirstOrDefault(row =>
				line.Address * 2 >= row.StartByteAddress &&
				line.Address * 2 < row.StartByteAddress + row.Bytes.Count);
			CurrentDisassemblyLine = line;
			CurrentInstructionText.Text = $"PC: {address:X4}   {line.OpcodeText}   {line.Mnemonic}";

			DisassemblyList.Items.Refresh();
			HexList.Items.Refresh();
			DisassemblyList.ScrollIntoView(line);
			if (CurrentHexRow != null)
				HexList.ScrollIntoView(CurrentHexRow);
		}

		private IEnumerable<HexByteCell> GetHexBytesForInstruction(DisassemblyLine line)
		{
			int startByteAddress = line.Address * 2;
			int endByteAddress = startByteAddress + line.WordLength * 2;

			return HexRows
				.SelectMany(row => row.Bytes)
				.Where(byteCell => byteCell.ByteAddress >= startByteAddress && byteCell.ByteAddress < endByteAddress);
		}

		private void DisassemblyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (DisassemblyList.SelectedItem is DisassemblyLine line)
				HighlightLinkedInstruction(line, scrollHex: true);
		}

		private void HexList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (HexList.SelectedItem is HexRow row)
				HighlightLinkedHexRow(row);
		}

		private void HexByte_MouseEnter(object sender, MouseEventArgs e)
		{
			if (((FrameworkElement)sender).DataContext is HexByteCell cell)
				HighlightLinkedByte(cell);
		}

		private void HexByte_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (((FrameworkElement)sender).DataContext is HexByteCell cell)
			{
				HighlightLinkedByte(cell);
				e.Handled = true;
			}
		}

		private void HighlightLinkedByte(HexByteCell cell)
		{
			if (DisassemblyByAddress.TryGetValue(cell.WordAddress, out DisassemblyLine? line))
				HighlightLinkedInstruction(line, scrollDisassembly: true);
		}

		private void HighlightLinkedInstruction(DisassemblyLine line, bool scrollDisassembly = false, bool scrollHex = false)
		{
			ClearLinkedHighlight();

			line.IsLinked = true;
			LinkedDisassemblyLines.Add(line);

			foreach (HexByteCell byteCell in GetHexBytesForInstruction(line))
			{
				byteCell.IsLinked = true;
				LinkedHexBytes.Add(byteCell);
			}

			RefreshCodeViews();

			if (scrollDisassembly)
				DisassemblyList.ScrollIntoView(line);

			if (scrollHex)
			{
				HexRow? row = FindHexRowForByteAddress(line.Address * 2);
				if (row != null)
					HexList.ScrollIntoView(row);
			}
		}

		private void HighlightLinkedHexRow(HexRow row)
		{
			ClearLinkedHighlight();

			foreach (HexByteCell byteCell in row.Bytes)
			{
				byteCell.IsLinked = true;
				LinkedHexBytes.Add(byteCell);

				if (DisassemblyByAddress.TryGetValue(byteCell.WordAddress, out DisassemblyLine? line) &&
				    !LinkedDisassemblyLines.Contains(line))
				{
					line.IsLinked = true;
					LinkedDisassemblyLines.Add(line);
				}
			}

			RefreshCodeViews();
		}

		private HexRow? FindHexRowForByteAddress(int byteAddress)
		{
			return HexRows.FirstOrDefault(row =>
				byteAddress >= row.StartByteAddress &&
				byteAddress < row.StartByteAddress + row.Bytes.Count);
		}

		private void ClearLinkedHighlight()
		{
			foreach (DisassemblyLine line in LinkedDisassemblyLines)
				line.IsLinked = false;

			foreach (HexByteCell byteCell in LinkedHexBytes)
				byteCell.IsLinked = false;

			LinkedDisassemblyLines.Clear();
			LinkedHexBytes.Clear();
		}

		private void RefreshCodeViews()
		{
			DisassemblyList.Items.Refresh();
			HexList.Items.Refresh();
		}

		private void AddPinRows(ItemsControl panel,
			IEnumerable<(AVRInterpreter.GPIOPin pin, string name)> pins)
		{
			var rows = new List<Grid>();

			foreach (var (pin, name) in pins)
			{
				// Glow effect shared between LED states
				var glow = new DropShadowEffect
				{
					ShadowDepth = 0,
					BlurRadius  = 8,
					Opacity     = 0,
					Color       = GlowGreen,
				};

				// LED ellipse — filled circle = HIGH, dim ring = LOW
				var led = new Ellipse
				{
					Width             = 10,
					Height            = 10,
					Fill              = BrushLow,
					Stroke            = new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70)),
					StrokeThickness   = 1,
					VerticalAlignment = VerticalAlignment.Center,
					Effect            = glow,
					ToolTip           = name,
					Cursor            = System.Windows.Input.Cursors.Hand,
				};

				// Invisible toggle hitbox over the LED — lets user drive INPUT pins
				var toggle = new ToggleButton
				{
					Width             = 10,
					Height            = 10,
					Opacity           = 0,
					VerticalAlignment = VerticalAlignment.Center,
					Focusable         = false,
					Cursor            = System.Windows.Input.Cursors.Hand,
					ToolTip           = $"{name} — click to toggle INPUT",
				};

				var capturedPin = pin;
				toggle.Checked   += (s, e) => { capturedPin.Value = true; };
				toggle.Unchecked += (s, e) => { capturedPin.Value = false; };

				// Pin name label
				var label = new TextBlock
				{
					Text              = name,
					Style             = (Style)FindResource("PinLabel"),
				};

				// Compact row: [LED+toggle overlay] [name]
				var canvas = new Grid
				{
					Width             = 10,
					Height            = 10,
					VerticalAlignment = VerticalAlignment.Center,
					Margin            = new Thickness(0, 0, 5, 0),
				};
				canvas.Children.Add(led);
				canvas.Children.Add(toggle);

				var row = new Grid
				{
					Height = 16,
					Margin = new Thickness(0, 1, 0, 1),
				};
				row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
				row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				Grid.SetColumn(canvas, 0);
				Grid.SetColumn(label,  1);
				row.Children.Add(canvas);
				row.Children.Add(label);
				rows.Add(row);

				AllPinRows.Add(new PinRow { Pin = pin, Led = led, Glow = glow, Toggle = toggle });
			}

			panel.ItemsSource = rows;
		}

		// ── Refresh all pin LEDs each tick ──────────────────────────────────
		private void RefreshPinRows()
		{
			foreach (var row in AllPinRows)
			{
				bool isOutput = row.Pin.Direction == AVRInterpreter.GPIOPinDirection.OUTPUT;
				bool val      = row.Pin.Value;

				if (val == row.LastVal) continue; // skip no-change
				row.LastVal = val;

				if (val)
				{
					row.Led.Fill      = isOutput ? BrushHigh : BrushInHigh;
					row.Led.Stroke    = isOutput ? BrushHigh : BrushInHigh;
					row.Glow.Color    = isOutput ? GlowGreen : GlowBlue;
					row.Glow.Opacity  = 0.9;
					row.Glow.BlurRadius = 10;
				}
				else
				{
					row.Led.Fill      = BrushLow;
					row.Led.Stroke    = new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70));
					row.Glow.Opacity  = 0;
				}

				// Keep toggle in sync for INPUT pins (avoid re-triggering)
				if (!isOutput && row.Toggle.IsChecked != val)
					row.Toggle.IsChecked = val;
			}
		}
	}
}
