from abc import ABC
import typing
from typing import Optional
from rdflib import BNode, Literal, Graph, URIRef
from rdflib.namespace import Namespace, RDF, OWL, XSD
from rdflib.term import IdentifiedNode

# g.parse("ruleless-digital-twins/models-and-rule s/instance-model-1.ttl")
SAREF = Namespace("https://saref.etsi.org/core/")
# OWL =  Namespace("http://www.w3.org/2002/07/owl#")
# RDF = Namespace("http://www.w3.org/1999/02/22-rdf-syntax-ns#")
RDT = Namespace("http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/")
SOSA = Namespace("http://www.w3.org/ns/sosa/")
SSN = Namespace("http://www.w3.org/ns/ssn/")

# TODO: use https://rdflib.readthedocs.io/en/stable/apidocs/rdflib.term/#rdflib.term.bind
class Node(ABC):
    node: BNode

class FMU(Node):
    def __init__(self, g, name: IdentifiedNode, fidelity):
        self.node = name
        g.add((self.node, RDF["type"], RDT["FmuModel"]))
        g.add((self.node, RDT["hasSimulationFidelitySeconds"], Literal(fidelity, datatype=XSD.integer)))
        g.add((self.node, RDT["hasUri"], Literal("NordPool.fmu", datatype=XSD.string)))

class Platform(Node):
    def __init__(self, g, name: IdentifiedNode):
        self.node = name
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDF["type"], SOSA["Platform"]))

    # TODO: eliminate `g`?
    def addFMU(self, g, fmu: FMU):
        g.add((self.node, RDT["hasSimulationModel"], fmu.node))

class Restriction(Node):
    pass

class RestrictionL1D(Restriction):
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

class Measure(Node):
    def __init__(self, g, name, restriction: Restriction):
        self.node = name
        g.add((self.node, RDF["type"], OWL["NamedIndividual"]))
        g.add((self.node, RDF["type"], SSN["Output"]))
        g.add((self.node, RDF["type"], SSN["Property"]))
        g.add((self.node, RDF["type"], restriction.node))

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
            g.add((self.node, SSN["implementedBy"], sensor.node))