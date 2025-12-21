model au_incubator
  input Integer in_heater_state(start = 0);
  input Real in_heater_voltage(start = 11.416531);
  input Real in_heater_current(start = 1.42706637);
  input Real G_box(start = 0.49605615);
  input Real C_air(start = 64.2171870); // This value is actually listed as 6.42171870e+02, which is over 640 degrees C. Not sure if this is a typo or if this means something else.
  
  parameter Real C_heater(fixed = false); // This has a start value, but I'm not sure where it's from.
  parameter Real G_heater(fixed = false); // This has a start value, but I'm not sure where it's from.
  parameter Real in_room_temperature(start = 10); // This has a start value, but I'm not sure where it's from.
  
  Real T;
  Real power_out_box;
  Real total_power_box;
  Real T_heater;
  Real power_transfer_heat;
  Real total_power_heater;
  
function GetPowerIn
  input Integer in_heater_state;
  input Real in_heater_voltage;
  input Real in_heater_current;
  output Real power_in;
algorithm
  if in_heater_state == 1 then
    power_in := in_heater_voltage * in_heater_current;
  else
    power_in := 0;
  end if;
end GetPowerIn;

initial equation
  T = in_room_temperature; // This has a start value, but I'm not sure where it's from.
  T_heater = in_room_temperature; // This has a start value, but I'm not sure where it's from.
equation
  power_out_box = G_box * (T - in_room_temperature);
  power_transfer_heat = G_heater * (T_heater - T);
  total_power_heater = GetPowerIn(in_heater_state, in_heater_voltage, in_heater_current) - power_transfer_heat;
  total_power_box = power_transfer_heat - power_out_box;
  der(T) = (1 / C_air) * total_power_box;
  der(T_heater) = (1 / C_heater) * total_power_heater;

end au_incubator;
