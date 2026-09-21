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
			Flash  = new ushort[0x4000];
			EEPROM = new byte[0x400];
			RAM	= new ObservableCollection<byte>(new byte[0x0900]);
			R	    = new MappedArray<byte>(RAM, 0x0000, 0x001F);
			IO	    = new MappedArray<byte>(RAM, 0x0020, 0x005F);
			ExtIO  = new MappedArray<byte>(RAM, 0x0060, 0x00FF);
			SRAM   = new MappedArray<byte>(RAM, 0x0100, 0x08FF);

			SP = (ushort)SRAM.End;

			PORTB = new GPIOB(IO);
			PORTC = new GPIOC(IO);
			PORTD = new GPIOD(IO);
			ADCUnit = new ADC(RAM);
			EEPROMUnit = new EEPROMController(RAM, EEPROM);
			Timer0Unit = new Timer0(RAM);
			DACUnit = new DAC();

			RAMChanged += new NotifyCollectionChangedEventHandler(Atmega328Interpreter_RAMChanged);
		}

		public MappedArray<byte> ExtIO { get; private set; }
		public ADC ADCUnit { get; private set; }
		public EEPROMController EEPROMUnit { get; private set; }
		public Timer0 Timer0Unit { get; private set; }
		public DAC DACUnit { get; private set; }

		public override void Execute()
		{
			base.Execute();
			Timer0Unit.Step(1);
		}

		public GPIOB PORTB { get; private set; }
		public GPIOC PORTC { get; private set; }
		public GPIOD PORTD { get; private set; }

		private void Atmega328Interpreter_RAMChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != NotifyCollectionChangedAction.Replace)
				return;

			if (e.NewStartingIndex >= IO.Start && e.NewStartingIndex <= IO.End)
			{
				switch (e.NewStartingIndex - IO.Start)
				{
					case 0x03: // PINB 0x03 (0x23)
					case 0x05: // PORTB 0x05 (0x25)
						PORTB.InvokeValueChanged(new GPIOValueChangedEventArgs((byte)e.OldItems[0], (byte)e.NewItems[0]));
						break;
					case 0x06: // PINC 0x06 (0x26)
					case 0x08: // PORTC 0x08 (0x28)
						PORTC.InvokeValueChanged(new GPIOValueChangedEventArgs((byte)e.OldItems[0], (byte)e.NewItems[0]));
						break;
					case 0x09: // PIND 0x09 (0x29)
					case 0x0B: // PORTD 0x0B (0x2B)
						PORTD.InvokeValueChanged(new GPIOValueChangedEventArgs((byte)e.OldItems[0], (byte)e.NewItems[0]));
						break;
				}
			}
			else if (e.NewStartingIndex == 0x7A) // ADCSRA
			{
				ADCUnit.Update();
			}
			else if (e.NewStartingIndex == 0x3F) // EECR
			{
				EEPROMUnit.Update();
			}
			else if (e.NewStartingIndex == 0xFE) // Virtual DAC
			{
				DACUnit.OutputValue = (byte)e.NewItems[0];
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
				byte eecr = RAM[0x3F];
				ushort eear = (ushort)((RAM[0x42] << 8) | RAM[0x41]);
				eear &= (ushort)(EEPROM.Length - 1);

				if ((eecr & 0x01) != 0) // EERE - Read Enable
				{
					RAM[0x40] = EEPROM[eear]; // EEDR
					RAM[0x3F] &= 0xFE; // Clear EERE
				}

				if ((eecr & 0x02) != 0) // EEPE - Write Enable
				{
					// In a real chip, EEMPE must be set first.
					// For simulation, we check if EEMPE (bit 2) is set.
					if ((eecr & 0x04) != 0)
					{
						EEPROM[eear] = RAM[0x40]; // EEDR
						RAM[0x3F] &= 0xF9; // Clear EEPE and EEMPE
					}
					else
					{
						RAM[0x3F] &= 0xFD; // Clear EEPE if EEMPE was not set
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
				byte tccr0b = RAM[0x45];
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

				prescalerCounter += cycles;
				if (prescalerCounter >= divider)
				{
					int ticks = prescalerCounter / divider;
					prescalerCounter %= divider;

					for (int i = 0; i < ticks; i++)
					{
						byte tcnt0 = RAM[0x46];
						if (tcnt0 == 0xFF)
						{
							RAM[0x46] = 0;
							// Set TOV0 in TIFR0 (0x35)
							RAM[0x35] |= 0x01;
						}
						else
						{
							RAM[0x46]++;
						}
					}
				}
			}
		}

		public sealed class DAC
		{
			public byte OutputValue { get; set; }
			public double Voltage => (OutputValue / 255.0) * 5.0;
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
				byte adcsra = RAM[0x7A];
				if ((adcsra & 0x40) != 0) // ADSC is set
				{
					// Perform conversion
					ushort result = (ushort)(Math.Max(0, Math.Min(5.0, AnalogInput)) / 5.0 * 1023.0);

					byte admux = RAM[0x7C];
					if ((admux & 0x20) != 0) // ADLAR is set
					{
						RAM[0x78] = (byte)((result << 6) & 0xFF);
						RAM[0x79] = (byte)(result >> 2);
					}
					else
					{
						RAM[0x78] = (byte)(result & 0xFF);
						RAM[0x79] = (byte)(result >> 8);
					}

					// Set ADIF, clear ADSC
					RAM[0x7A] = (byte)((adcsra | 0x10) & ~0x40);
				}
			}
		}

		public sealed class GPIOB : GPIO
		{
			public GPIOB(IList<byte> IO)
			{
				this.IO = IO;

				DDRx  = 0x04;
				PINx  = 0x03;
				PORTx = 0x05;

				PB0 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x01 };
				PB1 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x02 };
				PB2 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x04 };
				PB3 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x08 };
				PB4 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x10 };
				PB5 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x20 };
				PB6 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x40 };
				PB7 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x80 };
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
					PB0.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x01));
					PB1.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x02));
					PB2.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x04));
					PB3.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x08));
					PB4.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x10));
					PB5.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x20));
					PB6.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x40));
					PB7.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x80));
				}
			}
		}

		public sealed class GPIOC : GPIO
		{
			public GPIOC(IList<byte> IO)
			{
				this.IO = IO;

				DDRx  = 0x07;
				PINx  = 0x06;
				PORTx = 0x08;

				PC0 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x01 };
				PC1 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x02 };
				PC2 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x04 };
				PC3 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x08 };
				PC4 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x10 };
				PC5 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x20 };
				PC6 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x40 };
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
					PC0.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x01));
					PC1.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x02));
					PC2.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x04));
					PC3.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x08));
					PC4.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x10));
					PC5.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x20));
					PC6.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x40));
				}
			}
		}

		public sealed class GPIOD : GPIO
		{
			public GPIOD(IList<byte> IO)
			{
				this.IO = IO;

				DDRx  = 0x0A;
				PINx  = 0x09;
				PORTx = 0x0B;

				PD0 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x01 };
				PD1 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x02 };
				PD2 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x04 };
				PD3 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x08 };
				PD4 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x10 };
				PD5 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x20 };
				PD6 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x40 };
				PD7 = new GPIOPin { IO = this.IO, DDRx = DDRx, PINx = PINx, PORTx = PORTx, nMask = 0x80 };
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
					PD0.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x01));
					PD1.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x02));
					PD2.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x04));
					PD3.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x08));
					PD4.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x10));
					PD5.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x20));
					PD6.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x40));
					PD7.InvokeValueChanged(new GPIOPinValueChangedEventArgs(e.OldValue, e.NewValue, 0x80));
				}
			}
		}
	}
}