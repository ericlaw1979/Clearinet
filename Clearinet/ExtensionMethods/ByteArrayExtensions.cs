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
    }
}
