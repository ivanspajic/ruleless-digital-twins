model roomM370
  input Integer HeaterState(start = 0);
  input Integer FloorHeatingState(start = 0);
  input Integer DehumidifierState(start = 0);
  Real RoomTemperature;
  Real RoomHumidity;
  Real EnergyConsumption;
  constant Integer slowdownValue = 1000;
  
  // Used for testing with CLI tools since they complain they can't initialize with non input/parameter variables.
  input Real RoomTemperatureInitial(start = 20);
  input Real RoomHumidityInitial(start = 20);
  input Real EnergyConsumptionInitial(start = 0);
 
function GetRoomTemperatureLimit
  input Integer HeaterState;
  input Integer FloorHeatingState;
  output Integer RoomTemperatureLimit;
algorithm
  RoomTemperatureLimit := 12;
  if HeaterState == 2 then
    RoomTemperatureLimit := RoomTemperatureLimit + 10;
  elseif HeaterState == 1 then
    RoomTemperatureLimit := RoomTemperatureLimit + 5;
  end if;
  if FloorHeatingState == 1 then
    RoomTemperatureLimit := RoomTemperatureLimit + 7;
  end if;
end GetRoomTemperatureLimit;

function GetRoomHumidityLimit
  input Integer DehumidifierState;
  output Integer RoomHumidityLimit;
algorithm
  if DehumidifierState == 1 then
    RoomHumidityLimit := 2;
  else
    RoomHumidityLimit := 10;
  end if;
end GetRoomHumidityLimit;

function GetEnergyConsumptionRate
  input Integer HeaterState;
  input Integer FloorHeatingState;
  input Integer DehumidifierState;
  output Real EnergyConsumptionRate;
algorithm
  EnergyConsumptionRate := 0;
  if HeaterState == 2 then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.025;
  elseif HeaterState == 1 then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.01;
  end if;
  if FloorHeatingState == 1 then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.02;
  end if;
  if DehumidifierState == 1 then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.03;
  end if;
end GetEnergyConsumptionRate;

initial equation
  // Used for testing with CLI tools since they complain they can't initialize with non input/parameter variables.
  RoomTemperature = RoomTemperatureInitial;
  RoomHumidity = RoomHumidityInitial;
  EnergyConsumption = EnergyConsumptionInitial;
equation  
  der(RoomTemperature) = (GetRoomTemperatureLimit(HeaterState, FloorHeatingState) - RoomTemperature) / slowdownValue;
  der(RoomHumidity) = (GetRoomHumidityLimit(DehumidifierState) - RoomHumidity) / slowdownValue;
  der(EnergyConsumption) = GetEnergyConsumptionRate(HeaterState, FloorHeatingState, DehumidifierState);
  
annotation(
    experiment(StartTime = 0, StopTime = 8000, Tolerance = 1e-06, Interval = 1));
end roomM370;
