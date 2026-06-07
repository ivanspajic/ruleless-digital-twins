#!/usr/bin/env python3
"""Generate a *draft* HA bindings JSON from a SOSA/RDT TTL.

Reads the TTL produced by ``hacvt_rdt.py`` and writes
``config/ha-bindings.<profile>.draft.json``. The output is always a
draft. A human reviewer must drop irrelevant entries, confirm URIs,
resolve every haKind warning, and rename the file to
``ha-bindings.<profile>.json`` before SmartNode can use it.

Strictly offline. No HA_TOKEN, no HA_URL, no network calls.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Optional

from rdflib import Graph, Namespace, URIRef

import binding


SOSA = Namespace("http://www.w3.org/ns/sosa/")
SSN = Namespace("http://www.w3.org/ns/ssn/")
RDT = Namespace(
    "http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/"
)


def _looks_like_secret(value: str) -> bool:
    if not isinstance(value, str) or not value:
        return False
    if value.startswith("eyJ"):
        return True
    if value.lower().startswith("bearer "):
        return True
    return False


def _load_graph(ttl_path: Path) -> Graph:
    g = Graph()
    g.parse(str(ttl_path), format="turtle")
    return g


def _query_sensors(g: Graph) -> list[tuple[URIRef, str, Optional[URIRef]]]:
    q = """
    PREFIX sosa:  <http://www.w3.org/ns/sosa/>
    PREFIX ssn:   <http://www.w3.org/ns/ssn/>
    PREFIX rdfs:  <http://www.w3.org/2000/01/rdf-schema#>
    PREFIX rdt:   <http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/>
    SELECT DISTINCT ?indiv ?id ?proc
    WHERE {
        ?indiv a/rdfs:subClassOf* sosa:Sensor ;
               rdt:hasIdentifier ?id .
        OPTIONAL { ?indiv ssn:implements ?proc . }
    }
    """
    out: list[tuple[URIRef, str, Optional[URIRef]]] = []
    for indiv, ent, proc in g.query(q):
        if not isinstance(indiv, URIRef):
            continue
        out.append((indiv, str(ent), proc if isinstance(proc, URIRef) else None))
    out.sort(key=lambda t: (t[1], str(t[0])))
    return out


def _query_actuators(g: Graph) -> list[tuple[URIRef, str]]:
    q = """
    PREFIX sosa:  <http://www.w3.org/ns/sosa/>
    PREFIX rdfs:  <http://www.w3.org/2000/01/rdf-schema#>
    PREFIX rdt:   <http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/>
    SELECT DISTINCT ?indiv ?id
    WHERE {
        ?indiv a/rdfs:subClassOf* sosa:Actuator ;
               rdt:hasIdentifier ?id .
    }
    """
    out: list[tuple[URIRef, str]] = []
    for indiv, ent in g.query(q):
        if not isinstance(indiv, URIRef):
            continue
        out.append((indiv, str(ent)))
    out.sort(key=lambda t: (t[1], str(t[0])))
    return out


def generate_bindings(
    ttl_path: Path,
    profile: str,
    platform: str = "ha:HomeAssistantTest",
    include_unknown: bool = False,
) -> tuple[dict, list[str]]:
    """Build the bindings dict + warnings list from a TTL file.

    By default the generated dict's ``actuators[]`` only contains entries
    whose ``haKind`` could be inferred — so the draft is loadable by
    HaBindingsLoader.cs without manual fixes. Actuators whose domain is
    not driveable by SmartNode (``climate.*``, ``cover.*``, …) are
    recorded under a top-level ``ignoredCandidates[]`` key (silently
    ignored by System.Text.Json on the C# side, since HaBindingsConfig
    has no matching property).

    Pass ``include_unknown=True`` to additionally place those entries in
    ``actuators[]`` with ``haKind = null``. The resulting file is then
    intentionally NOT loadable by SmartNode — it is for human review.
    """
    if not ttl_path.is_file():
        raise FileNotFoundError(f"TTL not found: {ttl_path}")

    g = _load_graph(ttl_path)
    warnings: list[str] = []

    sensors_out: list[dict] = []
    for sensor_uri, entity_id, proc in _query_sensors(g):
        try:
            entry = binding.make_sensor_binding(
                sensor_uri=str(sensor_uri),
                entity_id=entity_id,
                procedure_uri=str(proc) if proc else None,
            )
        except ValueError as ex:
            warnings.append(f"sensor {sensor_uri}: {ex}")
            continue
        sensors_out.append(entry)

    actuators_out: list[dict] = []
    ignored: list[dict] = []
    for actuator_uri, entity_id in _query_actuators(g):
        try:
            kind = binding.infer_ha_kind(entity_id)
        except ValueError as ex:
            warnings.append(f"actuator {actuator_uri}: {ex}")
            continue

        if kind is None:
            domain = entity_id.split(".", 1)[0]
            if domain in binding.KNOWN_UNSUPPORTED_DOMAINS:
                reason = (
                    f"HA domain {domain!r} is not driveable by SmartNode "
                    "(not in HomeAssistantActuator.ActuatorKind)"
                )
            else:
                reason = f"unknown HA domain {domain!r}"
            warnings.append(f"actuator {entity_id}: {reason}")
            ignored.append({
                "type": "actuator",
                "actuatorUri": str(actuator_uri),
                "haEntityId": entity_id,
                "reason": reason,
            })
            if not include_unknown:
                continue
            # else: also place this entry in actuators[] with haKind=null
            #   (caller knows the resulting file will fail HaBindingsLoader).

        try:
            entry = binding.make_actuator_binding(
                actuator_uri=str(actuator_uri),
                entity_id=entity_id,
                ha_kind=kind,
            )
        except ValueError as ex:
            warnings.append(f"actuator {actuator_uri}: {ex}")
            continue
        actuators_out.append(entry)

    out: dict = {
        "profile": profile,
        "platform": platform,
        "sensors": sensors_out,
        "actuators": actuators_out,
    }
    if ignored:
        # ``ignoredCandidates`` is metadata for the human reviewer.
        # HaBindingsConfig.cs does not declare this property and
        # System.Text.Json silently ignores unknown JSON properties at
        # the default settings used by HaBindingsLoader.Load — so this
        # field never reaches SmartNode runtime.
        out["ignoredCandidates"] = ignored
    return out, warnings


def _default_output_path(profile: str, output_dir: Path) -> Path:
    return output_dir / f"ha-bindings.{profile}.draft.json"


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Generate a draft HA bindings JSON from a SOSA/RDT TTL. "
            "Strictly offline. Output requires manual review before use."
        ),
    )
    parser.add_argument("--ttl", required=True, help="Input TTL path.")
    parser.add_argument(
        "--profile", required=True,
        help="Profile name (e.g. showcase, testlab).",
    )
    parser.add_argument(
        "--out", default=None,
        help="Output JSON path. Default: <output-dir>/ha-bindings.<profile>.draft.json. "
             "Must end with .draft.json.",
    )
    parser.add_argument(
        "--output-dir", default="config",
        help="Output directory when --out is not given (default: config).",
    )
    parser.add_argument(
        "--platform", default="ha:HomeAssistantTest",
        help="Platform IRI to record in the JSON (default: ha:HomeAssistantTest).",
    )
    parser.add_argument(
        "--overwrite", action="store_true",
        help="Allow overwriting an existing output file.",
    )
    parser.add_argument(
        "--include-unknown", dest="include_unknown",
        action="store_true", default=False,
        help=(
            "Also place actuators with an unsupported HA domain "
            "(climate/cover/unknown) in actuators[] with haKind=null. "
            "Default: off — such candidates only appear under "
            "ignoredCandidates[] for human review. With this flag the "
            "draft file will NOT pass HaBindingsLoader validation."
        ),
    )
    parser.add_argument(
        "--pretty", dest="pretty", action="store_true", default=True,
        help="Indent JSON output (default).",
    )
    parser.add_argument(
        "--no-pretty", dest="pretty", action="store_false",
        help="Emit compact JSON.",
    )

    args = parser.parse_args(argv)

    for k, v in vars(args).items():
        if isinstance(v, str) and _looks_like_secret(v):
            print(
                f"ERROR: argument --{k} looks like a secret. Aborting.",
                file=sys.stderr,
            )
            return 2

    ttl_path = Path(args.ttl)
    out_path = (
        Path(args.out) if args.out
        else _default_output_path(args.profile, Path(args.output_dir))
    )

    if out_path.suffix != ".json":
        print(
            f"ERROR: output path must end with .json: {out_path}",
            file=sys.stderr,
        )
        return 2

    # The generator must never produce ``ha-bindings.<x>.json`` — only the
    # ``.draft.json`` form — so the curated, reviewed file is never clobbered.
    if not out_path.name.endswith(".draft.json"):
        print(
            f"ERROR: output filename must end with .draft.json (got {out_path.name}). "
            "The generator is not allowed to write the final binding file directly.",
            file=sys.stderr,
        )
        return 2

    if out_path.exists() and not args.overwrite:
        print(
            f"ERROR: refusing to overwrite existing file {out_path} "
            "(pass --overwrite).",
            file=sys.stderr,
        )
        return 1

    try:
        result, warnings = generate_bindings(
            ttl_path=ttl_path,
            profile=args.profile,
            platform=args.platform,
            include_unknown=args.include_unknown,
        )
    except FileNotFoundError as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        return 1

    out_path.parent.mkdir(parents=True, exist_ok=True)
    indent = 2 if args.pretty else None
    with out_path.open("w", encoding="utf-8") as fp:
        json.dump(result, fp, indent=indent, sort_keys=False, ensure_ascii=False)
        fp.write("\n")

    ignored_count = len(result.get("ignoredCandidates", []))
    print(f"Reading TTL: {ttl_path}")
    print(
        f"Found {len(result['sensors'])} sensors, "
        f"{len(result['actuators'])} actuators"
    )
    if ignored_count:
        print(f"Ignored candidates: {ignored_count}")
    if warnings:
        print(f"Warnings: {len(warnings)}")
        for w in warnings:
            print(f"  - {w}")
    else:
        print("Warnings: 0")
    print(f"Output: {out_path}")
    if args.include_unknown and ignored_count:
        print(
            "WARNING: --include-unknown was set; this draft will NOT pass "
            "HaBindingsLoader validation until every haKind=null entry is "
            "fixed or removed."
        )
    print(
        "Draft written. Review manually before renaming to "
        f"ha-bindings.{args.profile}.json."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
