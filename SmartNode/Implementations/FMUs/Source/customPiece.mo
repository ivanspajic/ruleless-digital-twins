model customPiece
  input Integer YrWeatherCategory(start = 0); // Represents a simplistic classification of Yr's weather forecasts.
  
  Integer BucketSize;
  Real Epsilon;
  String CompressedData;

equation
  CompressedData = "fake base64 binary";
  BucketSize = 10;
  Epsilon = 0.5;
  
annotation(
    experiment(StartTime = 0, StopTime = 8000, Tolerance = 1e-06, Interval = 1));
end customPiece;
