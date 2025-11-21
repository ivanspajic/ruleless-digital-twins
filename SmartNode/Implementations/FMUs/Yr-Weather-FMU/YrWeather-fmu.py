from datetime import datetime, timedelta, timezone
from pprint import pprint
from pythonfmu import Fmi2Causality, Fmi2Slave, Fmi2Variability, Boolean, Real
import sys
import traceback
import yr_weather

headers = {
    "User-Agent": "YrWeather FMU v0.1"
}

class YrWeather(Fmi2Slave):

    author = "Volker Stolz"
    description = "Yr Weather Forecast FMU"

    def __init__(self, **kwargs):
        super().__init__(**kwargs)

        self.lat = 60.3913
        self.long = 5.3221
        self.temperature = 0.0
        self.notFound = False
        self.register_variable(Real("lat", causality=Fmi2Causality.parameter, variability=Fmi2Variability.fixed))
        self.register_variable(Real("long", causality=Fmi2Causality.parameter, variability=Fmi2Variability.fixed))
        self.register_variable(Real("temperature", causality=Fmi2Causality.output))
        self.register_variable(Boolean("notFound", causality=Fmi2Causality.output, variability=Fmi2Variability.discrete))

    def setup_experiment(self, start_time, stop_time, tolerance):
        # Entry-point for main-branch of PythonFMU (>0.6.9)
        self.startTime = datetime.now(timezone.utc)+timedelta(seconds=start_time)

    def enter_initialization_mode(self):
        # The parameters/start values are set *after* `setup_experiment`, so fetch them here.
        self.fetcher = Fetcher((self.lat, self.long))
        # We need to update the very first data in our FMU, so we just call explicitly into `do_step` and feel dirty about it:
        self.do_step(0, 0)

    def do_step(self, current_time, step_size):
        # sys.stderr.write(f"step: {current_time} {step_size}\n")
        result = self.fetcher.set_data_for_time(self.startTime+timedelta(seconds=current_time+step_size))
        if result is None:
            self.notFound = True
            self.temperature = 0.0
        else:
            self.notFound = False
            self.temperature, _ = result
            # TODO: check units.
        return True

class Fetcher:
    def __init__(self, latlong):
        self.lat, self.long = latlong
        self.client = yr_weather.Locationforecast(headers=headers)
        self.forecast = self.client.get_forecast(self.lat, self.long)

    def set_data_for_time(self, dt):
        # sys.stderr.write(f"Query for ({self.lat},{self.long}): {dt}\n")
        try:
            f_now = self.forecast.get_forecast_time(dt)
            if f_now is None:
                # sys.stderr.write(f"No result for ({self.lat},{self.long}): {dt}\n")
                return None
            return (f_now.details.air_temperature, f_now.details.cloud_area_fraction)
        except KeyError:
            # Unclear where those come from.
            # I also saw some discontiguous values, i.e. notFound = .., False, True, .., True, False, ..
            return None
        except Exception as e:
            traceback.print_exception(e, file=sys.stderr)
            sys.stderr.write("Crashing.\n")
            return None

if __name__ == "__main__":
    o = Fetcher((60.3913,5.133))
    now = datetime.now(timezone.utc)+timedelta(seconds=0)
    print(now)
    ps = o.set_data_for_time(now)
    pprint(ps)
