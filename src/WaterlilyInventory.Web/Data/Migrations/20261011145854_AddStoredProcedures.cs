using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterlilyInventory.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Average Price per Category: AVG(Price) per category
            migrationBuilder.Sql(@"
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
');");

            // 2. Highest Stock Value Category: Greatest SUM(Price * Stock) per category, returning all tied leaders
            migrationBuilder.Sql(@"
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
');");

            // 3. Inventory Valuation by Category: SUM(Price * Stock) for every category and total stock units
            migrationBuilder.Sql(@"
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
');");

            // 4. Low-Stock Alert: Products with Stock < @Threshold (default 10)
            migrationBuilder.Sql(@"
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
');");

            // 5. Top Five Most Expensive: TOP (5) ordered by Price DESC, Id ASC
            migrationBuilder.Sql(@"
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
');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Product_AveragePriceByCategory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Product_HighestStockValueCategory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Product_InventoryValuationByCategory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Product_LowStock;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Product_TopFiveMostExpensive;");
        }
    }
}
