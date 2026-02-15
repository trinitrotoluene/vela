using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vela.Events;

namespace Vela.Data;

public class VelaDbContext(DbContextOptions<VelaDbContext> options, JsonSerializerOptions jsonOptions) : DbContext(options)
{
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    foreach (var type in BitcraftEventBase.DatabaseTypes)
    {
      var entity = modelBuilder.Entity(type);
      entity.HasKey("Id");

      // Configure properties based on their types
      foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        if (property.Name == "EqualityContract") continue;

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        // Arrays/collections → JSONB
        if (propertyType.IsArray && propertyType != typeof(byte[]))
        {
          entity.Property(property.Name).HasColumnType("jsonb")
            .HasConversion(CreateJsonConversion(property.PropertyType));
        }
        // Enums → store as string for consumer compatibility
        else if (propertyType.IsEnum)
        {
          entity.Property(property.Name).HasConversion<string>();
        }
        // Nested record types (non-primitive, non-enum, non-string) → JSONB
        // Can't use OwnsOne because EF Core can't bind owned navigations to record constructors
        else if (propertyType.IsClass
          && propertyType != typeof(string)
          && !propertyType.IsArray
          && !propertyType.IsAbstract)
        {
          entity.Property(property.Name).HasColumnType("jsonb")
            .HasConversion(CreateJsonConversion(property.PropertyType));
        }
      }
    }
  }

  private ValueConverter CreateJsonConversion(Type propertyType)
  {
    var converterType = typeof(JsonValueConverter<>).MakeGenericType(propertyType);
    return (ValueConverter)Activator.CreateInstance(converterType, jsonOptions)!;
  }
}

internal class JsonValueConverter<T>(JsonSerializerOptions jsonOptions) : ValueConverter<T, string>(
  v => JsonSerializer.Serialize(v, jsonOptions),
  v => JsonSerializer.Deserialize<T>(v, jsonOptions)!
);
