all: Fakepool.fmu

Fakepool.fmu: Fakepool.py fakepool.tsv requirements.txt
	pythonfmu build --no-external-tool -f Fakepool.py fakepool.tsv requirements.txt

clean:
	rm -f Fakepool.fmu
