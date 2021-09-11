/*
 * SPDX-FileCopyrightText: 2021 William Swartzendruber <wswartzendruber@gmail.com>
 *
 * SPDX-License-Identifier: CC0-1.0
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace GBX.Tests
{
    public class Tests
    {
        [Test]
        public void AllCodePointsRoundTrip()
        {
            var encoding = new GBXEncoding();
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