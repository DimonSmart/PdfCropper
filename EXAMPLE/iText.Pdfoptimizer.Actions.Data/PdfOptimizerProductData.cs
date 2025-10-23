using iText.Commons.Actions.Data;

namespace iText.Pdfoptimizer.Actions.Data;

public sealed class PdfOptimizerProductData
{
	private const string PDF_OPTIMIZER_PRODUCT_NAME = "pdfOptimizer";

	private const string PDF_OPTIMIZER_PUBLIC_PRODUCT_NAME = "pdfOptimizer";

	private const string PDF_OPTIMIZER_VERSION = "4.1.0";

	private const int PDF_OPTIMIZER_COPYRIGHT_SINCE = 2000;

	private const int PDF_OPTIMIZER_COPYRIGHT_TO = 2025;

	private static readonly ProductData PDF_OPTIMIZER_PRODUCT_DATA = new ProductData("pdfOptimizer", "pdfOptimizer", "4.1.0", 2000, 2025);

	private PdfOptimizerProductData()
	{
	}

	public static ProductData GetInstance()
	{
		return PDF_OPTIMIZER_PRODUCT_DATA;
	}
}
