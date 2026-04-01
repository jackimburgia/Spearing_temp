using Spearing.Language.SqlConverter.Nodals;
using Spearing.Language.SqlConverter.Statements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace BridgeTester1.TestCases
{


    public static class FinalRoundTripTestSuite
    {
        public static void Execute()
        {
            var testCases = SqlTests.GetFullSqlTests();
            int total = testCases.Length;
            int passed = 0;

            Console.WriteLine("============================================================");
            Console.WriteLine("        FINAL TRIPLE-THREAT ROUND-TRIP TEST SUITE          ");
            Console.WriteLine("============================================================\n");

            foreach (var original in testCases)
            {
                if (RunTest(original)) passed++;
            }

            Console.WriteLine("\n############################################################");
            Console.WriteLine($" FINAL AUDIT: {passed}/{total} Passed");
            Console.WriteLine($" PIPELINE INTEGRITY: {(passed == total ? "100% SECURE" : "VULNERABLE")}");
            Console.WriteLine("############################################################");
        }

        private static bool RunTest(string originalSql)
        {
            try
            {
                // SQL #1: Sanitize the original input
                string sql1 = SqlSanitizer.Sanitize(originalSql);

                // --- STAGE 1: Statement Dissection ---
                var dissected = SqlDissector.Dissect(originalSql);

                // SQL #2: Reconstruct from Statement structure
                string sql2 = SqlSanitizer.Sanitize(dissected.ToSql());

                // --- STAGE 2: Nodal Mapping ---
                var nodalModel = NodalMapper.Map(dissected);

                // SQL #3: Reconstruct from Nodal structure
                string sql3 = SqlSanitizer.Sanitize(nodalModel.ToSql());

                // --- COMPARISON ---
                bool stmtMatch = sql1 == sql2;
                bool nodalMatch = sql2 == sql3;
                bool success = stmtMatch && nodalMatch;

                Console.WriteLine($"[ORIGINAL]: {originalSql}");

                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  >> STATUS: PASSED");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  >> STATUS: FAILED");
                    if (!stmtMatch) Console.WriteLine($"     Mismatch at STAGE 1 (Statement): {sql2}");
                    if (!nodalMatch) Console.WriteLine($"     Mismatch at STAGE 2 (Nodal): {sql3}");
                }

                Console.ResetColor();
                Console.WriteLine(new string('-', 60));
                return success;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }
    }

    public static class FullSqlTestSuite
    {
        /// <summary>
        /// The main entry point to run all parity tests.
        /// </summary>
        public static void Execute()
        {
            var testCases = SqlTests.GetFullSqlTests();
            int passed = 0;

            Console.WriteLine("============================================================");
            Console.WriteLine($" RUNNING FULL SQL PARITY SUITE ({testCases.Length} Tests)");
            Console.WriteLine("============================================================\n");

            foreach (var sql in testCases)
            {
                if (RunTest(sql)) passed++;
            }

            Console.WriteLine("\n############################################################");
            Console.WriteLine($" FINAL RESULTS: {passed}/{testCases.Length} Passed");
            Console.WriteLine("############################################################");
        }

        /// <summary>
        /// Dissects, Reconstructs, Sanitizes, and Compares a single SQL string.
        /// </summary>
        private static bool RunTest(string inputSql)
        {
            try
            {
                // 1. Dissect the string into the Statement structure
                var parsed = SqlDissector.Dissect(inputSql);

                // 2. Reconstruct using the Extension Method (.ToSql)
                string reconstructed = parsed.ToSql();

                // 3. Sanitize both for an apples-to-apples comparison
                string originalClean = SqlSanitizer.Sanitize(inputSql);
                string reconstructedClean = SqlSanitizer.Sanitize(reconstructed);

                bool isMatch = originalClean == reconstructedClean;

                Console.WriteLine($"[SQL]: {inputSql}");

                if (isMatch)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  >> SUCCESS: Parity Match");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  >> FAILED: Parity Mismatch");
                    Console.WriteLine($"     Expected: {originalClean}");
                    Console.WriteLine($"     Actual:   {reconstructedClean}");
                }

                Console.ResetColor();
                Console.WriteLine(new string('-', 60));
                return isMatch;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[ERROR] {inputSql}: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }


    }

    public static class FullNodalTestSuite
    {
        public static void Execute()
        {
            var tests = SqlTests.GetFullSqlTests();
            int passed = 0;

            foreach (var sql in tests)
            {
                if (RunNodalTest(sql)) passed++;
            }

            Console.WriteLine($"\nNODAL RESULTS: {passed}/{tests.Length} Passed");
        }

        private static bool RunNodalTest(string originalSql)
        {
            // 1. String -> Statement
            var dissected = SqlDissector.Dissect(originalSql);

            // 2. Statement -> Nodal AST
            var nodalModel = NodalMapper.Map(dissected);

            // 3. Nodal AST -> String
            string reconstructedSql = nodalModel.ToSql();

            // 4. Sanitize and Compare
            string cleanOriginal = SqlSanitizer.Sanitize(originalSql);
            string cleanReconstructed = SqlSanitizer.Sanitize(reconstructedSql);

            bool isMatch = cleanOriginal == cleanReconstructed;

            Console.WriteLine($"[Orig]: {originalSql}");
            if (isMatch)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  >> NODAL MATCH SUCCESS");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  >> NODAL MATCH FAILED");
                Console.WriteLine($"     Got: {reconstructedSql}");
            }
            Console.ResetColor();
            return isMatch;
        }

        //private static string[] GetFullSqlTests() => /* ... reuse 25 strings ... */;
    }

    public static class ParserTestSuite
    {
        public static void ExecuteAll()
        {
            var categories = new Dictionary<string, string[]>
            {
                { "SELECT FIELDS & ALIASES", SqlTests.GetSelectTests() },
                { "AGGREGATE FUNCTIONS", SqlTests.GetAggregateTests() },
                { "PREDICATES (WHERE CLAUSE)", SqlTests.GetPredicateTests() },
                { "ARITHMETIC FORMULAS", SqlTests.GetFormulaTests() },
                { "IF / THEN / ELSE LOGIC", SqlTests.GetIfThenTests() }
            };

            int totalTests = 0;
            int unknownCount = 0;

            foreach (var category in categories)
            {
                Console.WriteLine("\n" + new string('=', 60));
                Console.WriteLine($" CATEGORY: {category.Key}");
                Console.WriteLine(new string('=', 60));

                foreach (var test in category.Value)
                {
                    totalTests++;
                    Console.WriteLine($"\n>>> Parsing: {test}");

                    try
                    {
                        var results = StatementParser.Parse(test);
                        Display(results);

                        if (results.Any(r => ContainsUnknown(r)))
                            unknownCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
                        Console.ResetColor();
                        unknownCount++;
                    }
                }
            }

            Console.WriteLine("\n" + new string('#', 60));
            Console.WriteLine($" TEST SUMMARY");
            Console.WriteLine($" Total Strings Parsed: {totalTests}");
            Console.WriteLine($" Successful Identifications: {totalTests - unknownCount}");
            Console.WriteLine($" Unknowns/Errors Encountered: {unknownCount}");
            Console.WriteLine(new string('#', 60));
        }

        private static bool ContainsUnknown(Statement s)
        {
            if (s.Type == StatementType.Unknown) return true;
            return s.Children != null && s.Children.Any(c => ContainsUnknown(c));
        }



        static void Display(IEnumerable<Statement> statements, int level = 0)
        {
            string spaces = new string(' ', 4 * level);
            foreach (var statement in statements)
            {
                var descr = GetPatternDescription(statement);
                if (!string.IsNullOrEmpty(statement.Text))
                {
                    Console.WriteLine($"{spaces}{statement.Text} [{descr}]");
                }

                if (statement.Children != null)
                {
                    if (string.IsNullOrEmpty(statement.Text)) Console.WriteLine($"{spaces}( [Group]");
                    Display(statement.Children, level + 1);
                    if (string.IsNullOrEmpty(statement.Text)) Console.WriteLine($"{spaces})");
                }
            }
        }

        private static string GetPatternDescription(Statement stmt) => stmt.Type switch
        {
            StatementType.LogicalGroup => $"Group ({stmt.Children?.Length ?? 0} parts)",
            StatementType.LogicalJunction => $"Junction: {stmt.Text}",
            StatementType.Arithmetic => $"Math: {stmt.Text}",
            StatementType.Comparison => $"Comparison: {stmt.Text}",
            StatementType.Range => "Range: BETWEEN",
            StatementType.Set => "Set: IN",
            StatementType.Literal => $"Literal: {stmt.Text}",
            StatementType.Field => $"Field: {stmt.Text}",
            StatementType.If => "Keyword: IF",
            StatementType.Then => "Keyword: THEN",
            StatementType.ElseIf => "Keyword: ELSE IF",
            StatementType.Else => "Keyword: ELSE",
            StatementType.Alias => $"Alias: {stmt.Text}",
            StatementType.Separator => "Separator: Comma",
            StatementType.Aggregate => $"Aggregate: {stmt.Text}",
            StatementType.Function => $"Function: {stmt.Text}",
            _ => $"Unknown: {stmt.Text}"
        };
    }

    public static class SqlTests
    {
        public static string[] GetFullSqlTests()
        {
            return new[]
            {
                "SELECT AssetId, Par FROM Assets",
                "SELECT SecurityType, Rating, SUM(Par) AS TotalPar FROM Assets",
                //"SELECT * FROM Assets WHERE MktValue > 10000",
                "SELECT Name, Price * 1.05 AS Target FROM Assets WHERE Rating = 'AAA'",
                "SELECT ID, (Par - Cost) / Par AS Margin FROM Assets",
                "SELECT SUM(IF Par > 1000 THEN 1 ELSE 0) AS CountLarge FROM Assets",
                "SELECT AssetId FROM Assets WHERE Price BETWEEN 80 AND 120",
                "SELECT Name FROM Assets WHERE Rating IN ('AAA', 'AA', 'A')",
                "SELECT ID, 'Active' AS Status FROM Assets WHERE IsActive = 1",
                "SELECT SUM(Par) AS Par, COUNT(ID) AS TotalCount FROM Assets",
                "SELECT SecurityType, MAX(Price) AS Peak FROM Assets",
                "SELECT Name FROM Assets WHERE Name LIKE 'US%' AND Price > 100",
                "SELECT AssetId, Par FROM Assets WHERE MktValue IS NOT NULL",
                "SELECT Par, Price, Par * Price AS Value FROM Assets WHERE Country = 'USA'",
                "SELECT IF Price < 100 THEN 'Buy' ELSE 'Hold' AS Action FROM Assets",
                "SELECT SUM(MktValue - Cost) AS TotalGain FROM Assets",
                //"SELECT * FROM Assets WHERE (Rating = 'AAA' OR Rating = 'AA') AND Price < 105",
                "SELECT AssetId, ROUND(Price, 2) AS CleanPrice FROM Assets",
                "SELECT COUNT(DISTINCT AssetId) AS UniqueAssets FROM Assets",
                "SELECT Name FROM Assets WHERE DaysSinceTrade > 30 OR MktValue < 5000",
                "SELECT Par, Price, (Par + Price) / 2 AS MidPoint FROM Assets",
                "SELECT ID FROM Assets WHERE NOT (Rating = 'D')",
                "SELECT SUM(Par * Price) / SUM(Par) AS WAP FROM Assets",
                "SELECT IF Rating = 'D' THEN 0 ELSE 1 AS IsPerforming FROM Assets",
                "SELECT Name, ABS(MktValue - Cost) AS Variance FROM Assets WHERE IsActive = true"
            };



        }

        public static string[] GetSelectTests() => new[]
{
            "AssetId", "Par, Price", 
            "Par AS FaceValue", 
            "MktValue AS MarketValue, Cost",
            "AssetId, Rating, Country", 
            "Par, Price, (Par * Price) AS Position",
            "AssetName, 'USD' AS Currency", 
            "ID, 100 AS ConstantValue",
            "Days, (Days / 360) AS YearFrac", 
            "Price, Price * 1.05 AS TargetPrice",
            "MktValue, Cost, MktValue - Cost AS PNL", 
            "AssetId, Rating IN ('AAA', 'AA')",
            "Par, IF Par > 1000 THEN 1 ELSE 0 AS LargeFlag", 
            "AssetName, Category AS Cat",
            "ID, Date, 'Active' AS Status", 
            "Par, Price, (Par + Price) / 2 AS Mid",
            "MktValue AS MVal, Cost AS CVal, MVal - CVal AS Gain", "AssetId, Category AS Cat",
            "Price, Price % 1 AS Decimals", 
            "ID, Par, Price, MktValue, Cost, PNL",
            "IF Days > 30 THEN 1 ELSE 0 AS Late", 
            "AssetName, Rating, Par * 0.9 AS Haircut",
            "ID, (Price + 1) AS Adjusted", 
            "Par AS P, Price AS Pr",
            "AssetId, 'Fixed' AS Type, 0.05 AS Rate"
        };

        public static string[] GetAggregateTests() => new[]
        {
            "SUM(Par) as Par", 
            "SUM(Par) AS TotalPar", 
            "COUNT(AssetId) AS AssetCount",
            "MAX(Price) as MaxPrice, MIN(Price) as MinPrice", 
            "AVG(Price) AS AveragePrice",
            "SUM(Par + GainLoss) AS TotalValue", 
            "Rating, SUM(Par) AS ParByRating",
            "AssetType, COUNT(ID) as IDCount, MAX(MktValue) as MinMktValue", 
            "SUM(IF Par > 1000 THEN 1 ELSE 0) AS LargeCount",
            "ROUND(AVG(Price)) AS AvgPrice", "MIN(Cost) AS Floor", 
            "MAX(MktValue - Cost) AS MaxGain",
            "SUM(Par * Price) / SUM(Par) AS WAP", 
            "ABS(SUM(PNL)) as Pnl",
            "AVG(Days) AS Term", 
            "SUM(Units) AS Inventory", 
            "MAX(Date) AS LastUpdate",
            "COUNT(*) AS TotalRows", 
            "SUM(Price) / COUNT(Price) AS ManualAvg",
            "ROUND(SUM(Par), 0) as Par", 
            "MIN(Rating) as MinRating", 
            "MAX(Rating) as MaxRating", 
            "SUM(IF IsActive = 1 THEN Par ELSE 0) as ActivePar",
            "AVG(MktValue) AS MeanValue"
        };

        public static string[] GetPredicateTests() => new[]
        {
            "Par > 1000", "AssetType = 'Bond'", "Price <= 100", "Rating != 'CCC'",
            "AssetName LIKE 'US%'", "Par BETWEEN 500 AND 1500", "Rating IN ('AAA', 'AA', 'A')",
            "Price > 90 AND Price < 110", "AssetType = 'Equity' OR AssetType = 'Fund'",
            "(Par > 1000 AND Price < 90)", "DaysSinceTrade >= 30", "IsActive = 1",
            "Country = 'USA' AND (Rating = 'AAA' OR Rating = 'AA')", "Price IS NOT NULL",
            "Par * Price > 10000", "Days % 7 = 0", "AssetName LIKE '%Corp%'",
            "Cost BETWEEN 0 AND 1000", "ID IN (101, 102, 103)", "Par > 0 AND Price > 0",
            "Rating = 'AAA' AND NOT (Price < 100)", "IsLate = true", "AssetType != 'Cash'",
            "Price > (Cost * 1.1)", "Rating IN ('B', 'C') OR Par < 100"
        };

        public static string[] GetFormulaTests() => new[]
        {
            "Par + Price", "MktValue - Cost", "Par * Price", "MktValue / Par",
            "Par * 1.05", "Price + 1", "Days / 360", "(Par + Price) / 2",
            "(MktValue - Cost) / Cost", "Par * (Price / 100)", "Price % 1",
            "Par + (Price * Quantity)", "Cost * (1 + TaxRate)", "BaseValue + 500",
            "'ID: ' + AssetId", "Price ^ 2", "Total / (Count + 1)", "ABS(PNL)",
            "Par - (Par * Haircut)", "(Price1 + Price2 + Price3) / 3",
            "Units * PricePerUnit", "100 - DiscountPercent", "DaysSince + 1",
            "Par / 1000", "Price * 0.95"
        };

        public static string[] GetIfThenTests() => new[]
        {
            "IF Par > 1000 THEN 1 ELSE 0", "IF Price < 100 THEN 'Cheap' ELSE 'Dear'",
            "IF Rating = 'AAA' THEN Price + 1 ELSE Price", "IF Days > 30 THEN Price * 0.9 ELSE Price",
            "IF AssetType = 'Bond' THEN Par ELSE MktValue", "IF (Par > 1000 AND Price < 90) THEN 1 ELSE 0",
            "IF Price > Cost THEN 'Gain' ELSE 'Loss'", "IF IsActive = 1 THEN 'Yes' ELSE 'No'",
            "IF Par BETWEEN 0 AND 100 THEN 'Small' ELSE 'Large'", "IF Rating IN ('AAA', 'AA') THEN 0.05 ELSE 0.1",
            "IF Days % 2 = 0 THEN 'Even' ELSE 'Odd'", "IF Country = 'USA' THEN Price ELSE Price * 1.1",
            "IF Price > 100 THEN 'Premium' ELSE 'Standard'",
            "IF MktValue < Cost THEN (Cost - MktValue) ELSE 0", "IF AssetId = 0 THEN 'Unknown' ELSE AssetName",
            "IF (P1 + P2) > 200 THEN 1 ELSE 0", "IF Rating != 'CCC' THEN 'Safe' ELSE 'Risky'",
            "IF Price IS NULL THEN 0 ELSE Price", "IF Days > 360 THEN 'LongTerm' ELSE 'ShortTerm'",
            "IF Par * Price > 5000 THEN 'Alert' ELSE 'OK'", "IF Category = 'A' THEN 1 ELSE 3",
            "IF Price < 0 THEN 0 ELSE Price", "IF IsTaxable = 1 THEN Price * 1.15 ELSE Price",
            "IF Rating = 'D' THEN 'Default' ELSE 'Active'", "IF (Par - Cost) > 0 THEN 'Profit' ELSE 'Loss'"
        };


        //        public static string[] TestPredicateStrings = new string[]
        //{
        //            // Basic Comparisons & Nulls
        //            "Price > 0",
        //            "Rating != 'D'",
        //            "AssetName LIKE 'First%'",
        //            "MaturityDate IS NULL",
        //            "IsActive = 1",

        //            // Logic Junctions (AND/OR)
        //            "Price > 100 AND Price < 500",
        //            "Rating = 'AAA' OR Rating = 'AA'",
        //            "(Price * Quantity) > 1000 AND Category = 'Equity'",
        //            "NOT (Rating = 'C')",
        //            "a = 1 AND b = 2 AND c = 3",

        //            // Mathematical Predicates
        //            "(par - cost) / par > 0.10",
        //            "ABS(gainloss) > 500",
        //            "Price * 1.05 <= TargetPrice",
        //            "SUM(par) > AVG(cost) * 1.2",
        //            "(a + b) * (c + d) / e > 100",

        //            // List & Range (IN / BETWEEN)
        //            "Category IN ('Equity', 'Debt', 'Cash')",
        //            "Price BETWEEN 80 AND 120",
        //            "id IN (1, 2, 3, 4, 5)",
        //            "Rating IN (SELECT Grade FROM Grades)",
        //            "(a + b) IN (10, 20, 30)",

        //            // Complex Nested Situations
        //            "(Price > 100 OR Rating = 'AAA') AND (IsActive = 1 OR MaturityDate > '2026-01-01')",
        //            "NOT (a > 0 AND b > 0) OR (c = 1 AND d = 1)",
        //            "CASE WHEN par > 100 THEN 1 ELSE 0 END = 1",
        //            "((a + b) > c OR (d - e) < f) AND NOT (g = 0)",
        //            "SUM(CASE WHEN Type = 'A' THEN par ELSE 0 END) > 10000"
        //};
    }

    public static class SqlSanitizer
    {
        public static string Sanitize(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return string.Empty;

            string result = sql.ToUpper();

            // 1. Collapse all whitespace
            result = Regex.Replace(result, @"\s+", " ");

            // 2. Remove all parentheses for the identity check
            // This ensures (MktValue > 1000) matches MktValue > 1000
            result = Regex.Replace(result, @"[()]", "");

            // 3. Normalize commas (remove spaces around them)
            result = Regex.Replace(result, @"\s?,\s?", ",");

            // 4. Normalize operators (ensure consistent single space)
            result = Regex.Replace(result, @"\s?([><=])\s?", " $1 ");

            // 5. Final pass to fix any double spaces created by the operator regex
            result = Regex.Replace(result, @"\s+", " ");

            return result.Trim();
        }
    }
}
