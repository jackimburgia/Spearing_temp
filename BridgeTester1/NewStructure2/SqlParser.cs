using Spearing.Data.Frames;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json.Serialization;

namespace BridgeTester1.NewStructure2
{

    public record SqlQueryStatement(
        SelectStatement Select,
        string From,
        WhereStatement? Where,
        OrderByStatement? OrderBy
    );

    //-----------------------------------------



    public enum SqlOperator { Add, Sub, Mul, Div, Eq, NotEq, Gt, Lt, Gte, Lte, And, Or, Like, NA }
    public enum AggregateFunc { Sum, Max, Min, Avg, Count }

    // This attribute tells the Serializer how to handle the different types of expressions
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(IdentifierExpr), "field")]
    [JsonDerivedType(typeof(LiteralExpr), "literal")]
    [JsonDerivedType(typeof(BinaryExpr), "binary")]
    [JsonDerivedType(typeof(UnaryExpr), "unary")]
    [JsonDerivedType(typeof(AggregateExpr), "aggregate")]
    [JsonDerivedType(typeof(ConditionalExpr), "conditional")]
    [JsonDerivedType(typeof(BetweenExpr), "between")]
    [JsonDerivedType(typeof(UnknownExpr), "unknown")]
    [JsonDerivedType(typeof(InExpr), "in")]
    public interface IExpression { }

    // Removed the manual "$type" properties as the Attributes above now handle them automatically
    public record IdentifierExpr(string Name) : IExpression;
    public record LiteralExpr(object Value) : IExpression;
    public record BinaryExpr(IExpression Left, SqlOperator Op, IExpression Right) : IExpression;
    public record UnaryExpr(SqlOperator Op, IExpression Operand) : IExpression;
    public record AggregateExpr(AggregateFunc Func, List<IExpression> Arguments) : IExpression;
    public record ConditionalExpr(IExpression Condition, IExpression Then, IExpression Else) : IExpression;

    public record BetweenExpr(IExpression Expression, IExpression Lower, IExpression Upper) : IExpression;
    public record InExpr(IExpression Expression, List<IExpression> Values) : IExpression;

    public record UnknownExpr(string Error, string Token) : IExpression;

    public record Projection(IExpression Expression, string Alias);
    public record SelectStatement(List<Projection> Projections);
    public record WhereStatement(IExpression Expression);
    public record OrderByTerm(IExpression Expression, bool IsAscending);
    public record OrderByStatement(List<OrderByTerm> Terms);


    //-------------------------------------------------------


    public enum TokenType { Identifier, String, Number, Plus, Minus, Star, Slash, Eq, Gt, Lt, Gte, Lte, NotEq, LParen, RParen, Comma, EOF }
    public record Token(TokenType Type, string Value);

    public class SqlParser
    {
        private readonly List<Token> _tokens;
        private int _current = 0;

        public SqlParser(string input) => _tokens = Tokenize(input);


        public SqlQueryStatement ParseQuery()
        {
            // 1. SELECT
            ConsumeKeyword("SELECT");
            var select = ParseSelect();

            // 2. FROM
            ConsumeKeyword("FROM");
            var fromFrame = Consume(TokenType.Identifier, "Expected frame name after FROM").Value;

            // 3. WHERE (Optional)
            WhereStatement? where = null;
            if (MatchKeyword("WHERE"))
            {
                where = ParseWhere();
            }

            // 4. ORDER BY (Optional)
            OrderByStatement? orderBy = null;
            if (MatchKeyword("ORDER"))
            {
                ConsumeKeyword("BY");
                orderBy = ParseOrderBy();
            }

            return new SqlQueryStatement(select, fromFrame, where, orderBy);
        }





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

        public OrderByStatement ParseOrderBy()
        {
            var terms = new List<OrderByTerm>();
            while (!IsAtEnd())
            {
                var expr = ParseExpression();
                bool ascending = true;
                if (MatchKeyword("DESC")) ascending = false;
                else MatchKeyword("ASC");
                terms.Add(new OrderByTerm(expr, ascending));
                if (!Match(TokenType.Comma)) break;
            }
            return new OrderByStatement(terms);
        }

        private IExpression ParseExpression() => Or();
        private IExpression Or() => Binary(And, "OR");
        private IExpression And() => Binary(Comparison, "AND");

        //private IExpression Comparison()
        //{
        //    var expr = Addition();
        //    if (MatchKeyword("BETWEEN"))
        //    {
        //        var lower = Addition();
        //        ConsumeKeyword("AND");
        //        return new BetweenExpr(expr, lower, Addition());
        //    }
        //    if (Match(TokenType.Gt, TokenType.Lt, TokenType.Eq, TokenType.Gte, TokenType.Lte, TokenType.NotEq))
        //    {
        //        var op = GetOperator(Previous());
        //        return new BinaryExpr(expr, op, Addition());
        //    }
        //    return expr;
        //}

        private IExpression Comparison()
        {
            var expr = Addition();

            if (MatchKeyword("BETWEEN"))
            {
                var lower = Addition();
                ConsumeKeyword("AND");
                return new BetweenExpr(expr, lower, Addition());
            }

            // --- ADD THIS BLOCK FOR IN CLAUSE ---
            if (MatchKeyword("IN"))
            {
                Consume(TokenType.LParen, "Expected '(' after IN");
                var values = new List<IExpression>();
                while (!Check(TokenType.RParen))
                {
                    values.Add(ParseExpression());
                    if (!Match(TokenType.Comma)) break;
                }
                Consume(TokenType.RParen, "Expected ')' after IN list");
                return new InExpr(expr, values);
            }

            if (MatchKeyword("LIKE"))
            {
                return new BinaryExpr(expr, SqlOperator.Like, Addition());
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

        //private IExpression ParseAggregate()
        //{
        //    var funcName = Previous().Value;
        //    var func = Enum.Parse<AggregateFunc>(funcName, true);
        //    Consume(TokenType.LParen, "Expected '('");
        //    var arg = ParseExpression();
        //    Consume(TokenType.RParen, "Expected ')'");
        //    return new AggregateExpr(func, arg);
        //}

        private IExpression ParseAggregate()
        {
            var funcName = Previous().Value;
            var func = Enum.Parse<AggregateFunc>(funcName, true);

            Consume(TokenType.LParen, "Expected '('");

            var args = new List<IExpression>();
            if (!Check(TokenType.RParen))
            {
                do
                {
                    args.Add(ParseExpression());
                } while (Match(TokenType.Comma));
            }

            Consume(TokenType.RParen, "Expected ')'");
            return new AggregateExpr(func, args);
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
            _ => t.Value.ToUpper() switch { 
                "AND" => SqlOperator.And, 
                "OR" => SqlOperator.Or,
                "LIKE" => SqlOperator.Like,
                _ => SqlOperator.NA 
            }
        };

        //private bool IsReserved(string v) => new[] { "AS", "IF", "THEN", "ELSE", "AND", "OR", "BETWEEN", "TRUE", "FALSE", "ASC", "DESC" }
        //    .Any(r => r.Equals(v, StringComparison.OrdinalIgnoreCase));

        //private bool IsReserved(string v) =>
        //new[] { "SELECT", "FROM", "WHERE", "ORDER", "BY", "AS", "IF", "THEN", "ELSE", "AND", "OR", "BETWEEN", "TRUE", "FALSE", "ASC", "DESC" }
        //.Any(r => r.Equals(v, StringComparison.OrdinalIgnoreCase));
        private bool IsReserved(string v) =>
            new[] { "SELECT", "FROM", "WHERE", "ORDER", "BY", "AS", "IF", "THEN", "ELSE", "AND", "OR", "BETWEEN", "IN", "TRUE", "FALSE", "ASC", "DESC", "LIKE" }
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




}
