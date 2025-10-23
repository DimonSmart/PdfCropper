using System;
using System.Collections.Generic;
using iText.Pdfoptimizer.Handlers;
using iText.Pdfoptimizer.Handlers.Imagequality.Processors;
using iText.Pdfoptimizer.Handlers.Imagequality.Processors.Scaling;

namespace iText.Pdfoptimizer;

public sealed class PdfOptimizerFactory
{
	private const double LOW_COMPRESSION_PARAMETER = 0.5;

	private const double MID_COMPRESSION_PARAMETER = 0.25;

	private const double HIGH_COMPRESSION_PARAMETER = 0.15;

	private const double LOW_SCALING_PARAMETER = 0.8;

	private const double MID_SCALING_PARAMETER = 0.5;

	private PdfOptimizerFactory()
	{
	}

	public static PdfOptimizer GetPdfOptimizerByProfile(PdfOptimizerProfile profile)
	{
		return profile switch
		{
			PdfOptimizerProfile.LOSSLESS_COMPRESSION => BuildLosslessOptimizer(), 
			PdfOptimizerProfile.LOW_COMPRESSION => BuildLowOptimizer(), 
			PdfOptimizerProfile.MID_COMPRESSION => BuildMidOptimizer(), 
			PdfOptimizerProfile.HIGH_COMPRESSION => BuildHighOptimizer(), 
			PdfOptimizerProfile.CUSTOM => new PdfOptimizer(), 
			_ => throw new ArgumentException("Profile cannot be null!"), 
		};
	}

	private static PdfOptimizer BuildLosslessOptimizer()
	{
		IList<AbstractOptimizationHandler> list = new List<AbstractOptimizationHandler>();
		list.Add(new FontDuplicationOptimizer());
		list.Add(new PdfXObjectDuplicationOptimizer());
		list.Add(new CompressionOptimizer());
		list.Add(new FontSubsettingOptimizer());
		list.Add(new FontMergingOptimizer());
		return new PdfOptimizer(PdfOptimizerProfile.LOSSLESS_COMPRESSION, list);
	}

	private static PdfOptimizer BuildLowOptimizer()
	{
		IList<AbstractOptimizationHandler> list = new List<AbstractOptimizationHandler>();
		list.Add(new FontDuplicationOptimizer());
		list.Add(new PdfXObjectDuplicationOptimizer());
		list.Add(new CompressionOptimizer());
		list.Add(new FontSubsettingOptimizer());
		list.Add(new FontMergingOptimizer());
		IImageProcessor imageProcessor = new CombinedImageProcessor().AddProcessor(new BitmapDeindexer()).AddProcessor(new BitmapScalingProcessor(0.8)).AddProcessor(new JpegCompressor(0.5))
			.AddProcessor(new BitmapIndexer());
		list.Add(new ImageQualityOptimizer().SetPngProcessor(imageProcessor).SetTiffProcessor(imageProcessor).SetJpegProcessor(new JpegCompressor(0.5)));
		return new PdfOptimizer(PdfOptimizerProfile.LOW_COMPRESSION, list);
	}

	private static PdfOptimizer BuildMidOptimizer()
	{
		IList<AbstractOptimizationHandler> list = new List<AbstractOptimizationHandler>();
		list.Add(new FontDuplicationOptimizer());
		list.Add(new PdfXObjectDuplicationOptimizer());
		list.Add(new CompressionOptimizer());
		list.Add(new FontSubsettingOptimizer());
		list.Add(new FontMergingOptimizer());
		IImageProcessor imageProcessor = new CombinedImageProcessor().AddProcessor(new BitmapDeindexer()).AddProcessor(new BitmapScalingProcessor(0.5)).AddProcessor(new JpegCompressor(0.25))
			.AddProcessor(new BitmapIndexer());
		list.Add(new ImageQualityOptimizer().SetPngProcessor(imageProcessor).SetTiffProcessor(imageProcessor).SetJpegProcessor(new JpegCompressor(0.25)));
		return new PdfOptimizer(PdfOptimizerProfile.MID_COMPRESSION, list);
	}

	private static PdfOptimizer BuildHighOptimizer()
	{
		IList<AbstractOptimizationHandler> list = new List<AbstractOptimizationHandler>();
		list.Add(new FontDuplicationOptimizer());
		list.Add(new PdfXObjectDuplicationOptimizer());
		list.Add(new CompressionOptimizer());
		list.Add(new FontSubsettingOptimizer());
		list.Add(new FontMergingOptimizer());
		IImageProcessor imageProcessor = new BitmapCompressor(0.5, new AverageCalculationAlgorithm(), 0.15);
		list.Add(new ImageQualityOptimizer().SetPngProcessor(imageProcessor).SetTiffProcessor(imageProcessor).SetJpegProcessor(new JpegCompressor(0.15)));
		return new PdfOptimizer(PdfOptimizerProfile.HIGH_COMPRESSION, list);
	}
}
