namespace iText.Pdfoptimizer.Report.Message;

public sealed class ReportMessageConstants
{
	public const string WRITER_PROPERTIES_ARE_NOT_ACCESSIBLE = "WriterProperties are not accessible!";

	public const string RESOURCES_FOR_CONVERSION_ARE_NOT_ACCESSIBLE = "Resources for conversion are not accessible!";

	public const string CONVERTED_CONTENT_STREAMS_ARE_NOT_ACCESSIBLE = "Converted content streams are not accessible!";

	public const string CURRENT_RESOURCES_ARE_NOT_ACCESSIBLE = "Current resources are not accessible!";

	public const string IS_PDF_A_DOCUMENT_ARE_NOT_ACCESSIBLE = "Is PDF\\A document key are not accessible!";

	public const string XOBJECT_DUPLICATIONS_FOUND = "Amount of found xObject duplications: {0}";

	public const string NO_XOBJECT_DUPLICATION_FOUND = "No xObject duplication found";

	public const string FONT_DUPLICATIONS_FOUND = "Amount of found font duplications: {0}";

	public const string NO_FONT_DUPLICATION_FOUND = "No font duplication found";

	public const string FONT_SUBSETTING_CANNOT_BE_USED_FOR_PDFA1 = "Unable to subset fonts for PDF/A-1 document. Font subsetting will not be applied to optimize the document.";

	public const string FONT_SUBSET_SKIPPED = "Font subset creation is skipped for {0}: {1}";

	public const string UNABLE_SUBSET_FONTS = "Unable to subset document fonts";

	public const string UNABLE_SUBSET_FONT = "Unable to subset document font: {0}";

	public const string UNABLE_SUBSET_FONT_DESCRIPTOR_SHARED = "Unable to subset document font, its font descriptor is shared with other fonts: {0}";

	public const string UNABLE_SUBSET_FONT_NOT_USED = "Unable to subset document font, no used glyphs were found: {0}";

	public const string UNABLE_SUBSET_FONT_UNKNOWN_GLYPHS = "Unable to subset document font, not all used glyphs were decoded: {0}";

	public const string GLYPHS_FOUND_SUCCESSFULLY = "Glyphs in document were found successfully.";

	public const string CANVAS_PROCESSOR_ARE_NOT_ACCESSIBLE = "Canvas processor are not accessible!";

	public const string UNABLE_TO_PROCESS_PAGE_GLYPHS = "Unable to process glyphs in page content stream with reference {0}.";

	public const string UNABLE_TO_PROCESS_RESOURCES_GLYPHS = "Unable to process glyphs of the content stream resources.";

	public const string PROCESSED_CONTENT_STREAMS_ARE_NOT_ACCESSIBLE = "Processed content streams are not accessible!";

	public const string USED_GLYPHS_EVENT_LISTENER_ARE_NOT_ACCESSIBLE = "Used glyphs event listener are not accessible!";

	public const string RESOURCE_FOR_STREAM_PROCESSING_ARE_NOT_ACCESSIBLE = "Resource for stream processing are not accessible!";

	public const string FONT_MERGING_CANNOT_BE_USED_FOR_PDFA1 = "Unable to merge fonts for PDF/A-1 document. Font merging will not be applied to optimize the document.";

	public const string UNABLE_MERGE_FONTS = "Unable to merge document fonts: {0}";

	public const string UNSUPPORTED_FONT_TYPE_MERGE = "Fonts merging is skipped for {0} because of unsupported font type.";

	public const string NO_FONTS_FOR_MERGE = "Fonts for merging are not found.";

	public const string FONTS_AMOUNT_FOR_MERGE = "Amount of found fonts groups to merge: {0}";

	public const string DIFFERENT_WIDTHS_DURING_FONT_MERGE = "Fonts merging is skipped for {0} because of incompatibility of Widths arrays.";

	public const string DIFFERENT_W_DURING_FONT_MERGE = "Fonts merging is skipped for {0} because of incompatibility of W arrays.";

	public const string DIFFERENT_TO_UNICODE_DURING_FONT_MERGE = "Fonts merging is skipped for {0} because of incompatibility of ToUnicode streams.";

