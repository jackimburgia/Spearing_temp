using Spearing.Data.Frames;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace BridgeTester1.NewStructure2
{
    public static class FormulaTestEnvironment
    {

        public static void RunScalarTests()
        {
            Dictionary<string, Frame> db = new Dictionary<string, Frame>();

            var assetsFrame = FormulaTestEnvironment.CreateTestFrame();
            assetsFrame.Print();

            var dealFrame = new Frame();
            dealFrame["DealName"] = new string[] { "CLO 1"};

            db.Add("Assets", assetsFrame);
            db.Add("Deal", dealFrame);

            dealFrame["TotalPar"] = db.ExecuteScalar("SELECT SUM(Par) FROM Assets");

            dealFrame["TotalParNul"] = db.ExecuteScalar("SELECT SUM(Par) FROM Assets WHERE 1 = 2");

            dealFrame.Print();
        }

        public static void RunMappingTests()
        {
            var assetsFrame = FormulaTestEnvironment.CreateTestFrame();
            assetsFrame.Print();

            Frame lookupFrame = new Frame();
            lookupFrame["AssetTypeDescr"] = new string[] { "First Lien", "Second Lien", "Equity" };
            lookupFrame["ShortCode"] = new string[] { "FL", "SL", "EQ" };

            lookupFrame.Print();

            // Map(string sourceColumnName, Frame lookupFrame, string lookupKeyColumnName, string lookupMapColumnName, string defaultValue)
            // Map("AssetType", "LookupFrameName", "AssetTypeDescr", "ShortCode", "Other")
            assetsFrame["AssetTypeShort"] = assetsFrame.Map("AssetType", lookupFrame, "AssetTypeDescr", "ShortCode", "Other");

            assetsFrame.Print();
        }


        // Console Color Helpers for clear visual feedback
        private static void WriteGreen(string text) { Console.ForegroundColor = ConsoleColor.Green; Console.Write(text); Console.ResetColor(); }
        private static void WriteRed(string text) { Console.ForegroundColor = ConsoleColor.Red; Console.Write(text); Console.ResetColor(); }
        private static void WriteHeader(string text) { Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine($"\n=== {text} ==="); Console.ResetColor(); }

        public static void RunReadCSVTests()
        {
            Console.WriteLine("Starting Full-Spectrum CSV Type Tests...");

            // ---------------------------------------------------------
            // SCENARIO 1: Strict Inference on 50 Rows (Rule #1)
            // ---------------------------------------------------------
            WriteHeader("SCENARIO 1: ALL-TYPE INFERENCE (STRICT)");
            try
            {
                var frame = FrameIO.ReadCsv("all_types_test.csv");

                VerifyType(frame, "ColBool", typeof(bool));
                VerifyType(frame, "ColChar", typeof(char));
                VerifyType(frame, "ColSByte", typeof(sbyte));
                VerifyType(frame, "ColInt16", typeof(short));
                VerifyType(frame, "ColInt32", typeof(int));
                VerifyType(frame, "ColInt64", typeof(long));
                VerifyType(frame, "ColDecimal", typeof(decimal));
                VerifyType(frame, "ColDouble", typeof(double));
                VerifyType(frame, "ColDateTime", typeof(DateTime));
                VerifyType(frame, "ColTimeSpan", typeof(TimeSpan));
                VerifyType(frame, "ColGuid", typeof(Guid));
                VerifyType(frame, "ColString", typeof(string));
            }
            catch (Exception ex) { WriteRed($"Inference Failed: {ex.Message}\n"); }

            // ---------------------------------------------------------
            // SCENARIO 2: Nullable Promotion on 50 Rows (Rule #1)
            // ---------------------------------------------------------
            WriteHeader("SCENARIO 2: NULLABLE PROMOTION");
            try
            {
                var frame = FrameIO.ReadCsv("all_types_nullable.csv");

                VerifyType(frame, "N_Bool", typeof(bool?));
                VerifyType(frame, "N_Char", typeof(char?));
                VerifyType(frame, "N_SByte", typeof(sbyte?));
                VerifyType(frame, "N_Int32", typeof(int?));
                VerifyType(frame, "N_Decimal", typeof(decimal?));
                VerifyType(frame, "N_DateTime", typeof(DateTime?));
                VerifyType(frame, "N_TimeSpan", typeof(TimeSpan?));
                VerifyType(frame, "N_Guid", typeof(Guid?));
            }
            catch (Exception ex) { WriteRed($"Nullable Promotion Failed: {ex.Message}\n"); }

            // ---------------------------------------------------------
            // SCENARIO 3: Unsigned & Explicit Mappings (Rule #2)
            // ---------------------------------------------------------
            WriteHeader("SCENARIO 3: EXPLICIT MAPPING (UNSIGNED/FLOAT)");
            try
            {
                var mappings = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
                {
                    { "ColByte", typeof(byte) },
                    { "ColUint16", typeof(ushort) },
                    { "ColUint32", typeof(uint) },
                    { "ColUint64", typeof(ulong) },
                    { "ColFloat", typeof(float) }
                };

                var frame = FrameIO.ReadCsv("all_types_test.csv", mappings);

                VerifyType(frame, "ColByte", typeof(byte));
                VerifyType(frame, "ColUint16", typeof(ushort));
                VerifyType(frame, "ColUint32", typeof(uint));
                VerifyType(frame, "ColUint64", typeof(ulong));
                VerifyType(frame, "ColFloat", typeof(float));
            }
            catch (Exception ex) { WriteRed($"Explicit Mapping Failed: {ex.Message}\n"); }

            // ---------------------------------------------------------
            // SCENARIO 4: Strict Mapping Violation (Rule #2 Blow Up)
            // ---------------------------------------------------------
            WriteHeader("SCENARIO 4: RULE #2 VIOLATION (EXPECTED BLOW UP)");
            try
            {
                // Force a String column containing "Row_1" into an Int32
                var mappings = new Dictionary<string, Type> { { "ColString", typeof(int) } };
                Console.WriteLine("Forcing 'ColString' to Int32 (Should throw FormatException)...");

                FrameIO.ReadCsv("all_types_test.csv", mappings);

                WriteRed("[FAIL] Engine did not blow up! String was incorrectly allowed in Int32 column.\n");
            }
            catch (FormatException)
            {
                WriteGreen("[PASS] Engine correctly threw FormatException on invalid mapping.\n");
            }
            catch (Exception ex)
            {
                WriteGreen($"[PASS] Engine correctly blew up with {ex.GetType().Name}.\n");
            }

            Console.WriteLine("\nTests Completed.");
        }

        private static void VerifyType(Frame frame, string colName, Type expected)
        {
            var actual = frame[colName].Buffer.ElementType;
            bool match = actual == expected;

            Console.Write($"{colName,-12} | ");

            // Format the type names for better readability (e.g., Int32? instead of Nullable`1)
            string actualName = GetFriendlyName(actual);
            string expectedName = GetFriendlyName(expected);

            if (match)
            {
                WriteGreen("PASS");
                Console.WriteLine($" (Actual: {actualName})");
            }
            else
            {
                WriteRed("FAIL");
                Console.WriteLine($" (Expected: {expectedName}, Actual: {actualName})");
            }
        }

        private static string GetFriendlyName(Type t)
        {
            var underlying = Nullable.GetUnderlyingType(t);
            return underlying != null ? $"{underlying.Name}?" : t.Name;
        }
        

        private static void PrintColumnTypes(Frame frame)
        {
            Console.WriteLine("{0,-15} | {1,-20} | {2,-10}", "Column", "Inferred Type", "Nullable?");
            Console.WriteLine(new string('-', 50));

            foreach (var colName in frame.ColumnNames)
            {
                var column = frame[colName];
                var type = column.Buffer.ElementType;
                bool isNullable = Nullable.GetUnderlyingType(type) != null;

                Console.WriteLine("{0,-15} | {1,-20} | {2,-10}",
                    colName,
                    isNullable ? Nullable.GetUnderlyingType(type).Name + "?" : type.Name,
                    isNullable);
            }
        }

        public static void RunBasicSelects()
        {
            // 1. Setup the "Database" Context
            var assetsFrame = FormulaTestEnvironment.CreateTestFrame();
            var context = new Dictionary<string, Frame>(StringComparer.OrdinalIgnoreCase)
            {
                { "Assets", assetsFrame }
            };

            //var engine = new FrameQueryEngine(context);

            Console.WriteLine("=== STARTING BASIC SELECT TESTS ===");

            // Test 1: Simple Column Selection
            Console.WriteLine("Test 1: SELECT AssetType, Par FROM Assets");
            context.GetFrame("SELECT AssetType, Par FROM Assets").Print();

            // Test 2: Selection with Aliases
            Console.WriteLine("Test 2: SELECT AssetType AS Type, Rating AS Rank FROM Assets");
            context.GetFrame("SELECT AssetType AS Type, Rating AS Rank FROM Assets").Print();

            // Test 3: Math in Select
            Console.WriteLine("Test 3: SELECT AssetType, Par * 1.1 AS NextYearPar FROM Assets");
            context.GetFrame("SELECT AssetType, Par * 1.1 AS NextYearPar FROM Assets").Print();

            // Test 4: SELECT with WHERE filter
            Console.WriteLine("Test 4: SELECT AssetType, Par FROM Assets WHERE Par > 100000");
            context.GetFrame("SELECT AssetType, Par FROM Assets WHERE Par > 100000").Print();

            // Test 5: SELECT with ORDER BY
            Console.WriteLine("Test 5: SELECT AssetType, Par FROM Assets ORDER BY Par DESC");
            context.GetFrame("SELECT AssetType, Par FROM Assets ORDER BY Par DESC").Print();

            // Test 6: All clauses combined
            Console.WriteLine("Test 6: Complex Query");
            string sql = @"
                SELECT AssetType, Par, IF Par > 100000 THEN 'High' ELSE 'Low' AS Tier 
                FROM Assets 
                WHERE IsLoan = true 
                ORDER BY Par ASC";
            context.GetFrame(sql).Print();
        }

        public static void RunGroupedSelects()
        {
            var myTestFrame = FormulaTestEnvironment.CreateTestFrame();
            Console.WriteLine("--- Initial Data ---");
            myTestFrame.Print();

            var context = new Dictionary<string, Frame> { { "Assets", myTestFrame } };
            //var engine = new FrameQueryEngine(context);



            // Example 1: Implicit Grouping by AssetType
            var groupedByType = context.GetFrame("SELECT AssetType, SUM(Par) as Par FROM Assets");
            groupedByType.Print();

            // Example 2: Implicit Grouping by Rating
            var groupedByRating = context.GetFrame("SELECT Rating, MAX(Par) as MaxPar FROM Assets");
            groupedByRating.Print();

            // Example 3: Multiple keys
            var multiGroup = context.GetFrame("SELECT AssetType, Rating, COUNT(Par) as Count FROM Assets");
            multiGroup.Print();
        }

        public static void RunInTests()
        {
            var assetsFrame = FormulaTestEnvironment.CreateTestFrame();
            assetsFrame.Print();


            var context = new Dictionary<string, Frame> { { "Assets", assetsFrame } };
            //var engine = new FrameQueryEngine(context);

            Console.WriteLine("--- Testing IN Clause ---");

            // 1. String set membership
            string sql1 = "SELECT AssetType, Rating FROM Assets WHERE Rating IN ('AAA', 'AA')";
            context.GetFrame(sql1).Print();

            // 2. Numeric set membership
            string sql2 = "SELECT AssetType, Par FROM Assets WHERE Par IN (1000, 75000)";
            context.GetFrame(sql2).Print();

            // 3. Assignment via IN
            assetsFrame.SetColumn("IsPriority", "Rating IN ('AAA', 'AA')");
            assetsFrame.Print();
        }


        public static void RunFormulaTests()
        {
            var frame = FormulaTestEnvironment.CreateTestFrame();
            Console.WriteLine("--- Initial Data ---");
            frame.Print();

            // Test 1: Simple Predicate (Returns Boolean)
            frame.SetColumn("IsHighValue", "Par > 100000");

            // Test 2: Logical Chain
            frame.SetColumn("IsSafeLien", "AssetType = 'First Lien' AND Rating = 'AAA'");

            // Test 3: Math on Columns
            frame.SetColumn("DoublePar", "Par * 2");

            // Test 4: Nested IF/THEN/ELSE (Returns String)
            frame.SetColumn("Category", "IF Par > 100000 THEN 'Gold' ELSE IF Par > 50000 THEN 'Silver' ELSE 'Bronze'");

            // Test 5: Boolean Constant Comparison
            frame.SetColumn("CheckLoan", "IsLoan = true");

            // Test 6: Math and Logic Combined
            frame.SetColumn("ComplexMath", "(Par + 1000) / 100.25");

            // Test 7: BETWEEN Clause
            frame.SetColumn("InMidRange", "Par BETWEEN 20000 AND 80000");

            Console.WriteLine("--- After Formula Assignments ---");
            frame.Print();
        }

        public static Frame CreateTestFrame()
        {
            Frame frame = new Frame();

            // 1. String Column (Asset Categories)
            frame.AddColumn("AssetType", new string[] {
                "First Lien", "Second Lien", "Cash", "Equity", "First Lien"
            });

            // 2. Numeric Column (Par Value) - Using double for high-speed math
            frame.AddColumn("Par", new double[] {
                150000.0, 75000.0, 1000.0, 25000.0, 200000.0
            });

            // 3. Boolean Column (Current Status)
            frame.AddColumn("IsLoan", new bool[] {
                true, true, false, false, true
            });

            // 4. DateTime Column (Maturity)
            frame.AddColumn("MaturityDate", new DateTime[] {
                new DateTime(2025, 5, 1),
                new DateTime(2026, 12, 31),
                new DateTime(2024, 1, 1),
                new DateTime(2028, 6, 15),
                new DateTime(2025, 5, 1)
            });

            // 5. Rating Column
            frame.AddColumn("Rating", new string[] {
                "AAA", "B", "NR", "AA", "AAA"
            });

            return frame;
        }
    }
}
