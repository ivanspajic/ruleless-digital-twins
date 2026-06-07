from pythonfmu import Fmi2Causality, Fmi2Slave, Fmi2Variability, Boolean, Integer, Real, String
from nordpool import elspot
from pprint import pprint
from datetime import date, datetime, timedelta, timezone
import sys

class NordPool(Fmi2Slave):

    author = "Volker Stolz"
    description = "Nord Pool FMU"

    def __init__(self, **kwargs):
        super().__init__(**kwargs)

        self.prices_spot = elspot.Prices()
        self.zone = "NO1"
        self.resolution = 15 # minutes, alternative: 60
        self.price = 0.0
        self.notFound = False
        self.register_variable(String("zone", causality=Fmi2Causality.parameter, variability=Fmi2Variability.fixed))
        self.register_variable(Integer("resolution", causality=Fmi2Causality.parameter, variability=Fmi2Variability.fixed))
        self.register_variable(Real("price", causality=Fmi2Causality.output))
        self.register_variable(Boolean("notFound", causality=Fmi2Causality.output, variability=Fmi2Variability.discrete))

    def setup_experiment(self, start_time, stop_time, tolerance):
        # Baseline is "now" plus whatever delta you gave
        self.startTime = datetime.now(timezone.utc)+timedelta(seconds=start_time)

    def exit_initialization_mode(self):
        # The parameters/start values are set *after* `setup_experiment`, so fetch them here.
        self.fetcher = Fetcher(self.zone, self.resolution)
        # We need to update the very first data in our FMU, so we just call explicitly into `do_step` and feel dirty about it:
        self.do_step(0, 0)

    def do_step(self, current_time, step_size):
        # sys.stderr.write(f"step: {current_time} {step_size}\n")
        result = self.fetcher.set_data_for_time(self.startTime+timedelta(seconds=current_time+step_size))
        if result is None:
            self.notFound = True
            self.price = 0.0
        else:
            self.notFound = False
            self.price = result
        return True

class Fetcher:
    def __init__(self, zone, resolution):
        self.prices_spot = elspot.Prices()
        self.zone = zone
        self.resolution = resolution

    def set_data_for_time(self, dt):
        try:
            if date.today() == dt.date() and dt.hour < 23:
              # Goes only up to 23:00?!
              prices = self.prices_spot.fetch(areas=[self.zone], end_date=date.today(), resolution=self.resolution)
            else:
              # No micro-optimization to preempt if we're far off; we'll just fall through for now
              prices = self.prices_spot.fetch(areas=[self.zone], resolution=self.resolution)
            if prices is None:
              sys.stderr.write(f"NordPool-FMU: didn't get any results for time {dt}, zone {self.zone}, resolution {self.resolution}.\n")
              return None
            areas = prices["areas"]
            area_values = areas[self.zone]["values"]
            for entry in area_values:
              # print(f"{entry["start"]} {dt} {entry["end"]}")
              if entry["start"] <= dt < entry["end"]:
                  return entry["value"]
            return None
        except Exception as e:
            sys.stderr.write("crashing\n" + str(e))
            return None

if __name__ == "__main__":
    o = Fetcher("NO5", 60)
    now = datetime.now(timezone.utc)+timedelta(seconds=36000)
    print(now)
    ps = o.set_data_for_time(now)
    pprint(ps)
