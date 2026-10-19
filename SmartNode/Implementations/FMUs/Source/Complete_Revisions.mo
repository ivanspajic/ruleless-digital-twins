within TAM_SCHT.Combined_Model;
model Complete_Revisions
  "Combined model of all the thermal zones of the home with the HVAC equipment"
  Components.ThermalZone thermalZone
    annotation (Placement(transformation(extent={{60,0},{100,40}})));
  Components.CrawlspaceThermalZone crawlspaceThermalZone
    annotation (Placement(transformation(extent={{60,-40},{100,-20}})));
   
   
  //======================================   
  input Modelica.Blocks.Interfaces.RealInput fmu_TDryBul(start = 305.8);
  input Modelica.Blocks.Interfaces.RealInput fmu_TWetBul(start = 298.15);
  input Modelica.Blocks.Interfaces.RealInput fmu_TDewPoi(start = 295.3);
  input Modelica.Blocks.Interfaces.RealInput fmu_TBlaSky(start = 295.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_relHum(start = 0.528);
  input Modelica.Blocks.Interfaces.RealInput fmu_HDirNor(start = 0.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_HGloHor(start = 0.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_HDifHor(start = 0.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_HHorIR(start = 0.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_winDir(start = 3.68);
  input Modelica.Blocks.Interfaces.RealInput fmu_winSpe(start = 4.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_ceiHei(start = 30.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_nOpa(start = 1.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_nTot(start = 1.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_lat(start = 0.534);
  input Modelica.Blocks.Interfaces.RealInput fmu_lon(start = 0.428);
  input Modelica.Blocks.Interfaces.RealInput fmu_alt(start = 90.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_pAtm(start = 101450.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_solAlt(start = 1.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_solDec(start = 1.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_solHouAng(start = 1.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_solZen(start = 1.047);
  input Modelica.Blocks.Interfaces.RealInput fmu_solTim(start = 36000.0);
  input Modelica.Blocks.Interfaces.RealInput fmu_cloTim(start = 18704100.0);
  input Modelica.Blocks.Interfaces.BooleanInput fmu_coil(start = false);
  
   
  parameter
    Buildings.Fluid.HeatExchangers.DXCoils.AirCooled.Data.Generic.DXCoil
    DXdata(nSta=1, sta={
        Buildings.Fluid.HeatExchangers.DXCoils.AirCooled.Data.Generic.BaseClasses.Stage(
            spe=1050/60,
            nomVal=
          Buildings.Fluid.HeatExchangers.DXCoils.AirCooled.Data.Generic.BaseClasses.NominalValues(
              Q_flow_nominal=-8323.2164,
              COP_nominal=3.5756,
              //SHR_nominal=0.80609,
              
              // Clauded
              SHR_nominal=0.9,
              // Clauded
              
              m_flow_nominal=0.56917,
              TEvaIn_nominal=273.15 + 19.444,
              
              // Clauded
              phiIn_nominal  = 0.5,
              // Clauded
              
              TConIn_nominal=273.15 + 35),
            perCur=
          Buildings.Fluid.HeatExchangers.DXCoils.AirCooled.Data.Generic.BaseClasses.PerformanceCurve(
              capFunT={1.3385,-5.24983e-2,2.43862e-3,4.26456e-3,-7.52958e-5,
            -4.36935e-4},
              capFunFF={1},
              EIRFunT={0.268515,7.87963e-2,-2.2665e-3,-1.23549e-2,7.2032e-4,
            -4.98223e-4},
              EIRFunFF={1},
              TConInMin=273.15 + 29.444,
              TConInMax=273.15 + 46.111,
              TEvaInMin=273.15 + 13.889,
              TEvaInMax=273.15 + 24.444,
              ffMin=0.875,
              ffMax=1.125))})
    "Contains the data for DX coil used to represent the split system AC at TAM-SCHT"
    annotation (Placement(transformation(extent={{-60,-40},{-40,-20}})));
  Buildings.Fluid.HeatExchangers.DXCoils.AirCooled.Data.Generic.BaseClasses.PerformanceCurve
    CalculatedPerformance(
    capFunT={1.33852372,-5.24983116e-2,4.2645614e-3,2.43862221e-3,-4.36934869e-4,
        -7.52957926e-5},
    capFunFF={1},
    EIRFunT={0.26851508,0.07879632,-0.01235492,-0.0022665,-0.00049822,
        0.00072032},
    EIRFunFF={1},
    TConInMin=302.5944,
    TConInMax=319.2611,
    TEvaInMin=287.0389,
    TEvaInMax=297.5944,
    ffMin=0.5,
    ffMax=1.5)
    "This is the calculated performance curve for the combined AC unit and indoor fan coil at TAM-SCHT. The AC unit is a 2.5 ton unit (#R4A530AGKB) and the fan coil is a 3 ton unit (#FEU003600)"
    annotation (Placement(transformation(extent={{-20,-40},{0,-20}})));
  Modelica.Blocks.Logical.OnOffController onOffController(bandwidth=200, pre_y_start=true)
    annotation (Placement(transformation(extent={{-80,40},{-60,60}})));
  Modelica.Blocks.Logical.Not not1
    annotation (Placement(transformation(extent={{-40,40},{-20,60}})));
  Modelica.Blocks.Sources.Constant TSetpoint(k=23.89)
    "This is the setpoint used for TAM-SCHT. Since it operates with 75F cooling and 69F heating setpoints, use 72F (22.22C) with a deadband of 3F to satisfy this dual setpoint control from the Ecobee"
    annotation (Placement(transformation(extent={{-10,-10},{10,10}},
        rotation=0,
        origin={-90,80})));
  Modelica.Fluid.Sensors.TemperatureTwoPort temperature(redeclare package
      Medium = Buildings.Media.Air)
    annotation (Placement(transformation(extent={{-40,0},{-20,20}})));
    

    
    //============================================
  //Buildings.BoundaryConditions.WeatherData.ReaderTMY3 weaDat(filNam=
  //Modelica.Utilities.Files.loadResource("modelica://TAM_SCHT/Resources/TAMSCHT_Weather_08042022_08112022.mos")

        // "C:/Users/thoma/Documents/Texas AM/Grad School/SHL/Modeling and Simulation/Modelica/TAMSCHT_Weather_08042022_08112022.mos")
//)   "College Station airport weather data"
    //annotation (Placement(transformation(extent={{-100,100},{-80,120}})));
    //============================================
    
    
    
    
  Buildings.BoundaryConditions.WeatherData.Bus weaBus annotation (Placement(
        transformation(extent={{-40,100},{-20,120}}),iconTransformation(
          extent={{-214,36},{-194,56}})));
  Buildings.Fluid.HeatExchangers.DXCoils.AirCooled.SingleSpeed sinSpeDX(
      datCoi=DXdata, redeclare package Medium = Buildings.Media.Air,
    allowFlowReversal=false,
    dp_nominal=1000
    //Clauded
    //computeReevaporation=false
    //Clauded
    )
    "DX coil to represent the AC system at TAM-SCHT. Uses the AHRI data to fit the modifier factors that Buildings library uses to calculate the perfromance curves"
    annotation (Placement(transformation(extent={{-80,0},{-60,20}})));
  Buildings.Fluid.Movers.FlowControlled_m_flow fan(
    redeclare package Medium = Buildings.Media.Air,
    allowFlowReversal=false,
    m_flow_nominal=0.56917,
    addPowerToMedium=false,
    nominalValuesDefineDefaultPressureCurve=true,
    // Clauded
    use_inputFilter=false)
    // Clauded
    annotation (Placement(transformation(extent={{0,0},{20,20}})));
  Components.AtticThermalZone_NewRoof atticThermalZone_NewRoof
    annotation (Placement(transformation(extent={{60,60},{100,100}})));
  Modelica.Blocks.Interfaces.RealOutput HouseTemp
    "Temperature output from the thermal zone of the living space"
    annotation (Placement(transformation(extent={{120,-10},{140,10}})));
  Modelica.Blocks.Interfaces.RealOutput ACPower
    "Power consumed by the AC unit (DX coil) to cool the house "
    annotation (Placement(transformation(extent={{120,66},{140,86}})));
  Modelica.Blocks.Interfaces.RealOutput FanPower
    "Power consumed by the fan "
    annotation (Placement(transformation(extent={{120,46},{140,66}})));
  Modelica.Blocks.Sources.Constant FanOn(k=0.56917)
    annotation (Placement(transformation(extent={{0,40},{20,60}})));  
    
  // Clauded
  Modelica.Blocks.Sources.BooleanStep startOn(startTime=1, startValue=true)
    "true until t = 1 s, then false";
  Modelica.Blocks.Logical.Or forceOn
    "coil on if the input says so, or during the first second";
  
  Buildings.Fluid.Sources.Outside out(
    redeclare package Medium = Buildings.Media.Air,
    nPorts=1);
  Buildings.Fluid.FixedResistances.PressureDrop leak(
    redeclare package Medium = Buildings.Media.Air,
    m_flow_nominal=0.05,
    dp_nominal=500);
  // Clauded
    
equation
  connect(TSetpoint.y, onOffController.reference) annotation (Line(points={{-79,80},
          {-74,80},{-74,66},{-82,66},{-82,56}}, color={0,0,127}));
  connect(thermalZone.ZoneTemp, onOffController.u) annotation (Line(points={{100,20},
          {106,20},{106,-14},{-86,-14},{-86,44},{-82,44}},        color={0,
          0,127}));
  connect(not1.u, onOffController.y)
    annotation (Line(points={{-42,50},{-59,50}}, color={255,0,255}));
    
  //connect(weatherReader.weaBus, weaBus) annotation (Line(
  //    points={{-80,110},{-30,110}},
  //    color={255,204,51},
  //    thickness=0.5), Text(
  //    string="%second",
  //    index=1,
  //    extent={{6,3},{6,3}},
  //    horizontalAlignment=TextAlignment.Left));
      
  //=======================================================  
  connect(fmu_TDryBul, weaBus.TDryBul);
  connect(fmu_TWetBul, weaBus.TWetBul);
  connect(fmu_TDewPoi, weaBus.TDewPoi);
  connect(fmu_TBlaSky, weaBus.TBlaSky);
  connect(fmu_relHum, weaBus.relHum);
  connect(fmu_HDirNor, weaBus.HDirNor);
  connect(fmu_HGloHor, weaBus.HGloHor);
  connect(fmu_HDifHor, weaBus.HDifHor);
  connect(fmu_HHorIR, weaBus.HHorIR);
  connect(fmu_winDir, weaBus.winDir);
  connect(fmu_winSpe, weaBus.winSpe);
  connect(fmu_ceiHei, weaBus.ceiHei);
  connect(fmu_nOpa, weaBus.nOpa);
  connect(fmu_nTot, weaBus.nTot);
  connect(fmu_lat, weaBus.lat);
  connect(fmu_lon, weaBus.lon);
  connect(fmu_alt, weaBus.alt);
  connect(fmu_pAtm, weaBus.pAtm);
  connect(fmu_solAlt, weaBus.solAlt);
  connect(fmu_solDec, weaBus.solDec);
  connect(fmu_solHouAng, weaBus.solHouAng);
  connect(fmu_solZen, weaBus.solZen);
  connect(fmu_solTim, weaBus.solTim);
  connect(fmu_cloTim, weaBus.cloTim);
      
  connect(crawlspaceThermalZone.FloorPort_Crawlspace, thermalZone.FloorPort)
    annotation (Line(points={{80,-20},{80,0}},  color={191,0,0}));
  connect(sinSpeDX.port_b, temperature.port_a)
    annotation (Line(points={{-60,10},{-40,10}},
                                               color={0,127,255}));
  connect(sinSpeDX.TConIn, weaBus.TDryBul) annotation (Line(points={{-81,13},
          {-100,13},{-100,98},{-30,98},{-30,110}},
                                                 color={0,0,127}), Text(
      string="%second",
      index=1,
      extent={{-6,3},{-6,3}},
      horizontalAlignment=TextAlignment.Right));
  //connect(sinSpeDX.on, not1.y) annotation (Line(points={{-81,18},{-81,36},{
          //-12,36},{-12,50},{-19,50}},
                                    //color={255,0,255})); 
  connect(temperature.port_b, fan.port_a)
    annotation (Line(points={{-20,10},{0,10}},
                                             color={0,127,255}));
  connect(fan.port_b, thermalZone.Supply) annotation (Line(points={{20,10},
          {46,10},{46,24},{60,24}},
                                  color={0,127,255}));
  connect(atticThermalZone_NewRoof.CeilingPort_Attic, thermalZone.CeilingPort)
    annotation (Line(points={{80,60},{80,40}}, color={191,0,0}));
  connect(weaBus, atticThermalZone_NewRoof.weaBus) annotation (Line(
      points={{-30,110},{106,110},{106,90},{70,90},{70,74}},
      color={255,204,51},
      thickness=0.5), Text(
      string="%first",
      index=-1,
      extent={{-3,6},{-3,6}},
      horizontalAlignment=TextAlignment.Right));
  connect(crawlspaceThermalZone.weaBus, atticThermalZone_NewRoof.weaBus)
    annotation (Line(
      points={{98,-21},{98,-4},{104,-4},{104,38},{108,38},{108,54},{100,54},
          {100,90},{70,90},{70,74}},
      color={255,204,51},
      thickness=0.5));
  connect(thermalZone.weaBus, atticThermalZone_NewRoof.weaBus) annotation (
      Line(
      points={{98,38},{108,38},{108,54},{100,54},{100,90},{70,90},{70,74}},
      color={255,204,51},
      thickness=0.5));
  connect(sinSpeDX.P, ACPower) annotation (Line(points={{-59,19},{-46,19},{
          -46,86},{116,86},{116,76},{130,76}},      color={0,0,127}));
  connect(fan.P, FanPower) annotation (Line(points={{21,19},{40,19},{40,82},
          {106,82},{106,64},{118,64},{118,56},{130,56}},
                      color={0,0,127}));
  connect(HouseTemp, thermalZone.ZoneTemp) annotation (Line(points={{130,0},
          {106,0},{106,20},{100,20}},  color={0,0,127}));
  connect(thermalZone.Return, sinSpeDX.port_a) annotation (Line(points={{60,20},
          {48,20},{48,-8},{-84,-8},{-84,10},{-80,10}},            color={0,
          127,255}));
  connect(FanOn.y, fan.m_flow_in) annotation (Line(points={{21,50},{26,50},  
          {26,28},{10,28},{10,22}}, color={0,0,127}));  
  
  // Clauded
  connect(forceOn.y, sinSpeDX.on);
  connect(weaBus, out.weaBus);
  connect(out.ports[1], leak.port_a);
  connect(leak.port_b, sinSpeDX.port_a);
  connect(fmu_coil, forceOn.u1);
  connect(startOn.y, forceOn.u2);
  connect(forceOn.y, sinSpeDX.on);
  // Clauded
  
  annotation (Icon(coordinateSystem(preserveAspectRatio=false, extent={{-100,
            -40},{120,120}})),                                   Diagram(
        coordinateSystem(preserveAspectRatio=false, extent={{-100,-40},{120,
            120}})),
    experiment(
      StartTime=0,
      StopTime=7200,
      Tolerance = 1e-6,
      Interval = 900));
end Complete_Revisions;
