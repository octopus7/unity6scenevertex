using NUnit.Framework;
using UnityEngine;

public sealed class PlacementEvaluationRendererTests
{
    [Test]
    public void CuratedJsonRendersNonEmpty1024x512Preview()
    {
        Assert.That(PlacementEvaluationDataLoader.TryLoad(out var catalog, out var layout, out var loadError), Is.True, loadError);

        var validation = PlacementValidation.Validate(catalog, layout);
        Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Errors));

        var texture = PlacementBitmapRenderer.Render(catalog, layout);
        try
        {
            Assert.That(texture.width, Is.EqualTo(PlacementEvaluationPaths.CanvasWidth));
            Assert.That(texture.height, Is.EqualTo(PlacementEvaluationPaths.CanvasHeight));

            var pixels = texture.GetPixels32();
            Assert.That(pixels.Length, Is.EqualTo(PlacementEvaluationPaths.CanvasWidth * PlacementEvaluationPaths.CanvasHeight));

            var first = pixels[0];
            var foundDifference = false;
            for (var index = 1; index < pixels.Length; index++)
            {
                if (!AreSameColor(first, pixels[index]))
                {
                    foundDifference = true;
                    break;
                }
            }

            Assert.That(foundDifference, Is.True, "Rendered preview should not be a flat single-color bitmap.");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static bool AreSameColor(Color32 left, Color32 right)
    {
        return left.r == right.r && left.g == right.g && left.b == right.b && left.a == right.a;
    }
}
