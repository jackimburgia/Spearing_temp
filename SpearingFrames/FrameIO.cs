using CsvHelper;
using CsvHelper.Configuration;
using System.Collections;
using System.Globalization;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Spearing.Data.Frames
{
    public static class FrameIO
    {
        public static Frame ReadCsv(string path, Dictionary<string, Type>? typeMappings = null)
        {
            typeMappings ??= new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.Trim()
            };

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, config);

            if (!csv.Read() || !csv.ReadHeader()) return new Frame(0);

            string[] headers = csv.HeaderRecord!;
            var rawRows = new List<string[]>();
            while (csv.Read())
            {
                var row = new string[headers.Length];
                for (int i = 0; i < headers.Length; i++) row[i] = csv.GetField(i) ?? "";
                rawRows.Add(row);
            }

            var frame = new Frame(rawRows.Count);

            for (int colIdx = 0; colIdx < headers.Length; colIdx++)
            {
                string colName = headers[colIdx];
                bool wasMapped = typeMappings.TryGetValue(colName, out Type? targetType);

                if (!wasMapped)
                {
                    targetType = InferFullType(rawRows, colIdx);
                }

                // Rule 2: If mapped, this will "blow up" on bad data.
                var columnData = ParseData(rawRows, colIdx, targetType!);
                frame.AddColumn(colName, columnData);
            }

            return frame;
        }

        //public static Type InferFullType(this List<object[]> data, int colIdx)
        //{
        //    var strings = data.Select(row => row.Select(r => r?.ToString()).ToArray()).ToList();
        //    var type = strings.InferFullType(colIdx);
        //    return type;
        //}

        public static Type InferFullType(this object[] values)
        {
            var strings = values.Select(value => value?.ToString()).ToArray();
            var t = strings.InferFullType();
            return t; 
        }


        public static Type InferFullType(this string[] values)
        {
            bool hasNulls = false;

            // Candidates for inference
            bool canBool = true, canChar = true, canGuid = true, canTime = true, canDate = true;
            bool canSbyte = true, canByte = true, canShort = true, canUshort = true;
            bool canInt = true, canUint = true, canLong = true, canUlong = true;
            bool canDec = true, canDouble = true, canFloat = true;

            foreach (var s in values)
            {
                //string s = row[colIdx];
                if (string.IsNullOrWhiteSpace(s))
                {
                    hasNulls = true;
                    continue;
                }

                // Logic: If a type fails a parse check once, it is dead for this column.
                if (canBool && !bool.TryParse(s, out _)) canBool = false;
                if (canChar && s.Length != 1) canChar = false;
                if (canGuid && !Guid.TryParse(s, out _)) canGuid = false;

                // Check TimeSpan BEFORE DateTime because DateTime.TryParse is "greedy" 
                // and will capture "12:30:00" as "Today at 12:30 PM".
                if (canTime && !TimeSpan.TryParse(s, out _)) canTime = false;
                if (canDate && !DateTime.TryParse(s, out _)) canDate = false;

                // --- Integer Ladder ---
                if (canSbyte && !sbyte.TryParse(s, out _)) canSbyte = false;
                if (canByte && !byte.TryParse(s, out _)) canByte = false;
                if (canShort && !short.TryParse(s, out _)) canShort = false;
                if (canUshort && !ushort.TryParse(s, out _)) canUshort = false;
                if (canInt && !int.TryParse(s, out _)) canInt = false;
                if (canUint && !uint.TryParse(s, out _)) canUint = false;
                if (canLong && !long.TryParse(s, out _)) canLong = false;
                if (canUlong && !ulong.TryParse(s, out _)) canUlong = false;

                // --- Floating Point & Financial ---
                if (canDec && !decimal.TryParse(s, out _)) canDec = false;
                if (canDouble && !double.TryParse(s, out _)) canDouble = false;
                if (canFloat && !float.TryParse(s, out _)) canFloat = false;
            }

            // --- The Final Promotion Ladder (Rule #1: Pessimistic Inference) ---
            // We start with the most restrictive types and move toward the most general.
            Type? result = null;

            if (canBool) result = typeof(bool);
            else if (canSbyte) result = typeof(sbyte);
            else if (canByte) result = typeof(byte);
            else if (canShort) result = typeof(short);
            else if (canUshort) result = typeof(ushort);
            else if (canInt) result = typeof(int);
            else if (canUint) result = typeof(uint);
            else if (canLong) result = typeof(long);
            else if (canUlong) result = typeof(ulong);

            // Financial Priority: Decimal is chosen over Double/Float for precision
            else if (canDec) result = typeof(decimal);
            else if (canDouble) result = typeof(double);
            else if (canFloat) result = typeof(float);

            // Structs: Duration (TimeSpan) is more specific than Point-in-Time (DateTime)
            else if (canTime) result = typeof(TimeSpan);
            else if (canDate) result = typeof(DateTime);

            else if (canGuid) result = typeof(Guid);
            else if (canChar) result = typeof(char);

            // Default Fallback
            if (result == null) return typeof(string);

            // Rule #1: Wrap in Nullable if any empty strings were found during the scan
            return hasNulls ? typeof(Nullable<>).MakeGenericType(result) : result;
        }

        public static Type InferFullType(this List<string[]> data, int colIdx)
        {
            var values = data.Select(row => row[colIdx]).ToArray();
            var t = values.InferFullType();

            return t;


            bool hasNulls = false;

            // Candidates for inference
            bool canBool = true, canChar = true, canGuid = true, canTime = true, canDate = true;
            bool canSbyte = true, canByte = true, canShort = true, canUshort = true;
            bool canInt = true, canUint = true, canLong = true, canUlong = true;
            bool canDec = true, canDouble = true, canFloat = true;

            foreach (var row in data)
            {
                string s = row[colIdx];
                if (string.IsNullOrWhiteSpace(s))
                {
                    hasNulls = true;
                    continue;
                }

                // Logic: If a type fails a parse check once, it is dead for this column.
                if (canBool && !bool.TryParse(s, out _)) canBool = false;
                if (canChar && s.Length != 1) canChar = false;
                if (canGuid && !Guid.TryParse(s, out _)) canGuid = false;

                // Check TimeSpan BEFORE DateTime because DateTime.TryParse is "greedy" 
                // and will capture "12:30:00" as "Today at 12:30 PM".
                if (canTime && !TimeSpan.TryParse(s, out _)) canTime = false;
                if (canDate && !DateTime.TryParse(s, out _)) canDate = false;

                // --- Integer Ladder ---
                if (canSbyte && !sbyte.TryParse(s, out _)) canSbyte = false;
                if (canByte && !byte.TryParse(s, out _)) canByte = false;
                if (canShort && !short.TryParse(s, out _)) canShort = false;
                if (canUshort && !ushort.TryParse(s, out _)) canUshort = false;
                if (canInt && !int.TryParse(s, out _)) canInt = false;
                if (canUint && !uint.TryParse(s, out _)) canUint = false;
                if (canLong && !long.TryParse(s, out _)) canLong = false;
                if (canUlong && !ulong.TryParse(s, out _)) canUlong = false;

                // --- Floating Point & Financial ---
                if (canDec && !decimal.TryParse(s, out _)) canDec = false;
                if (canDouble && !double.TryParse(s, out _)) canDouble = false;
                if (canFloat && !float.TryParse(s, out _)) canFloat = false;
            }

            // --- The Final Promotion Ladder (Rule #1: Pessimistic Inference) ---
            // We start with the most restrictive types and move toward the most general.
            Type? result = null;

            if (canBool) result = typeof(bool);
            else if (canSbyte) result = typeof(sbyte);
            else if (canByte) result = typeof(byte);
            else if (canShort) result = typeof(short);
            else if (canUshort) result = typeof(ushort);
            else if (canInt) result = typeof(int);
            else if (canUint) result = typeof(uint);
            else if (canLong) result = typeof(long);
            else if (canUlong) result = typeof(ulong);

            // Financial Priority: Decimal is chosen over Double/Float for precision
            else if (canDec) result = typeof(decimal);
            else if (canDouble) result = typeof(double);
            else if (canFloat) result = typeof(float);

            // Structs: Duration (TimeSpan) is more specific than Point-in-Time (DateTime)
            else if (canTime) result = typeof(TimeSpan);
            else if (canDate) result = typeof(DateTime);

            else if (canGuid) result = typeof(Guid);
            else if (canChar) result = typeof(char);

            // Default Fallback
            if (result == null) return typeof(string);

            // Rule #1: Wrap in Nullable if any empty strings were found during the scan
            return hasNulls ? typeof(Nullable<>).MakeGenericType(result) : result;
        }


        public static IList CreateTypedList(this IEnumerable<object> items, Type type)
        {
            // Create a List<T> where T is your inferred type
            var listType = typeof(List<>).MakeGenericType(type);
            var list = (IList)Activator.CreateInstance(listType);

            foreach (var item in items)
            {
                // Convert.ChangeType handles minor mismatches (like int to decimal)
                list.Add(Convert.ChangeType(item, type));
            }
            return list;
        }

        private static Array ParseData(List<string[]> rawRows, int colIdx, Type targetType)
        {
            int count = rawRows.Count;
            var arr = Array.CreateInstance(targetType, count);
            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            for (int i = 0; i < count; i++)
            {
                string s = rawRows[i][colIdx];
                if (string.IsNullOrWhiteSpace(s))
                {
                    arr.SetValue(null, i);
                    continue;
                }

                object val = underlyingType switch
                {
                    var t when t == typeof(bool) => bool.Parse(s),
                    var t when t == typeof(char) => s[0],
                    var t when t == typeof(sbyte) => sbyte.Parse(s),
                    var t when t == typeof(byte) => byte.Parse(s),
                    var t when t == typeof(short) => short.Parse(s),
                    var t when t == typeof(ushort) => ushort.Parse(s),
                    var t when t == typeof(int) => int.Parse(s),
                    var t when t == typeof(uint) => uint.Parse(s),
                    var t when t == typeof(long) => long.Parse(s),
                    var t when t == typeof(ulong) => ulong.Parse(s),
                    var t when t == typeof(decimal) => decimal.Parse(s),
                    var t when t == typeof(double) => double.Parse(s),
                    var t when t == typeof(float) => float.Parse(s),
                    var t when t == typeof(DateTime) => DateTime.Parse(s),
                    var t when t == typeof(TimeSpan) => TimeSpan.Parse(s),
                    var t when t == typeof(Guid) => Guid.Parse(s),
                    _ => s
                };
                arr.SetValue(val, i);
            }
            return arr;
        }
    }
}
