using MongoDB.Driver;

namespace TestProject.Mocks.MongoDB {
    internal class MongoAsyncCursorMock<T> : IAsyncCursor<T> {
        private readonly IEnumerable<T> _documents;
        private int currentIndex = 0;

        public MongoAsyncCursorMock(IEnumerable<T> documents) {
            _documents = documents;
        }

        public IEnumerable<T> Current => _documents;

        public void Dispose() {
            
        }

        public bool MoveNext(CancellationToken cancellationToken = default) {
            if (currentIndex < _documents.Count() - 1) {
                currentIndex++;

                return true;
            }

            return false;
        }

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }
    }
}
