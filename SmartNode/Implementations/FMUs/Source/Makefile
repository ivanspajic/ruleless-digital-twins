OMC=omc -d=gendebugsymbols
TARGETS=au_incubator.fmu roomM370.fmu

all: $(TARGETS)

clean:
	rm -f $(TARGETS)

roomM370.fmu: roomM370.mo
	$(OMC) doit.mos

au_incubator.fmu: au_incubator.mo
	$(OMC) buildIncubator.mos
