namespace Logic.Models.MapekModels {
    public interface ITreeNode<T, U> {
        public T NodeItem { get; set; }

        public IEnumerable<U> Children { get; set; }
    }
}
