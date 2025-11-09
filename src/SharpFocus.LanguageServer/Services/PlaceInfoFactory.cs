using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SharpFocus.Core.Models;
using SharpFocus.LanguageServer.Protocol;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Creates <see cref="PlaceInfo"/> instances from Roslyn syntax information.
/// </summary>
public static class PlaceInfoFactory
{
    public static PlaceInfo CreatePlaceInfo(SyntaxNode node, SourceText sourceText, Place place)
    {
        ArgumentNullException.ThrowIfNull(place);

        return CreatePlaceInfo(node, sourceText, place, null, null);
    }

    public static PlaceInfo CreatePlaceInfo(
        SyntaxNode node,
        SourceText sourceText,
        Place? place,
        string? fallbackName = null,
        string? fallbackKind = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(sourceText);

        var span = node.Span;
        var lineSpan = sourceText.Lines.GetLinePositionSpan(span);

        var displayName = place?.ToString() ?? fallbackName ?? node.ToString().Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "<expression>";
        }

        var kind = place?.Symbol.Kind.ToString() ?? fallbackKind ?? "Unknown";

        return new PlaceInfo
        {
            Name = displayName,
            Range = new LspRange
            {
                Start = new LspPosition(lineSpan.Start.Line, lineSpan.Start.Character),
                End = new LspPosition(lineSpan.End.Line, lineSpan.End.Character)
            },
            Kind = kind
        };
    }
}
