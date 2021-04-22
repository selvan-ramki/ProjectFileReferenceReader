using System;

namespace ProjectFileReferenceReader
{
    public class GeneralUtility
    {
        public static void ThrowIfInvalid(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new Exception($"Invalid {key}");
        }

        public static void ProcessDelay()
        {
            System.Threading.Thread.Sleep(50);
        }
    }
}
