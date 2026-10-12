namespace WaterlilyInventory.Web.Domain.Entities;

/// <summary>
/// Minimal entity contract exposing the integer primary key.
/// Used by <see cref="Repositories.Interfaces.IGenericRepository{T}"/> to provide
/// type-safe generic CRUD operations without requiring a large base entity class.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// The identity primary key assigned by the database.
    /// </summary>
    int Id { get; }
}
