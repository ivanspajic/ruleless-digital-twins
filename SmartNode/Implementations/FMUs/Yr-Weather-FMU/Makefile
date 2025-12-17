all: YrWeather.fmu

YrWeather.fmu: YrWeather-fmu.py requirements.txt
	pythonfmu build -f YrWeather-fmu.py requirements.txt

clean:
	rm -f YrWeather.fmu
