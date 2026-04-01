using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Spearing.Data.Frames
{

    internal static class StringMath
    {
        public static ColumnBuffer<string> Add(ReadOnlySpan<string> a, ReadOnlySpan<string> b)
        {
            var res = new ColumnBuffer<string>(a.Length);
            var span = res.Span;
            for (int i = 0; i < a.Length; i++) span[i] = a[i] + b[i];
            return res;
        }

        public static ColumnBuffer<string> AddScalar(ReadOnlySpan<string> a, string s)
        {
            var res = new ColumnBuffer<string>(a.Length);
            var span = res.Span;
            for (int i = 0; i < a.Length; i++) span[i] = a[i] + s;
            return res;
        }
    }

    internal static class NumericMath
    {
        #region DECIMAL (Financial Priority)
        public static void Mult(ReadOnlySpan<decimal> a, ReadOnlySpan<decimal> b, Span<decimal> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * b[i]; }
        public static void Add(ReadOnlySpan<decimal> a, ReadOnlySpan<decimal> b, Span<decimal> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + b[i]; }
        public static void Sub(ReadOnlySpan<decimal> a, ReadOnlySpan<decimal> b, Span<decimal> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] - b[i]; }
        public static void MultS(ReadOnlySpan<decimal> a, decimal s, Span<decimal> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * s; }
        public static void AddS(ReadOnlySpan<decimal> a, decimal s, Span<decimal> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + s; }
        #endregion

        #region DOUBLE (High-Speed Science)
        public static void Mult(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * b[i]; }
        public static void Add(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + b[i]; }
        public static void Sub(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] - b[i]; }
        public static void MultS(ReadOnlySpan<double> a, double s, Span<double> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * s; }
        public static void AddS(ReadOnlySpan<double> a, double s, Span<double> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + s; }
        #endregion

        #region FLOAT
        public static void Mult(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * b[i]; }
        public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + b[i]; }
        public static void Sub(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] - b[i]; }
        public static void MultS(ReadOnlySpan<float> a, float s, Span<float> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * s; }
        public static void AddS(ReadOnlySpan<float> a, float s, Span<float> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + s; }
        #endregion

        #region LONG
        public static void Mult(ReadOnlySpan<long> a, ReadOnlySpan<long> b, Span<long> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * b[i]; }
        public static void Add(ReadOnlySpan<long> a, ReadOnlySpan<long> b, Span<long> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + b[i]; }
        public static void Sub(ReadOnlySpan<long> a, ReadOnlySpan<long> b, Span<long> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] - b[i]; }
        public static void MultS(ReadOnlySpan<long> a, long s, Span<long> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * s; }
        public static void AddS(ReadOnlySpan<long> a, long s, Span<long> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + s; }
        #endregion

        #region INT
        public static void Mult(ReadOnlySpan<int> a, ReadOnlySpan<int> b, Span<int> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * b[i]; }
        public static void Add(ReadOnlySpan<int> a, ReadOnlySpan<int> b, Span<int> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + b[i]; }
        public static void Sub(ReadOnlySpan<int> a, ReadOnlySpan<int> b, Span<int> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] - b[i]; }
        public static void MultS(ReadOnlySpan<int> a, int s, Span<int> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] * s; }
        public static void AddS(ReadOnlySpan<int> a, int s, Span<int> d) { for (int i = 0; i < a.Length; i++) d[i] = a[i] + s; }
        #endregion
    }


    internal static class MathProvider<T>
    {
        public static ColumnBuffer<T> Multiply(ReadOnlySpan<T> a, ReadOnlySpan<T> b)
        {
            var res = new ColumnBuffer<T>(a.Length);
            if (typeof(T) == typeof(double)) NumericMath.Mult(AsD(a), AsD(b), AsD(res.Span));
            else if (typeof(T) == typeof(decimal)) NumericMath.Mult(AsDec(a), AsDec(b), AsDec(res.Span));
            else if (typeof(T) == typeof(float)) NumericMath.Mult(AsF(a), AsF(b), AsF(res.Span)); // Added
            else if (typeof(T) == typeof(long)) NumericMath.Mult(AsL(a), AsL(b), AsL(res.Span));  // Added
            else if (typeof(T) == typeof(int)) NumericMath.Mult(AsI(a), AsI(b), AsI(res.Span));
            else throw new NotSupportedException();
            return res;
        }

        public static ColumnBuffer<T> Add(ReadOnlySpan<T> a, ReadOnlySpan<T> b)
        {
            var res = new ColumnBuffer<T>(a.Length);
            if (typeof(T) == typeof(double)) NumericMath.Add(AsD(a), AsD(b), AsD(res.Span));
            else if (typeof(T) == typeof(decimal)) NumericMath.Add(AsDec(a), AsDec(b), AsDec(res.Span));
            else if (typeof(T) == typeof(float)) NumericMath.Add(AsF(a), AsF(b), AsF(res.Span)); // Added
            else if (typeof(T) == typeof(long)) NumericMath.Add(AsL(a), AsL(b), AsL(res.Span));  // Added
            else if (typeof(T) == typeof(int)) NumericMath.Add(AsI(a), AsI(b), AsI(res.Span));
            else if (typeof(T) == typeof(string)) return (ColumnBuffer<T>)(object)StringMath.Add(AsStr(a), AsStr(b));
            else throw new NotSupportedException();
            return res;
        }

        public static ColumnBuffer<T> Subtract(ReadOnlySpan<T> a, ReadOnlySpan<T> b)
        {
            var res = new ColumnBuffer<T>(a.Length);
            if (typeof(T) == typeof(double)) NumericMath.Sub(AsD(a), AsD(b), AsD(res.Span));
            else if (typeof(T) == typeof(decimal)) NumericMath.Sub(AsDec(a), AsDec(b), AsDec(res.Span)); // Added
            else if (typeof(T) == typeof(float)) NumericMath.Sub(AsF(a), AsF(b), AsF(res.Span));         // Added
            else if (typeof(T) == typeof(long)) NumericMath.Sub(AsL(a), AsL(b), AsL(res.Span));          // Added
            else if (typeof(T) == typeof(int)) NumericMath.Sub(AsI(a), AsI(b), AsI(res.Span));
            else throw new NotSupportedException();
            return res;
        }

        public static ColumnBuffer<T> MultiplyScalar(ReadOnlySpan<T> a, T s)
        {
            var res = new ColumnBuffer<T>(a.Length);
            if (typeof(T) == typeof(double)) NumericMath.MultS(AsD(a), Unsafe.As<T, double>(ref s), AsD(res.Span));
            else if (typeof(T) == typeof(decimal)) NumericMath.MultS(AsDec(a), Unsafe.As<T, decimal>(ref s), AsDec(res.Span)); // Added
            else if (typeof(T) == typeof(float)) NumericMath.MultS(AsF(a), Unsafe.As<T, float>(ref s), AsF(res.Span));         // Added
            else if (typeof(T) == typeof(long)) NumericMath.MultS(AsL(a), Unsafe.As<T, long>(ref s), AsL(res.Span));          // Added
            else if (typeof(T) == typeof(int)) NumericMath.MultS(AsI(a), Unsafe.As<T, int>(ref s), AsI(res.Span));
            else throw new NotSupportedException();
            return res;
        }

        public static ColumnBuffer<T> AddScalar(ReadOnlySpan<T> a, T s)
        {
            var res = new ColumnBuffer<T>(a.Length);
            if (typeof(T) == typeof(double)) NumericMath.AddS(AsD(a), Unsafe.As<T, double>(ref s), AsD(res.Span));
            else if (typeof(T) == typeof(decimal)) NumericMath.AddS(AsDec(a), Unsafe.As<T, decimal>(ref s), AsDec(res.Span)); // Added
            else if (typeof(T) == typeof(float)) NumericMath.AddS(AsF(a), Unsafe.As<T, float>(ref s), AsF(res.Span));         // Added
            else if (typeof(T) == typeof(long)) NumericMath.AddS(AsL(a), Unsafe.As<T, long>(ref s), AsL(res.Span));          // Added
            else if (typeof(T) == typeof(int)) NumericMath.AddS(AsI(a), Unsafe.As<T, int>(ref s), AsI(res.Span));
            else if (typeof(T) == typeof(string)) return (ColumnBuffer<T>)(object)StringMath.AddScalar(AsStr(a), (string)(object)s);
            else throw new NotSupportedException();
            return res;
        }

        public static ColumnBuffer<T> MultiplyArray(ReadOnlySpan<T> a, T[] b) => Multiply(a, b.AsSpan());

        #region Internal Casting Helpers (Added float & long)
        private static ReadOnlySpan<double> AsD(ReadOnlySpan<T> s) => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, double>(ref MemoryMarshal.GetReference(s)), s.Length);
        private static Span<double> AsD(Span<T> s) => MemoryMarshal.CreateSpan(ref Unsafe.As<T, double>(ref MemoryMarshal.GetReference(s)), s.Length);

        private static ReadOnlySpan<decimal> AsDec(ReadOnlySpan<T> s) => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, decimal>(ref MemoryMarshal.GetReference(s)), s.Length);
        private static Span<decimal> AsDec(Span<T> s) => MemoryMarshal.CreateSpan(ref Unsafe.As<T, decimal>(ref MemoryMarshal.GetReference(s)), s.Length);

        private static ReadOnlySpan<float> AsF(ReadOnlySpan<T> s) => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, float>(ref MemoryMarshal.GetReference(s)), s.Length);
        private static Span<float> AsF(Span<T> s) => MemoryMarshal.CreateSpan(ref Unsafe.As<T, float>(ref MemoryMarshal.GetReference(s)), s.Length);

        private static ReadOnlySpan<long> AsL(ReadOnlySpan<T> s) => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, long>(ref MemoryMarshal.GetReference(s)), s.Length);
        private static Span<long> AsL(Span<T> s) => MemoryMarshal.CreateSpan(ref Unsafe.As<T, long>(ref MemoryMarshal.GetReference(s)), s.Length);

        private static ReadOnlySpan<int> AsI(ReadOnlySpan<T> s) => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, int>(ref MemoryMarshal.GetReference(s)), s.Length);
        private static Span<int> AsI(Span<T> s) => MemoryMarshal.CreateSpan(ref Unsafe.As<T, int>(ref MemoryMarshal.GetReference(s)), s.Length);

        private static ReadOnlySpan<string> AsStr(ReadOnlySpan<T> s) => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, string>(ref MemoryMarshal.GetReference(s)), s.Length);
        #endregion
    }


}
