namespace SimpleRti.Ptm;

public sealed class PtmHeader
{
    public required string Version { get; init; }
    public required PtmFormat Format { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required float[] Scale { get; init; }
    public required int[] Bias { get; init; }
}
