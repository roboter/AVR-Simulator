using System;
using Xunit;

namespace AVR_Simulator.Core.Tests
{
    /// <summary>
    /// Integration-level tests that run real AVR instructions through Atmega328Interpreter
    /// and verify the resulting GPIO pin states.
    /// </summary>
    public class Atmega328Tests
    {
        // Blink.hex: DDRB=0xFF, PORTB=0x20, delay, PORTB=0x00, delay, loop
        // Minimal hex: only the body of Blink (DDRB + first PORTB write)
        // We just test the register manipulation directly without running Execute().

        private static Atmega328Interpreter CreateFresh() => new Atmega328Interpreter();

        // ── Default state ────────────────────────────────────────────────────

        [Fact]
        public void Fresh_AllPortB_AreInput()
        {
            var interp = CreateFresh();
            var pins = new[]
            {
                interp.PORTB.PB0, interp.PORTB.PB1, interp.PORTB.PB2, interp.PORTB.PB3,
                interp.PORTB.PB4, interp.PORTB.PB5, interp.PORTB.PB6, interp.PORTB.PB7,
            };
            foreach (var p in pins)
                Assert.Equal(AVRInterpreter.GPIOPinDirection.INPUT, p.Direction);
        }

        [Fact]
        public void Fresh_AllPortC_AreInput()
        {
            var interp = CreateFresh();
            var pins = new[]
            {
                interp.PORTC.PC0, interp.PORTC.PC1, interp.PORTC.PC2, interp.PORTC.PC3,
                interp.PORTC.PC4, interp.PORTC.PC5, interp.PORTC.PC6,
            };
            foreach (var p in pins)
                Assert.Equal(AVRInterpreter.GPIOPinDirection.INPUT, p.Direction);
        }

        [Fact]
        public void Fresh_AllPortD_AreInput()
        {
            var interp = CreateFresh();
            var pins = new[]
            {
                interp.PORTD.PD0, interp.PORTD.PD1, interp.PORTD.PD2, interp.PORTD.PD3,
                interp.PORTD.PD4, interp.PORTD.PD5, interp.PORTD.PD6, interp.PORTD.PD7,
            };
            foreach (var p in pins)
                Assert.Equal(AVRInterpreter.GPIOPinDirection.INPUT, p.Direction);
        }

        // ── Blink scenario: DDRB = 0xFF, PORTB = 0x20 ───────────────────────

        [Fact]
        public void Blink_DDRB_FF_AllPortB_BecomeOutput()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0xFF;   // DDRB

            var pins = new[]
            {
                interp.PORTB.PB0, interp.PORTB.PB1, interp.PORTB.PB2, interp.PORTB.PB3,
                interp.PORTB.PB4, interp.PORTB.PB5, interp.PORTB.PB6, interp.PORTB.PB7,
            };
            foreach (var p in pins)
                Assert.Equal(AVRInterpreter.GPIOPinDirection.OUTPUT, p.Direction);
        }

        [Fact]
        public void Blink_PORTB_0x20_OnlyPB5_IsHigh()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0xFF;   // DDRB  → all OUTPUT
            interp.IO[0x05] = 0x20;   // PORTB → PB5 HIGH

            Assert.False(interp.PORTB.PB0.Value, "PB0 should be LOW");
            Assert.False(interp.PORTB.PB1.Value, "PB1 should be LOW");
            Assert.False(interp.PORTB.PB2.Value, "PB2 should be LOW");
            Assert.False(interp.PORTB.PB3.Value, "PB3 should be LOW");
            Assert.False(interp.PORTB.PB4.Value, "PB4 should be LOW");
            Assert.True (interp.PORTB.PB5.Value, "PB5 should be HIGH (bit 5 of 0x20)");
            Assert.False(interp.PORTB.PB6.Value, "PB6 should be LOW");
            Assert.False(interp.PORTB.PB7.Value, "PB7 should be LOW");
        }

        [Fact]
        public void Blink_PORTB_0x00_AllPortB_AreLow()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0xFF;   // DDRB  → all OUTPUT
            interp.IO[0x05] = 0x20;   // set PB5 HIGH first
            interp.IO[0x05] = 0x00;   // then PORTB = 0  → all LOW

            var pins = new[]
            {
                interp.PORTB.PB0, interp.PORTB.PB1, interp.PORTB.PB2, interp.PORTB.PB3,
                interp.PORTB.PB4, interp.PORTB.PB5, interp.PORTB.PB6, interp.PORTB.PB7,
            };
            foreach (var p in pins)
                Assert.False(p.Value, $"{p} should be LOW after PORTB=0x00");
        }

        // ── Value setter ─────────────────────────────────────────────────────

        [Fact]
        public void SetValue_True_OnOutputPin_SetsPortxBit()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0xFF;          // DDRB → all OUTPUT
            interp.PORTB.PB3.Value = true;

            Assert.True(interp.PORTB.PB3.Value);
            Assert.Equal(0x08, (byte)(interp.IO[0x05] & 0x08));  // bit 3
        }

        [Fact]
        public void SetValue_False_OnOutputPin_ClearsPortxBit()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0xFF;
            interp.PORTB.PB3.Value = true;
            interp.PORTB.PB3.Value = false;

            Assert.False(interp.PORTB.PB3.Value);
            Assert.Equal(0x00, (byte)(interp.IO[0x05] & 0x08));
        }
    }
}
