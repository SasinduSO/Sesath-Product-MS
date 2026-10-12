using System.ComponentModel.DataAnnotations;

namespace WaterlilyInventory.Web.DTOs.Products;

/// <summary>
/// Input DTO for updating an existing product.
/// Does not include Id — the route parameter is authoritative for identifying the product.
/// Nullable numeric fields distinguish "omitted" from "intentional zero."
/// </summary>
public class UpdateProductDto : IValidatableObject
{
    /// <summary>
    /// Product name. Required, max 150 characters, must be nonblank after trimming.
    /// Special characters are disallowed; only letters, numbers, spaces, and hyphens are permitted.
    /// </summary>
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150, ErrorMessage = "Product name cannot exceed 150 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s-]+$", ErrorMessage = "Product name cannot contain special characters. Only letters, numbers, spaces, and hyphens are allowed.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Product category. Required, must be one of the allowed categories:
    /// Accessories, Electronics, Office Furniture, Stationary (Stationery).
    /// </summary>
    [Required(ErrorMessage = "Category is required.")]
    [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Product price. Required (nullable to detect omission), must be ≥ 0.
    /// Letters and excessive decimal precision are rejected.
    /// </summary>
    [Required(ErrorMessage = "Price is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be zero or positive.")]
    public decimal? Price { get; set; }

    /// <summary>
    /// Stock quantity. Required (nullable to detect omission), must be ≥ 0.
    /// Letters and decimal fractions are rejected.
    /// </summary>
    [Required(ErrorMessage = "Stock quantity is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity must be zero or positive integer.")]
    public int? Stock { get; set; }

    /// <summary>
    /// Allowed categories for products in the catalog.
    /// </summary>
    public static readonly string[] AllowedCategories =
    [
        "Accessories",
        "Electronics",
        "Office Furniture",
        "Stationery",
        "Stationary"
    ];

    /// <summary>
    /// Validates business constraints: non-blank after trimming, no special characters in name,
    /// valid category from the allowed set, and maximum 2 decimal places for price.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Product name cannot be blank or contain only whitespace.", new[] { nameof(Name) });
        }
        else
        {
            var trimmedName = Name.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedName, @"^[a-zA-Z0-9\s-]+$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(trimmedName, @"[a-zA-Z0-9]"))
            {
                yield return new ValidationResult("Product name cannot contain special characters. Only letters, numbers, spaces, and hyphens are allowed.", new[] { nameof(Name) });
            }
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            yield return new ValidationResult("Category cannot be blank or contain only whitespace.", new[] { nameof(Category) });
        }
        else if (!AllowedCategories.Any(c => string.Equals(c, Category.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ValidationResult("Category must be one of the allowed categories: Accessories, Electronics, Office Furniture, Stationary.", new[] { nameof(Category) });
        }

        if (Price.HasValue)
        {
            if (Price.Value > 999999999999999.99m)
            {
                yield return new ValidationResult("Price cannot exceed 999,999,999,999,999.99.", new[] { nameof(Price) });
            }
            else if (decimal.Round(Price.Value, 2) != Price.Value)
            {
                yield return new ValidationResult("Price cannot have more than 2 decimal places.", new[] { nameof(Price) });
            }
        }
    }
}
