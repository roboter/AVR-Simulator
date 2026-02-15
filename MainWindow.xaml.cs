using System;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace AVR_Simulator
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			ConsoleManager.Show();

			this.Worker = new BackgroundWorker()
			{
				WorkerSupportsCancellation = true
			};
			this.Worker.DoWork += new DoWorkEventHandler(this.Worker_DoWork);

			InitializeComponent();

			this.ADCTimer = new DispatcherTimer();
			this.ADCTimer.Interval = TimeSpan.FromMilliseconds(50);
			this.ADCTimer.Tick += new EventHandler(this.ADCTimer_Tick);
			this.ADCTimer.Start();
			this.StartTime = DateTime.Now;
		}

		private BackgroundWorker Worker;
		private Atmega328Interpreter Interpreter;
		private DispatcherTimer ADCTimer;
		private DateTime StartTime;

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			this.Worker.RunWorkerAsync();
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			this.Worker.CancelAsync();
		}

		private void ADCTimer_Tick(object sender, EventArgs e)
		{
			if (this.Interpreter == null || this.Interpreter.ADCUnit == null)
				return;

			double analogValue = 0;
			if (this.SineWaveCheckBox.IsChecked == true)
			{
				double t = (DateTime.Now - this.StartTime).TotalSeconds;
				double freq = this.FreqSlider.Value;
				double amp = this.AmpSlider.Value;
				analogValue = 2.5 + amp * Math.Sin(2 * Math.PI * freq * t);
			}
			else
			{
				analogValue = this.VoltageSlider.Value;
			}

			this.Interpreter.ADCUnit.AnalogInput = analogValue;
			this.CurrentValueText.Text = string.Format("Current ADC Input: {0:F2}V", analogValue);

			if (this.Interpreter.DACUnit != null)
			{
				this.DACOutputText.Text = string.Format("DAC Voltage: {0:F2}V (0x{1:X2})",
					this.Interpreter.DACUnit.Voltage,
					this.Interpreter.DACUnit.OutputValue);
			}
		}

		private void Worker_DoWork(object sender, DoWorkEventArgs e)
		{
			this.Interpreter = new Atmega328Interpreter();

			/*this.Interpreter.Load(
				IntelHEX.Parse(@"C:\Users\Tom\Documents\Atmel Studio\Projects\Blink\Debug\Blink.hex"),
				IntelHEX.Parse(@"C:\Users\Tom\Documents\Atmel Studio\Projects\Blink\Debug\Blink.eep")
				);*/

			if (System.IO.File.Exists("Blink.hex"))
			{
				this.Interpreter.Load(IntelHEX.Parse("Blink.hex"));
			}

			this.Interpreter.PORTB.PB5.ValueChanged += new EventHandler<AVRInterpreter.GPIOPinValueChangedEventArgs>(this.PB5_ValueChanged);

			for (; !this.Worker.CancellationPending; )
			{
				this.Interpreter.Execute();
			}
		}

		private void PB5_ValueChanged(object sender, AVRInterpreter.GPIOPinValueChangedEventArgs e)
		{
			this.Dispatcher.Invoke(new Action(() =>
			{
				this.Background = e.NewValue ? Brushes.Red : Brushes.Black;
			}));
		}
	}
}
