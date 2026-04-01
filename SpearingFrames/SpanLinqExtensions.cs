using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Numerics;

namespace Spearing.Data.Frames
{

    // TODO - are these used?
    //  - are they for performance gains?
    public static class SpanLinqExtensions
    {
        public static double Average(this ColumnEnumerable<double> col) => col.AsSpan().Length == 0 ? 0 : col.AsSpan().ToArray().Average();
        public static double Sum(this ColumnEnumerable<double> col) { double s = 0; foreach (var v in col.AsSpan()) s += v; return s; }
    }
}
