using System;
using Xunit;

namespace AVR_Simulator.Core.Tests
{
    /// <summary>
    /// Tests for IntelHEX.Parse() and its output contract.
    /// Uses an inline minimal HEX record so no file I/O is needed.
    /// </summary>
    public class IntelHEXTests
    {
        // Minimal valid Intel HEX:  one data record (NOP = 0x00 0x00) at address 0, then EOF
        private const string MinimalHex =
            ":020000000000FE\r\n" +
            ":00000001FF\r\n";

        // First two bytes of Blink.hex data record (jmp 0x0034 → 0C 94 34 00)
        private const string BlinkFirstRecord =
            ":100000000C9434000C943E000C943E000C943E0082\r\n" +
            ":00000001FF\r\n";

        [Fact]
        public void Parse_MinimalHex_DoesNotThrow()
        {
            var ex = Record.Exception(() => IntelHEX.Parse(MinimalHex));
            Assert.Null(ex);
        }

        [Fact]
        public void Parse_MinimalHex_ReturnsNonEmptyArray()
        {
            byte[] result = IntelHEX.Parse(MinimalHex);
            Assert.NotNull(result);
            Assert.True(result.Length > 0, "Parsed flash image must have at least one byte");
        }

        [Fact]
        public void Parse_MinimalHex_FirstByteIsZero()
        {
            byte[] result = IntelHEX.Parse(MinimalHex);
            Assert.Equal(0x00, result[0]);
        }

        [Fact]
        public void Parse_BlinkFirstRecord_FirstByteIs0x0C()
        {
            // 0x0C 0x94 is "jmp" opcode in AVR — first instruction of Blink
            byte[] result = IntelHEX.Parse(BlinkFirstRecord);
            Assert.Equal(0x0C, result[0]);
            Assert.Equal(0x94, result[1]);
        }

        [Fact]
        public void Parse_BlinkFirstRecord_16BytesLoaded()
        {
            byte[] result = IntelHEX.Parse(BlinkFirstRecord);
            // Record says 0x10 (16) bytes starting at address 0
            Assert.True(result.Length >= 16, "Should hold at least the 16 bytes from the record");
        }
    }
}
