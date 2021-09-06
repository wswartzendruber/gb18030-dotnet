using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Xml;

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
            public readonly (byte, byte, byte, byte) MinBytes;
            public readonly (byte, byte, byte, byte) MaxBytes;

            public Range
            (
                Rune firstValue,
                Rune lastValue,
                (byte, byte, byte, byte) firstBytes,
                (byte, byte, byte, byte) lastBytes,
                (byte, byte, byte, byte) minBytes,
                (byte, byte, byte, byte) maxBytes
            )
            {
                FirstValue = firstValue;
                LastValue = lastValue;
                FirstBytes = firstBytes;
                LastBytes = lastBytes;
                MinBytes = minBytes;
                MaxBytes = maxBytes;
            }
        }

        private readonly static ReadOnlyDictionary<(byte, byte), Rune> TwoByteCodePoints;

        private readonly static ReadOnlyDictionary<(byte, byte, byte, byte), Rune> FourByteCodePoints;

        private readonly static ReadOnlyDictionary<Rune, (byte, byte)> CodePointsTwoBytes;

        private readonly static ReadOnlyDictionary<Rune, (byte, byte, byte, byte)> CodePointsFourBytes;

        private readonly static ReadOnlyCollection<Range> Ranges;

        static GB18030Encoding()
        {
            using (var xmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GB18030.gb-18030-2000.xml")
                ?? throw new Exception("Could not load internal character map."))
            {
                var xdocument = new XmlDocument(); xdocument.Load(xmlStream);
                var xelement = xdocument.DocumentElement
                    ?? throw new XmlException("Could not load internal character map's root element.");
                var assignmentNodes = xelement.SelectNodes("//characterMapping/assignments/a")
                    ?? throw new XmlException("Could not locate internal character map's assignments element.");
                var rangeNodes = xelement.SelectNodes("//characterMapping/assignments/range")
                    ?? throw new XmlException("Could not locate internal character map's range element.");
                var twoByteCodePoints = new Dictionary<(byte, byte), Rune>();
                var fourByteCodePoints = new Dictionary<(byte, byte, byte, byte), Rune>();
                var codePointsTwoBytes = new Dictionary<Rune, (byte, byte)>();
                var codePointsFourBytes = new Dictionary<Rune, (byte, byte, byte, byte)>();
                var rangeList = new List<Range>();

                foreach (XmlNode assignmentNode in assignmentNodes)
                {
                    var byteStringArray = assignmentNode?.Attributes?["b"]?.Value?.Split(' ')
                        ?? throw new XmlException("Could not read assignment's byte sequence from internal character map.");
                    var codePoint = System.Convert.ToInt32(assignmentNode?.Attributes?["u"]?.Value
                        ?? throw new XmlException("Could not read assignment's code point from internal character map."), 16);

                    switch (byteStringArray.Length)
                    {
                        case 1:
                            continue;
                        case 2:
                            var bytes2 = (
                                System.Convert.ToByte(byteStringArray[0], 16),
                                System.Convert.ToByte(byteStringArray[1], 16)
                            );
                            twoByteCodePoints[bytes2] = new Rune(codePoint);
                            codePointsTwoBytes[new Rune(codePoint)] = bytes2;
                            break;
                        case 4:
                            var bytes4 = (
                                System.Convert.ToByte(byteStringArray[0], 16),
                                System.Convert.ToByte(byteStringArray[1], 16),
                                System.Convert.ToByte(byteStringArray[2], 16),
                                System.Convert.ToByte(byteStringArray[3], 16)
                            );
                            fourByteCodePoints[bytes4] = new Rune(codePoint);
                            codePointsFourBytes[new Rune(codePoint)] = bytes4;
                            break;
                        default:
                            throw new XmlException("Internal XML contains invalid byte sequence.");
                    }
                }

                TwoByteCodePoints = new ReadOnlyDictionary<(byte, byte), Rune>(twoByteCodePoints);
                FourByteCodePoints = new ReadOnlyDictionary<(byte, byte, byte, byte), Rune>(fourByteCodePoints);
                CodePointsTwoBytes = new ReadOnlyDictionary<Rune, (byte, byte)>(codePointsTwoBytes);
                CodePointsFourBytes = new ReadOnlyDictionary<Rune, (byte, byte, byte, byte)>(codePointsFourBytes);

                foreach (XmlNode rangeNode in rangeNodes)
                {
                    rangeList.Add(new Range(
                        new Rune(System.Convert.ToInt32(rangeNode?.Attributes?["uFirst"]?.Value
                            ?? throw new XmlException("Could not read range's first code point from internal character map."), 16)),
                        new Rune(System.Convert.ToInt32(rangeNode?.Attributes?["uLast"]?.Value
                            ?? throw new XmlException("Could not read range's last code point from internal character map."), 16)),
                        ByteStringToBytes(rangeNode?.Attributes?["bFirst"]?.Value
                            ?? throw new XmlException("Could not read range's first byte sequence from internal character map.")),
                        ByteStringToBytes(rangeNode?.Attributes?["bLast"]?.Value
                            ?? throw new XmlException("Could not read range's last byte sequence from internal character map.")),
                        ByteStringToBytes(rangeNode?.Attributes?["bMin"]?.Value
                            ?? throw new XmlException("Could not read range's minimum byte sequence from internal character map.")),
                        ByteStringToBytes(rangeNode?.Attributes?["bMax"]?.Value
                            ?? throw new XmlException("Could not read range's maximum byte sequence from internal character map."))
                    ));
                }

                Ranges = new ReadOnlyCollection<Range>(rangeList);
            }
        }

        private static (byte, byte, byte, byte) ByteStringToBytes(String byteString)
        {
            var byteStringArray = byteString.Split(' ');

            return (
                System.Convert.ToByte(byteStringArray[0], 16),
                System.Convert.ToByte(byteStringArray[1], 16),
                System.Convert.ToByte(byteStringArray[2], 16),
                System.Convert.ToByte(byteStringArray[3], 16)
            );
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            throw new NotImplementedException();
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            var byteCount = 0;
            var charLimit = charIndex + charCount;

            while (charIndex < charLimit) {

                var highChar = chars[charIndex++];

                if (Char.IsHighSurrogate(highChar))
                {
                    if (charIndex < charLimit)
                    {
                        var lowChar = chars[charIndex++];

                        if (Char.IsLowSurrogate(lowChar))
                        {

                        }
                        else
                        {
                            DecoderFallback.CreateFallbackBuffer().GetNextChar();
                        }
                    }
                    else
                    {
                        DecoderFallback.CreateFallbackBuffer().GetNextChar();
                    }
                }
            }

            throw new NotImplementedException();
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

                        if (secondByte == 0x7F)
                        {
                            // 0x7F may not appear here.
                            DecoderFallback.CreateFallbackBuffer().GetNextChar();
                        }

                        charCount++;
                    }
                    else if (0x30 <= secondByte && secondByte <= 0x39)
                    {
                        //
                        // 4 bytes
                        //

                        if (index + 1 < limit)
                        {
                            var codePoint = CalculatedCodePoint((firstByte, secondByte, bytes[index++], bytes[index++]));

                            if (codePoint.HasValue)
                            {
                                charCount += codePoint.Value.Utf16SequenceLength;
                            }
                            else
                            {
                                DecoderFallback.CreateFallbackBuffer().GetNextChar();
                                charCount++;
                            }
                        }
                        else
                        {
                            index = limit;
                            DecoderFallback.CreateFallbackBuffer().GetNextChar();
                            charCount++;
                        }
                    }
                }
                else
                {
                    DecoderFallback.CreateFallbackBuffer().GetNextChar();
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
                            ?? new Rune(DecoderFallback.CreateFallbackBuffer().GetNextChar())
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
                                ?? new Rune(DecoderFallback.CreateFallbackBuffer().GetNextChar())
                            ).EncodeToUtf16(new Span<char>(chars, charIndex, chars.Length - charIndex));
                        }
                        else
                        {
                            byteIndex = byteLimit;
                            chars[charIndex++] = DecoderFallback.CreateFallbackBuffer().GetNextChar();
                        }
                    }
                }
                else
                {
                    chars[charIndex++] = DecoderFallback.CreateFallbackBuffer().GetNextChar();
                }
            }

            return charIndex - charStart;
        }

        public override int GetMaxByteCount(int charCount) => 4 * charCount;

        public override int GetMaxCharCount(int byteCount)
        {
            throw new NotImplementedException();
        }

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

        private (byte, byte, byte, byte)? CalculatedFourBytes(Rune codePoint)
        {
            foreach (var range in Ranges)
            {
                if (range.FirstValue <= codePoint && codePoint <= range.LastValue)
                    return Unlinear(Linear(range.FirstBytes) + codePoint.Value - range.FirstValue.Value);
            }

            return null;
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
