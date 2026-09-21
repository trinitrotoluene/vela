namespace Vela.Events
{
  [Postgres]
  public record BitcraftBuildingDesc(
    string Id,
    string Name,
    string Description,
    BitcraftBuildingFunction[] Functions
  ) : BitcraftEventBase(Id);

  // FunctionType is a building_type_desc id - matched against a recipe's BuildingRequirement.
  public record BitcraftBuildingFunction(
    int FunctionType,
    int Level
  );
}