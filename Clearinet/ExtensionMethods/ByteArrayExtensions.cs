using System;

namespace Clearinet
{
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Generates a copy of the array, quickly.
        /// </summary>
        public static byte[] FastClone(this byte[] source)
        {
            if (source == null) return null;
            if (source.Length == 0) return Array.Empty<byte>();

            byte[] dest = new byte[source.Length];
            Buffer.BlockCopy(source, 0, dest, 0, source.Length);
            return dest;
        }
        public static byte[] Append(this byte[] arrPrefix, byte[] arrSuffix)
        {
            if (arrPrefix == null) throw new ArgumentNullException(nameof(arrPrefix));
            if (arrSuffix == null) return arrPrefix;

            byte[] arrFinal = new byte[arrPrefix.Length + arrSuffix.Length];
            Array.Copy(arrPrefix, 0, arrFinal, 0, arrPrefix.Length);
            Array.Copy(arrSuffix, 0, arrFinal, arrPrefix.Length, arrSuffix.Length);

            return arrFinal;
        }

        public static bool HasData(this byte[] source) => source?.Length > 0;

        public static bool HasNonTextData(this byte[] source)
        {
            // TODO: This needs to be smarter because 2-byte Unicode...
            if (source == null || source.Length == 0) return false;
            foreach (byte b in source)
            {
                if (b < 32 && b != 9 && b != 10 && b != 13) return true;
            }
            return false;
        }
    }
}
