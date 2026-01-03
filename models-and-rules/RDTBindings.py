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

class Restriction(Node):
    pass

class RestrictionL1D(Restriction): # probably not right.
    def __init__(self, g):
        self.node = BNode()
        g.add((self.node, RDF["type"], OWL["Restriction"]))
        g.add((self.node, OWL["onProperty"], RDT["hasValue"]))
        g.add((self.node, OWL["qualifiedCardinality"], Literal(1, datatype=XSD.nonNegativeInteger)))
        g.add((self.node, OWL["onDataRange"], XSD.double))

class ObservableProperty(Node):
    restriction: Restriction

    def __init__(self, g, name, restriction=None):
        elprice = name
        g.add((elprice, RDF["type"], SOSA["ObservableProperty"]))
        g.add((elprice, RDF["type"], OWL["NamedIndividual"]))
        if restriction is None:
            self.restriction = RestrictionL1D(g)            
        else:
            self.restriction = restriction
        g.add((elprice, RDF["type"], self.restriction.node))
        self.node = elprice

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
    def __init__(self, g, name: IdentifiedNode, onProperty: ObservableProperty, reachedInMaximumSeconds: int, minT: tuple[float,bool], maxT: tuple[float,bool]):
        self.node = name
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDF["type"], RDT["OptimalCondition"]))
        g.add((self.node, SSN["forProperty"], onProperty.node))
        res = BNode()
        g.add((res, RDF["type"], OWL["Restriction"]))
        g.add((res, OWL["onProperty"], RDT["hasValueConstraint"]))
        g.add((res, OWL["qualifiedCardinality"], Literal(1, datatype=XSD.nonNegativeInteger)))
        minmax = BNode()
        g.add((minmax, RDF["type"], RDFS["Datatype"]))
        g.add((minmax, OWL["onDatatype"], XSD.double))
        reslist = BNode()
        fNode = BNode()
        rNode = BNode()
        (min, minIncl) = minT
        (max, maxIncl) = maxT
        g.add((fNode, XSD["minInclusive" if minIncl else "minExclusive"], Literal(min, datatype=XSD.double)))
        g.add((rNode, XSD["maxInclusive" if maxIncl else "maxExclusive"], Literal(max, datatype=XSD.double)))        
        g.add((reslist, RDF["first"], fNode))
        g.add((reslist, RDF["rest"], rNode))
        g.add((minmax, OWL.withRestrictions, reslist))
        g.add((res, OWL["onDataRange"], minmax))

        g.add((self.node, RDF["type"], res))


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
        node = BNode()
        g.add((node, RDF["type"], OWL["Restriction"]))
        g.add((node, OWL["onProperty"], RDT["hasValue"]))
        g.add((node, OWL["hasValue"], Literal("0.0", datatype=XSD.double)))
        g.add((self.node, RDF["type"], node))

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