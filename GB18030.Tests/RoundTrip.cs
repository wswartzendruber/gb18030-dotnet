using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using GB18030;

namespace GB18030.Tests
{
    public class Tests
    {
        [Test]
        public void AllCodePointsRoundTrip()
        {
            var encoding = new GB18030Encoding();
            var inChars = new List<char>();

            for (var codePoint = 0x0000; codePoint < 0xD800; codePoint++)
                inChars.AddRange(RuneToChars(new Rune(codePoint)).ToArray());
            for (var codePoint = 0x00E000; codePoint < 0x10FFFF; codePoint++)
                inChars.AddRange(RuneToChars(new Rune(codePoint)).ToArray());

            var bytes = encoding.GetBytes(inChars.ToArray());
            var outChars = encoding.GetChars(bytes);

            if (inChars.SequenceEqual(outChars))
                Assert.Pass();
            else
                Assert.Fail("Input characters and output characters are not equal.");
        }

        private ReadOnlySpan<char> RuneToChars(Rune codePoint)
        {
            var utf16 = new char[2];
            
            return new ReadOnlySpan<char>(utf16, 0, codePoint.EncodeToUtf16(utf16));
        }
    }
}