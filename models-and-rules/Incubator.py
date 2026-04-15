import typing
from rdflib import BNode, Literal, Graph, URIRef
from rdflib.namespace import Namespace, RDF, RDFS, OWL, XSD
from RDTBindings import *

g = Graph()
MINE = Namespace("http://www.semanticweb.org/vs/ontologies/2025/12/incubator#")

g.add((URIRef(str(MINE)), OWL.imports, URIRef(str(RDT))))

heaterProperty = ObservableProperty(g, MINE["HeaterProperty"])
heaterChange = Change(g, MINE["HeaterChange"], heaterProperty, increase=True)
# Potential for RDT:Effect ->HERE<-.
heaterActuator = Actuator(g, MINE["HeaterActuator"], heaterChange, actuatorName="in_heater_state",
                              actuatorType=XSD.integer, actuatorStates=[0,1])

rtemp = ObservableProperty(g, MINE["in_room_temperature"])
rtempActuator = Actuator(g, MINE["TempActuator"], Change(g, MINE["TempChange"], rtemp), isParameter=True,
                             actuatorName="in_room_temperature", actuatorType=XSD.double, actuatorStates=[21.0])
# ^- Need to provide initial value here.

minTemp = Property(g, MINE["TemperatureLowerLimit"], 30)
maxTemp = Property(g, MINE["TemperatureUpperLimit"], 35)
temp = ObservableProperty(g, MINE["T"], None)
oc_rtemp = OptimalConditionDouble(g, MINE["oc_temp"], temp, minTemp, maxTemp)

tempSensor = Sensor(g, MINE["TempSensor"], [temp])
tempMeasure = Measure(g, MINE["TempMeasure"])

tempProcedure = Procedure(g, MINE["TempProcedure"], tempMeasure, tempSensor)

room = Platform(g, MINE["IncubatorTest"], False, [heaterActuator, rtempActuator, tempSensor], implements=[oc_rtemp])
fmu = FMU(g, MINE["Incubator_FMU"], "Source/au_incubator.fmu", 3) # 3s
room.addFMU(g, fmu)

output = g.serialize(destination=None)
print(output)
