-- Drop the Developers table if you want to remove all stored records
-- Run this in your database host's SQL console (idempotent)
IF OBJECT_ID(N'[dbo].[Developers]') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Developers];
END;
GO

-- Remove the migration history entry for InitialCreate (optional)
IF EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260515153558_InitialCreate')
BEGIN
    DELETE FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260515153558_InitialCreate';
END;
GO
