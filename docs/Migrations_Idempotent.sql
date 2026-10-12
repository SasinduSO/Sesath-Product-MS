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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011144950_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Stock] int NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Products_Price_NonNegative] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Products_Stock_NonNegative] CHECK ([Stock] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011144950_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_Category] ON [Products] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011144950_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011144950_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011145854_AddStoredProcedures'
)
BEGIN

    EXEC(N'
    CREATE OR ALTER PROCEDURE dbo.usp_Product_AveragePriceByCategory
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT 
            Category,
            AVG(Price) AS AveragePrice
        FROM dbo.Products
        GROUP BY Category
        ORDER BY Category ASC;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011145854_AddStoredProcedures'
)
BEGIN

    EXEC(N'
    CREATE OR ALTER PROCEDURE dbo.usp_Product_HighestStockValueCategory
    AS
    BEGIN
        SET NOCOUNT ON;

        ;WITH CategoryTotals AS (
            SELECT 
                Category,
                SUM(CAST(Stock AS BIGINT)) AS TotalStockUnits,
                SUM(Price * CAST(Stock AS BIGINT)) AS InventoryValue
            FROM dbo.Products
            GROUP BY Category
        ),
        RankedCategories AS (
            SELECT 
                Category,
                TotalStockUnits,
                InventoryValue,
                DENSE_RANK() OVER (ORDER BY InventoryValue DESC) AS ValueRank
            FROM CategoryTotals
        )
        SELECT 
            Category,
            TotalStockUnits,
            InventoryValue
        FROM RankedCategories
        WHERE ValueRank = 1
        ORDER BY Category ASC;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011145854_AddStoredProcedures'
)
BEGIN

    EXEC(N'
    CREATE OR ALTER PROCEDURE dbo.usp_Product_InventoryValuationByCategory
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT 
            Category,
            SUM(CAST(Stock AS BIGINT)) AS TotalStockUnits,
            SUM(Price * CAST(Stock AS BIGINT)) AS InventoryValue
        FROM dbo.Products
        GROUP BY Category
        ORDER BY Category ASC;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011145854_AddStoredProcedures'
)
BEGIN

    EXEC(N'
    CREATE OR ALTER PROCEDURE dbo.usp_Product_LowStock
        @Threshold INT = 10
    AS
    BEGIN
        SET NOCOUNT ON;

        -- Defensive normalization for negative threshold
        IF @Threshold < 0
        BEGIN
            SET @Threshold = 0;
        END;

        SELECT 
            Id,
            Name,
            Category,
            Price,
            Stock
        FROM dbo.Products
        WHERE Stock < @Threshold
        ORDER BY Stock ASC, Id ASC;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011145854_AddStoredProcedures'
)
BEGIN

    EXEC(N'
    CREATE OR ALTER PROCEDURE dbo.usp_Product_TopFiveMostExpensive
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT TOP (5)
            Id,
            Name,
            Category,
            Price,
            Stock
        FROM dbo.Products
        ORDER BY Price DESC, Id ASC;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011145854_AddStoredProcedures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011145854_AddStoredProcedures', N'10.0.12');
END;

COMMIT;
GO

