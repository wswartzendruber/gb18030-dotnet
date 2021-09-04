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
            public readonly int FirstValue;
            public readonly int LastValue;
            public readonly (byte, byte, byte, byte) FirstBytes;
            public readonly (byte, byte, byte, byte) LastBytes;
            public readonly (byte, byte, byte, byte) MinBytes;
            public readonly (byte, byte, byte, byte) MaxBytes;

            public Range
            (
                int firstValue,
                int lastValue,
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

        private readonly static ReadOnlyDictionary<(byte, byte), int> TwoByteCodePoints;

        private readonly static ReadOnlyDictionary<(byte, byte, byte, byte), int> FourByteCodePoints;

        private readonly static ReadOnlyCollection<Range> Ranges;

        static GB18030Encoding()
        {
            using (var xmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GB18030.gb-18030-2000.xml")
                ?? throw new Exception("Could not load internal XML mapping file."))
            {
                var xdocument = new XmlDocument(); xdocument.Load(xmlStream);
                var xelement = xdocument.DocumentElement
                    ?? throw new XmlException("Could not load root element in internal XML document.");
                var assignmentNodes = xelement.SelectNodes("//characterMapping/assignments/a")
                    ?? throw new XmlException("Could not locate internal character assignments.");
                var rangeNodes = xelement.SelectNodes("//characterMapping/assignments/range")
                    ?? throw new XmlException("Could not locate internal character ranges.");
                var twoByteCodePoints = new Dictionary<(byte, byte), int>();
                var fourByteCodePoints = new Dictionary<(byte, byte, byte, byte), int>();
                var rangeList = new List<Range>();

                foreach (XmlNode assignmentNode in assignmentNodes)
                {
                    var byteStringArray = assignmentNode?.Attributes?["b"]?.Value?.Split(' ')
                        ?? throw new XmlException("Could not read byte sequence from internal character mapping.");
                    var codePoint = System.Convert.ToInt32(assignmentNode?.Attributes?["u"]?.Value
                        ?? throw new XmlException("Could not read Unicode code point from internal character mapping."), 16);

                    switch (byteStringArray.Length)
                    {
                        case 1:
                            continue;
                        case 2:
                            twoByteCodePoints[(
                                System.Convert.ToByte(byteStringArray[0], 16),
                                System.Convert.ToByte(byteStringArray[1], 16)
                            )] = codePoint;
                            break;
                        case 4:
                            fourByteCodePoints[(
                                System.Convert.ToByte(byteStringArray[0], 16),
                                System.Convert.ToByte(byteStringArray[1], 16),
                                System.Convert.ToByte(byteStringArray[2], 16),
                                System.Convert.ToByte(byteStringArray[3], 16)
                            )] = codePoint;
                            break;
                        default:
                            throw new Exception("Internal XML contains invalid byte sequence.");
                    }
                }

                TwoByteCodePoints = new ReadOnlyDictionary<(byte, byte), int>(twoByteCodePoints);
                FourByteCodePoints = new ReadOnlyDictionary<(byte, byte, byte, byte), int>(fourByteCodePoints);

                foreach (XmlNode rangeNode in rangeNodes)
                {
                    rangeList.Add(new Range(
                        System.Convert.ToInt32(rangeNode?.Attributes?["uFirst"]?.Value
                            ?? throw new XmlException("First Unicode code point cannot be read from internal character mapping assignment."), 16),
                        System.Convert.ToInt32(rangeNode?.Attributes?["uLast"]?.Value
                            ?? throw new XmlException("Last Unicode code point cannot be read from internal character mapping assignment."), 16),
                        ByteStringToBytes(rangeNode?.Attributes?["bFirst"]?.Value
                            ?? throw new XmlException("First byte sequence cannot be read from internal character mapping assignment.")),
                        ByteStringToBytes(rangeNode?.Attributes?["bLast"]?.Value
                            ?? throw new XmlException("Last byte sequence cannot be read from internal character mapping assignment.")),
                        ByteStringToBytes(rangeNode?.Attributes?["bMin"]?.Value
                            ?? throw new XmlException("Minimum byte sequence cannot be read from internal character mapping assignment.")),
                        ByteStringToBytes(rangeNode?.Attributes?["bMax"]?.Value
                            ?? throw new XmlException("Maximum byte sequence cannot be read from internal character mapping assignment."))
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
            throw new NotImplementedException();
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            var charCount = 0;
            var limit = index + count;

            while (index < limit)
            {
                var firstByte = bytes[index++];

                if (0x00 <= firstByte && firstByte <= 0x7F)
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
                            var thirdByte = bytes[index++];
                            var fourthByte = bytes[index++];
                            var codePoint = CalculatedCodePoint((firstByte, secondByte, thirdByte, fourthByte));

                            if (codePoint.HasValue)
                            {
                                charCount += Utf16CharSize(codePoint.Value);
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

                if (0x00 <= firstByte && firstByte <= 0x7F)
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

                        if (secondByte == 0x7F)
                        {
                            // 0x7F may not appear here.
                            DecoderFallback.CreateFallbackBuffer().GetNextChar();
                            continue;
                        }

                        var twoBytes = (firstByte, secondByte);

                        if (!TwoByteCodePoints.ContainsKey(twoBytes))
                        {
                            // These two bytes don't resolve to any known thing.
                            DecoderFallback.CreateFallbackBuffer().GetNextChar();
                            continue;
                        }

                        charIndex += WriteUtf16CodePoint(chars, charIndex, TwoByteCodePoints[twoBytes]);
                    }
                    else if (0x30 <= secondByte && secondByte <= 0x39)
                    {
                        //
                        // 4 bytes
                        //

                        if (byteIndex + 1 < byteLimit)
                        {
                            var fourBytes = (firstByte, secondByte, bytes[byteIndex++], bytes[byteIndex++]);
                            var codePoint = FourByteCodePoints.ContainsKey(fourBytes) switch
                            {
                                true => FourByteCodePoints[fourBytes],
                                false => CalculatedCodePoint(fourBytes),
                            };

                            if (codePoint.HasValue)
                                charIndex += WriteUtf16CodePoint(chars, charIndex, codePoint.Value);
                            else
                                chars[charIndex++] = DecoderFallback.CreateFallbackBuffer().GetNextChar();
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

        private int WriteUtf16CodePoint(char[] chars, int charIndex, int codePoint)
        {
            if (0x010000 <= codePoint && codePoint <= 0x10FFFF)
            {
                var ncp = codePoint - 0x10000;

                chars[charIndex++] = (char)(0xD800 + (ncp >> 10));
                chars[charIndex] = (char)(0xDC00 + (ncp & 0x3FF));

                return 2;
            }
            else
            {
                chars[charIndex] = (char)codePoint;

                return 1;
            }
        }

        private int Utf16CharSize(int codePoint)
        {
            if (0x010000 <= codePoint && codePoint <= 0x10FFFF)
                return 2;
            else
                return 1;
        }

        private int? CalculatedCodePoint((byte, byte, byte, byte) bytes)
        {
            int linear = Linear(bytes);

            foreach (var range in Ranges)
            {
                if (Linear(range.FirstBytes) <= linear && linear <= Linear(range.LastBytes))
                    return range.FirstValue + (linear - Linear(range.FirstBytes));
            }

            return null;
        }

        private (byte, byte, byte, byte)? CalculatedFourBytes(int codePoint)
        {
            foreach (var range in Ranges)
            {
                if (range.FirstValue <= codePoint && codePoint <= range.LastValue)
                    return Unlinear(Linear(range.FirstBytes) + codePoint - range.FirstValue);
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
