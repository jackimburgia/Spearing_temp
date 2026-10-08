using BridgeTester1.NewStructure2;
using Microsoft.Extensions.DependencyInjection;
using Spearing.Data.Frames;
using Spearing.Entities.ComplianceEntities;
using Spearing.Language.SqlConverter.Statements;
using System.Collections;
using System.Diagnostics.SymbolStore;
using System.Net.Http.Headers;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace BridgeTester1
{

    public class ComplianceModel
    {
        public Dictionary<string, Frame> Database { get; set; } = new Dictionary<string, Frame>();



        public ModelBuild[] ModelBuilds { get; set; }

        // references (dependencies?)
        public Dictionary<(string, string), BuildResult> References { get; set; } = new Dictionary<(string, string), BuildResult>();
    }

    public enum BuildStatus
    {
        Success,
        Fail
    }

    public class BuildResult
    {
        public ModelBuild Build { get; set; }
        //public string ExcelFormula { get; set; }
        public SqlQueryStatement SqlQueryStatement { get; set; }
        public WhereStatement WhereStatement { get; set; }
    }

    public class LoadTester
    {
        public void Run()
        {
            ComplianceModel complianceModel = new ComplianceModel();

            ModelBuild[] modelBuilds =
            {
                new ModelBuild()
                {
                    BuildType = ModelBuildTypes.LoadDatasetFromCsvFile,
                    DatasetType = DatasetTypes.SingleRow,
                    DatasetName = "Deal",
                    BuildText = @"C:\Users\jack\Desktop\Spearing\Compliance\Data\Deal.csv"
                },
                new ModelBuild()
                {
                    BuildType = ModelBuildTypes.LoadDatasetFromCsvFile,
                    DatasetType = DatasetTypes.MultipleRow,
                    DatasetName = "Assets",
                    BuildText = @"C:\Users\jack\Desktop\Spearing\Compliance\Data\Assets.csv"
                },
                // new property Assets.IsEligible references [Assets.MoodysRating]
                new ModelBuild()
                {
                    BuildType = ModelBuildTypes.SetDatasetColumn,
                    DatasetType = DatasetTypes.NA,
                    DatasetName = "Assets",
                    PropertyName = "IsEligible",
                    BuildText = "IF MoodysRating = 'Default' THEN False ELSE True"
                },
                new ModelBuild()
                {
                    BuildType = ModelBuildTypes.SetScalar,
                    DatasetType = DatasetTypes.NA,
                    DatasetName = "Deal",
                    PropertyName = "Principal",
                    BuildText = "SELECT SUM(Principal) FROM Assets"
                }
            };

            complianceModel.ModelBuilds = modelBuilds;


            foreach (var build in modelBuilds)
            {
                if (build.BuildType == ModelBuildTypes.LoadDatasetFromCsvFile)
                {
                    var fr = FrameIO.ReadCsv(build.BuildText);
                    complianceModel.Database.Add(build.DatasetName, fr);
                }
                else if (build.BuildType == ModelBuildTypes.SetDatasetColumn)
                {
                    var fr = complianceModel.Database[build.DatasetName];

                    (Column column, WhereStatement where) = fr.ToColumnAll(build.BuildText);

                    fr[build.PropertyName] = column;

                    BuildResult result = new BuildResult() { Build = build, WhereStatement = where };
                    complianceModel.References.Add((build.DatasetName, build.PropertyName), result);
                }
                else if (build.BuildType == ModelBuildTypes.SetScalar)
                {
                    var fr = complianceModel.Database[build.DatasetName];

                    (Column column, SqlQueryStatement query) = complianceModel.Database.ExecuteScalarAll(build.BuildText);

                    fr[build.PropertyName] = column;

                    BuildResult result = new BuildResult() { Build = build, SqlQueryStatement = query };
                    complianceModel.References.Add((build.DatasetName, build.PropertyName), result);
                }
            }


            SpreadsheetBuild[] spreadsheetBuilds =
            {
                // put dataset (multiple columns / rows) on sheet
                new SpreadsheetBuild()
                {
                    BuildType = SpreadsheetBuildTypes.Dataset,
                    SheetName = "Data",
                    CellReference = "B4",
                    DatasetName = "Assets"
                },

                // put single cell values on sheet
                new SpreadsheetBuild()
                {
                    BuildType = SpreadsheetBuildTypes.SingleCell,
                    SheetName = "Deal",
                    CellReference = "A1",
                    DatasetName = "Deal",
                    PropertyName = "DealID"
                },
                new SpreadsheetBuild()
                {
                    BuildType = SpreadsheetBuildTypes.SingleCell,
                    SheetName = "Deal",
                    CellReference = "A2",
                    DatasetName = "Deal",
                    PropertyName = "DealName"
                },
                new SpreadsheetBuild()
                {
                    BuildType = SpreadsheetBuildTypes.SingleCell,
                    SheetName = "Deal",
                    CellReference = "A3",
                    DatasetName = "Deal",
                    PropertyName = "Principal"
                }
            };

            Console.WriteLine($"DATASETS");
            Console.WriteLine("------------------------------------------------------------------------------------");
            Console.WriteLine();

            foreach (var ds in complianceModel.Database)
            {
                ds.Value.Print();
                //Console.WriteLine("------------------------------------------------------------------------------------");
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("SPREADSHEET BUILD OUT");
            Console.WriteLine("------------------------------------------------------------------------------------");


            foreach (var spreadsheetBuild in spreadsheetBuilds)
            {
                if (spreadsheetBuild.BuildType == SpreadsheetBuildTypes.Dataset)
                {
                    var dataset = complianceModel.Database[spreadsheetBuild.DatasetName];

                    Console.WriteLine($"{spreadsheetBuild.SheetName}!{spreadsheetBuild.CellReference} = DATASET -> {spreadsheetBuild.DatasetName}");
                    foreach(var column in dataset.ColumnNames)
                    {
                        // check if column is a "formula"
                        if (complianceModel.References.ContainsKey((spreadsheetBuild.DatasetName, column)))
                        {
                            var modelBuild = complianceModel.References[(spreadsheetBuild.DatasetName, column)];
                            Console.WriteLine($"    {column} = FORMULA -> {modelBuild.Build.BuildText}");
                        }
                        else
                        {
                            Console.WriteLine($"    {column} = VALUE");
                        }

                    }
                }
                else if (spreadsheetBuild.BuildType == SpreadsheetBuildTypes.SingleCell)
                {
                    // check if column is a "formula"

                    if (complianceModel.References.ContainsKey((spreadsheetBuild.DatasetName, spreadsheetBuild.PropertyName)))
                    {
                        //var reference = complianceModel.References[(spreadsheetBuild.DatasetName, spreadsheetBuild.PropertyName)];
                        // formula - convert to Excel formula
                        var buildResult = complianceModel.References[(spreadsheetBuild.DatasetName, spreadsheetBuild.PropertyName)];
                        Console.WriteLine($"{spreadsheetBuild.SheetName}!{spreadsheetBuild.CellReference} = FORMULA -> {buildResult.Build.BuildText}");

                        if (buildResult.SqlQueryStatement != null && buildResult.SqlQueryStatement.Select.Projections.Count == 1)
                        {
                            var projection = buildResult.SqlQueryStatement.Select.Projections[0];
                            Console.WriteLine(projection.Expression.GetType());

                            if (projection.Expression is AggregateExpr agg)
                            {
                                // Sum, Max, Min, Avg, Count
                                Console.WriteLine(agg.Func);

                                bool hasWhere = buildResult.WhereStatement != null;
                                Console.WriteLine($"Has Where = {hasWhere}");

                                if (hasWhere)
                                {
                                    // the only support AND conditions
                                    // SUMIF or SUMIFS
                                    // MAXIFS
                                    // MINIFS
                                    // AVERAGEIF or AVERAGEIFS
                                    // COUNTIF or COUNTIFS

                                    // use SUMPRODUCT for OR conditions

                                    Console.WriteLine($"AGGREGATE with WHERE");
                                }
                                else
                                {
                                    //=SUM(H2:H4)
                                    //=MAX(H2:H4)
                                    //=MIN(H2:H4)
                                    //=AVERAGE(H2:H4)
                                    //=COUNT(H2:H4)

                                    // get the range for table
                                    Console.WriteLine($"Table = {buildResult.SqlQueryStatement.From}");
                                    //var ds = complianceModel.
                                }
                            }
                            else
                            {
                                Console.WriteLine("UNDEFINED EXPRESSION!!!");
                            }
                        }
                        else
                        {
                            Console.WriteLine("UNDEFINED!!!");
                        }
                    }
                    else
                    {
                        // not a formula; use value direcly from the frame
                        var fr = complianceModel.Database[spreadsheetBuild.DatasetName];
                        var column = fr[spreadsheetBuild.PropertyName];
                        var value = column.Buffer.GetValue(0);

                        Console.WriteLine($"{spreadsheetBuild.SheetName}!{spreadsheetBuild.CellReference} = VALUE -> {value}");
                    }
                }
            }

            // create the spreadsheet
            //  - put all datasets first
            //      - Dataset: Assets -> add each colun; check if that column is formula
            //          - Sheet: Data
            //          - Cell: B4
            //              - Column Formula: IsEligible -> IF MoodysRating = 'Default' THEN False ELSE True | =IF(G5="Default", FALSE,TRUE)
            //                     - MultipleRow Dataset column reference: Assets.IsEligible references [Assets.MoodysRating]
            //  - fill in Deal sheet
            //      - Sheet: Deal
            //      - Cell: A1
            //      - Dataset: Deal
            //      - Property: DealID
            //
            //      - Sheet: Deal
            //      - Cell: A2
            //      - Dataset: Deal
            //      - Property: DealName
            //
            //      - Sheet: Deal -> check if formula is a column
            //      - Cell: A3
            //      - Dataset: Deal
            //      - Property: Principal
            //      - Column Formula: SELECT SUM(Principal) FROM Assets | =SUM(Data!F5:F9)
            //          - SingleRow Dataset column reference: Deal.Principal references [Assets.Principal]


            Console.WriteLine("Done!");
        }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDependencies(this IServiceCollection services)
        {
            services.AddTransient<Program>();
            services.AddTransient<LoadTester>();

            return services;
        }
    }

    public static class ExcelMapper
    {
        public static string GetCellAddress(string startAddress, Frame frame, string columnName, int rowNumber)
        {
            // 1. Parse the starting coordinate (e.g., "B4" -> col 2, row 4)
            var (startCol, startRow) = ParseAddress(startAddress);

            // 2. Locate the column index within the Frame
            var columnKeys = frame.ColumnNames.ToList();
            int colOffset = columnKeys.IndexOf(columnName);

            if (colOffset == -1)
                throw new ArgumentException($"Column '{columnName}' not found.");

            // 3. Validate the rowNumber exists in the data
            int dataRowCount = frame[columnName].Buffer.Length;
            if (rowNumber < 1 || rowNumber > dataRowCount)
                throw new ArgumentOutOfRangeException(nameof(rowNumber), "Row number must be between 1 and the total data rows.");

            // 4. Calculate coordinates
            // Column: Start + Offset
            // Row: StartRow (Header) + rowNumber (Data)
            string colLetter = GetExcelColumnName(startCol + colOffset);
            int targetRow = startRow + rowNumber;

            return $"{colLetter}{targetRow}";
        }

        public static string GetColumnAddress(string startAddress, Frame frame, string columnName, bool includeHeader = true)
        {
            // 1. Parse the starting coordinate (e.g., "B4" -> col 2, row 4)
            var (startCol, startRow) = ParseAddress(startAddress);

            // 2. Locate the column index within the Frame
            // Using a list to ensure we match the dictionary's insertion order
            var columnKeys = frame.ColumnNames.ToList();
            //var columnKeys = frame.Columns.Keys.ToList();
            int colOffset = columnKeys.IndexOf(columnName);

            if (colOffset == -1)
                throw new ArgumentException($"Column '{columnName}' not found in Frame.");

            // 3. Calculate the target column letter
            int targetColIndex = startCol + colOffset;
            string colLetter = GetExcelColumnName(targetColIndex);

            // 4. Calculate row boundaries
            int dataRowCount = frame[columnName].Buffer.Length;

            // If includeHeader is true: Start at startRow, End at startRow + data
            // If includeHeader is false: Start at startRow + 1, End at startRow + data
            int finalStartRow = includeHeader ? startRow : startRow + 1;
            int finalEndRow = startRow + dataRowCount;

            return $"{colLetter}{finalStartRow}:{colLetter}{finalEndRow}";
        }

        public static string GetFrameAddress(string startAddress, Frame frame)
        {
            var (startCol, startRow) = ParseAddress(startAddress);

            var columns = frame.ColumnNames.ToList();

            int columnCount = columns.Count;
            // Rows = data rows + 1 for the header
            int rowCount = frame[columns.First()].Buffer.Length + 1;
            //int rowCount = frame.Columns.Values.First().Length + 1;

            int endCol = startCol + columnCount - 1;
            int endRow = startRow + rowCount - 1;

            return $"{startAddress}:{GetExcelColumnName(endCol)}{endRow}";
        }

        private static (int col, int row) ParseAddress(string address)
        {
            var match = Regex.Match(address.ToUpper(), @"([A-Z]+)(\d+)");
            if (!match.Success) throw new FormatException("Invalid Excel address format.");

            string colStr = match.Groups[1].Value;
            int row = int.Parse(match.Groups[2].Value);

            int col = 0;
            foreach (char c in colStr)
            {
                col = col * 26 + (c - 'A' + 1);
            }
            return (col, row);
        }

        private static string GetExcelColumnName(int columnNumber)
        {
            string columnName = string.Empty;
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnName = Convert.ToChar(65 + modulo) + columnName;
                columnNumber = (columnNumber - modulo) / 26;
            }
            return columnName;
        }
    }

    public class ExcelWorkbook
    {
        public Dictionary<string, ExcelWorksheet> Sheets { get; protected set; } = new Dictionary<string, ExcelWorksheet>();
    }

    public class ExcelWorksheet
    {
        public string SheetName { get; set; }
        public ExcelWorkbook Workbook { get; set; }

        public Frame Cells { get; set; } = new Frame();
    }

    public class ExcelCell
    {

    }

    public static class ExcelExtensions
    {
         

        public static ExcelWorksheet AddFrame(this ExcelWorksheet sheet, string frameName, string address)
        {


            return sheet;
        }
    }

    internal class Program
    {
        protected LoadTester loadTester;

        public Program(LoadTester loadTester)
        {
            this.loadTester = loadTester;
        }


        #region Utilties
        private static readonly JsonSerializerOptions MinifyOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter() },
            WriteIndented = true
        };



        static void ShowWhereJson(string s)
        {
            Console.WriteLine();
            var p = new SqlParser(s);
            WhereStatement res = p.ParseWhere();
            string actualJson = JsonSerializer.Serialize(res, MinifyOptions);

            Console.Write(actualJson);

            Console.WriteLine("----------------------------------------------");
            Console.WriteLine();
        }

        static void ShowJson(object o)
        {
            Console.WriteLine();
            string actualJson = JsonSerializer.Serialize(o, MinifyOptions);

            Console.Write(actualJson);

            Console.WriteLine("----------------------------------------------");
            Console.WriteLine();
        }

        static void ShowQueryJson(string s)
        {
            Console.WriteLine();
            var p = new SqlParser(s);
            SqlQueryStatement res = p.ParseQuery();
            string actualJson = JsonSerializer.Serialize(res, MinifyOptions);

            Console.Write(actualJson);

            Console.WriteLine("----------------------------------------------");
            Console.WriteLine();
        }

        static void ShowDataTypes(Frame fr, string title)
        {
            Console.WriteLine();

            Console.WriteLine($"{title}:");
            Console.WriteLine("----------------------------------------------");



            foreach (var column in fr.ColumnNames)
            {
                Console.WriteLine($"{column}: {fr[column].Buffer.ElementType}");
            }

            Console.WriteLine();

        }
        #endregion


        static void SetSingleValue(Dictionary<string, Frame> database, string destTable, string destProperty, string sql)
        {
            var fr = database[destTable];

            fr[destProperty] = database.ExecuteScalar(sql);
        }


        static void SetColumnValues(Dictionary<string, Frame> database, string destTable, string destProperty, string sql)
        {
            var fr = database[destTable];

            fr[destProperty] = fr.ToColumn(sql);
        }




        static void DisectSelects()
        {
            #region Paths / data
            string root = @"C:\Users\jack\Desktop\Spearing\Compliance\Data\";
            string dealPath = Path.Combine(root, "Deal.csv");
            string assetsPath = Path.Combine(root, "Assets.csv");

            Frame deal = FrameIO.ReadCsv(dealPath);
            Frame assets = FrameIO.ReadCsv(assetsPath);

            var database = new Dictionary<string, Frame> {
                { "Deal", deal } ,
                { "Assets", assets }
            };

            #endregion

            #region Address testing
            //string assetsAddress = ExcelMapper.GetFrameAddress("B4", assets);
            //string principalColumnAddress = ExcelMapper.GetColumnAddress("B4", assets, "Principal");
            //string principalColumnAddressData = ExcelMapper.GetColumnAddress("B4", assets, "Principal", false);

            //Console.WriteLine($"Assets address: {assetsAddress}");
            //Console.WriteLine($"Principal column address (with header): {principalColumnAddress}");
            //Console.WriteLine($"Principal column address (data only): {principalColumnAddressData}");

            //for (int x = 0; x < assets.RowCount; x++)
            //{
            //    string cellAddress = ExcelMapper.GetCellAddress("B4", assets, "Principal", x + 1);
            //    Console.WriteLine($"Row {x + 1}: {cellAddress}");
            //}


            //return;
            #endregion



            // Create the workbook
            ExcelWorkbook wb = new ExcelWorkbook();
            ExcelWorksheet dealSheet = new ExcelWorksheet() { SheetName = "Deal", Workbook = wb };
            ExcelWorksheet dataSheet = new ExcelWorksheet() { SheetName = "Data", Workbook = wb };

            wb.Sheets.Add(dealSheet.SheetName, dealSheet);
            wb.Sheets.Add(dataSheet.SheetName, dataSheet);



            //dealSheet.AddFrame(deal, "Deal", "A1");
            //dataSheet.AddFrame(assets, "Assets", "A1");


            return;


            //var instruction = "SELECT AssetId, SUM(Principal + 2) as Principal , if Principal > 100 then 'yes' else 'no' as Tester FROM Assets";



            //var instruction = "SELECT SUM(Principal) FROM Assets where Principal > 0";
            var instruction = "SELECT SUM(Principal) FROM Assets";
            //var instruction = "SELECT SUM(Principal) FROM Assets where Principal > 0 and Industry = 'Healthcare'";
            //var instruction = "SELECT Principal FROM Assets";
            //var instruction = "SELECT 'hello' as Name FROM Assets";
            //var instruction = "SELECT (Principal * 2) as Calc FROM Assets";
            //var instruction = "SELECT if Principal > 100 then 'yes' else 'no' as Tester FROM Assets";

            SqlQueryStatement query = new SqlParser(instruction).ParseQuery();


            Console.WriteLine(instruction);

            if (query.Select.Projections.Count == 0)
            {
                Console.Write("No fields defined!!!");
            }
            else if (query.Select.Projections.Count == 1)
            {
                Console.WriteLine("1 field defined");
                Projection projection = query.Select.Projections[0];
                Console.WriteLine(projection.Expression.GetType());

                if (projection.Expression is IdentifierExpr id)
                {

                }
                else if (projection.Expression is AggregateExpr agg)
                {
                    // Sum, Max, Min, Avg, Count
                    Console.WriteLine(agg.Func);

                    bool hasWhere = query.Where != null;
                    Console.WriteLine($"Has Where = {hasWhere}");


                    if (hasWhere)
                    {
                        // the only support AND conditions
                        // SUMIF or SUMIFS
                        // MAXIFS
                        // MINIFS
                        // AVERAGEIF or AVERAGEIFS
                        // COUNTIF or COUNTIFS

                        // use SUMPRODUCT for OR conditions

                        //Console.WriteLine($"Where count = {query.Where.}");
                    }
                    else
                    {
                        //=SUM(H2:H4)
                        //=MAX(H2:H4)
                        //=MIN(H2:H4)
                        //=AVERAGE(H2:H4)
                        //=COUNT(H2:H4)

                        // get the range for table
                    }

                }
                else if (projection.Expression is LiteralExpr lit)
                {

                }
                else if (projection.Expression is BinaryExpr bin)
                {

                }
                else if (projection.Expression is ConditionalExpr cond)
                {

                }
                else
                {
                    throw new Exception("This single projection not coded for");
                }
                // IdentifierExpr
                // AggregateExpr
                // LiteralExpr
                // BinaryExpr
                // ConditionalExpr
            }
            else
            {
                Console.WriteLine("Multiple fields - new dataset");
            }

            Console.Write($"Projections Count = {query.Select.Projections.Count}");
            Console.WriteLine();

            ShowJson(query);


        }


        public void Run()
        {
            this.loadTester.Run();

        }




        static void Main(string[] args)
        {
            var serviceProvider = new ServiceCollection()
                .AddTransient<Program>()
                .AddTransient<LoadTester>()
                .BuildServiceProvider();

            var program = serviceProvider.GetService<Program>();

            program.Run();

            return;

            //TestRunner.Run();

            //DisectSelects();

            //return;



            return;

            #region Paths / data
            string root = @"C:\Users\jack\Desktop\Spearing\Compliance\Data\";
            string dealPath = Path.Combine(root, "Deal.csv");
            string assetsPath = Path.Combine(root, "Assets.csv");
            string ratingAdvanceRatesPath = Path.Combine(root, "RatingAdvanceRates.csv");
            string seniorityCapsPath = Path.Combine(root, "SeniorityCaps.csv");

            Frame deal = FrameIO.ReadCsv(dealPath);
            Frame assets = FrameIO.ReadCsv(assetsPath);
            Frame ratingAdvanceRates = FrameIO.ReadCsv(ratingAdvanceRatesPath);
            Frame seniorityCaps = FrameIO.ReadCsv(seniorityCapsPath);

            var database = new Dictionary<string, Frame> { 
                { "Deal", deal } ,
                { "Assets", assets },
                { "RatingAdvanceRates", ratingAdvanceRates },
                { "SeniorityCaps", seniorityCaps },
            };

            #endregion


            ExcelWorkbook wb = new ExcelWorkbook();
            ExcelWorksheet dataSheet = new ExcelWorksheet() { SheetName = "Data" };

            // Translate this into an Excel formula

            var instruction = "SELECT SUM(Principal) FROM Assets";

            //ShowQueryJson(instruction);
            SqlQueryStatement query = new SqlParser(instruction).ParseQuery();
            ShowJson(query);

            string tableName = query.From;
            var selectField = query.Select.Projections[0].Expression;

            // create a dependency graph

            // Deal.Principal points to 'SELECT SUM(Principal) FROM Assets'
            // FROM.Assets points to Database.Assets
            // Database.Assets points to Excel.Data!A1:G6
            // Database.Assets.Principal points to Excel.Data!E2:E6
            // =SUM(Data!E2:E6)
            SetSingleValue(database, "Deal", "Principal", instruction);

            //assets.SetColumn("Total", "Principal * 2");
            //assets["Total"] = assets.ToColumn("Principal * 2");
            SetColumnValues(database, "Assets", "Total", "Principal * 2");

            // Deal.Principal depends on the Asset.Principal column data


            //deal["Principal"] = FrameQueryUtil.ExecuteScalar(
            //    instruction,
            //    database,
            //    deal.RowCount
            //);

            deal.Print();

            assets.Print();


            // load Assets data
            // load Deal data
            // set Deal.Principal = SELECT SUM(Principal) FROM Assets
            //  - Deal.Principal points to Assets.Principal range

            // add Assets data to Excel worksheet "Data" cell A1 (7 columns, 6 rows(1h, 5d))
            //  - AssetID - A1 Header, A2:A6 data
            //  - Principal - E1 Header, E2:E6 data
            // add Deals data to Excel worksheet "Deal"
            //  - Deal.DealID -> A1 - no pointer; use stored value
            //  - Deal.DealName -> A2 - no pointer; use stored value
            //  - Deal.Principal -> A3 - computed column points to Assets.Principal[Data] = E2:E6

            return;

            //BridgeTester1.NewStructure2.TestRunner.Run();
            //return;



            //string s = "IF MoodysRating = 'Default' THEN False ELSE True";
            //string s = "IF (MoodysRating = 'Default') THEN False ELSE True";

            //ShowJson("IF MoodysRating = 'Default' THEN False ELSE True");
            //ShowJson("IF (MoodysRating = 'Default') THEN False ELSE True");
            //ShowJson("IF (MoodysRating = 'Default' or AssetType = 'First Lien' and InterestType like 'PIK%') THEN False ELSE True");

            //ShowJson("InterestType like 'PIK%'");

            //return;







            //assets.SetColumn("IsEligible", "IF (MoodysRating = 'Default' OR InterestType like 'PIK%') THEN False ELSE True");
            assets["IsEligible"] = assets.ToColumn("IF (MoodysRating = 'Default' OR InterestType like 'PIK%') THEN False ELSE True");

            assets["RatingAR"] = assets.Map("MoodysRating", ratingAdvanceRates, "MoodysRating", "AdvanceRate", null);
            
            assets["SeniorityCap"] = assets.Map("Seniority", seniorityCaps, "Seniority", "Cap", null);

            //assets.SetColumn("AppliedAR", "IF IsEligible = True THEN MIN(RatingAR, SeniorityCap) ELSE 0");
            assets["AppliedAR"] = assets.ToColumn("IF IsEligible = True THEN MIN(RatingAR, SeniorityCap) ELSE 0");

            //assets.SetColumn("GrossBBContribution", "IF IsEligible = True THEN AppliedAR * Principal ELSE 0");
            assets["GrossBBContribution"] = assets.ToColumn("IF IsEligible = True THEN AppliedAR * Principal ELSE 0");

            deal["GrossBBContribution"] = database.ExecuteScalar("SELECT SUM(GrossBBContribution) FROM Assets WHERE IsEligible = True");

            deal["EligiblePrincipal"] = database.ExecuteScalar("SELECT SUM(Principal) FROM Assets WHERE IsEligible = True");

            //deal.SetColumn("IndustryLimit", "EligiblePrincipal * MaxIndustry");
            deal["IndustryLimit"] = deal.ToColumn("EligiblePrincipal * MaxIndustry");


            //var engine = new FrameQueryEngine(database);

            var industryConcentration = database.GetFrame("SELECT Industry, SUM(GrossBBContribution) as GrossBBContrib FROM Assets WHERE IsEligible = True");

            database.Add("IndustryConcentration", industryConcentration);


            industryConcentration["Limit"] = database.ExecuteScalar("SELECT IndustryLimit FROM Deal");

            //industryConcentration.SetColumn("ConcentrationExcess", "IF GrossBBContrib > Limit THEN Limit - GrossBBContrib ELSE 0");
            industryConcentration["ConcentrationExcess"] = industryConcentration.ToColumn("IF GrossBBContrib > Limit THEN Limit - GrossBBContrib ELSE 0");

            deal["ConcentrationExcess"] = database.ExecuteScalar("SELECT SUM(ConcentrationExcess) FROM IndustryConcentration");


            //deal.SetColumn("NetBorrowingBase", "GrossBBContribution + ConcentrationExcess");
            deal["NetBorrowingBase"] = deal.ToColumn("GrossBBContribution + ConcentrationExcess");


            deal.Print();
            assets.Print();
            ratingAdvanceRates.Print();
            seniorityCaps.Print();

            industryConcentration.Print();



            ShowDataTypes(deal, "DEAL");
            ShowDataTypes(assets, "ASSETS");
            ShowDataTypes(ratingAdvanceRates, "RATING_ADVANCE_RATES");
            ShowDataTypes(seniorityCaps, "SENIORTY_CAPS");
            ShowDataTypes(industryConcentration, "INDUSTRY_CONCENTRATION");





            // Tests
            //BridgeTester1.NewStructure2.TestRunner.Run();


            //FormulaTestEnvironment.RunBasicSelects();
            //FormulaTestEnvironment.RunGroupedSelects();
            //FormulaTestEnvironment.RunFormulaTests();
            //FormulaTestEnvironment.RunInTests();


            //FormulaTestEnvironment.RunReadCSVTests();
            //FormulaTestEnvironment.RunMappingTests();
            //FormulaTestEnvironment.RunScalarTests();



        }


        #region OLD
        public static void TestDataEngine() {
            Console.WriteLine("=== Spearing Data Engine Test ===\n");

            // 1. Setup Source Data
            // We use different types to test the dynamic FieldNode resolution
            using var df = new Frame();
            df.AddColumn("ID", new int[] { 1, 2, 3, 4, 5 });
            df.AddColumn("Product", new string[] { "CPU", "GPU", "RAM", "SSD", "PSU" });
            df.AddColumn("BasePrice", new double[] { 300.0, 800.0, 150.0, 100.0, 80.0 });
            df.AddColumn("Quantity", new int[] { 10, 5, 20, 15, 0 }); // Note: Quantity is 'int'
            df.AddColumn("Status", new string[] { "InStock", "InStock", "InStock", "InStock", "OutOfStock" });

            Console.WriteLine("Source Frame:");
            df.Print();

            // 2. Define the SQL Query
            // Tests: 
            // - Multi-column selection
            // - Math with promotion (int Quantity * double 0.9)
            // - String comparison (Status = 'InStock')
            // - Numeric comparison (BasePrice > 100)
            // - Logical AND
            string sql = @"
                SELECT 
                    ID, 
                    Product, 
                    BasePrice * 1.1 AS PriceWithTax, 
                    Quantity * 0.9 AS DiscountedStock 
                FROM Inventory 
                WHERE BasePrice > 90 AND Status = 'InStock'";

            Console.WriteLine($"Executing SQL Query:\n{sql}\n");

            try
            {
                // 3. Run the Query through the Bridge
                // This triggers: Dissector -> Parser -> Mapper -> Expression Compiler -> Execution
                using var result = df.Query(sql);

                // 4. Show Results
                Console.WriteLine("Query Results:");
                if (result.RowCount == 0)
                {
                    Console.WriteLine("No rows matched the criteria.");
                }
                else
                {
                    result.Print();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Query Error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Details: {ex.InnerException.Message}");
                Console.ResetColor();
            }
        }

        #endregion



    }


}
