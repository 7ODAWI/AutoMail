using Microsoft.Extensions.Configuration;
using System;

namespace AutoMail.BulkEmail.Ai
{
    public class GeminiAiOptions
    {
        public bool Enabled { get; set; } = true;
        public string ApiKey { get; set; }
        public string ModelRoute { get; set; } = "balanced";
        public string QualityModel { get; set; } = "gemini-flash-latest";
        public string SpeedModel { get; set; } = "gemini-flash-latest";
        public int TimeoutMs { get; set; } = 120000;
        public double Temperature { get; set; } = 0.9;
        public double SimilarityThreshold { get; set; } = 0.78;

        public static GeminiAiOptions FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("BulkEmail:Gemini");
            var options = section.Get<GeminiAiOptions>() ?? new GeminiAiOptions();

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            }

            options.Normalize();
            return options;
        }

        public void Normalize()
        {
            ModelRoute = string.IsNullOrWhiteSpace(ModelRoute) ? "balanced" : ModelRoute.Trim().ToLowerInvariant();
            QualityModel = string.IsNullOrWhiteSpace(QualityModel) ? "gemini-flash-latest" : QualityModel.Trim();
            SpeedModel = string.IsNullOrWhiteSpace(SpeedModel) ? "gemini-flash-latest" : SpeedModel.Trim();
            TimeoutMs = Math.Max(5000, TimeoutMs);
            Temperature = Math.Clamp(Temperature, 0, 2);
            SimilarityThreshold = Math.Clamp(SimilarityThreshold, 0.4, 0.95);
        }
    }
}