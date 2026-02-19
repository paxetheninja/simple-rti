namespace SimpleRti.Ptm;

public sealed class PtmFile
{
    public required PtmHeader Header { get; init; }

    /// <summary>
    /// 6 coefficient planes, each Width*Height floats.
    /// Stored in top-to-bottom scanline order (flipped from file's bottom-to-top).
    /// </summary>
    public required float[][] Coefficients { get; init; }

    /// <summary>
    /// RGB color data for LRGB format: Width*Height*3 bytes (R,G,B per pixel).
    /// Null for RGB format (coefficients contain per-channel data).
    /// </summary>
    public byte[]? Rgb { get; init; }
}
