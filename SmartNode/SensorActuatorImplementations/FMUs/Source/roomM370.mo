model roomM370
  input Integer HeaterState(start = 0);
  input Integer DehumidifierState(start = 0);
  Real RoomTemperature(start = 20.4);
  Real RoomHumidity(start = 20.0);
  Real EnergyConsumption(start = 0);
  constant Integer slowdownValue = 1000;
  
function GetRoomTemperatureLimit
  input Integer HeaterState;
  output Integer RoomTemperatureLimit;
algorithm
  if HeaterState == 3 then
    RoomTemperatureLimit := 30;
  elseif HeaterState == 2 then
    RoomTemperatureLimit := 24;
  elseif HeaterState == 1 then
    RoomTemperatureLimit := 18;
  else
    RoomTemperatureLimit := 12;
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
  input Integer DehumidifierState;
  output Real EnergyConsumptionRate;
algorithm
  EnergyConsumptionRate := 0;
  if HeaterState == 3 then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.05;
  elseif HeaterState == 2 then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.025;
  elseif HeaterState == 1 then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.01;
  end if;
  if DehumidifierState == 1 then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.03;
  end if;
end GetEnergyConsumptionRate;

equation
  der(RoomTemperature) = (GetRoomTemperatureLimit(0) - RoomTemperature) / slowdownValue;
  der(RoomHumidity) = (GetRoomHumidityLimit(0) - RoomHumidity) / slowdownValue;
  der(EnergyConsumption) = GetEnergyConsumptionRate(0, 0);
  
annotation(
    experiment(StartTime = 0, StopTime = 8000, Tolerance = 1e-06, Interval = 1));
end roomM370;
