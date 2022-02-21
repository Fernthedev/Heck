using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Heck
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TResult> SelectNonNull<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult?> func)
            where TResult : struct
        {
            return source.Select(func).OfType<TResult>();
        }
    }
}
