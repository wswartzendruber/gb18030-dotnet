/*
 * SPDX-FileCopyrightText: 2021 William Swartzendruber <wswartzendruber@gmail.com>
 *
 * SPDX-License-Identifier: CC0-1.0
 */

using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace GB18030.MapGenerator
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("ERROR: Three arguments are expected.");
                return 1;
            }

            using (var charMapStream = new FileStream(args[0], FileMode.Open))
            using (var twoByteCodePointStream = new FileStream(args[1], FileMode.Create))
            using (var fourByteCodePointStream = new FileStream(args[2], FileMode.Create))
            {
                var xdocument = new XmlDocument(); xdocument.Load(charMapStream);
                var xelement = xdocument.DocumentElement
                    ?? throw new XmlException("Could not load internal character map's root element.");
                var assignmentNodes = xelement.SelectNodes("//characterMapping/assignments/a")
                    ?? throw new XmlException("Could not locate internal character map's assignments element.");
                var rangeNodes = xelement.SelectNodes("//characterMapping/assignments/range")
                    ?? throw new XmlException("Could not locate internal character map's range element.");

                foreach (XmlNode assignmentNode in assignmentNodes)
                {
                    var byteArray = ByteStringToByteArray(assignmentNode?.Attributes?["b"]?.Value
                        ?? throw new XmlException("Could not read assignment's byte sequence from internal character map."));
                    var codePoint = ThreeByteCodePoint(Convert.ToInt32(assignmentNode?.Attributes?["u"]?.Value
                        ?? throw new XmlException("Could not read assignment's code point from internal character map."), 16));

                    switch (byteArray.Length)
                    {
                        case 1:
                            continue;
                        case 2:
                            twoByteCodePointStream.Write(codePoint);
                            twoByteCodePointStream.Write(byteArray);
                            break;
                        case 4:
                            fourByteCodePointStream.Write(codePoint);
                            fourByteCodePointStream.Write(byteArray);
                            break;
                        default:
                            throw new XmlException("Internal XML contains invalid byte sequence.");
                    }
                }
            }

            return 0;
        }

        private static byte[] ByteStringToByteArray(String byteString) =>
            byteString.Split(' ').Select(it => Convert.ToByte(it, 16)).ToArray();

        private static byte[] ThreeByteCodePoint(int codePoint) =>
            new byte[] { (byte)(codePoint >> 16 & 0xFF), (byte)(codePoint >> 8 & 0xFF), (byte)(codePoint & 0xFF) };
    }
}