	public const string EXCEPTION_DURING_FONTS_MERGING = "Fonts merging is skipped for {0} because of: {1}";

	public const string EXCEPTION_WHILE_FONT_PARSING = "An exception occurred while font parsing.";

	public const string UNABLE_TO_OPTIMIZE_IMAGE = "Unable to optimize image with reference {0} of type {1}";

	public const string COLOR_SPACE_IS_NOT_SUPPORTED = "Color space {0} is not supported by image processor {1}. Unable to optimize image with reference {2}";

	public const string FILTER_IS_NOT_SUPPORTED = "Filter {0} is not supported by image processor {1}. Unable to optimize image with reference {2}";

	public const string IMAGE_WAS_OPTIMIZED = "Image with reference {0} was optimized.";

	public const string IMAGE_INCREASED_SIZE_AFTER_OPTIMIZATION = "Image with reference {0} has increased size after optimization, the original image will be saved.";

	public const string IMAGE_COLOR_SPACE_WAS_CONVERTED = "Color space of the image with reference {0} was converted.";

	public const string UNABLE_TO_CONVERT_IMAGE_COLOR_SPACE = "Unable to convert color space of the image with reference {0} of type {1}.";

	public const string COLOR_SPACE_CONVERTER_NOT_INSTALLED = "No color space converter was installed.";

	public const string UNABLE_TO_CONVERT_IMAGE_WITH_SMASK_WITH_MATTE_FIELD = "Unable to convert color space of the image with reference {0} which contain sMask with Matte field.";

	public const string STREAM_COLOR_SPACE_WAS_CONVERTED = "Color space of the content stream with reference {0} was converted.";

	public const string UNABLE_TO_CONVERT_STREAM_COLOR_SPACE = "Unable to convert color space of the content stream with reference {0}.";

	public const string RESOURCES_COLOR_SPACE_WAS_CONVERTED = "Color space of the content stream resources was converted.";

	public const string UNABLE_TO_CONVERT_RESOURCES_COLOR_SPACE = "Unable to convert color space of the content stream resources.";

	public const string AP_COLOR_SPACE_WAS_CONVERTED = "Color space of the appearance stream with reference {0} was converted.";

	public const string UNABLE_TO_CONVERT_AP_COLOR_SPACE = "Unable to convert color space of the appearance stream with reference {0}.";

	public const string PDF_A_ID_SCHEMAS_WERE_REMOVED = "PDF\\A id schemas were removed from PDF XMP metadata.";

	public const string UNABLE_TO_REMOVE_PDF_A_ID_SCHEMAS = "Unable to remove PDF\\A id schemas from PDF XMP metadata.";

	public const string INLINE_IMAGE_WAS_CONVERTED = "Inline image was converted.";

	public const string UNABLE_TO_CONVERT_INLINE_IMAGE = "Unable to convert inline image.";

	public const string INLINE_IMAGE_WAS_TRANSFORMED_TO_XOBJECT = "Inline image was transformed to xObject.";

	public const string OUTPUT_INTENT_WAS_REPLACED = "Output intent was replaced.";

	public const string UNABLE_TO_CONVERT_SEPARATION_COLOR_SPACE = "Unable to convert separation color space.";

	public const string UNABLE_TO_CONVERT_DEVICEN_COLOR_SPACE = "Unable to convert deviceN color space.";

	public const string UNABLE_TO_CONVERT_SHADING_PATTERN_COLOR_SPACE = "Unable to convert shading pattern color space.";

	public const string UNABLE_TO_CONVERT_TRANSPARENCY_GROUP_COLOR_SPACE = "Unable to convert transparency group color space.";

	public const string UNABLE_TO_CONVERT_DEVICE_COLOR_SPACE_FOR_NON_BITMAP = "Unable to convert device color space for non-bitmap image.";

	public const string UNABLE_TO_CONVERT_INDEXED_COLOR_SPACE_FOR_NON_BITMAP = "Unable to convert indexed color space based on device color space for non-bitmap image.";

	private ReportMessageConstants()
	{
	}
}
