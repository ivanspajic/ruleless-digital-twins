using Logic.Models.MapekModels;

namespace Logic.Mapek {
    public interface IBangBangPlanner {
        public Simulation Plan(Cache cache);
    }
}
