using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;

namespace TestProject.Mocks.MongoDB {
    internal class MongoClientMock : IMongoClient {
        private readonly IMongoDatabase _mongoDatabase;

        public MongoClientMock(IMongoDatabase mongoDatabase) {
            _mongoDatabase = mongoDatabase;
        }

        public ICluster Cluster => throw new NotImplementedException();

        public MongoClientSettings Settings => throw new NotImplementedException();

        public ClientBulkWriteResult BulkWrite(IReadOnlyList<BulkWriteModel> models, ClientBulkWriteOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public ClientBulkWriteResult BulkWrite(IClientSessionHandle session, IReadOnlyList<BulkWriteModel> models, ClientBulkWriteOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<ClientBulkWriteResult> BulkWriteAsync(IReadOnlyList<BulkWriteModel> models, ClientBulkWriteOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<ClientBulkWriteResult> BulkWriteAsync(IClientSessionHandle session, IReadOnlyList<BulkWriteModel> models, ClientBulkWriteOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void Dispose() {
            throw new NotImplementedException();
        }

        public void DropDatabase(string name, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public void DropDatabase(IClientSessionHandle session, string name, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task DropDatabaseAsync(string name, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task DropDatabaseAsync(IClientSessionHandle session, string name, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IMongoDatabase GetDatabase(string name, MongoDatabaseSettings settings = null) {
            return _mongoDatabase;
        }

        public IAsyncCursor<string> ListDatabaseNames(CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<string> ListDatabaseNames(ListDatabaseNamesOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<string> ListDatabaseNames(IClientSessionHandle session, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<string> ListDatabaseNames(IClientSessionHandle session, ListDatabaseNamesOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<string>> ListDatabaseNamesAsync(CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<string>> ListDatabaseNamesAsync(ListDatabaseNamesOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<string>> ListDatabaseNamesAsync(IClientSessionHandle session, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<string>> ListDatabaseNamesAsync(IClientSessionHandle session, ListDatabaseNamesOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<BsonDocument> ListDatabases(CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<BsonDocument> ListDatabases(ListDatabasesOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<BsonDocument> ListDatabases(IClientSessionHandle session, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IAsyncCursor<BsonDocument> ListDatabases(IClientSessionHandle session, ListDatabasesOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<BsonDocument>> ListDatabasesAsync(CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<BsonDocument>> ListDatabasesAsync(ListDatabasesOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<BsonDocument>> ListDatabasesAsync(IClientSessionHandle session, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<BsonDocument>> ListDatabasesAsync(IClientSessionHandle session, ListDatabasesOptions options, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public IClientSessionHandle StartSession(ClientSessionOptions options = null, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<IClientSessionHandle> StartSessionAsync(ClientSessionOptions options = null, CancellationToken cancellationToken = default) {
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

        public IMongoClient WithReadConcern(ReadConcern readConcern) {
            throw new NotImplementedException();
        }

        public IMongoClient WithReadPreference(ReadPreference readPreference) {
            throw new NotImplementedException();
        }

        public IMongoClient WithWriteConcern(WriteConcern writeConcern) {
            throw new NotImplementedException();
        }
    }
}
