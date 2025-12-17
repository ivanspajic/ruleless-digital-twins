namespace Logic.Models.MapekModels {
    public class TreeNode<T, U> {
        public T NodeItem { get; set; }

        public IEnumerable<U> Children { get; set; }
    }
}
