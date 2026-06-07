all: NordPool.fmu

NordPool.fmu: NordPool-fmu.py requirements.txt nordpool/elspot.py
	pythonfmu build -f NordPool-fmu.py requirements.txt nordpool

clean:
	rm -f NordPool.fmu
