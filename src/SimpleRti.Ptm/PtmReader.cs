using System.Globalization;
using System.Text;

namespace SimpleRti.Ptm;

public static class PtmReader
{
    public static PtmFile Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Read(stream);
    }

    public static PtmFile Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        var header = ReadHeader(reader);

        return header.Format switch
        {
            PtmFormat.Lrgb => ReadLrgb(reader, header),
            PtmFormat.Rgb => ReadRgb(reader, header),
            _ => throw new NotSupportedException($"Unsupported PTM format: {header.Format}")
        };
    }

    private static PtmHeader ReadHeader(BinaryReader reader)
    {
        string version = ReadLine(reader);
        if (version != "PTM_1.2")
            throw new FormatException($"Unsupported PTM version: '{version}'");

        string formatStr = ReadLine(reader);
        PtmFormat format = formatStr switch
        {
            "PTM_FORMAT_LRGB" => PtmFormat.Lrgb,
            "PTM_FORMAT_RGB" => PtmFormat.Rgb,
            _ => throw new FormatException($"Unsupported PTM format type: '{formatStr}'")
        };

        // Dimensions can be "width height" on one line, or width and height on separate lines
        string dimLine = ReadLine(reader);
        var dimParts = dimLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int width, height;
        if (dimParts.Length >= 2)
        {
            width = int.Parse(dimParts[0], CultureInfo.InvariantCulture);
            height = int.Parse(dimParts[1], CultureInfo.InvariantCulture);
        }
        else
        {
            width = int.Parse(dimParts[0], CultureInfo.InvariantCulture);
            height = int.Parse(ReadLine(reader), CultureInfo.InvariantCulture);
        }

        string scaleLine = ReadLine(reader);
        var scaleParts = scaleLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (scaleParts.Length < 6)
            throw new FormatException($"Expected 6 scale values, got {scaleParts.Length}");
        float[] scale = new float[6];
        for (int i = 0; i < 6; i++)
            scale[i] = float.Parse(scaleParts[i], CultureInfo.InvariantCulture);

        string biasLine = ReadLine(reader);
        var biasParts = biasLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (biasParts.Length < 6)
            throw new FormatException($"Expected 6 bias values, got {biasParts.Length}");
        int[] bias = new int[6];
        for (int i = 0; i < 6; i++)
            bias[i] = int.Parse(biasParts[i], CultureInfo.InvariantCulture);

        return new PtmHeader
        {
            Version = version,
            Format = format,
            Width = width,
            Height = height,
            Scale = scale,
            Bias = bias
        };
    }

    private static PtmFile ReadLrgb(BinaryReader reader, PtmHeader header)
    {
        int pixelCount = header.Width * header.Height;

        // Read raw coefficient bytes: 6 bytes per pixel, interleaved per pixel
        // File order: for each pixel (bottom-to-top, left-to-right): a0,a1,a2,a3,a4,a5
        byte[] rawCoeffs = reader.ReadBytes(pixelCount * 6);
        if (rawCoeffs.Length < pixelCount * 6)
            throw new FormatException("Unexpected end of PTM coefficient data");

        // Read RGB bytes: 3 bytes per pixel
        byte[] rawRgb = reader.ReadBytes(pixelCount * 3);
        if (rawRgb.Length < pixelCount * 3)
            throw new FormatException("Unexpected end of PTM RGB data");

        // Decode coefficients and flip Y
        float[][] coefficients = new float[6][];
        for (int c = 0; c < 6; c++)
            coefficients[c] = new float[pixelCount];

        byte[] rgb = new byte[pixelCount * 3];

        for (int srcY = 0; srcY < header.Height; srcY++)
        {
            int dstY = header.Height - 1 - srcY; // flip Y
            for (int x = 0; x < header.Width; x++)
            {
                int srcIdx = srcY * header.Width + x;
                int dstIdx = dstY * header.Width + x;

                for (int c = 0; c < 6; c++)
                {
                    byte raw = rawCoeffs[srcIdx * 6 + c];
                    coefficients[c][dstIdx] = (raw - header.Bias[c]) * header.Scale[c];
                }

                rgb[dstIdx * 3 + 0] = rawRgb[srcIdx * 3 + 0];
                rgb[dstIdx * 3 + 1] = rawRgb[srcIdx * 3 + 1];
                rgb[dstIdx * 3 + 2] = rawRgb[srcIdx * 3 + 2];
            }
        }

        return new PtmFile
        {
            Header = header,
            Coefficients = coefficients,
            Rgb = rgb
        };
    }

    private static PtmFile ReadRgb(BinaryReader reader, PtmHeader header)
    {
        int pixelCount = header.Width * header.Height;

        // RGB format: 3 color planes (R, G, B), each containing 6 bytes per pixel
        // (one byte per polynomial coefficient a0..a5).
        // Total: 3 * pixelCount * 6 = pixelCount * 18 bytes.
        // Layout: [Red: 6 coeffs per pixel × all pixels] [Green: same] [Blue: same]
        // We store as 18 output planes: [coeffIdx * 3 + channel]
        byte[] rawData = reader.ReadBytes(pixelCount * 18);
        if (rawData.Length < pixelCount * 18)
            throw new FormatException("Unexpected end of PTM RGB coefficient data");

        float[][] coefficients = new float[18][];
        for (int c = 0; c < 18; c++)
            coefficients[c] = new float[pixelCount];

        // Data is organized as 3 color planes (R=0, G=1, B=2).
        // Within each color plane, per pixel: a0, a1, a2, a3, a4, a5 (6 bytes).
        for (int ch = 0; ch < 3; ch++)
        {
            int planeOffset = ch * pixelCount * 6;

            for (int srcY = 0; srcY < header.Height; srcY++)
            {
                int dstY = header.Height - 1 - srcY;
                for (int x = 0; x < header.Width; x++)
                {
                    int srcIdx = srcY * header.Width + x;
                    int dstIdx = dstY * header.Width + x;

                    for (int coeffIdx = 0; coeffIdx < 6; coeffIdx++)
                    {
                        byte raw = rawData[planeOffset + srcIdx * 6 + coeffIdx];
                        coefficients[coeffIdx * 3 + ch][dstIdx] =
                            (raw - header.Bias[coeffIdx]) * header.Scale[coeffIdx];
                    }
                }
            }
        }

        return new PtmFile
        {
            Header = header,
            Coefficients = coefficients,
            Rgb = null
        };
    }

    private static string ReadLine(BinaryReader reader)
    {
        var sb = new StringBuilder();
        while (true)
        {
            byte b = reader.ReadByte();
            if (b == (byte)'\n')
                break;
            if (b != (byte)'\r')
                sb.Append((char)b);
        }
        return sb.ToString();
    }
}
