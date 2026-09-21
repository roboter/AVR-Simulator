using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace AVR_Simulator
{
	public abstract class AVRInterpreter
	{
		protected AVRInterpreter()
		{
			Instructions = new InstructionFunc[]
			{
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x1C00, Func = ADC },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x0C00, Func = ADD },
				new InstructionFunc { Mask = 0xFF00, OpCode = 0x9600, Func = ADIW },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x2000, Func = AND },
				new InstructionFunc { Mask = 0xF000, OpCode = 0x7000, Func = ANDI },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9405, Func = ASR },
				new InstructionFunc { Mask = 0xFF8F, OpCode = 0x9488, Func = BCLR },
				new InstructionFunc { Mask = 0xFE08, OpCode = 0xF800, Func = BLD },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0xF400, Func = BRBC },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0xF000, Func = BRBS },
				// BRCC (See BRBC)
				// BRCS (See BRBS)
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x9598, Func = BREAK },
				// BREQ (See BRBS)
				// BRGE (See BRBC)
				// BRHC (See BRBC)
				// BRHS (See BRBS)
				// BRID (See BRBC)
				// BRIE (See BRBS)
				// BRLO (See BRBS)
				// BRLT (See BRBS)
				// BRMI (See BRBS)
				// BRNE (See BRBC)
				// BRPL (See BRBC)
				// BRSH (See BRBC)
				// BRTC (See BRBC)
				// BRTS (See BRBS)
				// BRVC (See BRBC)
				// BRVS (See BRBS)
				new InstructionFunc { Mask = 0xFF8F, OpCode = 0x9408, Func = BSET },
				new InstructionFunc { Mask = 0xFE08, OpCode = 0xFA00, Func = BST },
				new InstructionFunc { Mask = 0xFE0E, OpCode = 0x940E, Func = CALL, Double = true },
				new InstructionFunc { Mask = 0xFF00, OpCode = 0x9800, Func = CBI },
				// CBR (See ANDI)
				// CLC (See BCLR)
				// CLH (See BCLR)
				// CLI (See BCLR)
				// CLN (See BCLR)
				// CLR (See EOR)
				// CLS (See BCLR)
				// CLT (See BCLR)
				// CLV (See BCLR)
				// CLZ (See BCLR)
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9400, Func = COM },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x1400, Func = CP },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x0400, Func = CPC },
				new InstructionFunc { Mask = 0xF000, OpCode = 0x3000, Func = CPI },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x1000, Func = CPSE },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x940A, Func = DEC },
				new InstructionFunc { Mask = 0xFF0F, OpCode = 0x940B, Func = DES },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x9519, Func = EICALL },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x9419, Func = EIJMP },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x95D8, Func = ELPM_1 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9006, Func = ELPM_2 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9007, Func = ELPM_3 },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x2400, Func = EOR },
				new InstructionFunc { Mask = 0xFF88, OpCode = 0x0308, Func = FMUL },
				new InstructionFunc { Mask = 0xFF88, OpCode = 0x0380, Func = FMULS },
				new InstructionFunc { Mask = 0xFF88, OpCode = 0x0388, Func = FMULSU },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x9509, Func = ICALL },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x9409, Func = IJMP },
				new InstructionFunc { Mask = 0xF800, OpCode = 0xB000, Func = IN },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9403, Func = INC },
				new InstructionFunc { Mask = 0xFE0E, OpCode = 0x940C, Func = JMP, Double = true },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9206, Func = LAC },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9205, Func = LAS },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9207, Func = LAT },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x900C, Func = LD_X1 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x900D, Func = LD_X2 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x900E, Func = LD_X3 },
				// LD_Y1 (See LDD_Y)
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9009, Func = LD_Y2 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x900A, Func = LD_Y3 },
				new InstructionFunc { Mask = 0xD208, OpCode = 0x8008, Func = LDD_Y },
				// LD_Z1 (See LDD_Z)
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9001, Func = LD_Z2 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9002, Func = LD_Z3 },
				new InstructionFunc { Mask = 0xD208, OpCode = 0x8000, Func = LDD_Z },
				new InstructionFunc { Mask = 0xF000, OpCode = 0xE000, Func = LDI },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9000, Func = LDS, Double = true },
				new InstructionFunc { Mask = 0xF800, OpCode = 0xA000, Func = LDS_16bit },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x95C8, Func = LPM_1 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9004, Func = LPM_2 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9005, Func = LPM_3 },
				// LSL (See ADD)
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9406, Func = LSR },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x2C00, Func = MOV },
				new InstructionFunc { Mask = 0xFF00, OpCode = 0x0100, Func = MOVW },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x9C00, Func = MUL },
				new InstructionFunc { Mask = 0xFF00, OpCode = 0x0200, Func = MULS },
				new InstructionFunc { Mask = 0xFF88, OpCode = 0x0300, Func = MULSU },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9401, Func = NEG },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x0000, Func = NOP },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x2800, Func = OR },
				new InstructionFunc { Mask = 0xF000, OpCode = 0x6000, Func = ORI },
				new InstructionFunc { Mask = 0xF800, OpCode = 0xB800, Func = OUT },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x900F, Func = POP },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x920F, Func = PUSH },
				new InstructionFunc { Mask = 0xF000, OpCode = 0xD000, Func = RCALL },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x9508, Func = RET },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x9518, Func = RETI },
				new InstructionFunc { Mask = 0xF000, OpCode = 0xC000, Func = RJMP },
				// ROL (See ADC)
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9407, Func = ROR },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x0800, Func = SBC },
				new InstructionFunc { Mask = 0xF000, OpCode = 0x4000, Func = SBCI },
				new InstructionFunc { Mask = 0xFF00, OpCode = 0x9A00, Func = SBI },
				new InstructionFunc { Mask = 0xFF00, OpCode = 0x9900, Func = SBIC },
				new InstructionFunc { Mask = 0xFF00, OpCode = 0x9B00, Func = SBIS },
				new InstructionFunc { Mask = 0xFF00, OpCode = 0x9700, Func = SBIW },
				// SBR (See ORI)
				new InstructionFunc { Mask = 0xFE08, OpCode = 0xFC00, Func = SBRC },
				new InstructionFunc { Mask = 0xFE08, OpCode = 0xFE00, Func = SBRS },
				// SEC (See BSET)
				// SEH (See BSET)
				// SEI (See BSET)
				// SEN (See BSET)
				// SER (See LDI)
				// SES (See BSET)
				// SET (See BSET)
				// SEV (See BSET)
				// SEZ (See BSET)
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x9588, Func = SLEEP },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x95E8, Func = SPM },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x95E8, Func = SPM2_1_3 },
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x95F8, Func = SPM2_4_6 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x920C, Func = ST_X1 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x920D, Func = ST_X2 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x920E, Func = ST_X3 },
				// ST_Y1 (See STD_Y)
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9209, Func = ST_Y2 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x920A, Func = ST_Y3 },
				new InstructionFunc { Mask = 0xD208, OpCode = 0x8208, Func = STD_Y },
				// ST_Z1 (See STD_Z)
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9201, Func = ST_Z2 },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9202, Func = ST_Z3 },
				new InstructionFunc { Mask = 0xD208, OpCode = 0x8200, Func = STD_Z },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9200, Func = STS, Double = true },
				new InstructionFunc { Mask = 0xF800, OpCode = 0xA800, Func = STS_16bit },
				new InstructionFunc { Mask = 0xFC00, OpCode = 0x1800, Func = SUB },
				new InstructionFunc { Mask = 0xF000, OpCode = 0x5000, Func = SUBI },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9402, Func = SWAP },
				// TST (See AND)
				new InstructionFunc { Mask = 0xFFFF, OpCode = 0x95A8, Func = WDR },
				new InstructionFunc { Mask = 0xFE0F, OpCode = 0x9204, Func = XCH },
			};

			UnkownInstruction = new InstructionFunc
			{
				Mask = 0x0000,
				OpCode = 0xFFFF,
				Func = () =>
				{
					Console.WriteLine("Unkown instruction 0x{0:X4}", Instruction);
					PC++;
				}
			};
			
			InstructionMap = Enumerable.Range(0, 0xFFFF)
				.Select(i => Instructions.FirstOrDefault(func => (i & func.Mask) == func.OpCode) ?? UnkownInstruction)
				.ToArray();
			
			PC = 0;
		}

		protected InstructionFunc[] Instructions { get; set; }
		protected InstructionFunc UnkownInstruction { get; set; }

		protected InstructionFunc[] InstructionMap { get; set; }

		public event NotifyCollectionChangedEventHandler RAMChanged
		{
			add
			{
				RAM.CollectionChanged += value;
			}
			remove
			{
				RAM.CollectionChanged -= value;
			}
		}

		public ushort[] Flash { get; protected set; }
		public byte[] EEPROM { get; protected set; }
		public ObservableCollection<byte> RAM { get; protected set; }
		public MappedArray<byte> R { get; protected set; }
		public MappedArray<byte> IO { get; protected set; }
		public MappedArray<byte> SRAM { get; protected set; }

		public int PC { get; set; }
		public int LastExecutedPC { get; private set; }
		public ushort Instruction { get; set; }
		public ushort Instruction2 { get; set; }
		
		// Registers
		public virtual ushort X
		{
			get
			{
				return (ushort)((R[27] << 8) | R[26]);
			}
			set
			{
				R[27] = (byte)(value >> 8);
				R[26] = (byte)(value & 0xFF);
			}
		}
		public virtual ushort Y
		{
			get
			{
				return (ushort)((R[29] << 8) | R[28]);
			}
			set
			{
				R[28] = (byte)(value >> 8);
				R[29] = (byte)(value & 0xFF);
			}
		}
		public virtual ushort Z
		{
			get
			{
				return (ushort)((R[31] << 8) | R[30]);
			}
			set
			{
				R[31] = (byte)(value >> 8);
				R[30] = (byte)(value & 0xFF);
			}
		}
		
		// I/O
		public virtual byte RAMPD
		{
			get
			{
				return IO[0x38];
			}
			set
			{
				IO[0x38] = value;
			}
		}
		public virtual byte RAMPX
		{
			get
			{
				return IO[0x39];
			}
			set
			{
				IO[0x39] = value;
			}
		}
		public virtual byte RAMPY
		{
			get
			{
				return IO[0x3A];
			}
			set
			{
				IO[0x3A] = value;
			}
		}
		public virtual byte RAMPZ
		{
			get
			{
				return IO[0x3B];
			}
			set
			{
				IO[0x3B] = value;
			}
		}
		public virtual byte EIND
		{
			get
			{
				return IO[0x3C];
			}
			set
			{
				IO[0x3C] = value;
			}
		}
		public virtual ushort SP
		{
			get
			{
				if (RAM.Count > 256)
					return (ushort)((IO[0x3E] << 8) | IO[0x3D]);
				else
					return IO[0x3D];
			}
			set
			{
				if (RAM.Count > 256)
					IO[0x3E] = (byte)(value >> 8);

				IO[0x3D] = (byte)(value & 0xFF);
			}
		}
		public virtual byte SREG
		{
			get
			{
				return IO[0x3F];
			}
			set
			{
				IO[0x3F] = value;
			}
		}

		// Conglomorate
		protected virtual int RAMPXX
		{
			get
			{
				return (RAMPX << 16) | X;
			}
			set
			{
				RAMPX = (byte)((value & 0xFF0000) >> 16);
				X = (ushort)(value & 0x00FFFF);
			}
		}
		protected virtual int RAMPYY
		{
			get
			{
				return (RAMPY << 16) | Y;
			}
			set
			{
				RAMPY = (byte)((value & 0xFF0000) >> 16);
				Y = (ushort)(value & 0x00FFFF);
			}
		}
		protected virtual int RAMPZZ
		{
			get
			{
				return (RAMPZ << 16) | Z;
			}
			set
			{
				RAMPZ = (byte)((value & 0xFF0000) >> 16);
				Z = (ushort)(value & 0x00FFFF);
			}
		}
		protected virtual int EINDZ
		{
			get
			{
				return (EIND << 16) | Z;
			}
			set
			{
				EIND = (byte)((value & 0xFF0000) >> 16);
				Z = (ushort)(value & 0x00FFFF);
			}
		}

		public virtual void Reset()
		{
			Array.Clear(Flash, 0, Flash.Length);
			Array.Clear(EEPROM, 0, EEPROM.Length);
			RAM.Clear();

			SP = (ushort)SRAM.End;
			PC = 0;
			LastExecutedPC = 0;
		}

		#region Load
		public virtual void Load(string FlashPath)
		{
			Load(File.ReadAllBytes(FlashPath));
		}

		public virtual void Load(byte[] Flash)
		{
			Buffer.BlockCopy(Flash, 0, this.Flash, 0, Math.Min(Buffer.ByteLength(this.Flash), Flash.Length));
		}

		/*public virtual void Load(string FlashPath, string EEPROMPath)
		{
			this.Load(File.ReadAllBytes(FlashPath), File.ReadAllBytes(EEPROMPath));
		}

		public virtual void Load(byte[] Flash, byte[] EEPROM)
		{
			Buffer.BlockCopy(Flash, 0, this.Flash, 0, Math.Min(Buffer.ByteLength(this.Flash), Flash.Length));
			Buffer.BlockCopy(EEPROM, 0, this.EEPROM, 0, Math.Min(this.EEPROM.Length, EEPROM.Length));
		}*/
		#endregion

		public virtual void Execute()
		{
			if (PC >= Flash.Length)
			{
				PC = 0;
				return;
			}

			LastExecutedPC = PC;
			Instruction = Flash[PC];
			Instruction2 = (PC + 1 < Flash.Length) ? Flash[PC + 1] : (ushort)0;

			InstructionMap[Instruction].Func();
		}

		public virtual string Disassemble(int address)
		{
			if (address < 0 || address >= Flash.Length)
				return string.Empty;

			ushort instruction = Flash[address];
			InstructionFunc instructionFunc = InstructionMap[instruction];
			string mnemonic = instructionFunc.ToString().Replace('_', ' ');

			if (instructionFunc.Double && address + 1 < Flash.Length)
				return string.Format("{0:X4} {1:X4}  {2}", instruction, Flash[address + 1], mnemonic);

			return string.Format("{0:X4}       {1}", instruction, mnemonic);
		}

		public virtual int GetInstructionWordLength(int address)
		{
			if (address < 0 || address >= Flash.Length)
				return 1;

			return InstructionMap[Flash[address]].Double ? 2 : 1;
		}

		protected class InstructionFunc
		{
			public ushort Mask { get; set; }
			public ushort OpCode { get; set; }
			public Action Func { get; set; }
			public bool Double { get; set; }

			public override string ToString()
			{
				return Func.Method.Name;
			}
		}

		#region GPIO
		public class GPIO
		{
			public IList<byte> IO;
			public int DDRx;
			public int PORTx;
			public int PINx;
			public byte nMask;

			public event EventHandler<GPIOValueChangedEventArgs> ValueChanged;

			public byte Value
			{
				get
				{
					return IO[PINx];
				}
				set
				{
					IO[PINx] = value;
				}
			}

			public byte PullUp
			{
				get
				{
					return 0;
				}
				set
				{

				}
			}

			public virtual void InvokeValueChanged(GPIOValueChangedEventArgs e)
			{
				if (ValueChanged != null && e.OldValue != e.NewValue)
					ValueChanged(this, e);
			}

			public override string ToString()
			{
				return string.Format("0x{0:X2}", Value);
			}
		}

		public class GPIOPin
		{
			public IList<byte> IO;
			public int DDRx;
			public int PORTx;
			public int PINx;
			public byte nMask;

			public event EventHandler<GPIOPinValueChangedEventArgs> ValueChanged;

			public GPIOPinDirection Direction
			{
				get
				{
					return ((IO[DDRx] & nMask) == nMask)
						? GPIOPinDirection.OUTPUT
						: GPIOPinDirection.INPUT;
				}
				set
				{
					if (value == GPIOPinDirection.OUTPUT)
						IO[DDRx] |= nMask;
					else
						IO[DDRx] &= (byte)~nMask;
				}
			}

			public bool Value
			{
				get
				{
					// OUTPUT pins: the driven level is in PORTx (what the AVR program wrote).
					// INPUT  pins: the sampled level is in PINx.
					// This mirrors real AVR hardware behaviour.
					int idx = (Direction == GPIOPinDirection.OUTPUT) ? PORTx : PINx;
					return (IO[idx] & nMask) == nMask;
				}
				set
				{
					int idx = (Direction == GPIOPinDirection.INPUT)
						? PINx
						: PORTx;

					if (value)
						IO[idx] |= nMask;
					else
						IO[idx] &= (byte)~nMask;
				}
			}

			public bool PullUp
			{
				get
				{
					return false;
				}
				set
				{

				}
			}

			public void InvokeValueChanged(GPIOPinValueChangedEventArgs e)
			{
				if (ValueChanged != null && e.OldValue != e.NewValue)
					ValueChanged(this, e);
			}

			public override string ToString()
			{
				return Value.ToString();
			}
		}

		public class GPIOValueChangedEventArgs : EventArgs
		{
			public GPIOValueChangedEventArgs(byte OldValue, byte NewValue)
			{
				this.OldValue = OldValue;
				this.NewValue = NewValue;
			}

			public byte OldValue { get; private set; }
			public byte NewValue { get; private set; }
		}

		public class GPIOPinValueChangedEventArgs : EventArgs
		{
			public GPIOPinValueChangedEventArgs(byte OldValue, byte NewValue, byte nMask)
			{
				this.OldValue = (OldValue & nMask) == nMask;
				this.NewValue = (NewValue & nMask) == nMask;
			}

			public bool OldValue { get; private set; }
			public bool NewValue { get; private set; }
		}

		public enum GPIOPinDirection
		{
			INPUT,
			OUTPUT
		}
		#endregion

		#region Instructions
		/*
		 * Duplicates:
		 * 
		 * 0x95E8:
		 *	Void SPM(), 0xFFFF, 0x95E8
		 *	Void SPM2_1_3(), 0xFFFF, 0x95E8
		 */

		protected virtual void ADC()
		{
			// ADC 0b0001 11rd dddd rrrr
			PC++;
		}

		protected virtual void ADD()
		{
			// ADD 0b0000 11rd dddd rrrr
			PC++;
		}

		protected virtual void ADIW()
		{
			// ADIW 0b1001 0110 KKdd KKKK
			PC++;
		}

		protected virtual void AND()
		{
			// AND 0b0010 00rd dddd rrrr
			PC++;
		}

		protected virtual void ANDI()
		{
			// ANDI 0b0111 KKKK dddd KKKK
			PC++;
		}

		protected virtual void ASR()
		{
			// ASR 0b1001 010d dddd 0101
			PC++;
		}

		protected virtual void BCLR()
		{
			// BCLR 0b1001 0100 1sss 1000
			SREG &= (byte)~(1 << ((Instruction & 0x0070) >> 4));
			PC++;
		}

		protected virtual void BLD()
		{
			// BLD 0b1111 100d dddd 0bbb
			PC++;
		}

		protected virtual void BRBC()
		{
			// BRBC 0b1111 01kk kkkk ksss

			if ((SREG & (1 << (Instruction & 0x0007))) != 0)
				PC++;
			else
				PC += (((Instruction & 0x03F8) << 22) >> 25) + 1;
		}

		protected virtual void BRBS()
		{
			// BRBS 0b1111 00kk kkkk ksss

			if ((SREG & (1 << (Instruction & 0x0007))) != 0)
				PC += (((Instruction & 0x03F8) << 22) >> 25) + 1;
			else
				PC++;
		}

		// BRCC (See BRBC)

		// BRCS (See BRBS)

		protected virtual void BREAK()
		{
			// BREAK 0b1001 0101 1001 1000
			System.Diagnostics.Debugger.Break();
			PC++;
		}

		// BREQ (See BRBS)

		// BRGE (See BRBC)

		// BRHC (See BRBC)

		// BRHS (See BRBS)

		// BRID (See BRBC)

		// BRIE (See BRBS)

		// BRLO (See BRBS)

		// BRLT (See BRBS)

		// BRMI (See BRBS)

		// BRNE (See BRBC)

		// BRPL (See BRBC)

		// BRSH (See BRBC)

		// BRTC (See BRBC)

		// BRTS (See BRBS)

		// BRVC (See BRBC)

		// BRVS (See BRBS)

		protected virtual void BSET()
		{
			// BSET 0b1001 0100 0sss 1000
			SREG |= (byte)(1 << ((Instruction & 0x0070) >> 4));
			PC++;
		}

		protected virtual void BST()
		{
			// BST 0b1111 101d dddd 0bbb
			PC++;
		}

		protected virtual void CALL()
		{
			// CALL 0b1001 010k kkkk 111k kkkk kkkk kkkk kkkk
			RAM[SP--] = (byte)((PC + 2) & 0xFF);
			RAM[SP--] = (byte)(((PC + 2) >> 8) & 0xFF);

			if (Flash.Length > 0x20000)
				RAM[SP--] = (byte)(((PC + 2) >> 16) & 0x3F);
			
			PC = ((Instruction & 0x01F0) << 17) | ((Instruction & 0x0001) << 16) | Instruction2;
		}

		protected virtual void CBI()
		{
			// CBI 0b1001 1000 AAAA Abbb
			IO[(Instruction & 0x00F8) >> 3] &= (byte)~(1 << (Instruction & 0x0007));
			PC++;
		}

		// CBR (See ANDI)

		// CLC (See BCLR)

		// CLH (See BCLR)

		// CLI (See BCLR)

		// CLN (See BCLR)

		// CLR (See EOR)

		// CLS (See BCLR)

		// CLT (See BCLR)

		// CLV (See BCLR)

		// CLZ (See BCLR)

		protected virtual void COM()
		{
			// COM 0b1001 010d dddd 0000
			PC++;
		}

		protected virtual void CP()
		{
			// CP 0b0001 01rd dddd rrrr
			PC++;
		}

		protected virtual void CPC()
		{
			// CPC 0b0000 01rd dddd rrrr
			PC++;
		}

		protected virtual void CPI()
		{
			// CPI 0b0011 KKKK dddd KKKK
			PC++;
		}

		protected virtual void CPSE()
		{
			// CPSE 0b0001 00rd dddd rrrr
			
		}

		protected virtual void DEC()
		{
			// DEC 0b1001 010d dddd 1010
			PC++;
		}

		protected virtual void DES()
		{
			// DES 0b1001 0100 KKKK 1011
			PC++;
		}

		protected virtual void EICALL()
		{
			// EICALL 0b1001 0101 0001 1001
			RAM[SP--] = (byte)((PC + 1) & 0xFF);
			RAM[SP--] = (byte)(((PC + 1) >> 8) & 0xFF);
			
			if (Flash.Length > 0x20000)
				RAM[SP--] = (byte)(((PC + 1) >> 16) & 0x3F);
			
			PC = EINDZ;
		}

		protected virtual void EIJMP()
		{
			// EIJMP 0b1001 0100 0001 1001
			PC = EINDZ;
		}

		protected virtual void ELPM_1()
		{
			// ELPM 0b1001 0101 1101 1000
			PC++;
		}

		protected virtual void ELPM_2()
		{
			// ELPM 0b1001 000d dddd 0110
			PC++;
		}

		protected virtual void ELPM_3()
		{
			// ELPM 0b1001 000d dddd 0111
			PC++;
		}

		protected virtual void EOR()
		{
			// EOR 0b0010 01rd dddd rrrr
			PC++;
		}

		protected virtual void FMUL()
		{
			// FMUL 0b0000 0011 0ddd 1rrr
			PC++;
		}

		protected virtual void FMULS()
		{
			// FMULS 0b0000 0011 1ddd 0rrr
			PC++;
		}

		protected virtual void FMULSU()
		{
			// FMULSU 0b0000 0011 1ddd 1rrr
			PC++;
		}

		protected virtual void ICALL()
		{
			// ICALL 0b1001 0101 0000 1001
			RAM[SP--] = (byte)((PC + 1) & 0xFF);
			RAM[SP--] = (byte)(((PC + 1) >> 8) & 0xFF);

			if (Flash.Length > 0x20000)
				RAM[SP--] = (byte)(((PC + 1) >> 16) & 0x3F);

			PC = Z;
		}

		protected virtual void IJMP()
		{
			// IJMP 0b1001 0100 0000 1001
			PC = Z;
		}

		protected virtual void IN()
		{
			// IN 0b1011 0AAd dddd AAAA
			R[(Instruction & 0x01F0) >> 4] = IO[((Instruction & 0x0600) >> 5) | (Instruction & 0x000F)];
			PC++;
		}

		protected virtual void INC()
		{
			// INC 0b1001 010d dddd 0011
			PC++;
		}

		protected virtual void JMP()
		{
			// JMP 0b1001 010k kkkk 110k kkkk kkkk kkkk kkkk
			PC = ((Instruction & 0x1F0) << 17) | ((Instruction & 0x1) << 16) | Instruction2;
		}

		protected virtual void LAC()
		{
			// LAC 0b1001 001r rrrr 0110
			byte old = RAM[(RAMPZ << 16) | Z];
			RAM[(RAMPZ << 16) | Z] &= (byte)~R[(Instruction & 0x01F0) >> 4];
			R[(Instruction & 0x01F0) >> 4] = old;
			PC++;
		}

		protected virtual void LAS()
		{
			// LAS 0b1001 001r rrrr 0101
			byte old = RAM[(RAMPZ << 16) | Z];
			RAM[(RAMPZ << 16) | Z] |= R[(Instruction & 0x01F0) >> 4];
			R[(Instruction & 0x01F0) >> 4] = old;
			PC++;
		}

		protected virtual void LAT()
		{
			// LAT 0b1001 001r rrrr 0111
			byte old = RAM[(RAMPZ << 16) | Z];
			RAM[(RAMPZ << 16) | Z] ^= R[(Instruction & 0x01F0) >> 4];
			R[(Instruction & 0x01F0) >> 4] = old;
			PC++;
		}

		protected virtual void LD_X1()
		{
			// LD 0b1001 000d dddd 1100
			PC++;
		}

		protected virtual void LD_X2()
		{
			// LD 0b1001 000d dddd 1101
			PC++;
		}

		protected virtual void LD_X3()
		{
			// LD 0b1001 000d dddd 1110
			PC++;
		}

		// LD_Y1 (See LDD_Y)

		protected virtual void LD_Y2()
		{
			// LD 0b1001 000d dddd 1001
			PC++;
		}

		protected virtual void LD_Y3()
		{
			// LD 0b1001 000d dddd 1010
			PC++;
		}

		protected virtual void LDD_Y()
		{
			// LDD 0b10q0 qq0d dddd 1qqq
			PC++;
		}

		// LD_Z1 (See LDD_Z)

		protected virtual void LD_Z2()
		{
			// LD 0b1001 000d dddd 0001
			PC++;
		}

		protected virtual void LD_Z3()
		{
			// LD 0b1001 000d dddd 0010
			PC++;
		}

		protected virtual void LDD_Z()
		{
			// LDD 0b10q0 qq0d dddd 0qqq
			PC++;
		}

		protected virtual void LDI()
		{
			// LDI 0b1110 KKKK dddd KKKK
			R[((Instruction & 0x00F0) >> 4) + 16] = (byte)(((Instruction & 0x0F00) >> 4) | (Instruction & 0x000F));
			PC++;
		}

		protected virtual void LDS()
		{
			// LDS 0b1001 000d dddd 0000 kkkk kkkk kkkk kkkk
			R[(Instruction & 0x01F0) >> 4] = RAM[Instruction2 + RAMPD];
			PC += 2;
		}

		protected virtual void LDS_16bit()
		{
			// LDS 0b1010 0kkk dddd kkkk
			R[((Instruction & 0x00F0) >> 4) + 16]
				= RAM[((~Instruction2 & 0x0100) >> 2) | ((Instruction2 & 0x0100) >> 3) | ((Instruction2 & 0x0600) >> 5) | (Instruction2 & 0x000F)];
			PC++;
		}

		protected virtual void LPM_1()
		{
			// LPM 0b1001 0101 1100 1000
			PC++;
		}

		protected virtual void LPM_2()
		{
			// LPM 0b1001 000d dddd 0100
			PC++;
		}

		protected virtual void LPM_3()
		{
			// LPM 0b1001 000d dddd 0101
			PC++;
		}

		// LSL (See ADD)

		protected virtual void LSR()
		{
			// LSR 0b1001 010d dddd 0110
			PC++;
		}

		protected virtual void MOV()
		{
			// MOV 0b0010 11rd dddd rrrr
			R[(Instruction & 0x01F0) >> 4] = R[((Instruction & 0x0200) >> 5) | (Instruction & 0x000F)];
			PC++;
		}

		protected virtual void MOVW()
		{
			// MOVW 0b0000 0001 dddd rrrr
			R[((Instruction & 0x00F0) >> 3) | 0x0001] = R[((Instruction & 0x000F) << 1) | 0x0001];
			R[(Instruction & 0x00F0) >> 3] = R[(Instruction & 0x000F) << 1];
			PC++;
		}

		protected virtual void MUL()
		{
			// MUL 0b1001 11rd dddd rrrr
			PC++;
		}

		protected virtual void MULS()
		{
			// MULS 0b0000 0010 dddd rrrr
			PC++;
		}

		protected virtual void MULSU()
		{
			// MULSU 0b0000 0011 0ddd 0rrr
			PC++;
		}

		protected virtual void NEG()
		{
			// NEG 0b1001 010d dddd 0001
			PC++;
		}

		protected virtual void NOP()
		{
			// NOP 0b0000 0000 0000 0000
			PC++;
		}

		protected virtual void OR()
		{
			// OR 0b0010 10rd dddd rrrr
			PC++;
		}

		protected virtual void ORI()
		{
			// ORI 0b0110 KKKK dddd KKKK
			PC++;
		}

		protected virtual void OUT()
		{
			// OUT 0b1011 1AAr rrrr AAAA
			IO[((Instruction & 0x0600) >> 5) | (Instruction & 0x000F)] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void POP()
		{
			// POP 0b1001 000d dddd 1111
			R[(Instruction & 0x01F0) >> 4] = RAM[++SP];
			PC++;
		}

		protected virtual void PUSH()
		{
			// PUSH 0b1001 001d dddd 1111
			RAM[SP--] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void RCALL()
		{
			// RCALL 0b1101 kkkk kkkk kkkk
			RAM[SP--] = (byte)((PC + 1) & 0xFF);
			RAM[SP--] = (byte)(((PC + 1) >> 8) & 0xFF);

			if (Flash.Length > 0x20000)
				RAM[SP--] = (byte)(((PC + 1) >> 16) & 0x3F);

			PC += ((Instruction << 20) >> 20) + 1;
		}

		protected virtual void RET()
		{
			// RET 0b1001 0101 0000 1000
			PC = RAM[++SP] | RAM[++SP] << 8;

			if (Flash.Length > 0x20000)
				PC |= RAM[++SP] << 16;
		}

		protected virtual void RETI()
		{
			// RETI 0b1001 0101 0001 1000
			PC = RAM[++SP] | RAM[++SP] << 8;

			if (Flash.Length > 0x20000)
				PC |= RAM[++SP] << 16;

			SREG |= 0x80;
		}

		protected virtual void RJMP()
		{
			// RJMP 0b1100 kkkk kkkk kkkk
			PC += ((Instruction << 20) >> 20) + 1;
		}

		// ROL (See ADC)

		protected virtual void ROR()
		{
			// ROR 0b1001 010d dddd 0111
			PC++;
		}

		protected virtual void SBC()
		{
			// SBC 0b0000 10rd dddd rrrr
			PC++;
		}

		protected virtual void SBCI()
		{
			// SBCI 0b0100 KKKK dddd KKKK
			int d = ((Instruction & 0x00F0) >> 3) | 0x1;
			byte K = (byte)(((Instruction & 0x0F00) >> 4) | (Instruction & 0x000F));
			byte Rd = R[d];

			R[d] = (byte)(R[d] - K - (SREG & 0x01));

			SREG = (byte)((SREG & 0xC0)
				| ((((~Rd & 0x08) & (K & 0x08)) | ((K & 0x08) & (R[d] & 0x08)) | ((R[d] & 0x08) & (~Rd & 0x08))) << 2)// H
																																// S = N ⊕ V
				| ((((Rd & 0x80) & (~K & 0x80) & (~R[d] & 0x80)) | ((~Rd & 0x80) & (K & 0x80) & (R[d] & 0x80))) >> 4)	// V
				| ((R[d] & 0x80) >> 5)																						// N
				| ((R[d] == 0) ? (SREG & 0x02) : 0x00)																// Z
				| ((K > Rd) ? 0x01 : 0x00));																					// C

			// S = N ⊕ V
			SREG = (byte)((SREG & 0xEF) | ((((SREG & 0x04) >> 2) ^ ((SREG & 0x08) >> 3)) << 4));

			PC++;
		}

		protected virtual void SBI()
		{
			// SBI 0b1001 1010 AAAA Abbb
			IO[(Instruction & 0x00F8) >> 3] |= (byte)(1 << (Instruction & 0x0007));
			PC++;
		}

		protected virtual void SBIC()
		{
			// SBIC 0b1001 1001 AAAA Abbb
			
		}

		protected virtual void SBIS()
		{
			// SBIS 0b1001 1011 AAAA Abbb

		}

		protected virtual void SBIW()
		{
			// SBIW 0b1001 0111 KKdd KKKK
			PC++;
		}

		// SBR (See ORI)

		protected virtual void SBRC()
		{
			// SBRC 0b1111 110r rrrr 0bbb
			
		}

		protected virtual void SBRS()
		{
			// SBRS 0b1111 111r rrrr 0bbb

		}

		// SEC (See BSET)

		// SEH (See BSET)

		// SEI (See BSET)

		// SEN (See BSET)

		// SER (See LDI)

		// SES (See BSET)

		// SET (See BSET)

		// SEV (See BSET)

		// SEZ (See BSET)

		protected virtual void SLEEP()
		{
			// SLEEP 0b1001 0101 1000 1000
			PC++;
		}

		protected virtual void SPM()
		{
			// SPM 0b1001 0101 1110 1000
			PC++;
		}

		protected virtual void SPM2_1_3()
		{
			// SPM 0b1001 0101 1110 1000
			PC++;
		}

		protected virtual void SPM2_4_6()
		{
			// SPM 0b1001 0101 1111 1000
			PC++;
		}

		protected virtual void ST_X1()
		{
			// ST 0b1001 001r rrrr 1100
			RAM[X] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void ST_X2()
		{
			// ST 0b1001 001r rrrr 1101
			RAM[X++] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void ST_X3()
		{
			// ST 0b1001 001r rrrr 1110
			RAM[--X] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		// ST_Y1 (See STD_Y)

		protected virtual void ST_Y2()
		{
			// ST 0b1001 001r rrrr 1001
			RAM[Y++] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void ST_Y3()
		{
			// ST 0b1001 001r rrrr 1010
			RAM[--Y] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void STD_Y()
		{
			// STD 0b10q0 qq1r rrrr 1qqq
			RAM[Y + (((Instruction & 0x2000) >> 8) | ((Instruction & 0x0C00) >> 7) | (Instruction & 0x0007))]
				= R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		// ST_Z1 (See STD_Z)

		protected virtual void ST_Z2()
		{
			// ST 0b1001 001r rrrr 0001
			RAM[Z++] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void ST_Z3()
		{
			// ST 0b1001 001r rrrr 0010
			RAM[--Z] = R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void STD_Z()
		{
			// STD 0b10q0 qq1r rrrr 0qqq
			RAM[Z + (((Instruction & 0x2000) >> 8) | ((Instruction & 0x0C00) >> 7) | (Instruction & 0x0007))]
				= R[(Instruction & 0x01F0) >> 4];
			PC++;
		}

		protected virtual void STS()
		{
			// STS 0b1001 001d dddd 0000 kkkk kkkk kkkk kkkk
			RAM[Instruction2] = R[(Instruction & 0x01F0) >> 4];
			PC += 2;
		}

		protected virtual void STS_16bit()
		{
			// STS 0b1010 1kkk dddd kkkk
			RAM[((~Instruction2 & 0x0100) >> 2) | ((Instruction2 & 0x0100) >> 3) | ((Instruction2 & 0x0600) >> 5) | (Instruction2 & 0x000F)]
				= R[((Instruction & 0x00F0) >> 4) + 16];
			PC++;
		}

		protected virtual void SUB()
		{
			// SUB 0b0001 10rd dddd rrrr
			PC++;
		}

		protected virtual void SUBI()
		{
			// SUBI 0b0101 KKKK dddd KKKK
			int d = ((Instruction & 0x00F0) >> 3) | 0x1;
			byte K = (byte)(((Instruction & 0x0F00) >> 4) | (Instruction & 0x000F));
			byte Rd = R[d];

			R[d] -= K;

			SREG = (byte)((SREG & 0xC0)
				| ((((~Rd & 0x08) & (K & 0x08)) | ((K & 0x08) & (R[d] & 0x08)) | ((R[d] & 0x08) & (~Rd & 0x08))) << 2)// H
																																// S = N ⊕ V
				| ((((Rd & 0x80) & (~K & 0x80) & (~R[d] & 0x80)) | ((~Rd & 0x80) & (K & 0x80) & (R[d] & 0x80))) >> 4)	// V
				| ((R[d] & 0x80) >> 5)																						// N
				| ((R[d] == 0) ? 0x02 : 0x00)																				// Z
				| ((K > Rd) ? 0x01 : 0x00));																					// C

			// S = N ⊕ V
			SREG = (byte)((SREG & 0xEF) | ((((SREG & 0x04) >> 2) ^ ((SREG & 0x08) >> 3)) << 4));

			PC++;
		}

		protected virtual void SWAP()
		{
			// SWAP 0b1001 010d dddd 0010
			int d = (Instruction & 0x01F0) >> 4;
			R[d] = (byte)(((R[d] & 0xF0) >> 4) | ((R[d] & 0x0F) << 4));
			PC++;
		}

		// TST (See AND)

		protected virtual void WDR()
		{
			// WDR 0b1001 0101 1010 1000
			PC++;
		}

		protected virtual void XCH()
		{
			// XCH 0b1001 001r rrrr 0100
			byte old = RAM[Z];
			RAM[Z] = R[(Instruction & 0x01F0) >> 4];
			R[(Instruction & 0x01F0) >> 4] = old;
			PC++;
		}
		#endregion
	}
}
