using Godot;
using ZXing;
using ZXing.Common;

public partial class QRCodeGenerator : Node
{
    public ImageTexture GenerateQRCode(string text, int size = 256)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions { Width = size, Height = size, Margin = 1 }
        };

        var pixelData = writer.Write(text);
        var image = Image.CreateFromData(size, size, false, Image.Format.Rgba8, pixelData.Pixels);
        return ImageTexture.CreateFromImage(image);
    }
}
