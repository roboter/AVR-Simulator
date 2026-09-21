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
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

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
		private class PinRow
		{
			public AVRInterpreter.GPIOPin Pin { get; init; } = null!;
			public PinDatasheetInfo Info { get; init; } = null!;
			public Ellipse Led { get; init; } = null!;
			public DropShadowEffect Glow { get; init; } = null!;
			public ToggleButton Toggle { get; init; } = null!;
			public TextBlock DirText { get; init; } = null!;
			public Border DirBadge { get; init; } = null!;
			public TextBlock PullUpText { get; init; } = null!;
			public Border PullUpBadge { get; init; } = null!;
			public TextBlock LatchText { get; init; } = null!;
			public bool? LastVal { get; set; }
			public bool? LastDir { get; set; }
			public bool? LastPullUp { get; set; }
			public bool? LastPortBit { get; set; }
			public bool? LastPinBit { get; set; }
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
		private static readonly SolidColorBrush BrushHigh     = new(Color.FromRgb(0xA6, 0xE3, 0xA1));
		private static readonly SolidColorBrush BrushLow      = new(Color.FromRgb(0x45, 0x47, 0x5A));
		private static readonly SolidColorBrush BrushInHigh   = new(Color.FromRgb(0x89, 0xB4, 0xFA));
		private static readonly SolidColorBrush BrushAmber    = new(Color.FromRgb(0xF9, 0xE2, 0xAF));
		private static readonly SolidColorBrush BrushDim      = new(Color.FromRgb(0x6C, 0x70, 0x86));
		private static readonly SolidColorBrush BrushDark     = new(Color.FromRgb(0x18, 0x18, 0x25));
		private static readonly SolidColorBrush BrushBadgeOut = new(Color.FromRgb(0x28, 0x3D, 0x30));
		private static readonly SolidColorBrush BrushBadgeIn  = new(Color.FromRgb(0x20, 0x33, 0x47));
		private static readonly SolidColorBrush BrushBadgePu  = new(Color.FromRgb(0x3D, 0x35, 0x20));
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
			RebuildRecentMenu();

			if (File.Exists(BlinkHexPath))
				LoadHex(BlinkHexPath);
			else
				SetStatus("Ready — open a HEX file with File › Load HEX File (Ctrl+O)");
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			ADCTimer?.Stop();
			StopEmulation();
		}

		// ── Status bar helper ────────────────────────────────────────────────
		private void SetStatus(string msg) => StatusText.Text = msg;

		// ── Menu actions ────────────────────────────────────────────────────
		private void MenuLoad_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog
			{
				Title      = "Select Intel HEX File",
				Filter     = "Intel HEX Files (*.hex;*.obj)|*.hex;*.obj|All Files (*.*)|*.*",
				CheckFileExists = true,
			};

			if (dlg.ShowDialog(this) == true)
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
			RefreshPeripherals();
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
				RefreshPeripherals();
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

			// GPIO LEDs & Register Badges
			RefreshPinRows();

			// Grouped Peripherals Dashboard
			RefreshPeripherals();

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

		// ── Recent files handling ───────────────────────────────────────────
		private void LoadRecentFiles()
		{
			try
			{
				if (File.Exists(RecentFilesPath))
				{
					string json = File.ReadAllText(RecentFilesPath);
					RecentFiles = JsonSerializer.Deserialize<List<string>>(json) ?? new();
					RecentFiles.RemoveAll(f => !File.Exists(f));
				}
			}
			catch { RecentFiles = new(); }
		}

		private void SaveRecentFiles()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(RecentFilesPath)!);
				File.WriteAllText(RecentFilesPath, JsonSerializer.Serialize(RecentFiles));
			}
			catch { }
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

			if (RecentFiles.Count == 0)
			{
				var empty = new MenuItem
				{
					Header = "(No recent files)",
					IsEnabled = false,
					Style = (Style)FindResource("DarkMenuItem"),
				};
				MenuRecent.Items.Add(empty);
				return;
			}

			int i = 1;
			foreach (var file in RecentFiles)
			{
				string captured = file;
				var item = new MenuItem
				{
					Header = $"_{i++}  {Path.GetFileName(file)}",
					ToolTip = file,
					Style = (Style)FindResource("DarkMenuItem"),
				};
				item.Click += (s, e) => LoadHex(captured);
				MenuRecent.Items.Add(item);
			}

			MenuRecent.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)) });

			var clearItem = new MenuItem
			{
				Header = "Clear Recent Files",
				Style = (Style)FindResource("DarkMenuItem"),
			};
			clearItem.Click += (s, e) => { RecentFiles.Clear(); SaveRecentFiles(); RebuildRecentMenu(); };
			MenuRecent.Items.Add(clearItem);
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

			RefreshPinRows();
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
				DisassemblyByAddress[address] = line;
				address += Math.Max(wordLength, 1);
			}

			int totalBytes = lineCount * 2;
			for (int byteAddress = 0; byteAddress < totalBytes; byteAddress += 16)
			{
				var row = new HexRow
				{
					StartByteAddress = byteAddress,
					AddressText = string.Format("{0:X4}", byteAddress),
				};

				int count = Math.Min(16, totalBytes - byteAddress);
				for (int offset = 0; offset < count; offset++)
				{
					int currentByteAddress = byteAddress + offset;
					int wordAddress = currentByteAddress / 2;
					ushort word = Interpreter.Flash[wordAddress];
					byte value = (currentByteAddress % 2 == 0)
						? (byte)(word & 0xFF)
						: (byte)(word >> 8);

					row.Bytes.Add(new HexByteCell
					{
						ByteAddress = currentByteAddress,
						Text = string.Format("{0:X2}", value),
						TooltipText = $"Address: 0x{currentByteAddress:X4} (Word: 0x{wordAddress:X4})",
					});
				}

				HexRows.Add(row);
			}

			DisassemblyList.ItemsSource = DisassemblyLines;
			HexList.ItemsSource = HexRows;
			RefreshDisassemblyHighlight(forceScroll: true);
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

		// ── Add pin rows with full datasheet details ─────────────────────────
		private void AddPinRows(ItemsControl panel,
			IEnumerable<(AVRInterpreter.GPIOPin pin, string name)> pins)
		{
			var rows = new List<Border>();

			foreach (var (pin, name) in pins)
			{
				var info = PinCatalog.Get(name);

				var glow = new DropShadowEffect
				{
					ShadowDepth = 0,
					BlurRadius  = 8,
					Opacity     = 0,
					Color       = GlowGreen,
				};

				var led = new Ellipse
				{
					Width             = 10,
					Height            = 10,
					Fill              = BrushLow,
					Stroke            = new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70)),
					StrokeThickness   = 1,
					VerticalAlignment = VerticalAlignment.Center,
					Effect            = glow,
					ToolTip           = $"{name} ({info.ArduinoLabel}) — {info.AlternateDescription}",
					Cursor            = Cursors.Hand,
				};

				var toggle = new ToggleButton
				{
					Width             = 10,
					Height            = 10,
					Opacity           = 0,
					VerticalAlignment = VerticalAlignment.Center,
					Focusable         = false,
					Cursor            = Cursors.Hand,
					ToolTip           = $"{name} — Click to toggle INPUT level",
				};

				var capturedPin = pin;
				toggle.Checked   += (s, e) => { capturedPin.Value = true; };
				toggle.Unchecked += (s, e) => { capturedPin.Value = false; };

				var ledContainer = new Grid
				{
					Width = 12, Height = 12,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0, 0, 4, 0),
				};
				ledContainer.Children.Add(led);
				ledContainer.Children.Add(toggle);

				// Pin Name & Bit index
				var nameBlock = new TextBlock
				{
					Text = $"{name} (b{info.BitIndex})",
					Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
					FontFamily = new FontFamily("Consolas"),
					FontSize = 10,
					FontWeight = FontWeights.Bold,
					VerticalAlignment = VerticalAlignment.Center,
					Width = 52,
				};

				// Direction badge
				var dirText = new TextBlock
				{
					Text = "IN",
					FontSize = 9,
					FontFamily = new FontFamily("Consolas"),
					FontWeight = FontWeights.Bold,
					Foreground = BrushInHigh,
					HorizontalAlignment = HorizontalAlignment.Center,
				};
				var dirBadge = new Border
				{
					Background = BrushBadgeIn,
					BorderBrush = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
					BorderThickness = new Thickness(1),
					CornerRadius = new CornerRadius(3),
					Padding = new Thickness(4, 1, 4, 1),
					Width = 32,
					Margin = new Thickness(0, 0, 4, 0),
					VerticalAlignment = VerticalAlignment.Center,
					Child = dirText,
				};

				// Latches: P:0 I:0
				var latchText = new TextBlock
				{
					Text = "P:0 I:0",
					FontSize = 9,
					FontFamily = new FontFamily("Consolas"),
					Foreground = BrushDim,
					Width = 42,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0, 0, 4, 0),
				};

				// Pull-Up badge
				var pullUpText = new TextBlock
				{
					Text = "—",
					FontSize = 9,
					FontFamily = new FontFamily("Consolas"),
					Foreground = BrushDim,
					HorizontalAlignment = HorizontalAlignment.Center,
				};
				var pullUpBadge = new Border
				{
					Background = Brushes.Transparent,
					CornerRadius = new CornerRadius(3),
					Padding = new Thickness(3, 1, 3, 1),
					Width = 38,
					Margin = new Thickness(0, 0, 4, 0),
					VerticalAlignment = VerticalAlignment.Center,
					Child = pullUpText,
				};

				// Alternate functions tag
				var altText = new TextBlock
				{
					Text = info.AlternateFunctions,
					Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xA6, 0xF7)),
					FontFamily = new FontFamily("Segoe UI"),
					FontSize = 9,
					TextTrimming = TextTrimming.CharacterEllipsis,
					VerticalAlignment = VerticalAlignment.Center,
					ToolTip = $"{name} Alternate Functions:\n{info.AlternateDescription}\nPhysical: {info.PhysicalPin} | Arduino: {info.ArduinoLabel}",
				};

				var rowGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // LED
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) }); // Name
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // Dir
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); // Latches
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // PU
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Alt

				Grid.SetColumn(ledContainer, 0);
				Grid.SetColumn(nameBlock,    1);
				Grid.SetColumn(dirBadge,     2);
				Grid.SetColumn(latchText,    3);
				Grid.SetColumn(pullUpBadge,  4);
				Grid.SetColumn(altText,      5);

				rowGrid.Children.Add(ledContainer);
				rowGrid.Children.Add(nameBlock);
				rowGrid.Children.Add(dirBadge);
				rowGrid.Children.Add(latchText);
				rowGrid.Children.Add(pullUpBadge);
				rowGrid.Children.Add(altText);

				var rowBorder = new Border
				{
					Background = Brushes.Transparent,
					Padding = new Thickness(4, 2, 4, 2),
					Margin = new Thickness(0, 1, 0, 1),
					CornerRadius = new CornerRadius(4),
					Child = rowGrid,
				};

				rows.Add(rowBorder);

				AllPinRows.Add(new PinRow
				{
					Pin = pin,
					Info = info,
					Led = led,
					Glow = glow,
					Toggle = toggle,
					DirText = dirText,
					DirBadge = dirBadge,
					PullUpText = pullUpText,
					PullUpBadge = pullUpBadge,
					LatchText = latchText,
				});
			}

			panel.ItemsSource = rows;
		}

		// ── Refresh all pin LEDs and badges each tick ───────────────────────
		private void RefreshPinRows()
		{
			if (Interpreter == null) return;

			foreach (var row in AllPinRows)
			{
				bool isOutput = row.Pin.Direction == AVRInterpreter.GPIOPinDirection.OUTPUT;
				bool val      = row.Pin.Value;
				bool pullUp   = row.Pin.PullUp;
				bool portBit  = row.Pin.PortBit;
				bool pinBit   = row.Pin.PinBit;

				// LED state
				if (val != row.LastVal || isOutput != row.LastDir)
				{
					row.LastVal = val;
					if (val)
					{
						row.Led.Fill        = isOutput ? BrushHigh : BrushInHigh;
						row.Led.Stroke      = isOutput ? BrushHigh : BrushInHigh;
						row.Glow.Color      = isOutput ? GlowGreen : GlowBlue;
						row.Glow.Opacity    = 0.9;
						row.Glow.BlurRadius = 10;
					}
					else
					{
						row.Led.Fill        = BrushLow;
						row.Led.Stroke      = new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70));
						row.Glow.Opacity    = 0;
					}
				}

				// Direction badge
				if (isOutput != row.LastDir)
				{
					row.LastDir = isOutput;
					if (isOutput)
					{
						row.DirText.Text = "OUT";
						row.DirText.Foreground = BrushHigh;
						row.DirBadge.Background = BrushBadgeOut;
					}
					else
					{
						row.DirText.Text = "IN";
						row.DirText.Foreground = BrushInHigh;
						row.DirBadge.Background = BrushBadgeIn;
					}
				}

				// Latches (P:x I:x)
				if (portBit != row.LastPortBit || pinBit != row.LastPinBit)
				{
					row.LastPortBit = portBit;
					row.LastPinBit = pinBit;
					row.LatchText.Text = $"P:{(portBit ? 1 : 0)} I:{(pinBit ? 1 : 0)}";
				}

				// Pull-up badge
				if (pullUp != row.LastPullUp)
				{
					row.LastPullUp = pullUp;
					if (pullUp)
					{
						row.PullUpText.Text = "PULL-UP";
						row.PullUpText.Foreground = BrushAmber;
						row.PullUpBadge.Background = BrushBadgePu;
					}
					else
					{
						row.PullUpText.Text = "—";
						row.PullUpText.Foreground = BrushDim;
						row.PullUpBadge.Background = Brushes.Transparent;
					}
				}

				// Keep toggle in sync for INPUT pins
				if (!isOutput && row.Toggle.IsChecked != val)
					row.Toggle.IsChecked = val;
			}

			// Update Port summary banners
			byte pbVal = Interpreter.IO[0x05];
			byte pbDdr = Interpreter.IO[0x04];
			byte pbPin = Interpreter.IO[0x03];
			PortBHeader.Text = $"PORT: 0x{pbVal:X2} [{ToBin8(pbVal)}]  DDR: 0x{pbDdr:X2}  PIN: 0x{pbPin:X2}";

			byte pcVal = Interpreter.IO[0x08];
			byte pcDdr = Interpreter.IO[0x07];
			byte pcPin = Interpreter.IO[0x06];
			PortCHeader.Text = $"PORT: 0x{pcVal:X2} [{ToBin8(pcVal)}]  DDR: 0x{pcDdr:X2}  PIN: 0x{pcPin:X2}";

			byte pdVal = Interpreter.IO[0x0B];
			byte pdDdr = Interpreter.IO[0x0A];
			byte pdPin = Interpreter.IO[0x09];
			PortDHeader.Text = $"PORT: 0x{pdVal:X2} [{ToBin8(pdVal)}]  DDR: 0x{pdDdr:X2}  PIN: 0x{pdPin:X2}";
		}

		private static string ToBin8(byte b) => Convert.ToString(b, 2).PadLeft(8, '0');

		// ── Refresh Grouped Peripherals Dashboard ───────────────────────────
		private void RefreshPeripherals()
		{
			if (Interpreter == null) return;
			var ram = Interpreter.RAM;

			// ── Timer 0 ─────────────────────────────────────────
			byte tccr0a = ram[0x44];
			byte tccr0b = ram[0x45];
			byte tcnt0 = ram[0x46];
			byte ocr0a = ram[0x47];
			byte ocr0b = ram[0x48];
			byte tifr0 = ram[0x35];
			byte timsk0 = ram[0x6E];

			int cs0 = tccr0b & 0x07;
			string prescaler0 = cs0 switch
			{
				0 => "Clock: Stopped (Timer0 disabled)",
				1 => "Clock: clk / 1 (No prescaling)",
				2 => "Clock: clk / 8",
				3 => "Clock: clk / 64",
				4 => "Clock: clk / 256",
				5 => "Clock: clk / 1024",
				6 => "Clock: Ext T0 (Falling edge)",
				7 => "Clock: Ext T0 (Rising edge)",
				_ => "Clock: Stopped"
			};

			int wgm0 = ((tccr0b & 0x08) >> 1) | (tccr0a & 0x03);
			string mode0 = wgm0 switch
			{
				0 => "Mode: Normal (Top: 0xFF)",
				1 => "Mode: PWM, Phase Correct",
				2 => "Mode: CTC (Top: OCR0A)",
				3 => "Mode: Fast PWM (Top: 0xFF)",
				5 => "Mode: PWM, Phase Correct (Top: OCR0A)",
				7 => "Mode: Fast PWM (Top: OCR0A)",
				_ => "Mode: Reserved"
			};

			T0_ModeText.Text = mode0;
			T0_PrescalerText.Text = prescaler0;
			T0_TCNTText.Text = $"0x{tcnt0:X2} ({tcnt0})";
			T0_Bar.Value = tcnt0;
			T0_OCRAText.Text = $"OCR0A: 0x{ocr0a:X2} ({ocr0a})";
			T0_OCRBText.Text = $"OCR0B: 0x{ocr0b:X2} ({ocr0b})";
			T0_TCCRAText.Text = $"TCCR0A: 0x{tccr0a:X2} [{ToBin8(tccr0a)}]";
			T0_TCCRBText.Text = $"TCCR0B: 0x{tccr0b:X2} [{ToBin8(tccr0b)}]";
			T0_TIFRText.Text = $"TIFR0:  0x{tifr0:X2} [TOV0: {(tifr0 & 1):X}, OCF0A: {((tifr0 >> 1) & 1):X}, OCF0B: {((tifr0 >> 2) & 1):X}]";
			T0_TIMSKText.Text = $"TIMSK0: 0x{timsk0:X2} [TOIE0: {(timsk0 & 1):X}]";

			// ── Timer 1 ─────────────────────────────────────────
			byte tccr1a = ram[0x80];
			byte tccr1b = ram[0x81];
			byte tccr1c = ram[0x82];
			ushort tcnt1 = (ushort)((ram[0x85] << 8) | ram[0x84]);
			ushort ocr1a = (ushort)((ram[0x89] << 8) | ram[0x88]);
			ushort ocr1b = (ushort)((ram[0x8B] << 8) | ram[0x8A]);
			ushort icr1 = (ushort)((ram[0x87] << 8) | ram[0x86]);
			byte tifr1 = ram[0x36];

			int cs1 = tccr1b & 0x07;
			string prescaler1 = cs1 switch
			{
				0 => "Clock: Stopped",
				1 => "Clock: clk / 1",
				2 => "Clock: clk / 8",
				3 => "Clock: clk / 64",
				4 => "Clock: clk / 256",
				5 => "Clock: clk / 1024",
				_ => "Clock: Ext T1"
			};
			int wgm1 = ((tccr1b & 0x18) >> 1) | (tccr1a & 0x03);
			T1_ModeText.Text = $"Mode: WGM1={wgm1}";
			T1_PrescalerText.Text = prescaler1;
			T1_TCNTText.Text = $"0x{tcnt1:X4} ({tcnt1})";
			T1_Bar.Value = Math.Min(65535, (double)tcnt1);
			T1_OCRAText.Text = $"OCR1A: 0x{ocr1a:X4}";
			T1_OCRBText.Text = $"OCR1B: 0x{ocr1b:X4}";
			T1_ICRText.Text = $"ICR1:   0x{icr1:X4}";
			T1_TCCRAText.Text = $"TCCR1A: 0x{tccr1a:X2}  TCCR1B: 0x{tccr1b:X2}";
			T1_TCCRBText.Text = $"TCCR1C: 0x{tccr1c:X2}";
			T1_TIFRText.Text = $"TIFR1:  0x{tifr1:X2}";

			// ── Timer 2 ─────────────────────────────────────────
			byte tccr2a = ram[0xB0];
			byte tccr2b = ram[0xB1];
			byte tcnt2 = ram[0xB2];
			byte ocr2a = ram[0xB3];
			byte ocr2b = ram[0xB4];
			byte tifr2 = ram[0x37];
			byte timsk2 = ram[0x70];

			int cs2 = tccr2b & 0x07;
			string prescaler2 = cs2 switch
			{
				0 => "Clock: Stopped",
				1 => "Clock: clk / 1",
				2 => "Clock: clk / 8",
				3 => "Clock: clk / 32",
				4 => "Clock: clk / 64",
				5 => "Clock: clk / 128",
				6 => "Clock: clk / 256",
				7 => "Clock: clk / 1024",
				_ => "Clock: Stopped"
			};
			T2_PrescalerText.Text = prescaler2;
			T2_TCNTText.Text = $"0x{tcnt2:X2} ({tcnt2})";
			T2_Bar.Value = tcnt2;
			T2_OCRAText.Text = $"OCR2A: 0x{ocr2a:X2} ({ocr2a})";
			T2_OCRBText.Text = $"OCR2B: 0x{ocr2b:X2} ({ocr2b})";
			T2_TCCRAText.Text = $"TCCR2A: 0x{tccr2a:X2} [{ToBin8(tccr2a)}]";
			T2_TCCRBText.Text = $"TCCR2B: 0x{tccr2b:X2} [{ToBin8(tccr2b)}]";
			T2_TIFRText.Text = $"TIFR2:  0x{tifr2:X2}";
			T2_TIMSKText.Text = $"TIMSK2: 0x{timsk2:X2}";

			// ── USART ───────────────────────────────────────────
			byte udr0 = ram[0xC6];
			byte ucsr0a = ram[0xC0];
			byte ucsr0b = ram[0xC1];
			byte ucsr0c = ram[0xC2];
			ushort ubrr0 = (ushort)((ram[0xC5] << 8) | ram[0xC4]);

			bool rxen = (ucsr0b & 0x10) != 0;
			bool txen = (ucsr0b & 0x08) != 0;
			bool u2x = (ucsr0a & 0x02) != 0;
			if (ubrr0 > 0)
			{
				long fosc = 16000000;
				long baud = fosc / ((u2x ? 8 : 16) * (ubrr0 + 1));
				USART_BaudText.Text = $"Baud Rate: {baud} bps (UBRR0 = {ubrr0})";
			}
			else
			{
				USART_BaudText.Text = "Baud Rate: Disabled (UBRR0 = 0)";
			}

			char ch = (udr0 >= 32 && udr0 <= 126) ? (char)udr0 : '.';
			USART_UDRText.Text = $"UDR0 = 0x{udr0:X2} ({udr0}) '{ch}'";
			USART_UBRRText.Text = $"UBRR0H: 0x{ram[0xC5]:X2} | UBRR0L: 0x{ram[0xC4]:X2}  (UBRR = {ubrr0})";
			USART_UCSRAText.Text = $"0x{ucsr0a:X2} [RXC: {(ucsr0a >> 7) & 1}, TXC: {(ucsr0a >> 6) & 1}, UDRE: {(ucsr0a >> 5) & 1}, U2X: {(u2x ? 1 : 0)}]";
			USART_UCSRBText.Text = $"0x{ucsr0b:X2} [RXEN: {(rxen ? 1 : 0)}, TXEN: {(txen ? 1 : 0)}]";
			USART_UCSRCText.Text = $"0x{ucsr0c:X2} [Async, 8N1 default]";

			// ── ADC & DAC ───────────────────────────────────────
			byte admux = ram[0x7C];
			byte adcsra = ram[0x7A];
			byte adcsrb = ram[0x7B];
			ushort rawAdc = (ushort)((ram[0x79] << 8) | ram[0x78]);

			int adcChan = admux & 0x0F;
			ADC_ChannelText.Text = $"Selected Channel: ADC{adcChan} (Pin PC{adcChan})";
			int refs = (admux >> 6) & 0x03;
			ADC_RefText.Text = refs switch
			{
				0 => "Reference: AREF (External pin)",
				1 => "Reference: AVCC with external capacitor at AREF",
				3 => "Reference: Internal 1.1V Voltage Reference",
				_ => "Reference: Reserved"
			};
			ADC_RawText.Text = $"{rawAdc} (0x{rawAdc:X4})";
			ADC_Bar.Value = Math.Min(1023, (int)rawAdc);
			ADC_AdmuxText.Text = $"ADMUX:  0x{admux:X2} [{ToBin8(admux)}]";
			ADC_AdcsraText.Text = $"ADCSRA: 0x{adcsra:X2} [ADEN: {(adcsra >> 7) & 1}, ADSC: {(adcsra >> 6) & 1}, ADIF: {(adcsra >> 4) & 1}]";
			ADC_AdcsrbText.Text = $"ADCSRB: 0x{adcsrb:X2}";

			byte dacVal = Interpreter.DACUnit?.OutputValue ?? 0;
			DAC_Bar.Value = dacVal;

			// ── SPI & TWI ───────────────────────────────────────
			byte spcr = ram[0x4C];
			byte spsr = ram[0x4D];
			byte spdr = ram[0x4E];
			bool spe = (spcr & 0x40) != 0;
			bool mstr = (spcr & 0x10) != 0;
			SPI_StatusText.Text = spe ? $"Status: Enabled ({(mstr ? "Master" : "Slave")})" : "Status: Disabled (SPE = 0)";
			SPI_SpcrText.Text = $"SPCR: 0x{spcr:X2} [SPE: {(spe ? 1 : 0)}, MSTR: {(mstr ? 1 : 0)}]";
			SPI_SpsrText.Text = $"SPSR: 0x{spsr:X2} [SPIF: {(spsr >> 7) & 1}, 2X: {spsr & 1}]";
			SPI_SpdrText.Text = $"SPDR: 0x{spdr:X2} ({spdr})";

			byte twbr = ram[0xB8];
			byte twsr = ram[0xB9];
			byte twcr = ram[0xBC];
			bool twen = (twcr & 0x04) != 0;
			TWI_StatusText.Text = twen ? "Status: Enabled (TWEN = 1)" : "Status: Disabled (TWEN = 0)";
			TWI_TwbrText.Text = $"TWBR: 0x{twbr:X2} ({twbr})";
			TWI_TwsrText.Text = $"TWSR: 0x{twsr:X2} [Status: 0x{twsr & 0xF8:X2}]";
			TWI_TwcrText.Text = $"TWCR: 0x{twcr:X2} [TWINT: {(twcr >> 7) & 1}, TWEN: {(twen ? 1 : 0)}]";

			// ── Core & SREG ─────────────────────────────────────
			byte sreg = ram[0x5F];
			SREG_HexText.Text = $"SREG: 0x{sreg:X2} [{ToBin8(sreg)}]";
			UpdateFlagBadge(SREG_I, (sreg & 0x80) != 0, "I");
			UpdateFlagBadge(SREG_T, (sreg & 0x40) != 0, "T");
			UpdateFlagBadge(SREG_H, (sreg & 0x20) != 0, "H");
			UpdateFlagBadge(SREG_S, (sreg & 0x10) != 0, "S");
			UpdateFlagBadge(SREG_V, (sreg & 0x08) != 0, "V");
			UpdateFlagBadge(SREG_N, (sreg & 0x04) != 0, "N");
			UpdateFlagBadge(SREG_Z, (sreg & 0x02) != 0, "Z");
			UpdateFlagBadge(SREG_C, (sreg & 0x01) != 0, "C");

			ushort sp = (ushort)((ram[0x5E] << 8) | ram[0x5D]);
			SP_Text.Text = $"SP = 0x{sp:X4} (SPL: 0x{ram[0x5D]:X2}, SPH: 0x{ram[0x5E]:X2})";

			byte eecr = ram[0x3F];
			byte eedr = ram[0x40];
			ushort eear = (ushort)((ram[0x42] << 8) | ram[0x41]);
			EEPROM_Text.Text = $"EECR: 0x{eecr:X2}  |  EEDR: 0x{eedr:X2}  |  EEAR: 0x{eear:X4}";
		}

		private static void UpdateFlagBadge(Border badge, bool isSet, string name)
		{
			badge.Background = isSet ? BrushHigh : BrushDark;
			if (badge.Child is TextBlock tb)
			{
				tb.Foreground = isSet ? new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x25)) : BrushDim;
				tb.FontWeight = isSet ? FontWeights.Bold : FontWeights.Normal;
				tb.Text = $"{name}: {(isSet ? 1 : 0)}";
			}
		}
	}
}
