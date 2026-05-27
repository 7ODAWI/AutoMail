using Abp.Dependency;
using Abp.UI;
using AutoMail.BulkEmail.Dto;
using AutoMail.Project_Models;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail.Ai
{
    public class AiTemplateGenerationService : IAiTemplateGenerationService, ITransientDependency
    {
        private const int MaxVariantsPerRequest = 50;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly GeminiAiOptions _options;
        private readonly Client _genAiClient;

        public AiTemplateGenerationService(IConfiguration configuration)
        {
            _options = GeminiAiOptions.FromConfiguration(configuration);
            if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _genAiClient = new Client(apiKey: _options.ApiKey);
            }
        }

        public async Task<List<AiTemplateVariantDto>> GenerateTemplatesAsync(
            EmailOperation operation,
            List<EmailTemplate> historicalTemplates,
            GenerateAiTemplatesInput input,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var request = new EngineGenerateRequest
            {
                CorrelationId = Guid.NewGuid().ToString("N"),
                Operation = new EngineOperationContext
                {
                    OperationId = operation.Id,
                    Subject = operation.Subject,
                    Body = operation.Body,
                    Prompt = string.IsNullOrWhiteSpace(input.Prompt) ? operation.AiPrompt : input.Prompt,
                    Tone = string.IsNullOrWhiteSpace(input.Tone) ? operation.AiTone : input.Tone,
                    VariantCount = input.VariantCount,
                    BatchIndex = 1,
                    TotalRequestedVariants = input.VariantCount
                },
                HistoricalTemplates = BuildTemplateContexts(operation, historicalTemplates)
            };

            var response = await GenerateWithGeminiOrFallbackAsync(request, cancellationToken);

            if (response == null)
            {
                throw new UserFriendlyException("AI engine response could not be parsed.");
            }

            if (!response.Success)
            {
                throw new UserFriendlyException($"AI engine generation failed: {response.ErrorMessage ?? "Unknown error."}");
            }

            if (response.Variants == null || response.Variants.Count == 0)
            {
                throw new UserFriendlyException("AI engine returned no template variants.");
            }

            return response.Variants.Select(v => new AiTemplateVariantDto
            {
                Subject = v.Subject,
                PreviewText = v.PreviewText,
                BodyHtml = v.BodyHtml,
                Weight = v.Weight <= 0 ? 1 : v.Weight,
                OutlineJson = v.OutlineJson,
                ComponentOrderJson = v.ComponentOrderJson,
                ModelName = v.ModelName,
                PromptVersion = v.PromptVersion,
                InputTokens = v.InputTokens,
                OutputTokens = v.OutputTokens,
                LatencyMs = v.LatencyMs,
                SimilarityScore = v.SimilarityScore
            }).ToList();
        }

        private async Task<EngineGenerateResponse> GenerateWithGeminiOrFallbackAsync(
            EngineGenerateRequest request,
            CancellationToken cancellationToken)
        {
            var model = ResolveModel(request.Operation.VariantCount);
            var aggregated = new List<EngineVariant>();
            var remaining = Math.Max(1, request.Operation.VariantCount);
            var batchIndex = 0;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchSize = Math.Min(MaxVariantsPerRequest, remaining);
                var batchRequest = CreateBatchRequest(request, aggregated, batchSize, batchIndex + 1);
                var generated = await TryGenerateWithGeminiAsync(batchRequest, model, cancellationToken);

                if (generated != null && generated.Variants?.Count > 0)
                {
                    aggregated.AddRange(generated.Variants.Take(batchSize));
                }
                //else
                //{
                //    if (!_options.AllowFallback)
                //    {
                //        throw new UserFriendlyException("Gemini generation failed for this batch. No fallback template was created. Please verify API key/quota/model and retry.");
                //    }

                //    var fallbackVariants = BuildFallbackVariants(batchRequest, model);
                //    var uniqueFallback = KeepUniqueVariants(
                //            fallbackVariants,
                //            batchRequest.HistoricalTemplates,
                //            _options.SimilarityThreshold)
                //        .Take(batchSize)
                //        .ToList();

                //    if (uniqueFallback.Count == 0)
                //    {
                //        uniqueFallback = fallbackVariants
                //            .Select(v => NormalizeVariant(v, model))
                //            .Take(batchSize)
                //            .ToList();
                //    }

                //    aggregated.AddRange(uniqueFallback);
                //}

                remaining = request.Operation.VariantCount - aggregated.Count;
                batchIndex++;
            }

            return new EngineGenerateResponse
            {
                Success = true,
                Variants = aggregated
                    .Take(request.Operation.VariantCount)
                    .ToList()
            };
        }

        private static EngineGenerateRequest CreateBatchRequest(
            EngineGenerateRequest source,
            List<EngineVariant> acceptedVariants,
            int batchSize,
            int batchIndex)
        {
            var history = new List<EngineTemplateContext>();
            if (source.HistoricalTemplates != null && source.HistoricalTemplates.Count > 0)
            {
                history.AddRange(source.HistoricalTemplates);
            }

            if (acceptedVariants != null && acceptedVariants.Count > 0)
            {
                history.AddRange(acceptedVariants.Select(v => new EngineTemplateContext
                {
                    Subject = v.Subject,
                    PreviewText = v.PreviewText,
                    Body = v.BodyHtml,
                    Weight = v.Weight,
                    IsAiGenerated = true
                }));
            }

            return new EngineGenerateRequest
            {
                CorrelationId = $"{source.CorrelationId}-b{batchIndex}",
                Operation = new EngineOperationContext
                {
                    OperationId = source.Operation.OperationId,
                    Subject = source.Operation.Subject,
                    Body = source.Operation.Body,
                    Prompt = source.Operation.Prompt,
                    Tone = source.Operation.Tone,
                    VariantCount = batchSize,
                    BatchIndex = batchIndex,
                    TotalRequestedVariants = source.Operation.TotalRequestedVariants
                },
                HistoricalTemplates = history
            };
        }

        private async Task<EngineGenerateResponse> TryGenerateWithGeminiAsync(
            EngineGenerateRequest request,
            string model,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return null;
            }

            if (_genAiClient == null)
            {
                return null;
            }

            var prompt = BuildPrompt(request, model);
            var diversitySeed = ComputeStableSeed(request.CorrelationId);
            var config = new GenerateContentConfig
            {
                Temperature = ResolveRequestTemperature(_options.Temperature, diversitySeed),
                ResponseMimeType = "application/json"
            };

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.TimeoutMs);

            try
            {
                var sdkResponseTask = _genAiClient.Models.GenerateContentAsync(
                    model: model,
                    contents: prompt,
                    config: config);

                var response = await sdkResponseTask.WaitAsync(timeoutCts.Token);
                var jsonText = ExtractGeminiJsonText(response);
                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    return null;
                }

                var parsed = JsonSerializer.Deserialize<EngineGenerateResponse>(jsonText, JsonOptions);
                if (parsed == null || parsed.Variants == null)
                {
                    return null;
                }

                parsed.Success = true;
                parsed.Variants = KeepUniqueVariants(
                        parsed.Variants.Select(v => NormalizeVariant(v, model)).ToList(),
                        request.HistoricalTemplates,
                        _options.SimilarityThreshold)
                    .Take(request.Operation.VariantCount)
                    .ToList();

                return parsed;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractGeminiJsonText(GenerateContentResponse response)
        {
            try
            {
                if (response?.Candidates == null || response.Candidates.Count == 0)
                {
                    return null;
                }

                var candidate = response.Candidates[0];
                if (candidate?.Content?.Parts == null || candidate.Content.Parts.Count == 0)
                {
                    return null;
                }

                return candidate.Content.Parts[0].Text;
            }
            catch
            {
                return null;
            }
        }

        private static List<EngineVariant> BuildFallbackVariants(EngineGenerateRequest request, string model)
        {
            var variants = new List<EngineVariant>();
            var count = Math.Max(1, request.Operation.VariantCount);
            var tone = string.IsNullOrWhiteSpace(request.Operation.Tone)
                ? DetectTone(request.HistoricalTemplates)
                : request.Operation.Tone;
            var seed = ComputeStableSeed(request.CorrelationId) + request.Operation.BatchIndex;
            var subjectOffset = Math.Abs(seed) % 7;
            var sectionOffset = Math.Abs(seed) % 11;
            var ctaOffset = Math.Abs(seed) % 13;

            for (var index = 0; index < count * 2; index++)
            {
                var mixedIndex = index + subjectOffset;
                var subject = BuildSubjectVariant(request.Operation.Subject, tone, mixedIndex);
                var preview = BuildPreviewVariant(request.Operation.Body, mixedIndex);
                var sections = RotateSections(mixedIndex + sectionOffset);
                var cta = BuildCta(mixedIndex + ctaOffset);

                variants.Add(new EngineVariant
                {
                    Subject = subject,
                    PreviewText = preview,
                    BodyHtml = BuildHtml(subject, preview, cta, sections, mixedIndex),
                    Weight = 1,
                    OutlineJson = JsonSerializer.Serialize(new { sectionOrder = sections }, JsonOptions),
                    ComponentOrderJson = JsonSerializer.Serialize(sections, JsonOptions),
                    ModelName = model,
                    PromptVersion = "dotnet-fallback-v1",
                    SimilarityScore = 0
                });
            }

            return variants;
        }

        private static string BuildPrompt(EngineGenerateRequest request, string model)
        {
            var diversitySeed = ComputeStableSeed(request.CorrelationId);
            var diversityProfile = BuildDiversityProfile(diversitySeed);

            var styleSummary = new
            {
                tone = DetectTone(request.HistoricalTemplates),
                ctaSamples = request.HistoricalTemplates
                    .SelectMany(t => ExtractCtaPhrases(t.Body))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(100)
                    .ToList(),
                subjectSamples = request.HistoricalTemplates.Select(t => t.Subject).Where(x => !string.IsNullOrWhiteSpace(x)).Take(100).ToList()
            };

            return $@"You are an expert email campaign designer.
Generate exactly {request.Operation.VariantCount} unique variants as JSON.

Output JSON schema only:
{{
  ""success"": true,
  ""variants"": [
    {{
      ""subject"": ""..."",
      ""previewText"": ""..."",
      ""bodyHtml"": ""full responsive HTML email, table-based, inline CSS"",
      ""weight"": 1,
      ""outlineJson"": ""{{...}}"",
      ""componentOrderJson"": ""[...]"",
      ""modelName"": ""{model}"",
      ""promptVersion"": ""dotnet-gemini-v1"",
      ""inputTokens"": 0,
      ""outputTokens"": 0,
      ""latencyMs"": 0,
      ""similarityScore"": 0.0
    }}
  ]
}}

Constraints:
- Keep business meaning and brand intent.
- Variants must be naturally different in subject, structure, CTA wording, and section order.
- Every variant must have a distinct subject line; do not repeat or lightly rephrase the same subject.
- Never copy historical templates verbatim.
- Include header, body sections, CTA button, and footer.
- Email-safe HTML for major clients.
- Enforce this diversity profile for this request: {diversityProfile}.
- Use a different narrative angle than previous versions and avoid repeating the same headline pattern.

Campaign context:
{JsonSerializer.Serialize(request.Operation, JsonOptions)}

Historical style summary:
{JsonSerializer.Serialize(styleSummary, JsonOptions)}

Diversity seed: {diversitySeed}";
        }

        private static int ComputeStableSeed(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return global::System.Environment.TickCount;
            }

            unchecked
            {
                var hash = 17;
                foreach (var ch in value)
                {
                    hash = (hash * 31) + ch;
                }

                return hash;
            }
        }

        private static double ResolveRequestTemperature(double baseTemperature, int seed)
        {
            var jitter = ((Math.Abs(seed) % 7) - 3) * 0.05;
            return Math.Clamp(baseTemperature + jitter, 0, 2);
        }

        private static string BuildDiversityProfile(int seed)
        {
            var openings = new[]
            {
                "short personal update",
                "question-led opener",
                "story first",
                "problem-solution",
                "benefit first"
            };

            var ctaStyles = new[]
            {
                "soft invitation",
                "direct action",
                "informational CTA",
                "curiosity CTA",
                "consultative CTA"
            };

            var pace = new[]
            {
                "compact and punchy",
                "balanced medium length",
                "long-form narrative"
            };

            var indexA = Math.Abs(seed) % openings.Length;
            var indexB = Math.Abs(seed / 3) % ctaStyles.Length;
            var indexC = Math.Abs(seed / 5) % pace.Length;
            return $"opening={openings[indexA]}, cta={ctaStyles[indexB]}, pacing={pace[indexC]}";
        }

        private static EngineVariant NormalizeVariant(EngineVariant variant, string model)
        {
            variant.Subject = string.IsNullOrWhiteSpace(variant.Subject)
                ? "Campaign Update"
                : variant.Subject.Trim();
            variant.PreviewText = (variant.PreviewText ?? string.Empty).Trim();
            variant.BodyHtml = string.IsNullOrWhiteSpace(variant.BodyHtml)
                ? "<html><body><p>Content unavailable.</p></body></html>"
                : variant.BodyHtml;
            variant.Weight = variant.Weight <= 0 ? 1 : variant.Weight;
            variant.ModelName = string.IsNullOrWhiteSpace(variant.ModelName) ? model : variant.ModelName;
            variant.PromptVersion = string.IsNullOrWhiteSpace(variant.PromptVersion)
                ? "dotnet-gemini-v1"
                : variant.PromptVersion;
            variant.OutlineJson ??= "{}";
            variant.ComponentOrderJson ??= "[]";
            return variant;
        }

        private static List<EngineVariant> KeepUniqueVariants(
            List<EngineVariant> variants,
            List<EngineTemplateContext> history,
            double threshold)
        {
            var accepted = new List<EngineVariant>();
            var usedSubjectKeys = new HashSet<string>(StringComparer.Ordinal);

            if (history != null)
            {
                foreach (var historical in history)
                {
                    var historicalSubjectKey = Normalize(historical.Subject);
                    if (!string.IsNullOrWhiteSpace(historicalSubjectKey))
                    {
                        usedSubjectKeys.Add(historicalSubjectKey);
                    }
                }
            }

            foreach (var candidate in variants)
            {
                var candidateSubjectKey = Normalize(candidate.Subject);
                if (!string.IsNullOrWhiteSpace(candidateSubjectKey) && usedSubjectKeys.Contains(candidateSubjectKey))
                {
                    candidate.SimilarityScore = 1;
                    continue;
                }

                var maxScore = 0d;

                foreach (var old in history)
                {
                    maxScore = Math.Max(maxScore, ComputeSimilarity(candidate.Subject, old.Subject, candidate.BodyHtml, old.Body));
                }

                foreach (var existing in accepted)
                {
                    maxScore = Math.Max(maxScore, ComputeSimilarity(candidate.Subject, existing.Subject, candidate.BodyHtml, existing.BodyHtml));
                }

                candidate.SimilarityScore = Math.Round(maxScore, 4);
                if (maxScore < threshold)
                {
                    accepted.Add(candidate);
                    if (!string.IsNullOrWhiteSpace(candidateSubjectKey))
                    {
                        usedSubjectKeys.Add(candidateSubjectKey);
                    }
                }
            }

            return accepted;
        }

        private static double ComputeSimilarity(string subjectA, string subjectB, string bodyA, string bodyB)
        {
            var subjectScore = Jaccard(Shingles(subjectA, 2), Shingles(subjectB, 2));
            var bodyScore = Jaccard(Shingles(bodyA, 3), Shingles(bodyB, 3));
            return (subjectScore * 0.45) + (bodyScore * 0.55);
        }

        private static HashSet<string> Shingles(string value, int size)
        {
            var normalized = Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (normalized.Length == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            if (normalized.Length <= size)
            {
                return new HashSet<string>(normalized, StringComparer.Ordinal);
            }

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i <= normalized.Length - size; i++)
            {
                set.Add(string.Join(' ', normalized.Skip(i).Take(size)));
            }

            return set;
        }

        private static double Jaccard(HashSet<string> left, HashSet<string> right)
        {
            if (left.Count == 0 || right.Count == 0)
            {
                return 0;
            }

            var intersection = left.Intersect(right, StringComparer.Ordinal).Count();
            var union = left.Count + right.Count - intersection;
            return union <= 0 ? 0 : (double)intersection / union;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = value.ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ')
                .ToArray();

            return string.Join(' ', new string(chars)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static string DetectTone(IEnumerable<EngineTemplateContext> history)
        {
            if (history == null)
            {
                return "professional";
            }

            var combined = string.Join(" ", history.SelectMany(h => new[] { h.Subject, h.Body })).ToLowerInvariant();
            if (combined.Contains("offer") || combined.Contains("discount") || combined.Contains("exclusive"))
            {
                return "promotional";
            }

            if (combined.Contains("introducing") || combined.Contains("announcement") || combined.Contains("launch"))
            {
                return "announcement";
            }

            if (combined.Contains("thank") || combined.Contains("welcome"))
            {
                return "warm";
            }

            return "professional";
        }

        private static IEnumerable<string> ExtractCtaPhrases(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return Enumerable.Empty<string>();
            }

            var matches = new List<string>();
            var lower = html;
            var start = 0;
            while (true)
            {
                var open = lower.IndexOf(">", start, StringComparison.Ordinal);
                if (open < 0) break;
                var close = lower.IndexOf("</a>", open, StringComparison.OrdinalIgnoreCase);
                if (close < 0) break;
                var text = lower.Substring(open + 1, close - open - 1).Trim();
                if (!string.IsNullOrWhiteSpace(text) && text.Length <= 80)
                {
                    matches.Add(text);
                }
                start = close + 4;
            }

            return matches;
        }

        private static string[] RotateSections(int index)
        {
            var baseSections = new[] { "hero", "features", "offer", "testimonial", "cta" };
            var shift = index % baseSections.Length;
            return baseSections.Skip(shift).Concat(baseSections.Take(shift)).ToArray();
        }

        private static string BuildCta(int index)
        {
            var ctas = new[] { "Get Started", "Explore Now", "See Details", "Claim Offer", "Book a Demo" };
            return ctas[index % ctas.Length];
        }

        private static string BuildSubjectVariant(string subject, string tone, int index)
        {
            var baseSubject = string.IsNullOrWhiteSpace(subject) ? "Campaign Update" : subject.Trim();
            var openingPool = tone switch
            {
                "promotional" => new[] { "Special", "Limited", "Exclusive", "Today only", "Weekend", "Hot pick", "Flash", "Member" },
                "announcement" => new[] { "Introducing", "Now live", "Just launched", "Latest", "Fresh", "Heads-up", "New release", "First look" },
                "warm" => new[] { "A quick note", "Thanks", "For you", "Friendly update", "We thought of you", "Small favor", "Good news", "From our team" },
                _ => new[] { "Update", "Important", "This week", "New details", "Quick brief", "Top highlights", "Worth a look", "Action needed" }
            };

            var actionPool = new[]
            {
                "see what's new",
                "your next step",
                "inside details",
                "what changed",
                "new approach",
                "recommended for you",
                "latest version",
                "key update"
            };

            var opening = openingPool[index % openingPool.Length];
            var action = actionPool[Math.Abs(index / 2) % actionPool.Length];

            var composedSubject = (index % 5) switch
            {
                0 => $"{opening}: {baseSubject}",
                1 => $"{baseSubject} | {action}",
                2 => $"{opening} - {action}",
                3 => $"{action}: {baseSubject}",
                _ => $"{baseSubject} - {opening} update"
            };

            return composedSubject;
        }

        private static string BuildPreviewVariant(string body, int index)
        {
            var options = new[]
            {
                TruncatePlain(body, 140),
                "Fresh details inside with a clearer message and CTA.",
                "A newly tailored version built from your campaign style.",
                "Updated structure and copy designed for better engagement."
            };

            return options[index % options.Length];
        }

        private static string TruncatePlain(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "A fresh campaign update is ready.";
            }

            var plain = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return plain.Length <= max ? plain : plain.Substring(0, max);
        }

        private static string BuildHtml(string subject, string preview, string cta, string[] sections, int seed)
        {
            var colorA = seed % 2 == 0 ? "#0b5d7a" : "#1d4ed8";
            var colorB = seed % 2 == 0 ? "#f8fbff" : "#f8faff";
            var bodyBlocks = new StringBuilder();

            foreach (var section in sections)
            {
                if (section == "hero")
                {
                    bodyBlocks.Append($@"<tr><td style='padding:32px 28px;background:{colorB};'><h1 style='margin:0 0 10px 0;font-size:30px;line-height:1.25;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;'>{subject}</h1><p style='margin:0;font-size:15px;line-height:1.6;font-family:Segoe UI,Arial,sans-serif;color:#334155;'>{preview}</p></td></tr>");
                }
                else if (section == "features")
                {
                    bodyBlocks.Append("<tr><td style='padding:24px 28px;background:#ffffff;'><h2 style='margin:0 0 10px 0;font-size:21px;color:#0f172a;font-family:Segoe UI,Arial,sans-serif;'>Highlights</h2><ul style='margin:0;padding-left:18px;color:#334155;font-size:15px;line-height:1.6;font-family:Segoe UI,Arial,sans-serif;'><li>Adaptive structure tuned from historical winning styles</li><li>Responsive email-safe HTML with inline CSS</li><li>Unique language and section flow per variant</li></ul></td></tr>");
                }
                else if (section == "offer")
                {
                    bodyBlocks.Append("<tr><td style='padding:20px 28px;background:#ecfeff;border-top:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;'><h3 style='margin:0 0 8px 0;color:#0f172a;font-size:19px;font-family:Segoe UI,Arial,sans-serif;'>Offer Snapshot</h3><p style='margin:0;color:#334155;font-size:15px;line-height:1.6;font-family:Segoe UI,Arial,sans-serif;'>A cleaner message and stronger CTA tailored for this campaign.</p></td></tr>");
                }
                else if (section == "testimonial")
                {
                    bodyBlocks.Append("<tr><td style='padding:22px 28px;background:#f8fafc;'><p style='margin:0 0 8px 0;color:#0f172a;font-size:16px;line-height:1.6;font-family:Georgia,Times New Roman,serif;'>\"This new format improved engagement while keeping our brand voice consistent.\"</p><p style='margin:0;color:#64748b;font-size:13px;font-family:Segoe UI,Arial,sans-serif;'>Growth Team</p></td></tr>");
                }
                else if (section == "cta")
                {
                    bodyBlocks.Append($@"<tr><td style='padding:24px 28px;background:#ffffff;text-align:center;'><a href='#' style='display:inline-block;background:{colorA};color:#ffffff;text-decoration:none;font-weight:700;padding:12px 22px;border-radius:8px;font-family:Segoe UI,Arial,sans-serif;'>{cta}</a></td></tr>");
                }
            }

            return $@"<!doctype html><html><head><meta charset='utf-8' /><meta name='viewport' content='width=device-width, initial-scale=1.0' /><meta name='color-scheme' content='light dark' /><title>{subject}</title></head><body style='margin:0;padding:0;background:#eef2f7;'><div style='display:none;max-height:0;overflow:hidden;opacity:0;'>{preview}</div><table role='presentation' width='100%' cellpadding='0' cellspacing='0' border='0' style='background:#eef2f7;padding:16px 10px;'><tr><td align='center'><table role='presentation' width='640' cellpadding='0' cellspacing='0' border='0' style='max-width:640px;width:100%;background:#ffffff;border:1px solid #dbe5ef;border-radius:10px;overflow:hidden;border-collapse:collapse;'>{bodyBlocks}<tr><td style='padding:16px 28px;background:#f8fafc;font-size:12px;line-height:1.6;color:#64748b;font-family:Segoe UI,Arial,sans-serif;'>You are receiving this email because you interacted with our services. <a href='#' style='color:{colorA};'>Unsubscribe</a></td></tr></table></td></tr></table></body></html>";
        }

        private string ResolveModel(int variantCount)
        {
            if (_options.ModelRoute.Equals("quality", StringComparison.OrdinalIgnoreCase))
            {
                return _options.QualityModel;
            }

            if (_options.ModelRoute.Equals("speed", StringComparison.OrdinalIgnoreCase))
            {
                return _options.SpeedModel;
            }

            return variantCount > 6 ? _options.SpeedModel : _options.QualityModel;
        }

        private static List<EngineTemplateContext> BuildTemplateContexts(
            EmailOperation operation,
            List<EmailTemplate> historicalTemplates)
        {
            var contexts = new List<EngineTemplateContext>();
            if (historicalTemplates != null && historicalTemplates.Count > 0)
            {
                contexts.AddRange(historicalTemplates.Select(t => new EngineTemplateContext
                {
                    Subject = t.Subject,
                    Body = t.Body,
                    Weight = t.Weight,
                    PreviewText = t.PreviewText,
                    IsAiGenerated = t.IsAiGenerated
                }));
            }

            if (!contexts.Any(c => string.Equals(c.Subject, operation.Subject, StringComparison.OrdinalIgnoreCase)))
            {
                contexts.Add(new EngineTemplateContext
                {
                    Subject = operation.Subject,
                    Body = operation.Body,
                    Weight = 1,
                    IsAiGenerated = false
                });
            }

            return contexts;
        }

        private class EngineGenerateRequest
        {
            public string CorrelationId { get; set; }
            public EngineOperationContext Operation { get; set; }
            public List<EngineTemplateContext> HistoricalTemplates { get; set; }
        }

        private class EngineOperationContext
        {
            public long OperationId { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public string Prompt { get; set; }
            public string Tone { get; set; }
            public int VariantCount { get; set; }
            public int BatchIndex { get; set; }
            public int TotalRequestedVariants { get; set; }
        }

        private class EngineTemplateContext
        {
            public string Subject { get; set; }
            public string PreviewText { get; set; }
            public string Body { get; set; }
            public int Weight { get; set; }
            public bool IsAiGenerated { get; set; }
        }

        private class EngineGenerateResponse
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public List<EngineVariant> Variants { get; set; }
        }

        private class EngineVariant
        {
            public string Subject { get; set; }
            public string PreviewText { get; set; }
            public string BodyHtml { get; set; }
            public int Weight { get; set; }
            public string OutlineJson { get; set; }
            public string ComponentOrderJson { get; set; }
            public string ModelName { get; set; }
            public string PromptVersion { get; set; }
            public int? InputTokens { get; set; }
            public int? OutputTokens { get; set; }
            public int? LatencyMs { get; set; }
            public double SimilarityScore { get; set; }
        }
    }
}