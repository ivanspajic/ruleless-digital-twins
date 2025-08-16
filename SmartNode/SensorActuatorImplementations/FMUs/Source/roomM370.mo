model roomM370
  input String HeaterState(start = "");
  input String DehumidifierState(start = "");
  Real RoomTemperature(start = 20.4);
  Real RoomHumidity(start = 20.0);
  Real EnergyConsumption(start = 0);
  constant Integer slowdownValue = 1000;
  
function GetRoomTemperatureLimit
  input String HeaterState;
  output Integer RoomTemperatureLimit;
algorithm
  if HeaterState == "HeaterStrong" then
    RoomTemperatureLimit := 30;
  elseif HeaterState == "HeaterMedium" then
    RoomTemperatureLimit := 24;
  elseif HeaterState == "HeaterWeak" then
    RoomTemperatureLimit := 18;
  else
    RoomTemperatureLimit := 12;
  end if;
end GetRoomTemperatureLimit;

function GetRoomHumidityLimit
  input String DehumidifierState;
  output Integer RoomHumidityLimit;
algorithm
  if DehumidifierState == "DehumidifierOn" then
    RoomHumidityLimit := 2;
  else
    RoomHumidityLimit := 10;
  end if;
end GetRoomHumidityLimit;

function GetEnergyConsumptionRate
  input String HeaterState;
  input String DehumidifierState;
  output Real EnergyConsumptionRate;
algorithm
  EnergyConsumptionRate := 0;
  if HeaterState == "HeaterStrong" then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.05;
  elseif HeaterState == "HeaterMedium" then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.025;
  elseif HeaterState == "HeaterWeak" then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.01;
  end if;
  if DehumidifierState == "DehumidifierOn" then
    EnergyConsumptionRate := EnergyConsumptionRate + 0.03;
  end if;
end GetEnergyConsumptionRate;

equation
  der(RoomTemperature) = (GetRoomTemperatureLimit("") - RoomTemperature) / slowdownValue;
  der(RoomHumidity) = (GetRoomHumidityLimit("") - RoomHumidity) / slowdownValue;
  der(EnergyConsumption) = GetEnergyConsumptionRate("", "");
  
annotation(
    experiment(StartTime = 0, StopTime = 8000, Tolerance = 1e-06, Interval = 1));
end roomM370;
