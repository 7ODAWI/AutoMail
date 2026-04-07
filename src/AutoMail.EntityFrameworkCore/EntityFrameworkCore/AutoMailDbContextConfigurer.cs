using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace AutoMail.EntityFrameworkCore;

public static class AutoMailDbContextConfigurer
{
    public static void Configure(DbContextOptionsBuilder<AutoMailDbContext> builder, string connectionString)
    {
        builder.UseSqlServer(connectionString);
    }

    public static void Configure(DbContextOptionsBuilder<AutoMailDbContext> builder, DbConnection connection)
    {
        builder.UseSqlServer(connection);
    }
}
