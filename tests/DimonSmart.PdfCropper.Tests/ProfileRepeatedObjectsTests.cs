using Xunit;
using DimonSmart.PdfCropper;

namespace DimonSmart.PdfCropper.Tests
{
    public class ProfileRepeatedObjectsTests
    {
        [Fact]
        public void SimpleProfile_ShouldNotDetectRepeatedObjects()
        {
            var profile = PdfCropProfiles.Simple;
            Assert.False(profile.CropSettings.DetectRepeatedObjects);
        }

        [Fact]
        public void EbookProfile_ShouldDetectRepeatedObjects()
        {
            var profile = PdfCropProfiles.Ebook;
            Assert.True(profile.CropSettings.DetectRepeatedObjects);
            Assert.True(profile.CropSettings.ExcludeEdgeTouchingObjects);
            Assert.Equal(1.0f, profile.CropSettings.Margin);
            Assert.Equal(40.0, profile.CropSettings.RepeatedObjectOccurrenceThreshold);
        }

        [Fact]
        public void AggressiveProfile_ShouldDetectRepeatedObjects()
        {
            var profile = PdfCropProfiles.Aggressive;
            Assert.True(profile.CropSettings.DetectRepeatedObjects);
            Assert.True(profile.CropSettings.ExcludeEdgeTouchingObjects);
            Assert.Equal(0.25f, profile.CropSettings.Margin);
            Assert.Equal(40.0, profile.CropSettings.RepeatedObjectOccurrenceThreshold);
        }

        [Fact]
        public void ProfileByKey_ShouldReturnCorrectSettings()
        {
            Assert.True(PdfCropProfiles.TryGet("ebook", out var ebook));
            Assert.True(ebook.CropSettings.DetectRepeatedObjects);

            Assert.True(PdfCropProfiles.TryGet("aggressive", out var aggressive));
            Assert.True(aggressive.CropSettings.DetectRepeatedObjects);

            Assert.True(PdfCropProfiles.TryGet("simple", out var simple));
            Assert.False(simple.CropSettings.DetectRepeatedObjects);
        }

        [Fact]
        public void CropSettingsDefault_ShouldHaveCorrectRepeatedThreshold()
        {
            var defaultSettings = CropSettings.Default;
            Assert.Equal(40.0, defaultSettings.RepeatedObjectOccurrenceThreshold);
        }
    }
}