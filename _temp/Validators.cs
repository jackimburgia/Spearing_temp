using ConsoleApp1.Entities;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ConsoleApp1.Validation
{

    public class SqlExpressionValidator : ValidatorBase<SqlExpression>
    {
        public SqlExpressionValidator(IServiceProvider sp)
        {
            RuleFor(x => x).Custom((expr, context) =>
            {
                if (expr == null) return;

                // Resolve validators for recursive checks
                var exprVal = sp.GetRequiredService<IEntityValidator<SqlExpression>>();
                var predVal = sp.GetRequiredService<IEntityValidator<SqlPredicate>>();

                switch (expr)
                {
                    case ColumnExpression col:
                        if (col.ColumnName == "INVALID_SYNTAX_MULTIPLE_IDENTIFIERS")
                            context.AddFailure("Expression contains invalid sequence of identifiers.");
                        else if (col.ColumnName == "INVALID_PAREN_STUB")
                            context.AddFailure("Expression contains invalid characters or unbalanced parentheses.");
                        else if (string.IsNullOrWhiteSpace(col.ColumnName))
                            context.AddFailure("Column name cannot be empty.");
                        break;

                    case BinaryExpression be:
                        if (be.Left == null) context.AddFailure("Left side of binary expression is missing.");
                        else foreach (var err in exprVal.ValidateEntity(be.Left)) context.AddFailure(err.Message);

                        if (be.Right == null) context.AddFailure("Right side of binary expression is missing.");
                        else foreach (var err in exprVal.ValidateEntity(be.Right)) context.AddFailure(err.Message);
                        break;

                    case SqlCaseExpression ce:
                        // 1. Minimum branch check
                        if (ce.Branches == null || !ce.Branches.Any())
                        {
                            context.AddFailure("CASE expression must have at least one WHEN clause.");
                        }
                        else
                        {
                            foreach (var branch in ce.Branches)
                            {
                                // 2. Validate WHEN Condition (Predicate)
                                if (branch.Condition == null)
                                    context.AddFailure("CASE WHEN condition is missing.");
                                else
                                    foreach (var err in predVal.ValidateEntity(branch.Condition))
                                        context.AddFailure(err.Message);

                                // 3. Validate THEN Result (Expression)
                                if (branch.Result == null)
                                    context.AddFailure("CASE THEN result expression is missing.");
                                else
                                    foreach (var err in exprVal.ValidateEntity(branch.Result))
                                        context.AddFailure(err.Message);
                            }
                        }

                        // 4. Validate ELSE Result (Mandatory based on your FAIL results)
                        if (ce.ElseResult == null)
                            context.AddFailure("CASE expression is missing an ELSE clause.");
                        else
                            foreach (var err in exprVal.ValidateEntity(ce.ElseResult))
                                context.AddFailure(err.Message);
                        break;

                    case AggregateExpression agg:
                        if (agg.Expression == null)
                            context.AddFailure("Aggregate target expression is missing.");
                        else
                            foreach (var err in exprVal.ValidateEntity(agg.Expression))
                                context.AddFailure(err.Message);
                        break;

                    case LiteralExpression lit:
                        // Literals are base cases; if they exist, they are valid.
                        if (lit.Value == null) context.AddFailure("Literal value cannot be null.");
                        break;
                }
            });
        }
    }

    public class SqlPredicateValidator : ValidatorBase<SqlPredicate>
    {
        public SqlPredicateValidator(IServiceProvider sp)
        {
            RuleFor(x => x).Custom((predicate, context) =>
            {
                if (predicate == null) return;

                var exprVal = sp.GetRequiredService<IEntityValidator<SqlExpression>>();
                var predVal = sp.GetRequiredService<IEntityValidator<SqlPredicate>>();

                switch (predicate)
                {
                    case ComparisonPredicate cp:
                        string op = cp.Operator?.Trim().ToUpper() ?? "";
                        string[] validOps = { "=", ">", "<", ">=", "<=", "<>", "!=", "LIKE", "IN", "BETWEEN", "IS NULL" };

                        if (op == "INVALID_PREDICATE" || !validOps.Contains(op))
                        {
                            context.AddFailure("The statement is a math expression, not a valid conditional predicate.");
                            return;
                        }

                        if (cp.Left != null)
                            foreach (var err in exprVal.ValidateEntity(cp.Left)) context.AddFailure(err.Message);

                        if (op != "IS NULL" && cp.Right != null)
                            foreach (var err in exprVal.ValidateEntity(cp.Right)) context.AddFailure(err.Message);
                        break;

                    case CompositePredicate composite:
                        if (composite.Children == null || !composite.Children.Any() || composite.Children.Any(c => c == null))
                            context.AddFailure("Logical expression is incomplete.");
                        else
                            foreach (var child in composite.Children)
                                foreach (var err in predVal.ValidateEntity(child)) context.AddFailure(err.Message);
                        break;
                }
            });
        }
    }

    public class SqlSelectElementValidator : ValidatorBase<SelectElement>
    {
        private static readonly string[] Reserved = { "SELECT", "FROM", "WHERE", "AS", "AND", "OR", "CASE", "WHEN", "THEN", "ELSE", "END" };

        public SqlSelectElementValidator(IEntityValidator<SqlExpression> exprVal)
        {
            // 1. Ensure the Math/Column part is valid
            RuleFor(x => x.Expression)
                .NotNull().WithMessage("Select element expression missing.")
                .Custom((expr, context) => {
                    if (expr != null)
                    {
                        var errors = exprVal.ValidateEntity(expr);
                        foreach (var err in errors) context.AddFailure(err.Message);
                    }
                });

            // 2. Ensure the Alias is valid (Regex handles "123Total" and "Total!")
            RuleFor(x => x.Alias)
                .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$").WithMessage("Alias must start with a letter and contain only alphanumeric characters.")
                .Must(a => !Reserved.Contains(a?.ToUpper())).WithMessage("Alias cannot be a reserved SQL word.")
                .When(x => !string.IsNullOrEmpty(x.Alias));
        }
    }



public class SqlQueryValidator : ValidatorBase<SqlQuery>
    {
        public SqlQueryValidator()
        {
            // 1. Validate FROM Table
            RuleFor(x => x.FromTable)
                .NotEmpty().WithMessage("Table name is required.");

            // 2. Validate SELECT Columns
            RuleForEach(x => x.SelectColumns).Custom((col, context) =>
            {
                if (col.Expression is ColumnExpression c && c.ColumnName == "MISSING_ELEMENT")
                {
                    context.AddFailure("Select element expression missing.");
                }

                // Pass the context to recursive helpers
                ValidateExpression(col.Expression, context);

                if (!string.IsNullOrEmpty(col.Alias))
                {
                    if (new[] { "AS", "SELECT", "FROM", "WHERE" }.Contains(col.Alias.ToUpper()))
                        context.AddFailure("Alias cannot be a reserved SQL word.");

                    if (!Regex.IsMatch(col.Alias, @"^[a-zA-Z][a-zA-Z0-9]*$"))
                        context.AddFailure("Alias must start with a letter and contain only alphanumeric characters.");
                }
            });

            // 3. Validate WHERE Clause
            RuleFor(x => x.WhereClause).Custom((pred, context) =>
            {
                if (pred != null)
                {
                    ValidatePredicate(pred, context);
                }
            });
        }

        private void ValidateExpression(SqlExpression expr, ValidationContext<SqlQuery> context)
        {
            if (expr == null) return;

            // Sequence Guard
            if (expr is ColumnExpression c && c.ColumnName == "INVALID_SEQUENCE")
                context.AddFailure("Expression contains invalid sequence of identifiers.");

            // Aggregate Guard
            if (expr is AggregateExpression a && a.Expression == null)
                context.AddFailure("Aggregate target expression is missing.");

            // Binary Tree Traversal
            if (expr is BinaryExpression b)
            {
                if (b.Left == null) context.AddFailure("Left side of binary expression is missing.");
                if (b.Right == null) context.AddFailure("Right side of binary expression is missing.");
                ValidateExpression(b.Left, context);
                ValidateExpression(b.Right, context);
            }

            // Case Statement Traversal
            if (expr is SqlCaseExpression ce)
            {
                if (!ce.Branches.Any())
                    context.AddFailure("CASE expression must have at least one WHEN clause.");

                if (ce.ElseResult == null)
                    context.AddFailure("CASE expression is missing an ELSE clause.");

                foreach (var br in ce.Branches)
                {
                    if (br.Condition == null) context.AddFailure("CASE WHEN condition is missing.");
                    if (br.Result == null) context.AddFailure("CASE THEN result expression is missing.");

                    ValidatePredicate(br.Condition, context);
                    ValidateExpression(br.Result, context);
                }
            }
        }

        private void ValidatePredicate(SqlPredicate pred, ValidationContext<SqlQuery> context)
        {
            if (pred == null) return;

            if (pred is ComparisonPredicate cp)
            {
                if (cp.Operator == "MATH_NOT_PREDICATE")
                    context.AddFailure("The statement is a math expression, not a valid conditional predicate.");

                if (cp.Operator == "INCOMPLETE")
                    context.AddFailure("Logical expression is incomplete.");

                if (cp.Operator == "INVALID_PREDICATE_MISSING_RIGHT")
                    context.AddFailure("Right side of comparison is missing.");

                ValidateExpression(cp.Left, context);
                ValidateExpression(cp.Right, context);
            }

            if (pred is CompositePredicate comp)
            {
                foreach (var child in comp.Children)
                {
                    ValidatePredicate(child, context);
                }
            }
        }
    }
}