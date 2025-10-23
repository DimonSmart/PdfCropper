namespace iText.Pdfoptimizer.Exceptions;

public sealed class PdfOptimizerExceptionMessageConstant
{
	public const string INVALID_COMPRESSION_PARAMETER = "Invalid compression parameter! Value {0} is out of range [0, 1]";

	public const string INVALID_SCALING_PARAMETER = "Invalid scaling parameter! Value {0} is out of range (0, 1]";

	public const string PIXEL_OUT_OF_BORDERS = "Pixel ({0}, {1}) is out of borders of the picture with parameter {2} x {3}";

	public const string LENGTH_OF_ARRAY_SHOULD_MATCH_NUMBER_OF_COMPONENTS = "Length of pixel array ({0}) should match number of components ({1})";

	public const string MASKED_COLORS_HAVE_DIFFERENT_LENGTHS = "Minimum and maximum masked colors have different number of components: {0} and {1}.";

	public const string MASK_ARRAY_SHOULD_HAVE_EVEN_POSITIVE_LENGTHS = "Mask array has invalid length {0}. It should have even positive length";

	public const string MASK_ARRAY_DOES_NOT_CORRESPOND_WITH_CONVERTER = "Mask array does not correspond with the converter! Its length is {0} but expected length is {1}";

	public const string PROFILE_CANNOT_BE_NULL = "Profile cannot be null!";

	public const string INVALID_DATA_LENGTH = "Invalid data length, expected length = {0}, actual length = {1}";

	public const string CAN_NOT_CONVERT_SHADING_PATTERN_COLOR_SPACE = "Can't convert color space of shading pattern, PDF\\A conformance will be compromised.";

	public const string CAN_NOT_CONVERT_SEPARATION_COLOR_SPACE = "Can't convert separation color space, PDF\\A conformance will be compromised.";

	public const string CAN_NOT_CONVERT_DEVICEN_COLOR_SPACE = "Can't convert deviceN color space, PDF\\A conformance will be compromised.";

	public const string CAN_NOT_CONVERT_TRANSPARENCY_XOBJECT_GROUP_COLOR_SPACE = "Can't convert color space of transparency xObject group, PDF\\A conformance will be compromised.";

	public const string CAN_NOT_CONVERT_DEVICE_COLOR_SPACE_FOR_NON_BITMAP = "Can't convert original device color space for non-bitmap image, PDF\\A conformance will be compromised.";

	public const string CAN_NOT_CONVERT_INDEXED_COLOR_SPACE_FOR_NON_BITMAP = "Can't convert indexed color space based on original color space for non-bitmap image, PDF\\A conformance will be compromised.";

	public const string INVALID_OUTPUT_INTENT_SUBTYPE = "Invalid output intent subtype, should be GTS_PDFA1.";

	public const string INVALID_OUTPUT_INTENT_ICC_PROFILE_COLOR_SPACE = "Invalid output intent Icc profile color space, expected = {0}, actual = {1}.";

	public const string OUTPUT_INTENT_WAS_NOT_SET = "PDF/A document color space is under color conversion, but new output intent is not set. Either set new output intent or ignore PDF/A conformance in CsConverterProperties.";

	public const string INVALID_DECODE_ARRAY = "Invalid decode array.";

	public const string INVALID_COLOR_TO_DECODE = "Invalid color to decode.";

	private PdfOptimizerExceptionMessageConstant()
	{
	}
}
