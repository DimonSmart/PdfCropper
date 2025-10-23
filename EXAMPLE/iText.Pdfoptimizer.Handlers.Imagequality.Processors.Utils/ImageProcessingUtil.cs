using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors.Utils;

public sealed class ImageProcessingUtil
{
	public static byte[] CompressJpeg(byte[] originalJpeg, double compressionLevel)
	{
		using MemoryStream stream = new MemoryStream(originalJpeg);
		using MemoryStream memoryStream = new MemoryStream();
		Bitmap bitmap = new Bitmap(stream);
		ImageCodecInfo encoder = GetEncoder(ImageFormat.Jpeg);
		Encoder quality = Encoder.Quality;
		EncoderParameters encoderParameters = new EncoderParameters(1);
		EncoderParameter encoderParameter = new EncoderParameter(quality, (long)(100.0 * compressionLevel));
		encoderParameters.Param[0] = encoderParameter;
		bitmap.Save(memoryStream, encoder, encoderParameters);
		bitmap.Dispose();
		return memoryStream.ToArray();
	}

	private static ImageCodecInfo GetEncoder(ImageFormat format)
	{
		ImageCodecInfo[] imageDecoders = ImageCodecInfo.GetImageDecoders();
		foreach (ImageCodecInfo imageCodecInfo in imageDecoders)
		{
			if (imageCodecInfo.FormatID == format.Guid)
			{
				return imageCodecInfo;
			}
		}
		return null;
	}
}
