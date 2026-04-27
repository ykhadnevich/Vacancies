namespace Infrastructure.RelevancePipeline.Stage2_Embedding;

public static class CosineSimilarityCalculator
{
    public static float Calculate(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must have same dimension");

        var dotProduct = vectorA.Zip(vectorB, (a, b) => a * b).Sum();
        var magnitudeA = MathF.Sqrt(vectorA.Sum(a => a * a));
        var magnitudeB = MathF.Sqrt(vectorB.Sum(b => b * b));

        if (magnitudeA == 0 || magnitudeB == 0) return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}