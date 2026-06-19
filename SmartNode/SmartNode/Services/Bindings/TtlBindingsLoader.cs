using VDS.RDF;
using VDS.RDF.Parsing;

namespace SmartNode.Services.Bindings {

    public record SensorBinding(string Uri, string HaEntityId, string ObservesProperty, string ProcedureUri);

    public record ActuatorBinding(string Uri, string HaEntityId, List<string> PossibleStates);

    public record ConfigParamBinding(string Uri, string HaEntityId, string Field, List<string> PossibleValues);

    public class BindingsResult {
        public Dictionary<string, SensorBinding>       SensorMap      { get; } = new();
        public Dictionary<string, ActuatorBinding>     ActuatorMap    { get; } = new();
        public Dictionary<string, ConfigParamBinding>  ConfigParamMap { get; } = new();
    }

    public static class TtlBindingsLoader {

        // Namespace constants — match what hacvt_rdt.py emits
        private const string SosaNs = "http://www.w3.org/ns/sosa/";
        private const string RdtNs  = "http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/";

        private static readonly Uri SosaSensor   = new(SosaNs + "Sensor");
        private static readonly Uri SosaActuator = new(SosaNs + "Actuator");
        private static readonly Uri RdfType      = new("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
        private static readonly Uri RdfsSubClassOf = new("http://www.w3.org/2000/01/rdf-schema#subClassOf");

        private const string SsnNs = "http://www.w3.org/ns/ssn/";

        private static readonly Uri SosaObserves        = new(SosaNs + "observes");
        private static readonly Uri SsnImplements       = new(SsnNs  + "implements");
        private static readonly Uri RdtHasIdentifier        = new(RdtNs  + "hasIdentifier");
        private static readonly Uri RdtHasActuatorState     = new(RdtNs  + "hasActuatorState");
        private static readonly Uri RdtConfigurableParam    = new(RdtNs  + "ConfigurableParameter");
        private static readonly Uri RdtHasActuatorName      = new(RdtNs  + "hasActuatorName");
        private static readonly Uri RdtHasPossibleValue     = new(RdtNs  + "hasPossibleValue");

        /// <summary>
        /// Parses a Turtle file and returns sensor/actuator bindings derived from
        /// sosa:Sensor / sosa:Actuator individuals.  Returns null if the file cannot
        /// be read.
        /// </summary>
        public static BindingsResult? Load(string ttlPath) {
            if (!File.Exists(ttlPath)) {
                Console.WriteLine($"[TtlBindingsLoader] File not found: {ttlPath}");
                return null;
            }

            var store = new TripleStore();
            var parser = new TurtleParser();
            IGraph g = new Graph();

            try {
                parser.Load(g, ttlPath);
                store.Add(g);
            } catch (Exception ex) {
                Console.WriteLine($"[TtlBindingsLoader] Parse error in '{ttlPath}': {ex.Message}");
                return null;
            }

            var result = new BindingsResult();

            var typeNode           = g.CreateUriNode(RdfType);
            var sensorType         = g.CreateUriNode(SosaSensor);
            var actuatorType       = g.CreateUriNode(SosaActuator);
            var configParamType    = g.CreateUriNode(RdtConfigurableParam);
            var observesNode       = g.CreateUriNode(SosaObserves);
            var implementsNode     = g.CreateUriNode(SsnImplements);
            var identifierNode     = g.CreateUriNode(RdtHasIdentifier);
            var stateNode          = g.CreateUriNode(RdtHasActuatorState);
            var actuatorNameNode   = g.CreateUriNode(RdtHasActuatorName);
            var possibleValueNode  = g.CreateUriNode(RdtHasPossibleValue);

            // Resolve the set of types that count as Sensor / Actuator. Two TTL
            // dialects exist in this project:
            //   - upstream (incubator.ttl): individuals typed directly as
            //     `a sosa:Sensor` / `a sosa:Actuator`.
            //   - hacvt_rdt.py (HA export): individuals typed via a domain class
            //     (e.g. `a hass:Fan`), where `hass:Fan rdfs:subClassOf sosa:Actuator`.
            // Collect sosa:Sensor/Actuator plus every (transitive) subclass so
            // both dialects resolve.
            var sensorTypes   = CollectTypeAndSubclasses(g, sensorType);
            var actuatorTypes = CollectTypeAndSubclasses(g, actuatorType);

            int missingSensorId    = 0;
            int missingActuatorId  = 0;
            int statelessActuator  = 0;
            int missingParamId     = 0;
            int valuelessParam     = 0;

            // ---- Sensors ----
            foreach (var subject in InstancesOfTypes(g, typeNode, sensorTypes)) {
                string uri          = subject.Uri.AbsoluteUri;
                if (result.SensorMap.ContainsKey(uri)) continue;
                string entityId     = GetLiteralObject(g, subject, identifierNode);
                if (string.IsNullOrEmpty(entityId)) {
                    Console.WriteLine($"[TtlBindingsLoader] WARNING: sensor '{uri}' has no rdt:hasIdentifier — it will not map to a runtime HA entity.");
                    missingSensorId++;
                }
                string observes     = GetUriObject(g, subject, observesNode);
                string procedureUri = GetUriObject(g, subject, implementsNode);
                result.SensorMap[uri] = new SensorBinding(uri, entityId, observes, procedureUri);
            }

            // ---- Actuators ----
            foreach (var subject in InstancesOfTypes(g, typeNode, actuatorTypes)) {
                string uri      = subject.Uri.AbsoluteUri;
                if (result.ActuatorMap.ContainsKey(uri)) continue;
                string entityId = GetLiteralObject(g, subject, identifierNode);
                if (string.IsNullOrEmpty(entityId)) {
                    Console.WriteLine($"[TtlBindingsLoader] WARNING: actuator '{uri}' has no rdt:hasIdentifier — it will not map to a runtime HA entity.");
                    missingActuatorId++;
                }
                var states      = g.GetTriplesWithSubjectPredicate(subject, stateNode)
                                   .Select(st => st.Object is ILiteralNode ln ? ln.Value : "")
                                   .Where(s => s != "")
                                   .ToList();
                if (states.Count == 0) {
                    Console.WriteLine($"[TtlBindingsLoader] WARNING: actuator '{uri}' has no rdt:hasActuatorState — the planner cannot enumerate it.");
                    statelessActuator++;
                }
                result.ActuatorMap[uri] = new ActuatorBinding(uri, entityId, states);
            }

            // ---- ConfigurableParameters ----
            // Direct instances of rdt:ConfigurableParameter (not a subclass hierarchy needed here).
            var configParamTypes = new HashSet<string> { RdtConfigurableParam.AbsoluteUri };
            foreach (var subject in InstancesOfTypes(g, typeNode, configParamTypes)) {
                string uri      = subject.Uri.AbsoluteUri;
                if (result.ConfigParamMap.ContainsKey(uri)) continue;
                string entityId = GetLiteralObject(g, subject, identifierNode);
                if (string.IsNullOrEmpty(entityId)) {
                    Console.WriteLine($"[TtlBindingsLoader] WARNING: configurable parameter '{uri}' has no rdt:hasIdentifier — it will not map to a runtime HA entity.");
                    missingParamId++;
                }
                string field    = GetLiteralObject(g, subject, actuatorNameNode);
                var values      = g.GetTriplesWithSubjectPredicate(subject, possibleValueNode)
                                   .Select(st => st.Object is ILiteralNode ln ? ln.Value : "")
                                   .Where(s => s != "")
                                   .ToList();
                if (values.Count == 0) {
                    Console.WriteLine($"[TtlBindingsLoader] WARNING: configurable parameter '{uri}' has no rdt:hasPossibleValue — the planner cannot enumerate it.");
                    valuelessParam++;
                }
                result.ConfigParamMap[uri] = new ConfigParamBinding(uri, entityId, field, values);
            }

            Console.WriteLine(
                $"[TtlBindingsLoader] Loaded {result.SensorMap.Count} sensor(s), " +
                $"{result.ActuatorMap.Count} actuator(s) and " +
                $"{result.ConfigParamMap.Count} configurable parameter(s) from '{ttlPath}'.");
            if (missingSensorId + missingActuatorId + statelessActuator + missingParamId + valuelessParam > 0) {
                Console.WriteLine(
                    $"[TtlBindingsLoader] WARNING summary: {missingSensorId} sensor(s) and " +
                    $"{missingActuatorId} actuator(s) without an identifier, " +
                    $"{statelessActuator} actuator(s) without states, " +
                    $"{missingParamId} configurable parameter(s) without identifier, " +
                    $"{valuelessParam} configurable parameter(s) without values.");
            }

            return result;
        }

        // -- Helpers --

        /// <summary>
        /// Returns the given base type plus every class that is, directly or
        /// transitively, an rdfs:subClassOf of it. Lets the loader pick up
        /// individuals typed via a domain subclass (e.g. hass:Fan) as well as
        /// those typed directly as sosa:Sensor / sosa:Actuator.
        /// </summary>
        private static HashSet<string> CollectTypeAndSubclasses(IGraph g, IUriNode baseType) {
            var result  = new HashSet<string> { baseType.Uri.AbsoluteUri };
            var subClassOf = g.CreateUriNode(RdfsSubClassOf);
            var frontier = new Queue<IUriNode>();
            frontier.Enqueue(baseType);

            while (frontier.Count > 0) {
                var current = frontier.Dequeue();
                // find ?sub rdfs:subClassOf current
                foreach (Triple t in g.GetTriplesWithPredicateObject(subClassOf, current)) {
                    if (t.Subject is IUriNode sub && result.Add(sub.Uri.AbsoluteUri)) {
                        frontier.Enqueue(sub);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Yields the distinct IUriNode subjects whose rdf:type is in the given
        /// type set.
        /// </summary>
        private static IEnumerable<IUriNode> InstancesOfTypes(IGraph g, IUriNode typeNode, HashSet<string> types) {
            var seen = new HashSet<string>();
            foreach (Triple t in g.GetTriplesWithPredicate(typeNode)) {
                if (t.Object is not IUriNode objType) continue;
                if (!types.Contains(objType.Uri.AbsoluteUri)) continue;
                if (t.Subject is not IUriNode subject) continue;
                if (seen.Add(subject.Uri.AbsoluteUri)) yield return subject;
            }
        }

        private static string GetLiteralObject(IGraph g, IUriNode subject, IUriNode predicate) {
            var triple = g.GetTriplesWithSubjectPredicate(subject, predicate).FirstOrDefault();
            return triple?.Object is ILiteralNode ln ? ln.Value : string.Empty;
        }

        private static string GetUriObject(IGraph g, IUriNode subject, IUriNode predicate) {
            var triple = g.GetTriplesWithSubjectPredicate(subject, predicate).FirstOrDefault();
            return triple?.Object is IUriNode un ? un.Uri.AbsoluteUri : string.Empty;
        }
    }
}
