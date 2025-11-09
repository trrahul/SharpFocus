using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using SharpFocus.Core.Models;
using SharpFocus.LanguageServer.Protocol;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Composes cross-method slices for field symbols by filtering and transforming
/// field access summaries into slice responses.
/// </summary>
public sealed class CrossMethodSliceComposer : ICrossMethodSliceComposer
{
    private readonly ILogger<CrossMethodSliceComposer> _logger;

    public CrossMethodSliceComposer(ILogger<CrossMethodSliceComposer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public SliceResponse? BuildBackwardSlice(
        IFieldSymbol fieldSymbol,
        ClassDataflowSummary summary,
        PlaceInfo focusedPlace)
    {
        ArgumentNullException.ThrowIfNull(fieldSymbol);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(focusedPlace);

        var accesses = summary.GetFieldAccesses(fieldSymbol);

        // Backward slice: show where the field is written (sources)
        var writeAccesses = accesses
            .Where(a => a.Type == AccessType.Write || a.Type == AccessType.ReadWrite)
            .ToList();

        if (writeAccesses.Count == 0)
        {
            _logger.LogDebug(
                "No write accesses found for field {FieldName} in backward slice",
                fieldSymbol.Name);
            return null;
        }

        var rangeDetails = new List<SliceRangeInfo>();
        var ranges = new List<LspRange>();
        var containerRanges = new Dictionary<string, LspRange>(StringComparer.Ordinal);

        foreach (var access in writeAccesses)
        {
            if (!access.Location.IsInSource)
            {
                continue;
            }

            var lspRange = CreateRange(access.Location.GetLineSpan());

            ranges.Add(lspRange);

            // Create place info for the containing method or initializer
            var methodPlaceInfo = access.IsFieldInitializer
                ? new PlaceInfo
                {
                    Name = $"{fieldSymbol.Name} initializer",
                    Kind = "FieldInitializer",
                    Range = lspRange
                }
                : new PlaceInfo
                {
                    Name = access.ContainingMethod.Name,
                    Kind = "Method",
                    Range = lspRange
                };

            var summaryText = access.IsFieldInitializer
                ? $"{fieldSymbol.Name} initializer sets {focusedPlace.Name}"
                : $"{access.Type} in {access.ContainingMethod.Name}";

            rangeDetails.Add(new SliceRangeInfo
            {
                Range = lspRange,
                Place = methodPlaceInfo,
                Relation = SliceRelation.Source, // Write is a source for backward slice
                OperationKind = access.Operation.Kind.ToString(),
                Summary = summaryText
            });

            // Add containing method's range as container (skip synthetic initializer)
            if (!access.IsFieldInitializer &&
                access.ContainingMethod.DeclaringSyntaxReferences.FirstOrDefault() is { } syntaxRef)
            {
                var methodRange = CreateRange(syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span));
                var key = CreateRangeKey(methodRange);
                containerRanges[key] = methodRange;
            }
        }

        // Ensure the entire class stays visible so unrelated code within the class can be faded appropriately
        AddClassContainerRange(summary, containerRanges);

        _logger.LogInformation(
            "Built backward slice for field {FieldName}: {WriteCount} write accesses across {MethodCount} containers",
            fieldSymbol.Name,
            writeAccesses.Count,
            containerRanges.Count);

        return new SliceResponse
        {
            Direction = SliceDirection.Backward,
            FocusedPlace = focusedPlace,
            SliceRanges = ranges,
            SliceRangeDetails = rangeDetails,
            ContainerRanges = containerRanges.Values.ToList()
        };
    }

    /// <inheritdoc />
    public SliceResponse? BuildForwardSlice(
        IFieldSymbol fieldSymbol,
        ClassDataflowSummary summary,
        PlaceInfo focusedPlace)
    {
        ArgumentNullException.ThrowIfNull(fieldSymbol);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(focusedPlace);

        var accesses = summary.GetFieldAccesses(fieldSymbol);

        // Forward slice: show where the field is read (sinks)
        var readAccesses = accesses
            .Where(a => a.Type == AccessType.Read || a.Type == AccessType.ReadWrite)
            .ToList();

        if (readAccesses.Count == 0)
        {
            _logger.LogDebug(
                "No read accesses found for field {FieldName} in forward slice",
                fieldSymbol.Name);
            return null;
        }

        var rangeDetails = new List<SliceRangeInfo>();
        var ranges = new List<LspRange>();
        var containerRanges = new Dictionary<string, LspRange>(StringComparer.Ordinal);

        foreach (var access in readAccesses)
        {
            if (!access.Location.IsInSource)
            {
                continue;
            }

            var lspRange = CreateRange(access.Location.GetLineSpan());

            ranges.Add(lspRange);

            // Create place info for the containing method
            var methodPlaceInfo = new PlaceInfo
            {
                Name = access.ContainingMethod.Name,
                Kind = "Method",
                Range = lspRange
            };

            rangeDetails.Add(new SliceRangeInfo
            {
                Range = lspRange,
                Place = methodPlaceInfo,
                Relation = SliceRelation.Sink, // Read is a sink for forward slice
                OperationKind = access.Operation.Kind.ToString(),
                Summary = $"{access.Type} in {access.ContainingMethod.Name}"
            });

            // Add containing method's range as container
            if (access.ContainingMethod.DeclaringSyntaxReferences.FirstOrDefault() is { } syntaxRef)
            {
                var methodRange = CreateRange(syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span));
                var key = CreateRangeKey(methodRange);
                containerRanges[key] = methodRange;
            }
        }

        // Ensure the entire class stays visible so unrelated code within the class can be faded appropriately
        AddClassContainerRange(summary, containerRanges);

        _logger.LogInformation(
            "Built forward slice for field {FieldName}: {ReadCount} read accesses across {MethodCount} containers",
            fieldSymbol.Name,
            readAccesses.Count,
            containerRanges.Count);

        return new SliceResponse
        {
            Direction = SliceDirection.Forward,
            FocusedPlace = focusedPlace,
            SliceRanges = ranges,
            SliceRangeDetails = rangeDetails,
            ContainerRanges = containerRanges.Values.ToList()
        };
    }

    private static LspRange CreateRange(FileLinePositionSpan span)
    {
        var start = span.StartLinePosition;
        var end = span.EndLinePosition;

        return new LspRange
        {
            Start = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position(start.Line, start.Character),
            End = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position(end.Line, end.Character)
        };
    }

    private static string CreateRangeKey(LspRange range)
    {
        return $"{range.Start.Line}:{range.Start.Character}-{range.End.Line}:{range.End.Character}";
    }

    private static void AddClassContainerRange(
        ClassDataflowSummary summary,
        Dictionary<string, LspRange> containerRanges)
    {
        if (summary.ClassSymbol.DeclaringSyntaxReferences.FirstOrDefault() is not { } classSyntax)
        {
            return;
        }

        var classRange = CreateRange(classSyntax.SyntaxTree.GetLineSpan(classSyntax.Span));
        var classKey = CreateRangeKey(classRange);
        containerRanges[classKey] = classRange;
    }
}
