using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// LSP method names for SharpFocus custom commands.
/// </summary>
public static class SharpFocusMethods
{
    /// <summary>
    /// Focus command - highlights dependencies for a selected place.
    /// </summary>
    public const string Focus = "sharpfocus/focus";

    /// <summary>
    /// Focus Mode command - returns aggregated focus analysis for highlight rendering.
    /// </summary>
    public const string FocusMode = "sharpfocus/focusMode";

    /// <summary>
    /// Flow Analysis command - returns both backward and forward slices.
    /// </summary>
    public const string FlowAnalysis = "sharpfocus/flowAnalysis";

    /// <summary>
    /// Backward slice command - finds all statements that affect a selected place.
    /// </summary>
    public const string BackwardSlice = "sharpfocus/backwardSlice";

    /// <summary>
    /// Forward slice command - finds all statements affected by a selected place.
    /// </summary>
    public const string ForwardSlice = "sharpfocus/forwardSlice";
}

[Method(SharpFocusMethods.Focus, Direction.ClientToServer)]
public interface IFocusHandler : IJsonRpcRequestHandler<FocusRequest, FocusResponse?>
{
}

[Method(SharpFocusMethods.FocusMode, Direction.ClientToServer)]
public interface IFocusModeHandler : IJsonRpcRequestHandler<FocusModeRequest, FocusModeResponse?>
{
}

[Method(SharpFocusMethods.FlowAnalysis, Direction.ClientToServer)]
public interface IFlowAnalysisHandler : IJsonRpcRequestHandler<FlowAnalysisRequest, FlowAnalysisResponse?>
{
}

[Method(SharpFocusMethods.BackwardSlice, Direction.ClientToServer)]
public interface IBackwardSliceHandler : IJsonRpcRequestHandler<BackwardSliceRequest, SliceResponse?>
{
}

[Method(SharpFocusMethods.ForwardSlice, Direction.ClientToServer)]
public interface IForwardSliceHandler : IJsonRpcRequestHandler<ForwardSliceRequest, SliceResponse?>
{
}
