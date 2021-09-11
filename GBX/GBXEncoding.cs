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

namespace GBX
{
    public class GBXEncoding : Encoding
    {
        private readonly static ReadOnlyDictionary<(byte, byte), Rune> TwoByteCodePoints;

        private readonly static ReadOnlyDictionary<Rune, (byte, byte)> CodePointTwoBytes;

        static GBXEncoding()
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var mapStream = assembly.GetManifestResourceStream("GBX.TwoByteCodePoints.bin")
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

                    var value = codePoint.Value;
                    
                    bytes[byteIndex++] = (byte)(value >> 14 & 0b11111110 | 0b10000000);
                    bytes[byteIndex++] = (byte)(value >> 10 & 0b00011111 | 0b00100000);
                    bytes[byteIndex++] = (byte)(value >> 5 & 0b00011111 | 0b00100000);
                    bytes[byteIndex++] = (byte)(value & 0b00011111 | 0b00100000);
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
                else if (firstByte >> 7 == 1 && index < limit)
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
                    else if (secondByte >> 5 == 1)
                    {
                        //
                        // 4 bytes
                        //

                        if (index + 1 < limit)
                        {
                            var fourBytes = (firstByte, secondByte, bytes[index++], bytes[index++]);

                            if (fourBytes.Item3 >> 5 == 1 && fourBytes.Item4 >> 5 == 1)
                            {
                                var value = (fourBytes.Item1 & 0b01111111) << 14
                                    | (fourBytes.Item2 & 0b00011111) << 10
                                    | (fourBytes.Item3 & 0b00011111) << 5
                                    | (fourBytes.Item4 & 0b00011111);

                                if (Rune.IsValid(value))
                                {
                                    charCount += (
                                        new Rune(value)
                                    ).Utf16SequenceLength;
                                }
                                else
                                {
                                    charCount += (
                                        new Rune(NextReplacementChar())
                                    ).Utf16SequenceLength;
                                }
                            }
                            else
                            {
                                charCount += (
                                    new Rune(NextReplacementChar())
                                ).Utf16SequenceLength;
                            }

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
                else if (firstByte >> 7 == 1 && byteIndex < byteLimit)
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
                    else if (secondByte >> 5 == 1)
                    {
                        //
                        // 4 bytes
                        //

                        if (byteIndex + 1 < byteLimit)
                        {
                            var fourBytes = (firstByte, secondByte, bytes[byteIndex++], bytes[byteIndex++]);

                            if (fourBytes.Item3 >> 5 == 1 && fourBytes.Item4 >> 5 == 1)
                            {
                                var value = (fourBytes.Item1 & 0b01111111) << 14
                                    | (fourBytes.Item2 & 0b00011111) << 10
                                    | (fourBytes.Item3 & 0b00011111) << 5
                                    | (fourBytes.Item4 & 0b00011111);

                                if (Rune.IsValid(value))
                                {
                                    charIndex += (
                                        new Rune(value)
                                    ).EncodeToUtf16(new Span<char>(chars, charIndex, chars.Length - charIndex));
                                }
                                else
                                {
                                    charIndex += (
                                        new Rune(NextReplacementChar())
                                    ).EncodeToUtf16(new Span<char>(chars, charIndex, chars.Length - charIndex));
                                }
                            }
                            else
                            {
                                charIndex += (
                                    new Rune(NextReplacementChar())
                                ).EncodeToUtf16(new Span<char>(chars, charIndex, chars.Length - charIndex));
                            }

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
    }
}
