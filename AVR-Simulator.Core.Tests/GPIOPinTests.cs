using System;
using Xunit;

namespace AVR_Simulator.Core.Tests
{
    /// <summary>
    /// Tests for GPIOPin direction and value logic in AVRInterpreter.
    /// </summary>
    public class GPIOPinTests
    {
        private static Atmega328Interpreter CreateFresh() => new Atmega328Interpreter();

        // ── Direction ────────────────────────────────────────────────────────

        [Fact]
        public void AllPortB_DefaultDirection_IsInput()
        {
            var interp = CreateFresh();
            foreach (var pin in new[]
            {
                interp.PORTB.PB0, interp.PORTB.PB1, interp.PORTB.PB2, interp.PORTB.PB3,
                interp.PORTB.PB4, interp.PORTB.PB5, interp.PORTB.PB6, interp.PORTB.PB7,
            })
                Assert.Equal(AVRInterpreter.GPIOPinDirection.INPUT, pin.Direction);
        }

        [Fact]
        public void PB5_AfterDDRB_FF_Direction_IsOutput()
        {
            var interp = CreateFresh();
            // Simulate "DDRB = 0xFF" by writing to the IO register directly
            interp.IO[0x04] = 0xFF;   // DDRB IO offset on ATmega328
            Assert.Equal(AVRInterpreter.GPIOPinDirection.OUTPUT, interp.PORTB.PB5.Direction);
        }

        [Fact]
        public void PB5_AfterDDRB_0_Direction_IsInput()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0x00;
            Assert.Equal(AVRInterpreter.GPIOPinDirection.INPUT, interp.PORTB.PB5.Direction);
        }

        // ── Value reads correct register ──────────────────────────────────────

        [Fact]
        public void PB5_Output_ValueReadFromPORTx()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0xFF;   // DDRB → all OUTPUT
            interp.IO[0x05] = 0x20;   // PORTB bit 5 = 1  (PB5 HIGH)

            Assert.True(interp.PORTB.PB5.Value,
                "OUTPUT pin should read PORTx register (bit 5 of 0x05 = 1)");
        }

        [Fact]
        public void PB5_Output_LowWhenPortxBitClear()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0xFF;   // DDRB → all OUTPUT
            interp.IO[0x05] = 0x00;   // PORTB all LOW

            Assert.False(interp.PORTB.PB5.Value,
                "OUTPUT pin should read PORTx register (all bits 0)");
        }

        [Fact]
        public void PB5_Input_ValueReadFromPINx()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0x00;   // DDRB → all INPUT
            interp.IO[0x03] = 0x20;   // PINB bit 5 = 1  (external HIGH)

            Assert.True(interp.PORTB.PB5.Value,
                "INPUT pin should read PINx register (bit 5 of 0x03 = 1)");
        }

        [Fact]
        public void PB4_NotAffectedByPB5_PortWrite()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0xFF;
            interp.IO[0x05] = 0x20;   // Only PB5 HIGH

            Assert.False(interp.PORTB.PB4.Value, "PB4 should be LOW when only bit 5 is set");
            Assert.False(interp.PORTB.PB6.Value, "PB6 should be LOW when only bit 5 is set");
        }

        [Fact]
        public void PB5_PullUp_ActiveWhenInputAndPortHigh()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0x00; // DDRB = 0 (INPUT)
            interp.IO[0x05] = 0x20; // PORTB bit 5 = 1 (Pull-Up enabled)

            Assert.True(interp.PORTB.PB5.PullUp);
            Assert.True(interp.PORTB.PB5.PortBit);
            Assert.False(interp.PORTB.PB5.DDRBit);
            Assert.Equal(5, interp.PORTB.PB5.BitIndex);
        }

        [Fact]
        public void PB5_PullUp_InactiveWhenOutputEvenIfPortHigh()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0x20; // DDRB bit 5 = 1 (OUTPUT)
            interp.IO[0x05] = 0x20; // PORTB bit 5 = 1

            Assert.False(interp.PORTB.PB5.PullUp, "PullUp is not active on output pins");
            Assert.True(interp.PORTB.PB5.PortBit);
            Assert.True(interp.PORTB.PB5.DDRBit);
        }

        [Fact]
        public void PB5_PullUp_SetterTogglesPortBitWhenInput()
        {
            var interp = CreateFresh();
            interp.IO[0x04] = 0x00; // DDRB = 0 (INPUT)
            interp.PORTB.PB5.PullUp = true;

            Assert.True(interp.PORTB.PB5.PullUp);
            Assert.Equal(0x20, interp.IO[0x05] & 0x20);

            interp.PORTB.PB5.PullUp = false;
            Assert.False(interp.PORTB.PB5.PullUp);
            Assert.Equal(0x00, interp.IO[0x05] & 0x20);
        }
    }
}
