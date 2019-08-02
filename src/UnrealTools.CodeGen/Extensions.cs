using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace UnrealTools.CodeGen
{
    internal static class Extensions
    {
        internal static IEnumerable<T[]> Partition<T>(this IEnumerable<T> sequence, int partitionSize)
        {
            if (sequence is null)
                throw new ArgumentNullException(nameof(sequence));
            if (partitionSize <= 0)
                throw new ArgumentException(nameof(partitionSize));

            var buffer = new T[partitionSize];
            var n = 0;
            foreach (var item in sequence)
            {
                buffer[n] = item;
                n++;
                if (n == partitionSize)
                {
                    yield return buffer;
                    buffer = new T[partitionSize];
                    n = 0;
                }
            }
            //partial leftovers
            if (n > 0)
            {
                var retbuf = new T[n];
                Array.Copy(buffer, retbuf, n);
                yield return retbuf;
            }
        }
    }
}
