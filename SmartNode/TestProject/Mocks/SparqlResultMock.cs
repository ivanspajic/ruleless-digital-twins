using System;
using System.Collections;
using System.Collections.Generic;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Writing.Formatting;

namespace TestProject.Mocks {
    internal class SparqlResultMock : ISparqlResult {
        public Dictionary<string, INode> Nodes { get; set; }

        public INode this[string variable] => Nodes[variable];

        public INode this[int index] => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public IEnumerable<string> Variables => Nodes.Keys;

        public bool IsGroundResult => throw new NotImplementedException();

        public bool Equals(ISparqlResult? other) {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<string, INode>> GetEnumerator() {
            throw new NotImplementedException();
        }

        public bool HasBoundValue(string variable) {
            throw new NotImplementedException();
        }

        public bool HasValue(string variable) {
            throw new NotImplementedException();
        }

        public void SetValue(string variable, INode value) {
            throw new NotImplementedException();
        }

        public string ToString(INodeFormatter formatter) {
            throw new NotImplementedException();
        }

        public void Trim() {
            throw new NotImplementedException();
        }

        public bool TryGetBoundValue(string variable, out INode value) {
            throw new NotImplementedException();
        }

        public bool TryGetValue(string variable, out INode value) {
            throw new NotImplementedException();
        }

        public INode Value(string variable) {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
