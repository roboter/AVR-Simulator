using System;
using System.Collections.Generic;

namespace AVR_Simulator
{
	/// <summary>
	/// Metadata for an AVR microcontroller pin according to the ATmega328P datasheet.
	/// </summary>
	public class PinDatasheetInfo
	{
		public string PinName { get; set; } = string.Empty;
		public string PortName { get; set; } = string.Empty;
		public int BitIndex { get; set; }
		public string PhysicalPin { get; set; } = string.Empty;
		public string ArduinoLabel { get; set; } = string.Empty;
		public string AlternateFunctions { get; set; } = string.Empty;
		public string AlternateDescription { get; set; } = string.Empty;
	}

	/// <summary>
	/// Catalog of official ATmega328P pin definitions from the Microchip datasheet.
	/// </summary>
	public static class PinCatalog
	{
		private static readonly Dictionary<string, PinDatasheetInfo> PinMap =
			new Dictionary<string, PinDatasheetInfo>(StringComparer.OrdinalIgnoreCase)
			{
				// ── PORT B ──────────────────────────────────────────────────────────
				["PB0"] = new PinDatasheetInfo
				{
					PinName = "PB0", PortName = "PORTB", BitIndex = 0,
					PhysicalPin = "Pin 14", ArduinoLabel = "D8",
					AlternateFunctions = "ICP1 • CLKO • PCINT0",
					AlternateDescription = "Timer1 Input Capture / Divided Clock Output / Pin Change Interrupt 0"
				},
				["PB1"] = new PinDatasheetInfo
				{
					PinName = "PB1", PortName = "PORTB", BitIndex = 1,
					PhysicalPin = "Pin 15", ArduinoLabel = "D9 (PWM)",
					AlternateFunctions = "OC1A • PCINT1",
					AlternateDescription = "Timer1 Output Compare A / Pin Change Interrupt 1"
				},
				["PB2"] = new PinDatasheetInfo
				{
					PinName = "PB2", PortName = "PORTB", BitIndex = 2,
					PhysicalPin = "Pin 16", ArduinoLabel = "D10 (PWM)",
					AlternateFunctions = "SS • OC1B • PCINT2",
					AlternateDescription = "SPI Slave Select / Timer1 Output Compare B / Pin Change Interrupt 2"
				},
				["PB3"] = new PinDatasheetInfo
				{
					PinName = "PB3", PortName = "PORTB", BitIndex = 3,
					PhysicalPin = "Pin 17", ArduinoLabel = "D11 (PWM)",
					AlternateFunctions = "MOSI • OC2A • PCINT3",
					AlternateDescription = "SPI Master Out Slave In / Timer2 Output Compare A / Pin Change Interrupt 3"
				},
				["PB4"] = new PinDatasheetInfo
				{
					PinName = "PB4", PortName = "PORTB", BitIndex = 4,
					PhysicalPin = "Pin 18", ArduinoLabel = "D12",
					AlternateFunctions = "MISO • PCINT4",
					AlternateDescription = "SPI Master In Slave Out / Pin Change Interrupt 4"
				},
				["PB5"] = new PinDatasheetInfo
				{
					PinName = "PB5", PortName = "PORTB", BitIndex = 5,
					PhysicalPin = "Pin 19", ArduinoLabel = "D13 (LED)",
					AlternateFunctions = "SCK • PCINT5",
					AlternateDescription = "SPI Master Clock / Pin Change Interrupt 5 / Arduino Built-in LED"
				},
				["PB6"] = new PinDatasheetInfo
				{
					PinName = "PB6", PortName = "PORTB", BitIndex = 6,
					PhysicalPin = "Pin 9", ArduinoLabel = "XTAL1",
					AlternateFunctions = "TOSC1 • XTAL1 • PCINT6",
					AlternateDescription = "Timer Oscillator 1 / Crystal Oscillator 1 / Pin Change Interrupt 6"
				},
				["PB7"] = new PinDatasheetInfo
				{
					PinName = "PB7", PortName = "PORTB", BitIndex = 7,
					PhysicalPin = "Pin 10", ArduinoLabel = "XTAL2",
					AlternateFunctions = "TOSC2 • XTAL2 • PCINT7",
					AlternateDescription = "Timer Oscillator 2 / Crystal Oscillator 2 / Pin Change Interrupt 7"
				},

				// ── PORT C ──────────────────────────────────────────────────────────
				["PC0"] = new PinDatasheetInfo
				{
					PinName = "PC0", PortName = "PORTC", BitIndex = 0,
					PhysicalPin = "Pin 23", ArduinoLabel = "A0",
					AlternateFunctions = "ADC0 • PCINT8",
					AlternateDescription = "ADC Analog Input Channel 0 / Pin Change Interrupt 8"
				},
				["PC1"] = new PinDatasheetInfo
				{
					PinName = "PC1", PortName = "PORTC", BitIndex = 1,
					PhysicalPin = "Pin 24", ArduinoLabel = "A1",
					AlternateFunctions = "ADC1 • PCINT9",
					AlternateDescription = "ADC Analog Input Channel 1 / Pin Change Interrupt 9"
				},
				["PC2"] = new PinDatasheetInfo
				{
					PinName = "PC2", PortName = "PORTC", BitIndex = 2,
					PhysicalPin = "Pin 25", ArduinoLabel = "A2",
					AlternateFunctions = "ADC2 • PCINT10",
					AlternateDescription = "ADC Analog Input Channel 2 / Pin Change Interrupt 10"
				},
				["PC3"] = new PinDatasheetInfo
				{
					PinName = "PC3", PortName = "PORTC", BitIndex = 3,
					PhysicalPin = "Pin 26", ArduinoLabel = "A3",
					AlternateFunctions = "ADC3 • PCINT11",
					AlternateDescription = "ADC Analog Input Channel 3 / Pin Change Interrupt 11"
				},
				["PC4"] = new PinDatasheetInfo
				{
					PinName = "PC4", PortName = "PORTC", BitIndex = 4,
					PhysicalPin = "Pin 27", ArduinoLabel = "A4 (SDA)",
					AlternateFunctions = "ADC4 • SDA • PCINT12",
					AlternateDescription = "ADC Analog Input 4 / I2C (TWI) Serial Data / Pin Change Interrupt 12"
				},
				["PC5"] = new PinDatasheetInfo
				{
					PinName = "PC5", PortName = "PORTC", BitIndex = 5,
					PhysicalPin = "Pin 28", ArduinoLabel = "A5 (SCL)",
					AlternateFunctions = "ADC5 • SCL • PCINT13",
					AlternateDescription = "ADC Analog Input 5 / I2C (TWI) Serial Clock / Pin Change Interrupt 13"
				},
				["PC6"] = new PinDatasheetInfo
				{
					PinName = "PC6", PortName = "PORTC", BitIndex = 6,
					PhysicalPin = "Pin 1", ArduinoLabel = "RESET",
					AlternateFunctions = "RESET • PCINT14",
					AlternateDescription = "MCU Reset Input / Pin Change Interrupt 14"
				},

				// ── PORT D ──────────────────────────────────────────────────────────
				["PD0"] = new PinDatasheetInfo
				{
					PinName = "PD0", PortName = "PORTD", BitIndex = 0,
					PhysicalPin = "Pin 2", ArduinoLabel = "D0 (RX)",
					AlternateFunctions = "RXD • PCINT16",
					AlternateDescription = "USART Receive Data / Pin Change Interrupt 16"
				},
				["PD1"] = new PinDatasheetInfo
				{
					PinName = "PD1", PortName = "PORTD", BitIndex = 1,
					PhysicalPin = "Pin 3", ArduinoLabel = "D1 (TX)",
					AlternateFunctions = "TXD • PCINT17",
					AlternateDescription = "USART Transmit Data / Pin Change Interrupt 17"
				},
				["PD2"] = new PinDatasheetInfo
				{
					PinName = "PD2", PortName = "PORTD", BitIndex = 2,
					PhysicalPin = "Pin 4", ArduinoLabel = "D2",
					AlternateFunctions = "INT0 • PCINT18",
					AlternateDescription = "External Interrupt 0 / Pin Change Interrupt 18"
				},
				["PD3"] = new PinDatasheetInfo
				{
					PinName = "PD3", PortName = "PORTD", BitIndex = 3,
					PhysicalPin = "Pin 5", ArduinoLabel = "D3 (PWM)",
					AlternateFunctions = "INT1 • OC2B • PCINT19",
					AlternateDescription = "External Interrupt 1 / Timer2 Output Compare B / Pin Change Interrupt 19"
				},
				["PD4"] = new PinDatasheetInfo
				{
					PinName = "PD4", PortName = "PORTD", BitIndex = 4,
					PhysicalPin = "Pin 6", ArduinoLabel = "D4",
					AlternateFunctions = "XCK • T0 • PCINT20",
					AlternateDescription = "USART External Clock / Timer0 External Counter Clock / PCINT20"
				},
				["PD5"] = new PinDatasheetInfo
				{
					PinName = "PD5", PortName = "PORTD", BitIndex = 5,
					PhysicalPin = "Pin 11", ArduinoLabel = "D5 (PWM)",
					AlternateFunctions = "T1 • OC0B • PCINT21",
					AlternateDescription = "Timer1 External Counter Clock / Timer0 Output Compare B / PCINT21"
				},
				["PD6"] = new PinDatasheetInfo
				{
					PinName = "PD6", PortName = "PORTD", BitIndex = 6,
					PhysicalPin = "Pin 12", ArduinoLabel = "D6 (PWM)",
					AlternateFunctions = "AIN0 • OC0A • PCINT22",
					AlternateDescription = "Analog Comparator Positive Input / Timer0 Output Compare A / PCINT22"
				},
				["PD7"] = new PinDatasheetInfo
				{
					PinName = "PD7", PortName = "PORTD", BitIndex = 7,
					PhysicalPin = "Pin 13", ArduinoLabel = "D7",
					AlternateFunctions = "AIN1 • PCINT23",
					AlternateDescription = "Analog Comparator Negative Input / Pin Change Interrupt 23"
				},
			};

		public static PinDatasheetInfo Get(string pinName)
		{
			if (PinMap.TryGetValue(pinName, out var info))
				return info;

			return new PinDatasheetInfo
			{
				PinName = pinName,
				PortName = pinName.Length > 2 ? pinName.Substring(0, 2) : "PORT",
				AlternateFunctions = "GPIO"
			};
		}
	}
}
