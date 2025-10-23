using System;
using iText.Commons.Utils;
using iText.Pdfoptimizer.Handlers.Util;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors.Scaling;

public class AverageCalculationAlgorithm : IScalingAlgorithm
{
	public virtual BitmapImagePixels Scale(BitmapImagePixels original, double scaling)
	{
		int num = Math.Max((int)((double)original.GetWidth() * scaling), 1);
		int num2 = Math.Max((int)((double)original.GetHeight() * scaling), 1);
		double widthScaling = (double)num / (double)original.GetWidth();
		double heightScaling = (double)num2 / (double)original.GetHeight();
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(num, num2, original.GetBitsPerComponent(), original.GetNumberOfComponents());
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				bitmapImagePixels.SetPixel(j, i, CalculatePixel(j, i, widthScaling, heightScaling, original));
			}
		}
		return bitmapImagePixels;
	}

	private static double[] CalculatePixel(int x, int y, double widthScaling, double heightScaling, BitmapImagePixels original)
	{
		double num = (double)x / widthScaling;
		double num2 = (double)(x + 1) / widthScaling;
		int num3 = Math.Max(0, (int)MathematicUtil.Round(Math.Floor(num)));
		int num4 = Math.Min(original.GetWidth(), (int)MathematicUtil.Round(Math.Ceiling(num2)));
		double num5 = (double)y / heightScaling;
		double num6 = (double)(y + 1) / heightScaling;
		int num7 = Math.Max(0, (int)MathematicUtil.Round(Math.Floor(num5)));
		int num8 = Math.Min(original.GetHeight(), (int)MathematicUtil.Round(Math.Ceiling(num6)));
		double[] array = new double[original.GetNumberOfComponents()];
		for (int i = num3; i < num4; i++)
		{
			for (int j = num7; j < num8; j++)
			{
				double[] pixel = original.GetPixel(i, j);
				double num9 = Min(1.0, (double)(i + 1) - num, num2 - (double)i) * Min(1.0, (double)(j + 1) - num5, num6 - (double)j) * (widthScaling * heightScaling);
				for (int k = 0; k < original.GetNumberOfComponents(); k++)
				{
					array[k] += num9 * pixel[k];
				}
			}
		}
		return array;
	}

	private static double Min(params double[] values)
	{
		double num = values[0];
		for (int i = 1; i < values.Length; i++)
		{
			num = Math.Min(num, values[i]);
		}
		return num;
	}
}
