/*
 * SPDX-FileCopyrightText: 2021 William Swartzendruber <wswartzendruber@gmail.com>
 *
 * SPDX-License-Identifier: CC0-1.0
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;

namespace GB18030
{
    public class GB18030Encoding : Encoding
    {
        private readonly struct Range
        {
            public readonly Rune FirstValue;
            public readonly Rune LastValue;
            public readonly (byte, byte, byte, byte) FirstBytes;
            public readonly (byte, byte, byte, byte) LastBytes;

            public Range
            (
                Rune firstValue,
                Rune lastValue,
                (byte, byte, byte, byte) firstBytes,
                (byte, byte, byte, byte) lastBytes
            )
            {
                FirstValue = firstValue;
                LastValue = lastValue;
                FirstBytes = firstBytes;
                LastBytes = lastBytes;
            }
        }

        private readonly static ReadOnlyDictionary<(byte, byte), Rune> TwoByteCodePoints;

        private readonly static ReadOnlyDictionary<(byte, byte, byte, byte), Rune> FourByteCodePoints;

        private readonly static ReadOnlyDictionary<Rune, (byte, byte)> CodePointTwoBytes;

        private readonly static ReadOnlyDictionary<Rune, (byte, byte, byte, byte)> CodePointFourBytes;

        private readonly static ReadOnlyCollection<Range> Ranges = new(new List<Range> {
            new Range(new Rune(0x00452), new Rune(0x00200F), (0x81, 0x30, 0xD3, 0x30), (0x81, 0x36, 0xA5, 0x31)),
            new Range(new Rune(0x02643), new Rune(0x002E80), (0x81, 0x37, 0xA8, 0x39), (0x81, 0x38, 0xFD, 0x38)),
            new Range(new Rune(0x0361B), new Rune(0x003917), (0x82, 0x30, 0xA6, 0x33), (0x82, 0x30, 0xF2, 0x37)),
            new Range(new Rune(0x03CE1), new Rune(0x004055), (0x82, 0x31, 0xD4, 0x38), (0x82, 0x32, 0xAF, 0x32)),
            new Range(new Rune(0x04160), new Rune(0x004336), (0x82, 0x32, 0xC9, 0x37), (0x82, 0x32, 0xF8, 0x37)),
            new Range(new Rune(0x044D7), new Rune(0x00464B), (0x82, 0x33, 0xA3, 0x39), (0x82, 0x33, 0xC9, 0x31)),
            new Range(new Rune(0x0478E), new Rune(0x004946), (0x82, 0x33, 0xE8, 0x38), (0x82, 0x34, 0x96, 0x38)),
            new Range(new Rune(0x049B8), new Rune(0x004C76), (0x82, 0x34, 0xA1, 0x31), (0x82, 0x34, 0xE7, 0x33)),
            new Range(new Rune(0x09FA6), new Rune(0x00D7FF), (0x82, 0x35, 0x8F, 0x33), (0x83, 0x36, 0xC7, 0x38)),
            new Range(new Rune(0x0E865), new Rune(0x00F92B), (0x83, 0x36, 0xD0, 0x30), (0x84, 0x30, 0x85, 0x34)),
            new Range(new Rune(0x0FA2A), new Rune(0x00FE2F), (0x84, 0x30, 0x9C, 0x38), (0x84, 0x31, 0x85, 0x37)),
            new Range(new Rune(0x0FFE6), new Rune(0x00FFFF), (0x84, 0x31, 0xA2, 0x34), (0x84, 0x31, 0xA4, 0x39)),
            new Range(new Rune(0x10000), new Rune(0x10FFFF), (0x90, 0x30, 0x81, 0x30), (0xE3, 0x32, 0x9A, 0x35))
        });

        static GB18030Encoding()
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var mapStream = assembly.GetManifestResourceStream("GB18030.TwoByteCodePoints.bin")
                ?? throw new Exception("Cannot locate embedded two byte code point map file."))
            {
                var twoByteCodePoints = new Dictionary<(byte, byte), Rune>();
                var codePointTwoBytes = new Dictionary<Rune, (byte, byte)>();
                var buffer = new byte[5];

                for (int bytesRead = mapStream.Read(buffer); bytesRead == 5; bytesRead = mapStream.Read(buffer))
                {
                    var codePoint = new Rune(buffer[0] << 16 | buffer[1] << 8 | buffer[2]);
                    var bytes = (buffer[3], buffer[4]);
                    
                    twoByteCodePoints[bytes] = codePoint;
                    codePointTwoBytes[codePoint] = bytes;
                }

                TwoByteCodePoints = new(twoByteCodePoints);
                CodePointTwoBytes = new(codePointTwoBytes);
            }

            using (var mapStream = assembly.GetManifestResourceStream("GB18030.FourByteCodePoints.bin")
                ?? throw new Exception("Cannot locate embedded four byte code point map file."))
            {
                var fourByteCodePoints = new Dictionary<(byte, byte, byte, byte), Rune>();
                var codePointFourBytes = new Dictionary<Rune, (byte, byte, byte, byte)>();
                var buffer = new byte[7];

                for (int bytesRead = mapStream.Read(buffer); bytesRead == 7; bytesRead = mapStream.Read(buffer))
                {
                    var codePoint = new Rune(buffer[0] << 16 | buffer[1] << 8 | buffer[2]);
                    var bytes = (buffer[3], buffer[4], buffer[5], buffer[6]);

                    fourByteCodePoints[bytes] = codePoint;
                    codePointFourBytes[codePoint] = bytes;
                }

                FourByteCodePoints = new(fourByteCodePoints);
                CodePointFourBytes = new(codePointFourBytes);
            }
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            var byteCount = 0;
            var charSpan = new ReadOnlySpan<char>(chars, index, count);

            foreach (Rune codePoint in charSpan.EnumerateRunes())
            {
                if (codePoint.IsAscii)
                {
                    //
                    // 1 byte - US-ASCII
                    //

                    byteCount++;
                }
                else if (CodePointTwoBytes.ContainsKey(codePoint))
                {
                    //
                    // 2 bytes
                    //

                    byteCount += 2;
                }
                else
                {
                    //
                    // 4 bytes
                    //

                    byteCount += 4;
                }
            }

            return byteCount;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            var charSpan = new ReadOnlySpan<char>(chars, charIndex, charCount);
            var byteStart = byteIndex;

            foreach (Rune codePoint in charSpan.EnumerateRunes())
            {
                if (codePoint.IsAscii)
                {
                    //
                    // 1 byte - US-ASCII
                    //

                    bytes[byteIndex++] = (byte)codePoint.Value;
                }
                else if (CodePointTwoBytes.ContainsKey(codePoint))
                {
                    //
                    // 2 bytes
                    //

                    var twoBytes = CodePointTwoBytes[codePoint];

                    bytes[byteIndex++] = twoBytes.Item1;
                    bytes[byteIndex++] = twoBytes.Item2;
                }
                else
                {
                    //
                    // 4 bytes
                    //

                    var fourBytes = CodePointFourBytes.GetOrNull(codePoint)
                        ?? CalculatedFourBytes(codePoint);

                    bytes[byteIndex++] = fourBytes.Item1;
                    bytes[byteIndex++] = fourBytes.Item2;
                    bytes[byteIndex++] = fourBytes.Item3;
                    bytes[byteIndex++] = fourBytes.Item4;
                }
            }

            return byteIndex - byteStart;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            var charCount = 0;
            var limit = index + count;

            while (index < limit)
            {
                var firstByte = bytes[index++];

                if (firstByte >> 7 == 0)
                {
                    //
                    // 1 byte - US-ASCII
                    //

                    charCount++;
                }
                else if (0x81 <= firstByte && firstByte <= 0xFE && index < limit)
                {
                    //
                    // 2 or 4 bytes
                    //

                    var secondByte = bytes[index++];

                    if (0x40 <= secondByte && secondByte <= 0xFE)
                    {
                        //
                        // 2 bytes
                        //

                        charCount += (
                            TwoByteCodePoints.GetOrNull((firstByte, secondByte))
                            ?? new Rune(NextReplacementChar())
                        ).Utf16SequenceLength;
                    }
                    else if (0x30 <= secondByte && secondByte <= 0x39)
                    {
                        //
                        // 4 bytes
                        //

                        if (index + 1 < limit)
                        {
                            var fourBytes = (firstByte, secondByte, bytes[index++], bytes[index++]);

                            charCount += (
                                FourByteCodePoints.GetOrNull(fourBytes)
                                ?? CalculatedCodePoint(fourBytes)
                                ?? new Rune(NextReplacementChar())
                            ).Utf16SequenceLength;
                        }
                        else
                        {
                            index = limit;
                            NextReplacementChar();
                            charCount++;
                        }
                    }
                }
                else
                {
                    NextReplacementChar();
                    charCount++;
                }
            }

            return charCount;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            var charStart = charIndex;
            var byteLimit = byteIndex + byteCount;

            while (byteIndex < byteLimit)
            {
                var firstByte = bytes[byteIndex++];

                if (firstByte >> 7 == 0)
                {
                    //
                    // 1 byte - US-ASCII
                    //
                    
                    chars[charIndex++] = (char)firstByte;
                }
                else if (0x81 <= firstByte && firstByte <= 0xFE && byteIndex < byteLimit)
                {
                    //
                    // 2 or 4 bytes
                    //

                    var secondByte = bytes[byteIndex++];

                    if (0x40 <= secondByte && secondByte <= 0xFE)
                    {
                        //
                        // 2 bytes
                        //

                        charIndex += (
                            TwoByteCodePoints.GetOrNull((firstByte, secondByte))
                            ?? new Rune(NextReplacementChar())
                        ).EncodeToUtf16(new Span<char>(chars, charIndex, chars.Length - charIndex));
                    }
                    else if (0x30 <= secondByte && secondByte <= 0x39)
                    {
                        //
                        // 4 bytes
                        //

                        if (byteIndex + 1 < byteLimit)
                        {
                            var fourBytes = (firstByte, secondByte, bytes[byteIndex++], bytes[byteIndex++]);

                            charIndex += (
                                FourByteCodePoints.GetOrNull(fourBytes)
                                ?? CalculatedCodePoint(fourBytes)
                                ?? new Rune(NextReplacementChar())
                            ).EncodeToUtf16(new Span<char>(chars, charIndex, chars.Length - charIndex));
                        }
                        else
                        {
                            byteIndex = byteLimit;
                            chars[charIndex++] = NextReplacementChar();
                        }
                    }
                }
                else
                {
                    chars[charIndex++] = NextReplacementChar();
                }
            }

            return charIndex - charStart;
        }

        public override int GetMaxByteCount(int charCount) => 4 * charCount;

        public override int GetMaxCharCount(int byteCount) => byteCount;

        private char NextReplacementChar() => DecoderFallback.CreateFallbackBuffer().GetNextChar();

        private Rune? CalculatedCodePoint((byte, byte, byte, byte) bytes)
        {
            int linear = Linear(bytes);

            foreach (var range in Ranges)
            {
                if (Linear(range.FirstBytes) <= linear && linear <= Linear(range.LastBytes))
                    return new Rune(range.FirstValue.Value + (linear - Linear(range.FirstBytes)));
            }

            return null;
        }

        private (byte, byte, byte, byte) CalculatedFourBytes(Rune codePoint)
        {
            foreach (var range in Ranges)
            {
                if (range.FirstValue <= codePoint && codePoint <= range.LastValue)
                    return Unlinear(Linear(range.FirstBytes) + codePoint.Value - range.FirstValue.Value);
            }

            throw new InvalidOperationException("Byte sequence could not be calculated.");
        }

        private (byte, byte, byte, byte) Unlinear(int linear)
        {
            (byte, byte, byte, byte) bytes;

            linear -= Linear((0x81, 0x30, 0x81, 0x30));

            bytes.Item4 = (byte)(0x30 + linear % 10);
            linear /= 10;
            bytes.Item3 = (byte)(0x81 + linear % 126);
            linear /= 126;
            bytes.Item2 = (byte)(0x30 + linear % 10);
            linear /= 10;
            bytes.Item1 = (byte)(0x81 + linear);

            return bytes;
        }

        private int Linear((byte, byte, byte, byte) bytes) =>
            ((bytes.Item1 * 10 + bytes.Item2) * 126 + bytes.Item3) * 10 + bytes.Item4;
    }
}
