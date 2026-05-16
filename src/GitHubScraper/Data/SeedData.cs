using System.Text.Json;
using GitHubScraper.Models.Entities;
using GitHubScraper.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GitHubScraper.Data;

internal static class SeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Operations.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        var keywords = new[]
        {
            // Core Roles
            "Software Engineer",
            "Software Developer",
            "Full Stack Developer",
            "Backend Developer",
            "Frontend Developer",
            "Web Developer",
            "Mobile Developer",
            "Application Developer",
            "Platform Engineer",
            "Systems Engineer",
            "Solutions Architect",
            "Technical Lead",
            "Engineering Manager",

            // Frontend
            "React Developer",
            "Vue Developer",
            "Angular Developer",
            "Next.js Developer",
            "Nuxt.js Developer",
            "JavaScript Developer",
            "TypeScript Developer",
            "UI Developer",
            "UX Engineer",

            // Backend
            "Node.js Developer",
            "Express.js Developer",
            "NestJS Developer",
            "ASP.NET Developer",
            ".NET Developer",
            "Python Developer",
            "Java Developer",
            "Spring Boot Developer",
            "Laravel Developer",
            "PHP Developer",
            "Symfony Developer",
            "Django Developer",
            "Flask Developer",
            "FastAPI Developer",
            "Ruby on Rails",
            "Golang Developer",
            "Rust Developer",
            "C# Developer",
            "Scala Developer",
            "Kotlin Developer",

            // Database / Infra
            "SQL Developer",
            "PostgreSQL",
            "MongoDB",
            "Redis",
            "Elasticsearch",
            "Database Engineer",

            // DevOps / Cloud
            "DevOps Engineer",
            "Cloud Engineer",
            "AWS Engineer",
            "Azure Engineer",
            "Google Cloud Engineer",
            "Site Reliability Engineer",
            "Infrastructure Engineer",
            "Platform Engineer",
            "Docker",
            "Kubernetes",
            "Terraform",
            "CI/CD Engineer",

            // AI / Data
            "AI Engineer",
            "ML Engineer",
            "LLM Engineer",
            "Prompt Engineer",
            "Data Engineer",
            "Data Scientist",
            "Data Analyst",
            "Computer Vision Engineer",
            "NLP Engineer",

            // Security
            "Cybersecurity Engineer",
            "Security Researcher",
            "Application Security Engineer",
            "Penetration Tester",

            // Startup / Remote
            "Remote Developer",
            "Freelancer",
            "Startup Founder",
            "Technical Founder",
            "CTO",
            "Indie Hacker",
            "Open Source Contributor",
            "Digital Nomad",

            // Seniority
            "Senior Software Engineer",
            "Senior Backend Developer",
            "Senior Frontend Developer",
            "Senior Full Stack Developer",
            "Lead Developer",
            "Principal Engineer",
            "Software Architect",
            "Junior Developer",
            "Staff Engineer",

            // Popular search phrases
            "Building SaaS",
            "Open Source",
            "Building in Public",
            "Tech Startup",
            "Available for Hire",
            "Remote Work",
            "Freelance Developer",
            "Tech Enthusiast"
        };

        var locations = new[]
        {
            // United States
            "United States",
            "California",
            "Texas",
            "Florida",
            "Washington",
            "New York",
            "Massachusetts",
            "Virginia",
            "North Carolina",
            "Colorado",
            "Illinois",

            // US Tech Cities
            "San Francisco",
            "New York City",
            "Seattle",
            "Austin",
            "Los Angeles",
            "Chicago",
            "Boston",
            "Denver",
            "Miami",
            "Dallas",
            "Houston",
            "Atlanta",
            "Phoenix",
            "San Diego",
            "Silicon Valley",

            // United Kingdom
            "United Kingdom",
            "England",
            "Scotland",
            "Wales",
            "Northern Ireland",

            // UK Cities
            "London",
            "Manchester",
            "Birmingham",
            "Liverpool",
            "Leeds",
            "Bristol",
            "Glasgow",
            "Edinburgh",
            "Cambridge",
            "Oxford",

            // Europe
            "Germany",
            "France",
            "Netherlands",
            "Belgium",
            "Sweden",
            "Norway",
            "Finland",
            "Denmark",
            "Switzerland",
            "Austria",
            "Poland",
            "Romania",
            "Ukraine",
            "Spain",
            "Italy",
            "Portugal",
            "Ireland",
            "Czech Republic",
            "Hungary",
            "Greece",
            "Croatia",
            "Serbia",

            // European Tech Cities
            "Berlin",
            "Munich",
            "Hamburg",
            "Amsterdam",
            "Rotterdam",
            "Paris",
            "Lyon",
            "Stockholm",
            "Oslo",
            "Helsinki",
            "Copenhagen",
            "Zurich",
            "Warsaw",
            "Krakow",
            "Barcelona",
            "Madrid",
            "Lisbon",
            "Dublin",
            "Prague",
            "Vienna",

            // Canada
            "Canada",
            "Toronto",
            "Vancouver",
            "Montreal",
            "Ottawa",
            "Calgary",

            // Asia
            "India",
            "Bangalore",
            "Hyderabad",
            "Mumbai",
            "Delhi",
            "Singapore",
            "Tokyo",
            "Seoul",
            "Dubai",

            // Oceania
            "Australia",
            "Sydney",
            "Melbourne",
            "Brisbane",

            // South America
            "Brazil",
            "Sao Paulo",
            "Argentina",
            "Buenos Aires",

            // Middle East
            "UAE",
            "Saudi Arabia",
            "Qatar"
        };

        var operation = new ScrapingOperation
        {
            Id = Guid.NewGuid(),

            Name = "Mass Global Developer Discovery",

            GitHubToken = null,

            Status = OperationStatus.Idle,

            KeywordsJson = JsonSerializer.Serialize(keywords),

            LocationsJson = JsonSerializer.Serialize(locations),

            // Very low filter for maximum scraping coverage
            MinFollowers = 0,

            CreatedAt = now,

            ProfilesScanned = 0,

            EmailsFound = 0,

            Failures = 0
        };

        await db.Operations.AddAsync(operation);

        await db.SaveChangesAsync();
    }

}
