/*
 * SPDX-FileCopyrightText: 2021 William Swartzendruber <wswartzendruber@gmail.com>
 *
 * SPDX-License-Identifier: CC0-1.0
 */

using System.Collections.ObjectModel;

namespace GBX
{
    internal static class ReadOnlyDictionaryExtensions
    {
        public static T2? GetOrNull<T1, T2>(this ReadOnlyDictionary<T1, T2> self, T1 key)
            where T1: struct
            where T2: struct
        {
            if (self.ContainsKey(key))
                return self[key];
            else
                return null;
        }
    }
}
