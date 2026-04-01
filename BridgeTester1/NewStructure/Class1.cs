using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BridgeTester1.NewStructure
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SqlOperator
    {
        Add, Sub, Mul, Div, Eq, Gt, Lt, Gte, Lte,
        NotEq, // Added <>
        And, Or, Between, NA
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AggregateFunc { Sum, Max, Min, Avg, Count, NA }

    [JsonDerivedType(typeof(LiteralExpr), "literal")]
    [JsonDerivedType(typeof(IdentifierExpr), "field")]
    [JsonDerivedType(typeof(BinaryExpr), "binary")]
    [JsonDerivedType(typeof(UnaryExpr), "unary")]
    [JsonDerivedType(typeof(BetweenExpr), "between")]
    [JsonDerivedType(typeof(ConditionalExpr), "conditional")]
    [JsonDerivedType(typeof(AggregateExpr), "aggregate")]
    [JsonDerivedType(typeof(UnknownExpr), "unknown")]
    public interface IExpression { }

    public record LiteralExpr(object Value) : IExpression;
    public record IdentifierExpr(string Name) : IExpression;
    public record UnaryExpr(SqlOperator Op, IExpression Operand) : IExpression;
    public record BinaryExpr(IExpression Left, SqlOperator Op, IExpression Right) : IExpression;
    public record BetweenExpr(IExpression Expression, IExpression Lower, IExpression Upper) : IExpression;
    public record ConditionalExpr(IExpression Condition, IExpression Then, IExpression Else) : IExpression;
    public record AggregateExpr(AggregateFunc Func, IExpression Argument) : IExpression;
    public record UnknownExpr(string Error, string Raw) : IExpression;

    public record Projection(IExpression Expression, string Alias);
    public record SelectStatement(List<Projection> Projections);
    public record WhereStatement(IExpression Expression);


    /// <summary>
    /// /////////////////////////////////////////////////
    /// </summary>

    public enum TokenType { Identifier, String, Number, Plus, Minus, Star, Slash, Eq, Gt, Lt, Gte, Lte, NotEq, LParen, RParen, Comma, EOF }
    public record Token(TokenType Type, string Value);

    public class SqlParser
    {
        private readonly List<Token> _tokens;
        private int _current = 0;

        public SqlParser(string input) => _tokens = Tokenize(input);

        public SelectStatement ParseSelect()
        {
            var projections = new List<Projection>();
            while (!IsAtEnd())
            {
                var expr = ParseExpression();
                string alias = null;
                if (MatchKeyword("AS")) alias = Consume(TokenType.Identifier, "Expected alias").Value;
                else if (Check(TokenType.Identifier) && !IsReserved(Peek().Value)) alias = Advance().Value;
                projections.Add(new Projection(expr, alias));
                if (!Match(TokenType.Comma)) break;
            }
            return new SelectStatement(projections);
        }

        public WhereStatement ParseWhere() => new WhereStatement(ParseExpression());

        private IExpression ParseExpression() => Or();
        private IExpression Or() => Binary(And, "OR");
        private IExpression And() => Binary(Comparison, "AND");

        private IExpression Comparison()
        {
            var expr = Addition();
            if (MatchKeyword("BETWEEN"))
            {
                var lower = Addition();
                ConsumeKeyword("AND");
                return new BetweenExpr(expr, lower, Addition());
            }
            if (Match(TokenType.Gt, TokenType.Lt, TokenType.Eq, TokenType.Gte, TokenType.Lte, TokenType.NotEq))
            {
                var op = GetOperator(Previous());
                return new BinaryExpr(expr, op, Addition());
            }
            return expr;
        }

        private IExpression Addition() => Binary(Multiplication, TokenType.Plus, TokenType.Minus);
        private IExpression Multiplication() => Binary(Unary, TokenType.Star, TokenType.Slash);

        private IExpression Unary()
        {
            if (Match(TokenType.Minus)) return new UnaryExpr(SqlOperator.Sub, Unary());
            return Primary();
        }

        private IExpression Primary()
        {
            if (MatchKeyword("IF")) return ParseIf();
            if (MatchKeyword("SUM", "MAX", "MIN", "AVG", "COUNT")) return ParseAggregate();

            // --- Boolean Literal Support ---
            if (MatchKeyword("TRUE")) return new LiteralExpr(true);
            if (MatchKeyword("FALSE")) return new LiteralExpr(false);

            if (Match(TokenType.LParen))
            {
                var expr = ParseExpression();
                Consume(TokenType.RParen, "Expected ')'");
                return expr;
            }
            if (Match(TokenType.Number)) return new LiteralExpr(decimal.Parse(Previous().Value));
            if (Match(TokenType.String)) return new LiteralExpr(Previous().Value);
            if (Match(TokenType.Identifier)) return new IdentifierExpr(Previous().Value);
            return new UnknownExpr("Unexpected token", Peek().Value);
        }

        private IExpression ParseIf()
        {
            var condition = ParseExpression();
            ConsumeKeyword("THEN");
            var thenBranch = ParseExpression();
            IExpression elseBranch = new LiteralExpr(null);
            if (MatchKeyword("ELSE")) elseBranch = ParseExpression();
            return new ConditionalExpr(condition, thenBranch, elseBranch);
        }

        private IExpression ParseAggregate()
        {
            var func = Enum.Parse<AggregateFunc>(Previous().Value, true);
            Consume(TokenType.LParen, "Expected '('");
            var arg = ParseExpression();
            Consume(TokenType.RParen, "Expected ')'");
            return new AggregateExpr(func, arg);
        }

        private IExpression Binary(Func<IExpression> next, params object[] types)
        {
            var expr = next();
            while (Match(types))
            {
                var op = GetOperator(Previous());
                expr = new BinaryExpr(expr, op, next());
            }
            return expr;
        }

        private bool Match(params object[] types)
        {
            foreach (var t in types)
            {
                if (t is TokenType tt && Check(tt)) { Advance(); return true; }
                if (t is string s && CheckKeyword(s)) { Advance(); return true; }
            }
            return false;
        }

        private bool MatchKeyword(params string[] ks) => ks.Any(k => Match(k));
        private bool Check(TokenType t) => !IsAtEnd() && Peek().Type == t;
        private bool CheckKeyword(string k) => !IsAtEnd() && Peek().Type == TokenType.Identifier && Peek().Value.Equals(k, StringComparison.OrdinalIgnoreCase);
        private Token Consume(TokenType t, string m) => Check(t) ? Advance() : throw new Exception(m);
        private void ConsumeKeyword(string k) { if (!CheckKeyword(k)) throw new Exception($"Expected {k}"); Advance(); }
        private Token Advance() => !IsAtEnd() ? _tokens[_current++] : Previous();
        private bool IsAtEnd() => Peek().Type == TokenType.EOF;
        private Token Peek() => _tokens[_current];
        private Token Previous() => _tokens[_current - 1];

        private SqlOperator GetOperator(Token t) => t.Type switch
        {
            TokenType.Plus => SqlOperator.Add,
            TokenType.Minus => SqlOperator.Sub,
            TokenType.Star => SqlOperator.Mul,
            TokenType.Slash => SqlOperator.Div,
            TokenType.Eq => SqlOperator.Eq,
            TokenType.Gt => SqlOperator.Gt,
            TokenType.Lt => SqlOperator.Lt,
            TokenType.Gte => SqlOperator.Gte,
            TokenType.Lte => SqlOperator.Lte,
            TokenType.NotEq => SqlOperator.NotEq,
            _ => t.Value.ToUpper() switch { "AND" => SqlOperator.And, "OR" => SqlOperator.Or, _ => SqlOperator.NA }
        };

        // Updated with TRUE/FALSE
        private bool IsReserved(string v) => new[] { "AS", "IF", "THEN", "ELSE", "AND", "OR", "BETWEEN", "TRUE", "FALSE" }
            .Any(r => r.Equals(v, StringComparison.OrdinalIgnoreCase));

        private List<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < input.Length)
            {
                char c = input[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (char.IsLetter(c))
                {
                    int s = i; while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_')) i++;
                    tokens.Add(new Token(TokenType.Identifier, input[s..i]));
                }
                else if (char.IsDigit(c))
                {
                    int s = i; while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.')) i++;
                    tokens.Add(new Token(TokenType.Number, input[s..i]));
                }
                else if (c == '\'')
                {
                    int s = ++i; while (i < input.Length && input[i] != '\'') i++;
                    tokens.Add(new Token(TokenType.String, input[s..(i++)]));
                }
                else
                {
                    var type = c switch
                    {
                        '(' => TokenType.LParen,
                        ')' => TokenType.RParen,
                        ',' => TokenType.Comma,
                        '+' => TokenType.Plus,
                        '-' => TokenType.Minus,
                        '*' => TokenType.Star,
                        '/' => TokenType.Slash,
                        '=' => TokenType.Eq,
                        '>' => i + 1 < input.Length && input[i + 1] == '=' ? IncrementAndReturn(ref i, TokenType.Gte) : TokenType.Gt,
                        '<' => i + 1 < input.Length && input[i + 1] == '=' ? IncrementAndReturn(ref i, TokenType.Lte) : (i + 1 < input.Length && input[i + 1] == '>' ? IncrementAndReturn(ref i, TokenType.NotEq) : TokenType.Lt),
                        _ => TokenType.EOF
                    };
                    if (type != TokenType.EOF) tokens.Add(new Token(type, c.ToString()));
                    i++;
                }
            }
            tokens.Add(new Token(TokenType.EOF, ""));
            return tokens;
        }
        private TokenType IncrementAndReturn(ref int i, TokenType t) { i++; return t; }
    }


    #region OLD
    //    public enum TokenType { Identifier, String, Number, Plus, Minus, Star, Slash, Eq, Gt, Lt, Gte, Lte, NotEq, LParen, RParen, Comma, EOF }
    //    public record Token(TokenType Type, string Value);

    //    public class SqlParser
    //    {
    //        private readonly List<Token> _tokens;
    //        private int _current = 0;

    //        public SqlParser(string input) => _tokens = Tokenize(input);

    //        public SelectStatement ParseSelect()
    //        {
    //            var projections = new List<Projection>();
    //            while (!IsAtEnd())
    //            {
    //                var expr = ParseExpression();
    //                string alias = null;
    //                if (MatchKeyword("AS")) alias = Consume(TokenType.Identifier, "Expected alias").Value;
    //                else if (Check(TokenType.Identifier) && !IsReserved(Peek().Value)) alias = Advance().Value;
    //                projections.Add(new Projection(expr, alias));
    //                if (!Match(TokenType.Comma)) break;
    //            }
    //            return new SelectStatement(projections);
    //        }

    //        public WhereStatement ParseWhere() => new WhereStatement(ParseExpression());

    //        private IExpression ParseExpression() => Or();
    //        private IExpression Or() => Binary(And, "OR");
    //        private IExpression And() => Binary(Comparison, "AND");

    //        private IExpression Comparison()
    //        {
    //            var expr = Addition();
    //            if (MatchKeyword("BETWEEN"))
    //            {
    //                var lower = Addition();
    //                ConsumeKeyword("AND");
    //                return new BetweenExpr(expr, lower, Addition());
    //            }
    //            if (Match(TokenType.Gt, TokenType.Lt, TokenType.Eq, TokenType.Gte, TokenType.Lte, TokenType.NotEq))
    //            {
    //                var op = GetOperator(Previous());
    //                return new BinaryExpr(expr, op, Addition());
    //            }
    //            return expr;
    //        }

    //        private IExpression Addition() => Binary(Multiplication, TokenType.Plus, TokenType.Minus);
    //        private IExpression Multiplication() => Binary(Unary, TokenType.Star, TokenType.Slash);

    //        private IExpression Unary()
    //        {
    //            if (Match(TokenType.Minus)) return new UnaryExpr(SqlOperator.Sub, Unary());
    //            return Primary();
    //        }

    //        private IExpression Primary()
    //        {
    //            if (MatchKeyword("IF")) return ParseIf();
    //            if (MatchKeyword("SUM", "MAX", "MIN", "AVG", "COUNT")) return ParseAggregate();
    //            if (Match(TokenType.LParen))
    //            {
    //                var expr = ParseExpression();
    //                Consume(TokenType.RParen, "Expected ')'");
    //                return expr;
    //            }
    //            if (Match(TokenType.Number)) return new LiteralExpr(decimal.Parse(Previous().Value));
    //            if (Match(TokenType.String)) return new LiteralExpr(Previous().Value);
    //            if (Match(TokenType.Identifier)) return new IdentifierExpr(Previous().Value);
    //            return new UnknownExpr("Unexpected token", Peek().Value);
    //        }

    //        private IExpression ParseIf()
    //        {
    //            var condition = ParseExpression();
    //            ConsumeKeyword("THEN");
    //            var thenBranch = ParseExpression();
    //            IExpression elseBranch = new LiteralExpr(null);
    //            if (MatchKeyword("ELSE")) elseBranch = ParseExpression();
    //            return new ConditionalExpr(condition, thenBranch, elseBranch);
    //        }

    //        private IExpression ParseAggregate()
    //        {
    //            var func = Enum.Parse<AggregateFunc>(Previous().Value, true);
    //            Consume(TokenType.LParen, "Expected '('");
    //            var arg = ParseExpression();
    //            Consume(TokenType.RParen, "Expected ')'");
    //            return new AggregateExpr(func, arg);
    //        }

    //        private IExpression Binary(Func<IExpression> next, params object[] types)
    //        {
    //            var expr = next();
    //            while (Match(types))
    //            {
    //                var op = GetOperator(Previous());
    //                expr = new BinaryExpr(expr, op, next());
    //            }
    //            return expr;
    //        }

    //        private bool Match(params object[] types)
    //        {
    //            foreach (var t in types)
    //            {
    //                if (t is TokenType tt && Check(tt)) { Advance(); return true; }
    //                if (t is string s && CheckKeyword(s)) { Advance(); return true; }
    //            }
    //            return false;
    //        }

    //        private bool MatchKeyword(params string[] ks) => ks.Any(k => Match(k));
    //        private bool Check(TokenType t) => !IsAtEnd() && Peek().Type == t;
    //        private bool CheckKeyword(string k) => !IsAtEnd() && Peek().Type == TokenType.Identifier && Peek().Value.Equals(k, StringComparison.OrdinalIgnoreCase);
    //        private Token Consume(TokenType t, string m) => Check(t) ? Advance() : throw new Exception(m);
    //        private void ConsumeKeyword(string k) { if (!CheckKeyword(k)) throw new Exception($"Expected {k}"); Advance(); }
    //        private Token Advance() => !IsAtEnd() ? _tokens[_current++] : Previous();
    //        private bool IsAtEnd() => Peek().Type == TokenType.EOF;
    //        private Token Peek() => _tokens[_current];
    //        private Token Previous() => _tokens[_current - 1];

    //        private SqlOperator GetOperator(Token t) => t.Type switch
    //        {
    //            TokenType.Plus => SqlOperator.Add,
    //            TokenType.Minus => SqlOperator.Sub,
    //            TokenType.Star => SqlOperator.Mul,
    //            TokenType.Slash => SqlOperator.Div,
    //            TokenType.Eq => SqlOperator.Eq,
    //            TokenType.Gt => SqlOperator.Gt,
    //            TokenType.Lt => SqlOperator.Lt,
    //            TokenType.Gte => SqlOperator.Gte,
    //            TokenType.Lte => SqlOperator.Lte,
    //            TokenType.NotEq => SqlOperator.NotEq,
    //            _ => t.Value.ToUpper() switch { "AND" => SqlOperator.And, "OR" => SqlOperator.Or, _ => SqlOperator.NA }
    //        };

    //        private bool IsReserved(string v) => new[] { "AS", "IF", "THEN", "ELSE", "AND", "OR", "BETWEEN" }.Any(r => r.Equals(v, StringComparison.OrdinalIgnoreCase));

    //        private List<Token> Tokenize(string input)
    //        {
    //            var tokens = new List<Token>();
    //            int i = 0;
    //            while (i < input.Length)
    //            {
    //                char c = input[i];
    //                if (char.IsWhiteSpace(c)) { i++; continue; }
    //                if (char.IsLetter(c))
    //                {
    //                    int s = i; while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_')) i++;
    //                    tokens.Add(new Token(TokenType.Identifier, input[s..i]));
    //                }
    //                else if (char.IsDigit(c))
    //                {
    //                    int s = i; while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.')) i++;
    //                    tokens.Add(new Token(TokenType.Number, input[s..i]));
    //                }
    //                else if (c == '\'')
    //                {
    //                    int s = ++i; while (i < input.Length && input[i] != '\'') i++;
    //                    tokens.Add(new Token(TokenType.String, input[s..(i++)]));
    //                }
    //                else
    //                {
    //                    var type = c switch
    //                    {
    //                        '(' => TokenType.LParen,
    //                        ')' => TokenType.RParen,
    //                        ',' => TokenType.Comma,
    //                        '+' => TokenType.Plus,
    //                        '-' => TokenType.Minus,
    //                        '*' => TokenType.Star,
    //                        '/' => TokenType.Slash,
    //                        '=' => TokenType.Eq,
    //                        '>' => i + 1 < input.Length && input[i + 1] == '=' ? IncrementAndReturn(ref i, TokenType.Gte) : TokenType.Gt,
    //                        '<' => i + 1 < input.Length && input[i + 1] == '=' ? IncrementAndReturn(ref i, TokenType.Lte) : (i + 1 < input.Length && input[i + 1] == '>' ? IncrementAndReturn(ref i, TokenType.NotEq) : TokenType.Lt),
    //                        _ => TokenType.EOF
    //                    };
    //                    if (type != TokenType.EOF) tokens.Add(new Token(type, c.ToString()));
    //                    i++;
    //                }
    //            }
    //            tokens.Add(new Token(TokenType.EOF, ""));
    //            return tokens;
    //        }
    //        private TokenType IncrementAndReturn(ref int i, TokenType t) { i++; return t; }

    //}


    #endregion



    public static class TestRunner
    {
        private static readonly JsonSerializerOptions MinifyOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter() }
        };

        public static void Run()
        {
            var selectSuite = GenerateSelectTests();
            var whereSuite = GenerateWhereTests();

            RunSuite("SELECT", selectSuite, sql => new SqlParser(sql).ParseSelect());
            RunSuite("WHERE", whereSuite, sql => new SqlParser(sql).ParseWhere());
        }

        private static (string sql, string expected)[] GenerateSelectTests()
        {
            // Already passing 50/50
            return new (string, string)[]
            {
                ("Par", "{\"Projections\":[{\"Expression\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Alias\":null}]}"),
                ("MktValue, Par", "{\"Projections\":[{\"Expression\":{\"$type\":\"field\",\"Name\":\"MktValue\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Alias\":null}]}"),
                ("AssetType AS Type", "{\"Projections\":[{\"Expression\":{\"$type\":\"field\",\"Name\":\"AssetType\"},\"Alias\":\"Type\"}]}"),
                ("mKtVaLuE AS market_value", "{\"Projections\":[{\"Expression\":{\"$type\":\"field\",\"Name\":\"mKtVaLuE\"},\"Alias\":\"market_value\"}]}"),
                ("100 AS Const", "{\"Projections\":[{\"Expression\":{\"$type\":\"literal\",\"Value\":100},\"Alias\":\"Const\"}]}"),
                ("Par + GL", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"GL\"}},\"Alias\":null}]}"),
                ("Par - GL AS Net", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Sub\",\"Right\":{\"$type\":\"field\",\"Name\":\"GL\"}},\"Alias\":\"Net\"}]}"),
                ("Price * Qty AS Value", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"Qty\"}},\"Alias\":\"Value\"}]}"),
                ("Price / 100 AS Pct", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Op\":\"Div\",\"Right\":{\"$type\":\"literal\",\"Value\":100}},\"Alias\":\"Pct\"}]}"),
                ("A + B + C AS Total", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}},\"Alias\":\"Total\"}]}"),
                ("(A + B) * C", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}},\"Alias\":null}]}"),
                ("A + B * C", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}}},\"Alias\":null}]}"),
                ("A + (B - C) * D AS MixedOrder", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Sub\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}}},\"Alias\":\"MixedOrder\"}]}"),
                ("Price * 1.10 AS MarkUp", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"literal\",\"Value\":1.10}},\"Alias\":\"MarkUp\"}]}"),
                ("100 * (A + B) / 10", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"literal\",\"Value\":100},\"Op\":\"Mul\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}}},\"Op\":\"Div\",\"Right\":{\"$type\":\"literal\",\"Value\":10}},\"Alias\":null}]}"),
                ("SUM(Par) AS S", "{\"Projections\":[{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Par\"}},\"Alias\":\"S\"}]}"),
                ("MAX(Price), MIN(Price)", "{\"Projections\":[{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Max\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Price\"}},\"Alias\":null},{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Min\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Price\"}},\"Alias\":null}]}"),
                ("AVG(Qty) AS Mean", "{\"Projections\":[{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Avg\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Qty\"}},\"Alias\":\"Mean\"}]}"),
                ("COUNT(ID) AS Vol", "{\"Projections\":[{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Count\",\"Argument\":{\"$type\":\"field\",\"Name\":\"ID\"}},\"Alias\":\"Vol\"}]}"),
                ("SUM(A + B) AS TotalSum", "{\"Projections\":[{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}}},\"Alias\":\"TotalSum\"}]}"),
                ("SUM(Par) / COUNT(ID) AS AvgPar", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Par\"}},\"Op\":\"Div\",\"Right\":{\"$type\":\"aggregate\",\"Func\":\"Count\",\"Argument\":{\"$type\":\"field\",\"Name\":\"ID\"}}},\"Alias\":\"AvgPar\"}]}"),
                ("SUM(P) + SUM(G) - SUM(L)", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"P\"}},\"Op\":\"Add\",\"Right\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"G\"}}},\"Op\":\"Sub\",\"Right\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"L\"}}},\"Alias\":null}]}"),
                ("MAX(Price) - MIN(Price) AS Spread", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"aggregate\",\"Func\":\"Max\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Price\"}},\"Op\":\"Sub\",\"Right\":{\"$type\":\"aggregate\",\"Func\":\"Min\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Price\"}}},\"Alias\":\"Spread\"}]}"),
                ("SUM(Par) * 0.01 AS Fee", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Par\"}},\"Op\":\"Mul\",\"Right\":{\"$type\":\"literal\",\"Value\":0.01}},\"Alias\":\"Fee\"}]}"),
                ("COUNT(ID) + 1 AS NextId", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"aggregate\",\"Func\":\"Count\",\"Argument\":{\"$type\":\"field\",\"Name\":\"ID\"}},\"Op\":\"Add\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Alias\":\"NextId\"}]}"),
                ("IF A > 10 THEN 'H' ELSE 'L'", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":10}},\"Then\":{\"$type\":\"literal\",\"Value\":\"H\"},\"Else\":{\"$type\":\"literal\",\"Value\":\"L\"}},\"Alias\":null}]}"),
                ("IF A = B THEN 1 ELSE 0 AS Match", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Then\":{\"$type\":\"literal\",\"Value\":1},\"Else\":{\"$type\":\"literal\",\"Value\":0}},\"Alias\":\"Match\"}]}"),
                ("IF P > 0 THEN P ELSE 0 AS PosOnly", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"P\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}},\"Then\":{\"$type\":\"field\",\"Name\":\"P\"},\"Else\":{\"$type\":\"literal\",\"Value\":0}},\"Alias\":\"PosOnly\"}]}"),
                ("IF Status = 'Closed' THEN 0 ELSE Price AS ActivePrice", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Status\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"Closed\"}},\"Then\":{\"$type\":\"literal\",\"Value\":0},\"Else\":{\"$type\":\"field\",\"Name\":\"Price\"}},\"Alias\":\"ActivePrice\"}]}"),
                ("IF (A + B) > 100 THEN 1 ELSE 0", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":100}},\"Then\":{\"$type\":\"literal\",\"Value\":1},\"Else\":{\"$type\":\"literal\",\"Value\":0}},\"Alias\":null}]}"),
                ("IF A > 10 THEN 'H' ELSE IF A > 5 THEN 'M' ELSE 'L' AS Gr", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":10}},\"Then\":{\"$type\":\"literal\",\"Value\":\"H\"},\"Else\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":5}},\"Then\":{\"$type\":\"literal\",\"Value\":\"M\"},\"Else\":{\"$type\":\"literal\",\"Value\":\"L\"}}},\"Alias\":\"Gr\"}]}"),
                ("SUM(IF R = 'AAA' THEN P ELSE 0) AS AaaAmt", "{\"Projections\":[{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"R\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"AAA\"}},\"Then\":{\"$type\":\"field\",\"Name\":\"P\"},\"Else\":{\"$type\":\"literal\",\"Value\":0}}},\"Alias\":\"AaaAmt\"}]}"),
                ("MAX(IF A = 1 THEN B ELSE 0) AS Result", "{\"Projections\":[{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Max\",\"Argument\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Then\":{\"$type\":\"field\",\"Name\":\"B\"},\"Else\":{\"$type\":\"literal\",\"Value\":0}}},\"Alias\":\"Result\"}]}"),
                ("IF SUM(P) > 1000 THEN 'Big' ELSE 'Small' AS Size", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"P\"}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":1000}},\"Then\":{\"$type\":\"literal\",\"Value\":\"Big\"},\"Else\":{\"$type\":\"literal\",\"Value\":\"Small\"}},\"Alias\":\"Size\"}]}"),
                ("IF A=1 THEN 'Y' ELSE 'N' AS Short", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Then\":{\"$type\":\"literal\",\"Value\":\"Y\"},\"Else\":{\"$type\":\"literal\",\"Value\":\"N\"}},\"Alias\":\"Short\"}]}"),
                ("1 + 2 + 3 + 4 + 5 AS Seq", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"literal\",\"Value\":1},\"Op\":\"Add\",\"Right\":{\"$type\":\"literal\",\"Value\":2}},\"Op\":\"Add\",\"Right\":{\"$type\":\"literal\",\"Value\":3}},\"Op\":\"Add\",\"Right\":{\"$type\":\"literal\",\"Value\":4}},\"Op\":\"Add\",\"Right\":{\"$type\":\"literal\",\"Value\":5}},\"Alias\":\"Seq\"}]}"),
                ("(((A+B)*C)/D)+E AS Deep", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}},\"Op\":\"Div\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"E\"}},\"Alias\":\"Deep\"}]}"),
                ("A * B / C * D AS MulDivChain", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Div\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}},\"Alias\":\"MulDivChain\"}]}"),
                ("A + B * C + D AS Prec", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}}},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}},\"Alias\":\"Prec\"}]}"),
                ("123.45 AS ConstantVal", "{\"Projections\":[{\"Expression\":{\"$type\":\"literal\",\"Value\":123.45},\"Alias\":\"ConstantVal\"}]}"),
                ("Field1, Field2, Field3, Field4, Field5", "{\"Projections\":[{\"Expression\":{\"$type\":\"field\",\"Name\":\"Field1\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"Field2\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"Field3\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"Field4\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"Field5\"},\"Alias\":null}]}"),
                ("SUM(P) / COUNT(ID) AS Avg", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"P\"}},\"Op\":\"Div\",\"Right\":{\"$type\":\"aggregate\",\"Func\":\"Count\",\"Argument\":{\"$type\":\"field\",\"Name\":\"ID\"}}},\"Alias\":\"Avg\"}]}"),
                ("IF A > B THEN A ELSE B AS Big", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Then\":{\"$type\":\"field\",\"Name\":\"A\"},\"Else\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Alias\":\"Big\"}]}"),
                ("AssetType AS T, Rating AS R, SUM(Par) AS S", "{\"Projections\":[{\"Expression\":{\"$type\":\"field\",\"Name\":\"AssetType\"},\"Alias\":\"T\"},{\"Expression\":{\"$type\":\"field\",\"Name\":\"Rating\"},\"Alias\":\"R\"},{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"field\",\"Name\":\"Par\"}},\"Alias\":\"S\"}]}"),
                ("MAX(IF S = 'Active' THEN Price ELSE 0) AS PeakActive", "{\"Projections\":[{\"Expression\":{\"$type\":\"aggregate\",\"Func\":\"Max\",\"Argument\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"S\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"Active\"}},\"Then\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Else\":{\"$type\":\"literal\",\"Value\":0}}},\"Alias\":\"PeakActive\"}]}"),
                ("IF A=1 THEN (B+C) ELSE (B-C) AS BranchMath", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Then\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}},\"Else\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Sub\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}}},\"Alias\":\"BranchMath\"}]}"),
                ("SUM(IF A>0 THEN A ELSE 0) + SUM(IF B>0 THEN B ELSE 0) AS DoubleSum", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}},\"Then\":{\"$type\":\"field\",\"Name\":\"A\"},\"Else\":{\"$type\":\"literal\",\"Value\":0}}},\"Op\":\"Add\",\"Right\":{\"$type\":\"aggregate\",\"Func\":\"Sum\",\"Argument\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}},\"Then\":{\"$type\":\"field\",\"Name\":\"B\"},\"Else\":{\"$type\":\"literal\",\"Value\":0}}}},\"Alias\":\"DoubleSum\"}]}"),
                ("IF X=1 THEN A ELSE IF X=2 THEN B ELSE IF X=3 THEN C ELSE D AS Quad", "{\"Projections\":[{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Then\":{\"$type\":\"field\",\"Name\":\"A\"},\"Else\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}},\"Then\":{\"$type\":\"field\",\"Name\":\"B\"},\"Else\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":3}},\"Then\":{\"$type\":\"field\",\"Name\":\"C\"},\"Else\":{\"$type\":\"field\",\"Name\":\"D\"}}}},\"Alias\":\"Quad\"}]}"),
                ("A, B, C, D, E", "{\"Projections\":[{\"Expression\":{\"$type\":\"field\",\"Name\":\"A\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"B\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"C\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"D\"},\"Alias\":null},{\"Expression\":{\"$type\":\"field\",\"Name\":\"E\"},\"Alias\":null}]}"),
                ("1 + (2 * 3) / 4 AS MathConst", "{\"Projections\":[{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"literal\",\"Value\":1},\"Op\":\"Add\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"literal\",\"Value\":2},\"Op\":\"Mul\",\"Right\":{\"$type\":\"literal\",\"Value\":3}},\"Op\":\"Div\",\"Right\":{\"$type\":\"literal\",\"Value\":4}}},\"Alias\":\"MathConst\"}]}")
            };
        }

        private static (string sql, string expected)[] GenerateWhereTests()
        {
            return new (string, string)[]
            {
                ("IsLoan = true", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"IsLoan\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":true}}}"),

                ("A = 1", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}"),
                ("AssetType = 'Equity'", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"AssetType\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"Equity\"}}}"),
                ("AssetType <> 'Cash'", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"AssetType\"},\"Op\":\"NotEq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"Cash\"}}}"),
                ("Par > 1000", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":1000}}}"),
                ("Par < 500", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Lt\",\"Right\":{\"$type\":\"literal\",\"Value\":500}}}"),
                ("A = 1 AND B = 2", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}}}}"),
                ("A = 1 OR B = 2", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}}}}"),
                ("A = 1 OR A = 2 OR A = 3", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":3}}}}"),
                ("A = 1 AND B = 2 AND C = 3", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":3}}}}"),
                ("A = 1 OR B = 2 AND C = 3", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":3}}}}}"),
                ("(A = 1 OR B = 1) AND C = 1", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}"),
                ("A = 1 AND (B = 1 OR C = 1)", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}}"),
                ("((A = 1 AND B = 1) OR (C = 1 AND D = 1))", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"D\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}}"),
                ("A > 10 AND B > 20 AND C > 30", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":10}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":20}}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":30}}}}"),
                ("X = 1 OR Y = 1 OR Z = 1", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Y\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Z\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}"),
                ("(A + B) > 100", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":100}}}"),
                ("Price * Qty = Value", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"Qty\"}},\"Op\":\"Eq\",\"Right\":{\"$type\":\"field\",\"Name\":\"Value\"}}}"),
                ("A + B > C + D", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}}}}"),
                ("Par / 100 > 0.5", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Div\",\"Right\":{\"$type\":\"literal\",\"Value\":100}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0.5}}}"),
                ("(A - B) < (C - D)", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Sub\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Lt\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Sub\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}}}}"),
                ("A BETWEEN 1 AND 10", "{\"Expression\":{\"$type\":\"between\",\"Expression\":{\"$type\":\"field\",\"Name\":\"A\"},\"Lower\":{\"$type\":\"literal\",\"Value\":1},\"Upper\":{\"$type\":\"literal\",\"Value\":10}}}"),
                ("Price BETWEEN 100 AND 500", "{\"Expression\":{\"$type\":\"between\",\"Expression\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Lower\":{\"$type\":\"literal\",\"Value\":100},\"Upper\":{\"$type\":\"literal\",\"Value\":500}}}"),
                ("A BETWEEN (B - 1) AND (B + 1)", "{\"Expression\":{\"$type\":\"between\",\"Expression\":{\"$type\":\"field\",\"Name\":\"A\"},\"Lower\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Sub\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Upper\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}"),
                ("GL BETWEEN (Par * -1) AND Par", "{\"Expression\":{\"$type\":\"between\",\"Expression\":{\"$type\":\"field\",\"Name\":\"GL\"},\"Lower\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"unary\",\"Op\":\"Sub\",\"Operand\":{\"$type\":\"literal\",\"Value\":1}}},\"Upper\":{\"$type\":\"field\",\"Name\":\"Par\"}}}"),
                ("A BETWEEN 1 AND 10 AND B = 1", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"between\",\"Expression\":{\"$type\":\"field\",\"Name\":\"A\"},\"Lower\":{\"$type\":\"literal\",\"Value\":1},\"Upper\":{\"$type\":\"literal\",\"Value\":10}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}"),
                ("-A = -1", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"unary\",\"Op\":\"Sub\",\"Operand\":{\"$type\":\"field\",\"Name\":\"A\"}},\"Op\":\"Eq\",\"Right\":{\"$type\":\"unary\",\"Op\":\"Sub\",\"Operand\":{\"$type\":\"literal\",\"Value\":1}}}}"),
                ("A <> B AND C <> D", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"NotEq\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"NotEq\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}}}}"),
                ("A >= 100 AND A <= 200", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Gte\",\"Right\":{\"$type\":\"literal\",\"Value\":100}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Lte\",\"Right\":{\"$type\":\"literal\",\"Value\":200}}}}"),
                (" ( ( A = 1 ) ) ", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}"),
                ("A = 'X' AND B = 'Y'", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"X\"}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"Y\"}}}}"),
                ("IF A = 1 THEN B = 1 ELSE B = 0", "{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Then\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Else\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":0}}}}"),
                ("IF Flag = 1 THEN A > 0 ELSE A <= 0", "{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Flag\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Then\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}},\"Else\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Lte\",\"Right\":{\"$type\":\"literal\",\"Value\":0}}}}"),
                ("IF Status = 'Active' THEN Price > 0 ELSE Price = 0", "{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Status\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"Active\"}},\"Then\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}},\"Else\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":0}}}}"),
                // FIXED BRACE DEPTH: 5
                ("IF A = 1 THEN B = 1 ELSE IF A = 2 THEN B = 2 ELSE B = 0", "{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Then\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Else\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}},\"Then\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}},\"Else\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":0}}}}}"),
                // FIXED NUMBERS AND ASSOCIATIVITY
                ("X = 10 OR X = 20 OR X = 30 OR X = 40", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":10}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":20}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":30}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":40}}}}"),
                ("A=1 AND (B=2 OR C=3) AND D=4", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":2}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":3}}}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"D\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":4}}}}"),
                ("Rating = 'AAA' AND Par > 1000", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Rating\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"AAA\"}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":1000}}}}"),
                ("Rating = 'AAA' OR Rating = 'AA' OR Rating = 'A'", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Rating\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"AAA\"}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Rating\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"AA\"}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Rating\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"A\"}}}}"),
                // FIXED RIGHT SIDE NESTING
                ("A + B + C > X + Y + Z", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"C\"}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"Y\"}},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"Z\"}}}}"),
                ("Price * 1.1 > Target", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Price\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"literal\",\"Value\":1.1}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"field\",\"Name\":\"Target\"}}}"),
                ("A = 1 AND B = 1 OR C = 1", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}"),
                ("X <> 0 AND (Y / X) > 1", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"X\"},\"Op\":\"NotEq\",\"Right\":{\"$type\":\"literal\",\"Value\":0}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Y\"},\"Op\":\"Div\",\"Right\":{\"$type\":\"field\",\"Name\":\"X\"}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}"),
                ("A = 'A' OR B = 'B' AND C = 'C' OR D = 'D'", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"A\"}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"B\"}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"C\"}}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"D\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"D\"}}}}"),
                ("Par > 0 AND (GL / Par) < -0.5", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"GL\"},\"Op\":\"Div\",\"Right\":{\"$type\":\"field\",\"Name\":\"Par\"}},\"Op\":\"Lt\",\"Right\":{\"$type\":\"unary\",\"Op\":\"Sub\",\"Operand\":{\"$type\":\"literal\",\"Value\":0.5}}}}}"),
                ("IF A = B THEN (C + D) > 0 ELSE (E - F) < 0", "{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Then\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}},\"Else\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"E\"},\"Op\":\"Sub\",\"Right\":{\"$type\":\"field\",\"Name\":\"F\"}},\"Op\":\"Lt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}}}}"),
                ("A BETWEEN 100 AND 200 OR A BETWEEN 300 AND 400", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"between\",\"Expression\":{\"$type\":\"field\",\"Name\":\"A\"},\"Lower\":{\"$type\":\"literal\",\"Value\":100},\"Upper\":{\"$type\":\"literal\",\"Value\":200}},\"Op\":\"Or\",\"Right\":{\"$type\":\"between\",\"Expression\":{\"$type\":\"field\",\"Name\":\"A\"},\"Lower\":{\"$type\":\"literal\",\"Value\":300},\"Upper\":{\"$type\":\"literal\",\"Value\":400}}}}"),
                ("(A * B) + (C * D) <> (E / F)", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Add\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Mul\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}}},\"Op\":\"NotEq\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"E\"},\"Op\":\"Div\",\"Right\":{\"$type\":\"field\",\"Name\":\"F\"}}}}"),
                // FIXED ELSE BRANCH NESTING
                ("IF Rating = 'AAA' THEN Par > 1000 ELSE Par > 500 AND Status = 'Active'", "{\"Expression\":{\"$type\":\"conditional\",\"Condition\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Rating\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"AAA\"}},\"Then\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":1000}},\"Else\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Par\"},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":500}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"Status\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":\"Active\"}}}}}"),
                ("( ( ( A = 1 ) AND ( B = 1 ) ) OR ( ( C = 1 ) AND ( D = 1 ) ) )", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"B\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}},\"Op\":\"Or\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}},\"Op\":\"And\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"D\"},\"Op\":\"Eq\",\"Right\":{\"$type\":\"literal\",\"Value\":1}}}}}")
                // Final 50th test
                ,("((A + B) * (C - D)) / E > 0", "{\"Expression\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"A\"},\"Op\":\"Add\",\"Right\":{\"$type\":\"field\",\"Name\":\"B\"}},\"Op\":\"Mul\",\"Right\":{\"$type\":\"binary\",\"Left\":{\"$type\":\"field\",\"Name\":\"C\"},\"Op\":\"Sub\",\"Right\":{\"$type\":\"field\",\"Name\":\"D\"}}},\"Op\":\"Div\",\"Right\":{\"$type\":\"field\",\"Name\":\"E\"}},\"Op\":\"Gt\",\"Right\":{\"$type\":\"literal\",\"Value\":0}}}")
            };
        }

        private static void RunSuite(string label, (string sql, string expected)[] suite, Func<string, object> parseFunc)
        {
            Console.WriteLine($"\n--- {label} SUITE ---");
            int passedCount = 0;

            foreach (var test in suite)
            {
                try
                {
                    var actualObj = parseFunc(test.sql);
                    string actualJson = JsonSerializer.Serialize(actualObj, MinifyOptions);
                    bool pass = string.Equals(actualJson, test.expected);

                    if (pass)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[PASS] SQL: {test.sql}");
                        passedCount++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[FAIL] SQL: {test.sql}");
                        Console.WriteLine($"   Expected: {test.expected}");
                        Console.WriteLine($"   Actual:   {actualJson}");
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] SQL: {test.sql} - {ex.Message}");
                }
                finally { Console.ResetColor(); }
            }
            Console.WriteLine($"\n{label} Summary: {passedCount}/{suite.Length} Passed.");
        }
    }



    public static class JsonExtensions
    {
        private static readonly JsonSerializerOptions MinifiedOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public static string SanitizeJson(object obj)
        {
            if (obj == null) return "{}";
            return JsonSerializer.Serialize(obj, MinifiedOptions);
        }
    }

}
