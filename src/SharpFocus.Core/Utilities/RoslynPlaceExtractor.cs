using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Utilities;

/// <summary>
/// Roslyn-based implementation that converts operations into <see cref="Place"/> instances.
/// </summary>
public sealed class RoslynPlaceExtractor : IPlaceExtractor
{
    /// <inheritdoc />
    public Place? TryCreatePlace(IOperation? operation)
    {
        return TryCreatePlaceInternal(operation);
    }

    private Place? TryCreatePlaceInternal(IOperation? operation)
    {
        if (operation is null)
            return null;

        return operation switch
        {
            ILocalReferenceOperation local => CreatePlace(local.Local),
            IParameterReferenceOperation parameter => CreatePlace(parameter.Parameter),
            IFieldReferenceOperation field => CreateMemberPlace(field.Field, field.Instance, field.Field.IsStatic),
            IPropertyReferenceOperation property => CreateMemberPlace(property.Property, property.Instance, property.Property.IsStatic),
            IEventReferenceOperation @event => CreateMemberPlace(@event.Event, @event.Instance, @event.Event.IsStatic),
            IArrayElementReferenceOperation arrayElement => TryCreatePlaceInternal(arrayElement.ArrayReference),
            IConversionOperation conversion => TryCreatePlaceInternal(conversion.Operand),
            IParenthesizedOperation parenthesized => TryCreatePlaceInternal(parenthesized.Operand),
            IAwaitOperation awaitOperation => TryCreatePlaceInternal(awaitOperation.Operation),
            IConditionalAccessOperation conditional => TryCreatePlaceInternal(conditional.WhenNotNull),
            _ => null
        };
    }

    private Place? CreateMemberPlace(ISymbol member, IOperation? instance, bool isStatic)
    {
        if (isStatic)
            return CreatePlace(member);

        var basePlace = TryCreatePlaceInternal(instance);
        if (basePlace == null)
            return CreatePlace(member);

        return basePlace.WithProjection(member);
    }

    private static Place CreatePlace(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return new Place(symbol);
    }
}
