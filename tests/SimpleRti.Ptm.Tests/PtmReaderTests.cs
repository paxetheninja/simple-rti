using System.Globalization;
using System.Text;
using SimpleRti.Ptm;

namespace SimpleRti.Ptm.Tests;

public class PtmReaderTests
{
    /// <summary>
    /// Creates a minimal 2x2 LRGB PTM file in memory for testing.
    /// </summary>
    private static byte[] CreateTestLrgbPtm(int width, int height, float[] scale, int[] bias,
        byte[] coeffData, byte[] rgbData)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);
        writer.NewLine = "\n";
        writer.WriteLine("PTM_1.2");
        writer.WriteLine("PTM_FORMAT_LRGB");
        writer.WriteLine($"{width} {height}");
        writer.WriteLine(string.Join(" ", scale.Select(s => s.ToString(CultureInfo.InvariantCulture))));
        writer.WriteLine(string.Join(" ", bias.Select(b => b.ToString(CultureInfo.InvariantCulture))));
        writer.Flush();
        ms.Write(coeffData);
        ms.Write(rgbData);
        return ms.ToArray();
    }

    [Fact]
    public void Read_Lrgb_ParsesHeaderCorrectly()
    {
        float[] scale = [1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f];
        int[] bias = [0, 0, 0, 0, 0, 0];
        byte[] coeffs = new byte[4 * 6]; // 2x2, 6 coefficients per pixel
        byte[] rgb = new byte[4 * 3];    // 2x2, 3 bytes per pixel

        var data = CreateTestLrgbPtm(2, 2, scale, bias, coeffs, rgb);
        using var stream = new MemoryStream(data);

        var ptm = PtmReader.Read(stream);

        Assert.Equal("PTM_1.2", ptm.Header.Version);
        Assert.Equal(PtmFormat.Lrgb, ptm.Header.Format);
        Assert.Equal(2, ptm.Header.Width);
        Assert.Equal(2, ptm.Header.Height);
    }

    [Fact]
    public void Read_Lrgb_DecodesCoefficientsWithScaleAndBias()
    {
        float[] scale = [2.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f];
        int[] bias = [10, 0, 0, 0, 0, 0];

        // 1x1 pixel: coefficients a0=20, a1=100, a2=50, a3=30, a4=60, a5=128
        byte[] coeffs = [20, 100, 50, 30, 60, 128];
        byte[] rgb = [255, 128, 64];

        var data = CreateTestLrgbPtm(1, 1, scale, bias, coeffs, rgb);
        using var stream = new MemoryStream(data);

        var ptm = PtmReader.Read(stream);

        // a0 = (20 - 10) * 2.0 = 20.0
        Assert.Equal(20.0f, ptm.Coefficients[0][0]);
        // a1 = (100 - 0) * 1.0 = 100.0
        Assert.Equal(100.0f, ptm.Coefficients[1][0]);

        Assert.NotNull(ptm.Rgb);
        Assert.Equal(255, ptm.Rgb![0]);
        Assert.Equal(128, ptm.Rgb[1]);
        Assert.Equal(64, ptm.Rgb[2]);
    }

    [Fact]
    public void Read_Lrgb_FlipsYAxis()
    {
        float[] scale = [1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f];
        int[] bias = [0, 0, 0, 0, 0, 0];

        // 2x2 image: bottom row first in file
        // Pixel (0,0) in file = bottom-left, a5=10
        // Pixel (1,0) in file = bottom-right, a5=20
        // Pixel (0,1) in file = top-left, a5=30
        // Pixel (1,1) in file = top-right, a5=40
        byte[] coeffs = new byte[4 * 6];
        // Row 0 (bottom row in file): pixel 0 and pixel 1
        coeffs[0 * 6 + 5] = 10; // bottom-left, a5
        coeffs[1 * 6 + 5] = 20; // bottom-right, a5
        // Row 1 (top row in file): pixel 2 and pixel 3
        coeffs[2 * 6 + 5] = 30; // top-left, a5
        coeffs[3 * 6 + 5] = 40; // top-right, a5

        byte[] rgb = new byte[4 * 3];

        var data = CreateTestLrgbPtm(2, 2, scale, bias, coeffs, rgb);
        using var stream = new MemoryStream(data);

        var ptm = PtmReader.Read(stream);

        // After Y flip: top row in output = file's top row (row 1)
        // Output index 0 = top-left = file's (0,1) = 30
        // Output index 1 = top-right = file's (1,1) = 40
        // Output index 2 = bottom-left = file's (0,0) = 10
        // Output index 3 = bottom-right = file's (1,0) = 20
        Assert.Equal(30.0f, ptm.Coefficients[5][0]); // top-left
        Assert.Equal(40.0f, ptm.Coefficients[5][1]); // top-right
        Assert.Equal(10.0f, ptm.Coefficients[5][2]); // bottom-left
        Assert.Equal(20.0f, ptm.Coefficients[5][3]); // bottom-right
    }

    [Fact]
    public void Read_InvalidVersion_Throws()
    {
        var data = Encoding.ASCII.GetBytes("PTM_2.0\nPTM_FORMAT_LRGB\n1 1\n1 1 1 1 1 1\n0 0 0 0 0 0\n");
        using var stream = new MemoryStream(data);

        Assert.Throws<FormatException>(() => PtmReader.Read(stream));
    }

    [Fact]
    public void Read_UnsupportedFormat_Throws()
    {
        var data = Encoding.ASCII.GetBytes("PTM_1.2\nPTM_FORMAT_JPEG_LRGB\n1 1\n1 1 1 1 1 1\n0 0 0 0 0 0\n");
        using var stream = new MemoryStream(data);

        Assert.Throws<FormatException>(() => PtmReader.Read(stream));
    }
}
