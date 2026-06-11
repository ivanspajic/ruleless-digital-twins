using SmartNode.Models.Goals;

namespace SmartNode.Services.Goals;

// Read access to the active user goals. Loading (JSON file vs SQLite) is an
// implementation detail of the concrete provider, not part of this contract
// The concrete JSON provider keeps its own LoadFromFile.
public interface IGoalRepository
{
    IReadOnlyList<UserGoal> GetAll();
}
