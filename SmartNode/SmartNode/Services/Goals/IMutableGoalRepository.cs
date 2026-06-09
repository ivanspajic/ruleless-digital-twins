using SmartNode.Models.Goals;

namespace SmartNode.Services.Goals;

// Write access to user goals. Only durable providers
// implement this; the JSON provider stays read-only (IGoalRepository), so it can
// never be turned into a half-baked writable store. The goal-editor endpoints
// reject POST/DELETE when the active provider is not mutable.
public interface IMutableGoalRepository : IGoalRepository
{
    UserGoal? GetById(string id);

    // Insert or replace a goal by its id.
    void Upsert(UserGoal goal);

    // Delete a goal by id. Returns true if a goal was removed, false if none matched.
    bool Delete(string id);
}
