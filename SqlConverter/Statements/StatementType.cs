namespace Spearing.Language.SqlConverter.Statements
{
    public enum StatementType
    {
        Unknown,
        LogicalGroup,
        LogicalJunction,   // AND, OR
        Arithmetic,        // +, -, *, /, %, ^
        Comparison,        // =, >, <, !=, LIKE, IS NULL
        Range,             // BETWEEN
        Set,               // IN
        Literal,           // 12, 'Active', DISTINCT, NULL
        Field,             // Par, Price
        If,                // IF
        Then,              // THEN
        ElseIf,            // ELSE IF
        Else,              // ELSE
        Alias,             // AS
        Separator,         // Comma (,)
        Aggregate,         // SUM, MIN, MAX, COUNT, AVG
        Function           // ABS, ROUND, etc.
    }
}
