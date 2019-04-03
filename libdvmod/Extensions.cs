using System;
using System.IO;
using JetBrains.Annotations;

namespace libdvmod
{
    public static class Extensions
    {
        [NotNull]
        public static T NotNull<T>([CanBeNull] this T obj) where T : class
        {
            return obj ?? throw new NullReferenceException();
        }

        public static void CopyTo([NotNull] this Stream src, [NotNull] Stream dst)
        {
            var buffer = new byte[4096];
            int read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                dst.Write(buffer, 0, read);
        }
    }
}