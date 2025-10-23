using System;
using iText.Pdfoptimizer.Handlers.Util;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors.Scaling;

public class DroppingAlgorithm : IScalingAlgorithm
{
	public virtual BitmapImagePixels Scale(BitmapImagePixels original, double scaling)
	{
		int num = Math.Max((int)((double)original.GetWidth() * scaling), 1);
		int num2 = Math.Max((int)((double)original.GetHeight() * scaling), 1);
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(num, num2, original.GetBitsPerComponent(), original.GetNumberOfComponents());
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				int x = (int)((double)j / scaling);
				int y = (int)((double)i / scaling);
				bitmapImagePixels.SetPixel(j, i, original.GetPixel(x, y));
			}
		}
		return bitmapImagePixels;
	}
}
