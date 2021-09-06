using System.Collections.ObjectModel;

namespace GB18030
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
