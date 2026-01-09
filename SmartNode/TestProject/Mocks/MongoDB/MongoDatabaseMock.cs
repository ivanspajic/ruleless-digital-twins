using MongoDB.Bson;
using MongoDB.Driver;

namespace TestProject.Mocks.MongoDB {
    internal class MongoDatabaseMock : IMongoDatabase {
        // The default IMongoCollection interface is inaccessible due to its protection level, so we're using object instead.
        private readonly Dictionary<Type, object> _collections = [];

        public IMongoClient Client => throw new NotImplementedException();

        public DatabaseNamespace DatabaseNamespace => throw new NotImplementedException();

        public MongoDatabaseSettings Settings => throw new NotImplementedException();

        public void AddCollection<T>(IMongoCollection<T> mongoCollection) {
            _collections.Add(typeof(T), mongoCollection);
        }

        public IAsyncCursor<TResult> Aggregate<TResult>(PipelineDefinition<NoPipelineInput, TResult> pipeline, AggregateOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<TResult> Aggregate<TResult>(IClientSessionHandle session, PipelineDefinition<NoPipelineInput, TResult> pipeline, AggregateOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<TResult>> AggregateAsync<TResult>(PipelineDefinition<NoPipelineInput, TResult> pipeline, AggregateOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<TResult>> AggregateAsync<TResult>(IClientSessionHandle session, PipelineDefinition<NoPipelineInput, TResult> pipeline, AggregateOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void AggregateToCollection<TResult>(PipelineDefinition<NoPipelineInput, TResult> pipeline, AggregateOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void AggregateToCollection<TResult>(IClientSessionHandle session, PipelineDefinition<NoPipelineInput, TResult> pipeline, AggregateOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task AggregateToCollectionAsync<TResult>(PipelineDefinition<NoPipelineInput, TResult> pipeline, AggregateOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task AggregateToCollectionAsync<TResult>(IClientSessionHandle session, PipelineDefinition<NoPipelineInput, TResult> pipeline, AggregateOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void CreateCollection(string name, CreateCollectionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void CreateCollection(IClientSessionHandle session, string name, CreateCollectionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task CreateCollectionAsync(string name, CreateCollectionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task CreateCollectionAsync(IClientSessionHandle session, string name, CreateCollectionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void CreateView<TDocument, TResult>(string viewName, string viewOn, PipelineDefinition<TDocument, TResult> pipeline, CreateViewOptions<TDocument> options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void CreateView<TDocument, TResult>(IClientSessionHandle session, string viewName, string viewOn, PipelineDefinition<TDocument, TResult> pipeline, CreateViewOptions<TDocument> options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task CreateViewAsync<TDocument, TResult>(string viewName, string viewOn, PipelineDefinition<TDocument, TResult> pipeline, CreateViewOptions<TDocument> options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task CreateViewAsync<TDocument, TResult>(IClientSessionHandle session, string viewName, string viewOn, PipelineDefinition<TDocument, TResult> pipeline, CreateViewOptions<TDocument> options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void DropCollection(string name, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void DropCollection(string name, DropCollectionOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void DropCollection(IClientSessionHandle session, string name, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void DropCollection(IClientSessionHandle session, string name, DropCollectionOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task DropCollectionAsync(string name, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task DropCollectionAsync(string name, DropCollectionOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task DropCollectionAsync(IClientSessionHandle session, string name, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task DropCollectionAsync(IClientSessionHandle session, string name, DropCollectionOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IMongoCollection<TDocument> GetCollection<TDocument>(string name, MongoCollectionSettings settings = null) {
            if (_collections.TryGetValue(typeof(TDocument), out object? collection)) {
                return (IMongoCollection<TDocument>)collection;
            }

            throw new ArgumentException($"No instance of type {typeof(TDocument)} was added to the collection.");
        }

        public IAsyncCursor<string> ListCollectionNames(ListCollectionNamesOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<string> ListCollectionNames(IClientSessionHandle session, ListCollectionNamesOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<string>> ListCollectionNamesAsync(ListCollectionNamesOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<string>> ListCollectionNamesAsync(IClientSessionHandle session, ListCollectionNamesOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<BsonDocument> ListCollections(ListCollectionsOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<BsonDocument> ListCollections(IClientSessionHandle session, ListCollectionsOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<BsonDocument>> ListCollectionsAsync(ListCollectionsOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<BsonDocument>> ListCollectionsAsync(IClientSessionHandle session, ListCollectionsOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void RenameCollection(string oldName, string newName, RenameCollectionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void RenameCollection(IClientSessionHandle session, string oldName, string newName, RenameCollectionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task RenameCollectionAsync(string oldName, string newName, RenameCollectionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task RenameCollectionAsync(IClientSessionHandle session, string oldName, string newName, RenameCollectionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public TResult RunCommand<TResult>(Command<TResult> command, ReadPreference readPreference = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public TResult RunCommand<TResult>(IClientSessionHandle session, Command<TResult> command, ReadPreference readPreference = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<TResult> RunCommandAsync<TResult>(Command<TResult> command, ReadPreference readPreference = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<TResult> RunCommandAsync<TResult>(IClientSessionHandle session, Command<TResult> command, ReadPreference readPreference = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IChangeStreamCursor<TResult> Watch<TResult>(PipelineDefinition<ChangeStreamDocument<BsonDocument>, TResult> pipeline, ChangeStreamOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IChangeStreamCursor<TResult> Watch<TResult>(IClientSessionHandle session, PipelineDefinition<ChangeStreamDocument<BsonDocument>, TResult> pipeline, ChangeStreamOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IChangeStreamCursor<TResult>> WatchAsync<TResult>(PipelineDefinition<ChangeStreamDocument<BsonDocument>, TResult> pipeline, ChangeStreamOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IChangeStreamCursor<TResult>> WatchAsync<TResult>(IClientSessionHandle session, PipelineDefinition<ChangeStreamDocument<BsonDocument>, TResult> pipeline, ChangeStreamOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IMongoDatabase WithReadConcern(ReadConcern readConcern) {
            throw new NotImplementedException();
        }

        public IMongoDatabase WithReadPreference(ReadPreference readPreference) {
            throw new NotImplementedException();
        }

        public IMongoDatabase WithWriteConcern(WriteConcern writeConcern) {
            throw new NotImplementedException();
        }
    }
}
