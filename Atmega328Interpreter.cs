using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace AVR_Simulator
{
	public sealed class Atmega328Interpreter : AVRInterpreter
	{
		public Atmega328Interpreter()
		{
			this.Flash  = new ushort[0x4000];
			this.EEPROM = new byte[0x400];
			this.RAM	= new ObservableCollection<byte>(new byte[0x0900]);
			this.R	    = new MappedArray<byte>(this.RAM, 0x0000, 0x001F);
			this.IO	    = new MappedArray<byte>(this.RAM, 0x0020, 0x005F);
			this.ExtIO  = new MappedArray<byte>(this.RAM, 0x0060, 0x00FF);
			this.SRAM   = new MappedArray<byte>(this.RAM, 0x0100, 0x08FF);

			this.SP = (ushort)this.SRAM.End;

			this.PORTB = new GPIOB(this.IO);
			this.PORTC = new GPIOC(this.IO);
			this.PORTD = new GPIOD(this.IO);
			this.ADCUnit = new ADC(this.RAM);
			this.EEPROMUnit = new EEPROMController(this.RAM, this.EEPROM);
			this.Timer0Unit = new Timer0(this.RAM);
			this.DACUnit = new DAC();
			this.UARTUnit = new UARTController(this.RAM);

			this.RAMChanged += new NotifyCollectionChangedEventHandler(this.Atmega328Interpreter_RAMChanged);
		}

		public MappedArray<byte> ExtIO { get; private set; }
		public ADC ADCUnit { get; private set; }
		public EEPROMController EEPROMUnit { get; private set; }
		public Timer0 Timer0Unit { get; private set; }
		public DAC DACUnit { get; private set; }
		public UARTController UARTUnit { get; private set; }

		public override void Execute()
		{
			base.Execute();
			this.Timer0Unit.Step(1);
		}

		public GPIOB PORTB { get; private set; }
		public GPIOC PORTC { get; private set; }
		public GPIOD PORTD { get; private set; }

		private void Atmega328Interpreter_RAMChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != NotifyCollectionChangedAction.Replace)
				return;

			if (e.NewStartingIndex >= this.IO.Start && e.NewStartingIndex <= this.IO.End)
			{
				switch (e.NewStartingIndex - this.IO.Start)
				{
					case 0x03: // PINB 0x03 (0x23)
					case 0x05: // PORTB 0x05 (0x25)
						this.PORTB.InvokeValueChanged(new GPIOValueChangedEventArgs((byte)e.OldItems[0], (byte)e.NewItems[0]));
						break;
					case 0x06: // PINC 0x06 (0x26)
					case 0x08: // PORTC 0x08 (0x28)
						this.PORTC.InvokeValueChanged(new GPIOValueChangedEventArgs((byte)e.OldItems[0], (byte)e.NewItems[0]));
						break;
					case 0x09: // PIND 0x09 (0x29)
					case 0x0B: // PORTD 0x0B (0x2B)
						this.PORTD.InvokeValueChanged(new GPIOValueChangedEventArgs((byte)e.OldItems[0], (byte)e.NewItems[0]));
						break;
				}
			}
			else if (e.NewStartingIndex == 0x7A) // ADCSRA
			{
				this.ADCUnit.Update();
			}
			else if (e.NewStartingIndex == 0x3F) // EECR
			{
				this.EEPROMUnit.Update();
			}
			else if (e.NewStartingIndex == 0xFE) // Virtual DAC
			{
				this.DACUnit.OutputValue = (byte)e.NewItems[0];
			}
			else if (e.NewStartingIndex == 0xC6) // UDR0
			{
				this.UARTUnit.OnUDR0Write();
			}
		}

		public sealed class EEPROMController
		{
			public EEPROMController(IList<byte> RAM, byte[] EEPROM)
			{
				this.RAM = RAM;
				this.EEPROM = EEPROM;
			}

			private IList<byte> RAM;
			private byte[] EEPROM;

			public void Update()
			{
				byte eecr = this.RAM[0x3F];
				ushort eear = (ushort)((this.RAM[0x42] << 8) | this.RAM[0x41]);
				eear &= (ushort)(this.EEPROM.Length - 1);

				if ((eecr & 0x01) != 0) // EERE - Read Enable
				{
					this.RAM[0x40] = this.EEPROM[eear]; // EEDR
					this.RAM[0x3F] &= 0xFE; // Clear EERE
				}

				if ((eecr & 0x02) != 0) // EEPE - Write Enable
				{
					// In a real chip, EEMPE must be set first.
					// For simulation, we check if EEMPE (bit 2) is set.
					if ((eecr & 0x04) != 0)
					{
						this.EEPROM[eear] = this.RAM[0x40]; // EEDR
						this.RAM[0x3F] &= 0xF9; // Clear EEPE and EEMPE
					}
					else
					{
						this.RAM[0x3F] &= 0xFD; // Clear EEPE if EEMPE was not set
					}
				}
			}
		}

		public sealed class Timer0
		{
			public Timer0(IList<byte> RAM)
			{
				this.RAM = RAM;
			}

			private IList<byte> RAM;
			private int prescalerCounter = 0;

			public void Step(int cycles)
			{
				byte tccr0b = this.RAM[0x45];
				int cs = tccr0b & 0x07;
				if (cs == 0) return; // Stopped

				int divider = 1;
				switch (cs)
				{
					case 1: divider = 1; break;
					case 2: divider = 8; break;
					case 3: divider = 64; break;
					case 4: divider = 256; break;
					case 5: divider = 1024; break;
					default: return; // External clock not implemented
				}

				this.prescalerCounter += cycles;
				if (this.prescalerCounter >= divider)
				{
					int ticks = this.prescalerCounter / divider;
					this.prescalerCounter %= divider;

					for (int i = 0; i < ticks; i++)
					{
						byte tcnt0 = this.RAM[0x46];
						if (tcnt0 == 0xFF)
						{
							this.RAM[0x46] = 0;
							// Set TOV0 in TIFR0 (0x35)
							this.RAM[0x35] |= 0x01;
						}
						else
						{
							this.RAM[0x46]++;
						}
					}
				}
			}
		}

		public sealed class DAC
		{
			public byte OutputValue { get; set; }
			public double Voltage => (this.OutputValue / 255.0) * 5.0;
		}

		public sealed class UARTController
		{
			public UARTController(IList<byte> RAM)
			{
				this.RAM = RAM;
				// Initial state: TX Empty
				this.RAM[0xC0] |= 0x20; // UDRE0
			}

			private IList<byte> RAM;
			public event EventHandler<byte> DataTransmitted;

			public void OnUDR0Write()
			{
				byte ucsr0b = this.RAM[0xC1];
				if ((ucsr0b & 0x08) != 0) // TXEN0
				{
					byte data = this.RAM[0xC6];
					this.DataTransmitted?.Invoke(this, data);

					// Set TXC0 (bit 6) and keep UDRE0 (bit 5)
					this.RAM[0xC0] |= 0x60;
				}
			}

			public void ReceiveByte(byte data)
			{
				byte ucsr0b = this.RAM[0xC1];
				if ((ucsr0b & 0x10) != 0) // RXEN0
				{
					this.RAM[0xC6] = data;
					this.RAM[0xC0] |= 0x80; // RXC0
				}
			}
		}

		public sealed class ADC
		{
			public ADC(IList<byte> RAM)
			{
				this.RAM = RAM;
			}

			private IList<byte> RAM;

			public double AnalogInput { get; set; } // 0.0 to 5.0

			public void Update()
			{
				byte adcsra = this.RAM[0x7A];
				if ((adcsra & 0x40) != 0) // ADSC is set
				{
					// Perform conversion
					ushort result = (ushort)(Math.Max(0, Math.Min(5.0, this.AnalogInput)) / 5.0 * 1023.0);

					byte admux = this.RAM[0x7C];
					if ((admux & 0x20) != 0) // ADLAR is set
					{
						this.RAM[0x78] = (byte)((result << 6) & 0xFF);
						this.RAM[0x79] = (byte)(result >> 2);
					}
					else
					{
						this.RAM[0x78] = (byte)(result & 0xFF);
						this.RAM[0x79] = (byte)(result >> 8);
					}

					// Set ADIF, clear ADSC
					this.RAM[0x7A] = (byte)((adcsra | 0x10) & ~0x40);
				}
			}
		}

		public sealed class GPIOB : GPIO
		{
			public GPIOB(IList<byte> IO)
			{
				this.IO = IO;

				this.DDRx  = 0x04;
				this.PINx  = 0x03;
				this.PORTx = 0x05;

				this.PB0 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x01 };
				this.PB1 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x02 };
				this.PB2 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x04 };
				this.PB3 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x08 };
				this.PB4 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x10 };
				this.PB5 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x20 };
				this.PB6 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x40 };
				this.PB7 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x80 };
			}

			public GPIOPin PB0 { get; private set; }
			public GPIOPin PB1 { get; private set; }
			public GPIOPin PB2 { get; private set; }
			public GPIOPin PB3 { get; private set; }
			public GPIOPin PB4 { get; private set; }
			public GPIOPin PB5 { get; private set; }
			public GPIOPin PB6 { get; private set; }
			public GPIOPin PB7 { get; private set; }

			public override void InvokeValueChanged(GPIOValueChangedEventArgs e)
			{
				base.InvokeValueChanged(e);

				if (e.OldValue != e.NewValue)
				{
					this.PB0.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x01));
					this.PB1.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x02));
					this.PB2.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x04));
					this.PB3.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x08));
					this.PB4.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x10));
					this.PB5.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x20));
					this.PB6.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x40));
					this.PB7.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x80));
				}
			}
		}

		public sealed class GPIOC : GPIO
		{
			public GPIOC(IList<byte> IO)
			{
				this.IO = IO;

				this.DDRx  = 0x07;
				this.PINx  = 0x06;
				this.PORTx = 0x08;

				this.PC0 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x01 };
				this.PC1 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x02 };
				this.PC2 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x04 };
				this.PC3 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x08 };
				this.PC4 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x10 };
				this.PC5 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x20 };
				this.PC6 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x40 };
			}

			public GPIOPin PC0 { get; private set; }
			public GPIOPin PC1 { get; private set; }
			public GPIOPin PC2 { get; private set; }
			public GPIOPin PC3 { get; private set; }
			public GPIOPin PC4 { get; private set; }
			public GPIOPin PC5 { get; private set; }
			public GPIOPin PC6 { get; private set; }

			public override void InvokeValueChanged(GPIOValueChangedEventArgs e)
			{
				base.InvokeValueChanged(e);

				if (e.OldValue != e.NewValue)
				{
					this.PC0.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x01));
					this.PC1.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x02));
					this.PC2.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x04));
					this.PC3.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x08));
					this.PC4.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x10));
					this.PC5.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x20));
					this.PC6.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x40));
				}
			}
		}

		public sealed class GPIOD : GPIO
		{
			public GPIOD(IList<byte> IO)
			{
				this.IO = IO;

				this.DDRx  = 0x0A;
				this.PINx  = 0x09;
				this.PORTx = 0x0B;

				this.PD0 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x01 };
				this.PD1 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x02 };
				this.PD2 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x04 };
				this.PD3 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x08 };
				this.PD4 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x10 };
				this.PD5 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x20 };
				this.PD6 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x40 };
				this.PD7 = new GPIOPin { IO = this.IO, DDRx = this.DDRx, PINx = this.PINx, PORTx = this.PORTx, nMask = 0x80 };
			}

			public GPIOPin PD0 { get; private set; }
			public GPIOPin PD1 { get; private set; }
			public GPIOPin PD2 { get; private set; }
			public GPIOPin PD3 { get; private set; }
			public GPIOPin PD4 { get; private set; }
			public GPIOPin PD5 { get; private set; }
			public GPIOPin PD6 { get; private set; }
			public GPIOPin PD7 { get; private set; }

			public override void InvokeValueChanged(GPIOValueChangedEventArgs e)
			{
				base.InvokeValueChanged(e);

				if (e.OldValue != e.NewValue)
				{
					this.PD0.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x01));
					this.PD1.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x02));
					this.PD2.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x04));
					this.PD3.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x08));
					this.PD4.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x10));
					this.PD5.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x20));
					this.PD6.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x40));
					this.PD7.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x80));
				}
			}
		}
	}
}