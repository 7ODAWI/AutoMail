IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515153558_InitialCreate'
)
BEGIN
    CREATE TABLE [Developers] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(200) NOT NULL,
        [Name] nvarchar(500) NULL,
        [Location] nvarchar(500) NULL,
        [Bio] nvarchar(max) NULL,
        [Email] nvarchar(500) NULL,
        [EmailSource] nvarchar(200) NULL,
        [EmailConfidence] nvarchar(50) NULL,
        [Website] nvarchar(500) NULL,
        [Followers] int NOT NULL DEFAULT 0,
        [Repos] int NOT NULL DEFAULT 0,
        [ProfileUrl] nvarchar(500) NULL,
        [FoundAtUtc] nvarchar(50) NULL,
        CONSTRAINT [PK_Developers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515153558_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Developers_Email] ON [Developers] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515153558_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Developers_Username] ON [Developers] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515153558_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515153558_InitialCreate', N'8.0.4');
END;
GO

COMMIT;
GO

