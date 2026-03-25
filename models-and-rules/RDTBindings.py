from abc import ABC
import typing
from typing import Optional
from rdflib import BNode, Literal, Graph, URIRef
from rdflib.namespace import Namespace, RDF, RDFS, OWL, XSD
from rdflib.term import IdentifiedNode

RDT = Namespace("http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/")
SOSA = Namespace("http://www.w3.org/ns/sosa/")
SSN = Namespace("http://www.w3.org/ns/ssn/")

# TODO: use https://rdflib.readthedocs.io/en/stable/apidocs/rdflib.term/#rdflib.term.bind
class Node(ABC):
    node: BNode

class FMU(Node):
    def __init__(self, g, name: IdentifiedNode, fmuPath: str, fidelity):
        self.node = name
        g.add((self.node, RDF["type"], RDT["FmuModel"]))
        g.add((self.node, RDT["hasSimulationFidelitySeconds"], Literal(fidelity, datatype=XSD.integer)))
        g.add((self.node, RDT["hasURI"], Literal(fmuPath, datatype=XSD.anyURI)))

class ObservableProperty(Node):
    def __init__(self, g, name, restriction=None):
        self.node = name
        g.add((self.node, RDF["type"], SOSA["ObservableProperty"]))
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDT["hasValue"], Literal("0.0", datatype=XSD.double)))

class Property(Node):
    def __init__(self, g, name, value = 0.0):
        self.node = name
        g.add((self.node, RDF["type"], SOSA["Property"]))
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDT["hasValue"], Literal(value, datatype=XSD.double)))

class Change(Node):
    def __init__(self, g, name: IdentifiedNode, affects: ObservableProperty):
        self.node = name
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDF["type"], RDT["PropertyChangeByActuation"]))
        g.add((self.node, SSN["forProperty"], affects.node))
        g.add((self.node, RDT["affectsPropertyWith"], RDT["ValueIncrease"])) # TODO

class Actuator(Node):
    def __init__(self, g, name: IdentifiedNode, enacts: Change):
        self.node = name
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDF["type"], SOSA["Actuator"]))
        g.add((self.node, SOSA["enacts"], enacts.node))

class OptimalCondition(Node):
    pass

class OptimalConditionDouble(OptimalCondition):
    def __init__(self, g, name: IdentifiedNode, onProperty: ObservableProperty, minT: Property, maxT: Property, breakable:bool = True):
        self.node = name
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDF["type"], RDT["OptimalCondition"]))
        g.add((self.node, SSN["forProperty"], onProperty.node))
        g.add((self.node, RDT["isBreakable"], Literal(breakable, datatype=XSD.boolean)))
        min = BNode()
        g.add((min, RDF["type"], OWL["Restriction"]))
        g.add((min, OWL["onProperty"], RDT["greaterThan"])) #TODO: fix this when making this class accept all 5 supported types of OptimalConditions.
        g.add((min, OWL["hasValue"], minT.node))
        max = BNode()
        g.add((max, RDF["type"], OWL["Restriction"]))
        g.add((max, OWL["onProperty"], RDT["lessThan"]))
        g.add((max, OWL["hasValue"], maxT.node))
        intersectionContainer = BNode()
        g.add((intersectionContainer, RDF["type"], OWL["Class"]))
        intersectionList2 = BNode()
        g.add((intersectionList2, RDF.first, max))
        g.add((intersectionList2, RDF.rest, RDF.nil))
        intersectionList1 = BNode()
        g.add((intersectionList1, RDF.first, min))
        g.add((intersectionList1, RDF.rest, intersectionList2))
        g.add((intersectionContainer, OWL["intersectionOf"], intersectionList1))
        res = BNode()
        g.add((res, RDF["type"], OWL["Restriction"]))
        g.add((res, OWL["onProperty"], RDT["hasConstraint"]))
        g.add((res, OWL["qualifiedCardinality"], Literal(1, datatype=XSD.nonNegativeInteger)))
        g.add((res, OWL["onClass"], intersectionContainer))
        g.add((self.node, RDF["type"], res))

        #minmax = BNode()
        #g.add((minmax, RDF["type"], RDFS["Datatype"]))
        #g.add((minmax, OWL["onDatatype"], XSD.double))
        #reslist = BNode()
        #fNode = BNode()
        #rNode = BNode()
        #f2Node = BNode()
        #r2Node = BNode()
        #(min, minIncl) = minT
        #(max, maxIncl) = maxT
        #g.add((fNode, XSD["minInclusive" if minIncl else "minExclusive"], Literal(min, datatype=XSD.double)))
        #g.add((f2Node, XSD["maxInclusive" if maxIncl else "maxExclusive"], Literal(max, datatype=XSD.double)))        
        #g.add((reslist, RDF["first"], fNode))
        #g.add((reslist, RDF["rest"], rNode))
        #g.add((rNode, RDF["first"], f2Node))
        #g.add((rNode, RDF["rest"], r2Node))
        #g.add((minmax, OWL.withRestrictions, reslist))


class Platform(Node):
    def __init__(self, g, name: IdentifiedNode, gcofoc: bool, hosts: Actuator | list[Actuator], implements = []):
        self.node = name
        g.add((self.node, RDF.type, OWL.NamedIndividual))
        g.add((self.node, RDF.type, SOSA.Platform))
        g.add((self.node, RDT.generateCombinationsOnlyFromOptimalConditions, Literal("true" if gcofoc else "false", datatype=XSD.boolean))) # TODO?
        if isinstance(hosts, list):
            for a in hosts:
                g.add((self.node, SOSA.hosts, a.node))
        else:
            g.add((self.node, SOSA.hosts, hosts.node))
        for i in implements:
            g.add((self.node, SSN.implements, i.node))


    # TODO: eliminate `g`?
    def addFMU(self, g, fmu: FMU):
        g.add((self.node, RDT["hasSimulationModel"], fmu.node))

class Measure(Node):
    def __init__(self, g, name):
        self.node = name
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        # g.add((self.node, RDF["type"], SSN["Input"]))
        g.add((self.node, RDF["type"], SSN["Output"]))
        g.add((self.node, RDF["type"], SSN["Property"]))
        g.add((self.node, RDT["hasValue"], Literal("0.0", datatype=XSD.double)))

class Sensor(Node):
    def __init__(self, g, name, observes: ObservableProperty | list[ObservableProperty]):        
        self.node = name
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDF["type"], SOSA["Sensor"]))
        if isinstance(observes, list):
            for o in observes:
                g.add((self.node, SOSA["observes"], o.node))
        else:
            g.add((self.node, SOSA["observes"], observes.node))

class Procedure(Node):
    measure: Measure
    sensor: Sensor

    def __init__(self, g, name, measure: Measure, sensor: Optional[Sensor] = None):
        self.node = name
        self.measure = measure
        self.sensor = sensor
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDF["type"], SOSA["Procedure"]))
        g.add((self.node, SSN["hasOutput"], measure.node))
        if sensor is not None:
            g.add((sensor.node, SSN["implements"], self.node))