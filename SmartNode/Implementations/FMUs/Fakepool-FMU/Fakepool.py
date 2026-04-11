import csv
import pathlib
from pythonfmu import Fmi2Causality, Fmi2Slave, Fmi2Variability, Boolean, Real, String, Integer
import sys
import traceback


class Fakepool(Fmi2Slave):

    author = "Volker Stolz"
    description = "Nordpool fake data FMU"

    def __init__(self, **kwargs):
        super().__init__(**kwargs)

        self.data = []
        self.zone = "NO5"
        self.step = 900 # "width" of CSV in seconds, i.e. 15 minutes
        self.resolution = 15 # minutes
        self.notFound = False
        self.price = 0.0
        self.register_variable(String("zone", causality=Fmi2Causality.parameter, variability=Fmi2Variability.fixed))
        # How "long" should we use one row? Optional.
        self.register_variable(Integer("step", causality=Fmi2Causality.parameter, variability=Fmi2Variability.fixed))
        # Unused:
        self.register_variable(Integer("resolution", causality=Fmi2Causality.parameter, variability=Fmi2Variability.fixed))
        self.register_variable(Boolean("notFound", causality=Fmi2Causality.output))
        self.register_variable(Real("price", causality=Fmi2Causality.output))

    def setup_experiment(self, start_time, stop_time, tolerance):
        # Entry-point for main-branch of PythonFMU (>0.6.9)
        # TODO: worry about `start_time`?
        pass

    def enter_initialization_mode(self):
        # sys.stderr.write("enter_initialization_mode\n")
        # The parameters/start values are set *after* `setup_experiment`, so fetch them here.
        header_found = False
        # TODO: Should be in constructor.
        parent_path = pathlib.Path(__file__).parent
        assert parent_path.name == "resources"
        with open(f"{parent_path}/fakepool.tsv", "r") as f:
            reader = csv.reader(f, delimiter="\t")
            for row in reader:
                if row[0] == "state":
                    assert not header_found
                    header_found = True
                else: # We only take the value and ignore timestamps completely
                    self.data.append(float(row[0]))
        assert header_found
        # We need to update the very first data in our FMU, so we just call explicitly into `do_step` and feel dirty about it:
        self.do_step(0, 0)

    def do_step(self, current_time, step_size):
        interval = int((current_time+step_size) // self.step)
        try:
            result = self.data[interval]
            # sys.stderr.write(f"step: {current_time} {step_size} {interval} {result}\n")
            self.notFound = False
            self.price = result
        except IndexError:
            self.notFound = True
            self.price = 0.0
        return True
