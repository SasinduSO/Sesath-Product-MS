-- ============================================================================
-- WaterlilyInventory — Development Demo Seed Script
-- ============================================================================
-- Seeds 13 products across 4 categories to test:
-- 1. Average Price per Category: distinct averages across 4 categories.
-- 2. Highest Stock Value Category: exact tie ($3,000.00) between 'Electronics' and 'Office Furniture'.
-- 3. Inventory Valuation: full category inventory values and total stock units.
-- 4. Low-Stock Alert: boundary testing with Stock 0, 9, 10, 11 (strict Stock < 10 filter).
-- 5. Top 5 Most Expensive Products: Price DESC, Id ASC tie-breaker ($200, $150, $50, $50, $50).
--
-- Safety Guard: Refuses to insert if the Products table already contains data.
-- ============================================================================

USE [WaterlilyInventoryDb];
GO

IF EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    PRINT 'Products table already contains data. Demo seeding skipped to prevent data overwrite.';
END
ELSE
BEGIN
    PRINT 'Products table is empty. Inserting 13 demo products across 4 categories...';

    INSERT INTO dbo.Products ([Name], [Category], [Price], [Stock])
    VALUES
        -- Category: Electronics (Valuation: $3,000.00, Units: 110, Avg Price: $31.67)
        (N'Laptop Stand', N'Electronics', 50.00, 20),
        (N'USB-C Cable', N'Electronics', 20.00, 50),
        (N'Wireless Mouse', N'Electronics', 25.00, 40),

        -- Category: Office Furniture (Valuation: $3,000.00, Units: 30, Avg Price: $100.00)
        -- Includes boundary stock levels 10, 11, and 9
        (N'Ergonomic Chair', N'Office Furniture', 200.00, 10),
        (N'Desk Lamp', N'Office Furniture', 50.00, 11),
        (N'Footrest', N'Office Furniture', 50.00, 9),

        -- Category: Stationery (Valuation: $1,500.00, Units: 130, Avg Price: $17.00)
        -- Includes zero-stock item
        (N'Notebook Set', N'Stationery', 15.00, 0),
        (N'Premium Pen', N'Stationery', 30.00, 30),
        (N'Sticky Notes Pack', N'Stationery', 6.00, 100),

        -- Category: Accessories (Valuation: $500.00, Units: 40, Avg Price: $47.50)
        -- Includes boundary stock 10 and zero-stock expensive item
        (N'Desk Mat', N'Accessories', 25.00, 10),
        (N'Cable Organizer', N'Accessories', 10.00, 20),
        (N'Monitor Wipe', N'Accessories', 5.00, 10),
        (N'Mechanical Keyboard', N'Accessories', 150.00, 0);

    PRINT 'Demo products inserted successfully.';
END;
GO
