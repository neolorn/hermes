using System;
using System.Collections.Generic;

using Fluid;
using Fluid.Ast;
using Fluid.Parser;
using Fluid.Values;

namespace Hermes.Content;

/// <summary>
/// Walks a parsed template's syntax tree when the application starts (<c>03</c> MSG-TPL-004),
/// since Fluid resolves filters only when it renders: it finds a filter outside the allowed
/// set or given fewer arguments than it requires, a range that is not two literal numbers or
/// spans more than <c>Hermes:Templates:MaxSteps</c>, <c>for</c> loops nested deeper than
/// <c>Hermes:Templates:MaxRecursion</c> and a <c>format_date</c> or <c>format_number</c> whose
/// format is not a literal string, or too precise; and it collects the top-level names the
/// template reads and the names it sets itself (its loop variables, assigned and captured
/// names), which a send need not pass.
/// </summary>
internal sealed class TemplateWalk : AstVisitor
{
    private readonly int maxSteps;

    private readonly int maxRecursion;

    private readonly HashSet<string> reads = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> sets = new(StringComparer.OrdinalIgnoreCase);

    private int depth;

    private TemplateWalk(int maxSteps, int maxRecursion)
    {
        this.maxSteps = maxSteps;
        this.maxRecursion = maxRecursion;
    }

    private bool Invalid { get; set; }

    /// <summary>Walks <paramref name="template"/>, answering what it reads, or none where it is outside the rules.</summary>
    internal static TemplateReads? Walk(IFluidTemplate template, int maxSteps, int maxRecursion)
    {
        ArgumentNullException.ThrowIfNull(template);
        TemplateWalk walk = new(maxSteps, maxRecursion);
        walk.VisitTemplate(template);
        if (walk.Invalid)
        {
            return null;
        }

        HashSet<string> needed = new(walk.reads, StringComparer.OrdinalIgnoreCase);
        needed.ExceptWith(walk.sets);
        needed.Remove(TemplateNames.ForLoop);
        return new TemplateReads(walk.reads, needed);
    }

    protected override Expression VisitMemberExpression(MemberExpression memberExpression)
    {
        if (memberExpression.Segments[0] is IdentifierSegment initial)
        {
            reads.Add(initial.Identifier);
        }

        foreach (MemberSegment segment in memberExpression.Segments)
        {
            if (segment is IndexerSegment indexer)
            {
                Visit(indexer.Expression);
            }
        }

        return memberExpression;
    }

    protected override Statement VisitForStatement(ForStatement forStatement)
    {
        sets.Add(forStatement.Identifier);
        depth++;
        if (depth > maxRecursion)
        {
            Invalid = true;
        }

        Visit(forStatement.Source);
        Visit(forStatement.Limit);
        if (!forStatement.OffsetIsContinue)
        {
            Visit(forStatement.Offset);
        }

        foreach (Statement statement in forStatement.Statements)
        {
            Visit(statement);
        }

        depth--;
        Visit(forStatement.Else);
        return forStatement;
    }

    protected override Expression VisitRangeExpression(RangeExpression rangeExpression)
    {
        if (Bound(rangeExpression.From) is not { } from || Bound(rangeExpression.To) is not { } to || to - from + 1 > maxSteps)
        {
            Invalid = true;
        }

        return rangeExpression;
    }

    protected override Expression VisitFilterExpression(FilterExpression filterExpression)
    {
        // Fluid passes a named argument by its position too.
        if (FilterRules.Arguments(filterExpression.Name) is not { } required || filterExpression.Parameters.Count < required)
        {
            Invalid = true;
        }
        else if (filterExpression.Name is FilterRules.FormatDate or FilterRules.FormatNumber)
        {
            Expression format = filterExpression.Parameters[0].Expression;
            if (format is not LiteralExpression { Value: StringValue literal }
                || (filterExpression.Name == FilterRules.FormatNumber && FilterRules.IsTooPrecise(literal.ToStringValue())))
            {
                Invalid = true;
            }
        }

        return base.VisitFilterExpression(filterExpression);
    }

    protected override Statement VisitAssignStatement(AssignStatement assignStatement)
    {
        sets.Add(assignStatement.Identifier);
        return base.VisitAssignStatement(assignStatement);
    }

    protected override Statement VisitCaptureStatement(CaptureStatement captureStatement)
    {
        sets.Add(captureStatement.Identifier);
        return base.VisitCaptureStatement(captureStatement);
    }

    protected override Statement VisitParserBlockStatement<T>(ParserBlockStatement<T> parserBlockStatement)
    {
        // The library's own capture block, which takes Fluid's place (03 MSG-TPL-007).
        if (parserBlockStatement.Value is string identifier)
        {
            sets.Add(identifier);
        }

        return base.VisitParserBlockStatement(parserBlockStatement);
    }

    private static decimal? Bound(Expression expression)
    {
        return expression is LiteralExpression { Value: NumberValue number } ? number.ToNumberValue() : null;
    }
}