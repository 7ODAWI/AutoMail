using System.Text.Json;
using GitHubScraper.Models.Entities;
using GitHubScraper.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitHubScraper.Data;

internal static class SeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Operations.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        // Helper to create operation
        ScrapingOperation Create(string name, string[] keywords, string[] locations, int minFollowers = 0)
        {
            return new ScrapingOperation
            {
                Id = Guid.NewGuid(),
                Name = name,
                GitHubToken = null,
                Status = OperationStatus.Idle,
                KeywordsJson = JsonSerializer.Serialize(keywords),
                LocationsJson = JsonSerializer.Serialize(locations),
                MinFollowers = minFollowers,
                CreatedAt = now,
                ProfilesScanned = 0,
                EmailsFound = 0,
                Failures = 0
            };
        }

        var seeds = new List<ScrapingOperation>
        {
            Create("Canada Developers", new[]{"Full Stack","React Developer","ASP.NET","Backend Developer","Frontend Developer","Node.js","Python","Java Developer","PHP Developer","DevOps","Software Engineer"},
                new[]{"Canada","Toronto","Vancouver","Montreal","Ottawa","Calgary","Edmonton"}),

            Create("USA Developers", new[]{"Full Stack","React Developer","ASP.NET","Software Engineer","Backend Developer","Frontend Developer","JavaScript","Python","SaaS Engineer"},
                new[]{"United States","California","New York","Texas","Florida","Seattle","Boston","San Francisco","Chicago"}),

            Create("UK Developers", new[]{"Full Stack",".NET Developer","React Developer","Backend Engineer","Frontend Engineer","DevOps","Python"},
                new[]{"United Kingdom","London","Manchester","Birmingham","Liverpool","Scotland"}),

            Create("Germany Developers", new[]{"Full Stack","Java Developer","React Developer","Backend Developer","ASP.NET","DevOps","Python"},
                new[]{"Germany","Berlin","Munich","Hamburg","Frankfurt","Cologne"}),

            Create("France Developers", new[]{"Full Stack","Symfony","PHP Developer","React Developer","Backend Engineer","Software Engineer"},
                new[]{"France","Paris","Lyon","Marseille","Toulouse"}),

            Create("Italy Developers", new[]{"Full Stack","PHP Developer","React Developer","ASP.NET","Backend Developer"},
                new[]{"Italy","Milan","Rome","Naples","Turin"}),

            Create("Spain Developers", new[]{"Full Stack","React Developer","Node.js","Backend Engineer","Python Developer"},
                new[]{"Spain","Madrid","Barcelona","Valencia","Seville"}),

            Create("Netherlands Developers", new[]{"Full Stack","Software Engineer","React","DevOps","Python","Cloud Engineer"},
                new[]{"Netherlands","Amsterdam","Rotterdam","Utrecht","Eindhoven"}),

                Create("Sweden Developers", new[]{"Full Stack","Backend Developer","Frontend Engineer","React","Node.js","DevOps"},
                new[]{"Sweden","Stockholm","Gothenburg","Malmo"}),

            Create("Norway Developers", new[]{"Full Stack","ASP.NET","React Developer","Cloud Engineer","Software Engineer"},
                new[]{"Norway","Oslo","Bergen","Trondheim"}),

            Create("Denmark Developers", new[]{"Full Stack","Backend Engineer","Frontend Developer","React","Node.js"},
                new[]{"Denmark","Copenhagen","Aarhus","Odense"}),

            Create("Switzerland Developers", new[]{"Full Stack","Software Engineer","Java Developer","React Developer","DevOps"},
                new[]{"Switzerland","Zurich","Geneva","Basel"}),

            Create("Belgium Developers", new[]{"Full Stack","PHP Developer","ASP.NET","React Developer","Backend Developer"},
                new[]{"Belgium","Brussels","Antwerp","Ghent"}),

            Create("Austria Developers", new[]{"Full Stack","Backend Engineer","React","Java","Python"},
                new[]{"Austria","Vienna","Salzburg","Graz"}),

            Create("Poland Developers", new[]{"Full Stack","React Developer","Backend Developer","ASP.NET","Java Developer"},
                new[]{"Poland","Warsaw","Krakow","Wroclaw","Gdansk"}),

            Create("Portugal Developers", new[]{"Full Stack","Frontend Developer","React Developer","Backend Engineer"},
                new[]{"Portugal","Lisbon","Porto","Braga"}),

            Create("Ireland Developers", new[]{"Full Stack","React Developer","Cloud Engineer","DevOps","ASP.NET"},
                new[]{"Ireland","Dublin","Cork","Galway"}),

            Create("Australia Developers", new[]{"Full Stack","React Developer","Backend Engineer","Node.js","DevOps"},
                new[]{"Australia","Sydney","Melbourne","Brisbane","Perth"}),

            Create("New Zealand Developers", new[]{"Full Stack","ASP.NET","React Developer","Backend Developer"},
                new[]{"New Zealand","Auckland","Wellington","Christchurch"}),

            Create("Japan Developers", new[]{"Full Stack","Java Developer","React","Backend Engineer","Software Engineer"},
                new[]{"Japan","Tokyo","Osaka","Kyoto"}),

            Create("Korea Developers", new[]{"Full Stack","Backend Developer","React Developer","Python Engineer"},
                new[]{"South Korea","Seoul","Busan","Incheon"}),

            Create("Singapore Developers", new[]{"Full Stack","Software Engineer","Cloud Engineer","React Developer"},
                new[]{"Singapore"}),

            Create("Finland Developers", new[]{"Full Stack","Backend Engineer","Frontend Engineer","React"},
                new[]{"Finland","Helsinki","Tampere"}),

            Create("Czech Developers", new[]{"Full Stack","ASP.NET","Java Developer","React Developer"},
                new[]{"Czech Republic","Prague","Brno"}),

            Create("Romania Developers", new[]{"Full Stack","PHP Developer","React Developer","Backend Engineer"},
                new[]{"Romania","Bucharest","Cluj-Napoca"}),

            Create("Hungary Developers", new[]{"Full Stack","Backend Developer","React","ASP.NET"},
                new[]{"Hungary","Budapest"}),

            Create("Greece Developers", new[]{"Full Stack","React Developer","Node.js","Backend Engineer"},
                new[]{"Greece","Athens","Thessaloniki"}),

            // Additional high-value countries
            Create("Brazil Developers", new[]{"Full Stack","React Developer","Node.js","Backend Engineer","Python Developer","PHP Developer"},
                new[]{"Brazil","Sao Paulo","Rio de Janeiro","Curitiba","Porto Alegre"}),

            Create("Mexico Developers", new[]{"Full Stack","ASP.NET","React Developer","Backend Developer","JavaScript Engineer"},
                new[]{"Mexico","Mexico City","Guadalajara","Monterrey"}),

            Create("Argentina Developers", new[]{"Full Stack","React Developer","Python Developer","Backend Engineer"},
                new[]{"Argentina","Buenos Aires","Cordoba","Rosario"}),

            Create("Chile Developers", new[]{"Full Stack","React Developer","Backend Developer","DevOps"},
                new[]{"Chile","Santiago","Valparaiso"}),

            Create("Colombia Developers", new[]{"Full Stack","Backend Engineer","Frontend Developer","React"},
                new[]{"Colombia","Bogota","Medellin","Cali"}),

            Create("Serbia Developers", new[]{"Full Stack","ASP.NET","React Developer","Backend Engineer"},
                new[]{"Serbia","Belgrade","Novi Sad"}),

            Create("Croatia Developers", new[]{"Full Stack","Backend Developer","Frontend Engineer","React"},
                new[]{"Croatia","Zagreb","Split"}),

            Create("Slovakia Developers", new[]{"Full Stack","Java Developer","ASP.NET","React Developer"},
                new[]{"Slovakia","Bratislava","Kosice"}),

            Create("Slovenia Developers", new[]{"Full Stack","Backend Engineer","React Developer"},
                new[]{"Slovenia","Ljubljana"}),

            Create("Estonia Developers", new[]{"Full Stack","React Developer","DevOps","Software Engineer"},
                new[]{"Estonia","Tallinn"}),

            Create("Latvia Developers", new[]{"Full Stack","ASP.NET","Backend Developer","React"},
                new[]{"Latvia","Riga"}),

            Create("Lithuania Developers", new[]{"Full Stack","React Developer","Java Developer"},
                new[]{"Lithuania","Vilnius","Kaunas"}),

            Create("Bulgaria Developers", new[]{"Full Stack","Backend Engineer","React Developer","PHP Developer"},
                new[]{"Bulgaria","Sofia","Plovdiv"}),

            Create("Ukraine Developers", new[]{"Full Stack","ASP.NET","React Developer","Node.js","Backend Engineer"},
                new[]{"Ukraine","Kyiv","Lviv","Kharkiv"}),

            Create("Turkey Developers", new[]{"Full Stack","React Developer","Backend Developer","ASP.NET"},
                new[]{"Turkey","Istanbul","Ankara","Izmir"}),

            Create("India Developers", new[]{"Full Stack","React Developer","ASP.NET","Python Developer","Java Developer","Backend Engineer"},
                new[]{"India","Bangalore","Hyderabad","Pune","Mumbai","Delhi","Chennai"}),

            Create("Pakistan Developers", new[]{"Full Stack","React Developer","PHP Developer","ASP.NET","Backend Engineer"},
                new[]{"Pakistan","Lahore","Karachi","Islamabad"}),

            Create("Indonesia Developers", new[]{"Full Stack","Backend Developer","React Engineer","Software Engineer"},
                new[]{"Indonesia","Jakarta","Bandung"}),

            Create("Philippines Developers", new[]{"Full Stack","React Developer","ASP.NET","Backend Developer"},
                new[]{"Philippines","Manila","Cebu"}),

            Create("Vietnam Developers", new[]{"Full Stack","React Developer","Java Developer","Backend Engineer"},
                new[]{"Vietnam","Ho Chi Minh City","Hanoi"}),

            Create("Thailand Developers", new[]{"Full Stack","Frontend Developer","Backend Engineer","React"},
                new[]{"Thailand","Bangkok","Chiang Mai"}),

            Create("Taiwan Developers", new[]{"Full Stack","Backend Developer","React Developer","Software Engineer"},
                new[]{"Taiwan","Taipei","Taichung"}),

            Create("Hong Kong Developers", new[]{"Full Stack","Cloud Engineer","React Developer","Backend Engineer"},
                new[]{"Hong Kong"}),

            Create("South Africa Developers", new[]{"Full Stack","ASP.NET","React Developer","Backend Developer"},
                new[]{"South Africa","Cape Town","Johannesburg","Durban"}),

            Create("Nigeria Developers", new[]{"Full Stack","React Developer","Node.js","Backend Engineer"},
                new[]{"Nigeria","Lagos","Abuja"}),

            Create("Kenya Developers", new[]{"Full Stack","Frontend Developer","Backend Developer","React"},
                new[]{"Kenya","Nairobi"}),

            Create("Iceland Developers", new[]{"Full Stack","Software Engineer","React Developer"},
                new[]{"Iceland","Reykjavik"})
        };

        await db.Operations.AddRangeAsync(seeds);
        await db.SaveChangesAsync();
    }
}
