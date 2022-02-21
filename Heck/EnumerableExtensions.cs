using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Heck
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TResult> SafeCast<TResult>(this IEnumerable source)
        {
            if (source is IEnumerable<TResult> results)
            {
                return results;
            }

            return source != null ? CastIterator<TResult>(source) : throw new NullReferenceException();
        }

        public static IEnumerable<TResult> SelectNonNull<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult?> func)
            where TResult : struct
        {
            return source.Select(func).SafeCast<TResult>();
        }

        private static IEnumerable<TResult> CastIterator<TResult>(IEnumerable source)
        {
            foreach (object result in source)
            {
                if (result is TResult r)
                {
                    yield return r;
                }
            }
        }
    }
}
