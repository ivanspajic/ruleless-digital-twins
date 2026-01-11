model au_incubator
  // Transcribed from `incubator/models/plant_models/two_parameters_model/two_parameter_model.py`
  input Integer in_heater_state(start = 0);
  input Real in_heater_voltage(start = 12.15579391);
  input Real in_heater_current(start = 1.53551347);
  input Real G_box(start = 0.5763498);
  input Real C_air(start = 267.55929458);
  
  parameter Real in_room_temperature(start = 20);
  parameter Real in_box_temperature(start = 20);
  
  Real power_out_box;
  Real total_power_box;
  Real T;

initial equation
  T = in_box_temperature;

equation
  power_out_box = G_box * (T - in_room_temperature);
  total_power_box = GetPowerIn(in_heater_state, in_heater_voltage, in_heater_current) - power_out_box;
  der(T) = (1 / C_air) * total_power_box;
end au_incubator;

function GetPowerIn
  input Integer in_heater_state;
  input Real in_heater_voltage;
  input Real in_heater_current;
  output Real power_in;
algorithm
    power_in := in_heater_state * in_heater_voltage * in_heater_current;
end GetPowerIn;
