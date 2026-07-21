#!/usr/bin/env python3
"""Summarize a Microsoft Code Coverage cobertura report for the meaningful namespaces.

Usage:
    python coverage-summary.py <path-to-*.cobertura.xml>

Reports line coverage for NOOSE_Website.Services / .Authorization / .Models only,
excluding the test project, Infrastructure, and Migrations. Classes marked
[ExcludeFromCodeCoverage] are already absent from the report.
"""
import sys
import collections
import xml.etree.ElementTree as ET

path = sys.argv[1]
buckets = {"Services": {}, "Authorization": {}, "Models": {}}


def bucket_for(fn):
    n = fn.replace("\\", "/")
    if "NOOSE-Website.Tests/" in n or "/Infrastructure/" in n or "/Migrations/" in n:
        return None
    for b in buckets:
        if f"/NOOSE-Website/{b}/" in n or n.startswith(f"{b}/"):
            return b
    return None


cur_file = None
cur_bucket = None
for ev, el in ET.iterparse(path, events=("start", "end")):
    if ev == "start" and el.tag == "class":
        cur_file = el.get("filename", "")
        cur_bucket = bucket_for(cur_file)
    elif ev == "end":
        if el.tag == "line" and cur_bucket:
            ln = el.get("number")
            hits = int(el.get("hits", "0"))
            d = buckets[cur_bucket]
            key = (cur_file, ln)
            if key not in d or hits > d[key]:
                d[key] = hits
        elif el.tag == "class":
            el.clear()

perfile = {}
tot_cov = tot_val = 0
for b, d in buckets.items():
    cov = sum(1 for v in d.values() if v > 0)
    val = len(d)
    tot_cov += cov
    tot_val += val
    pct = 100 * cov / val if val else 0
    print(f"{b:16} {cov:6}/{val:6}  {pct:5.1f}%")
    for (fn, ln), hits in d.items():
        pf = perfile.setdefault(fn, [0, 0])
        pf[1] += 1
        if hits > 0:
            pf[0] += 1
print("-" * 40)
pct = 100 * tot_cov / tot_val if tot_val else 0
print(f"{'TOTAL':16} {tot_cov:6}/{tot_val:6}  {pct:5.1f}%")
print("\n== least-covered files ==")
rows = sorted(perfile.items(), key=lambda kv: (kv[1][1] - kv[1][0]), reverse=True)
for fn, (c, v) in rows[:25]:
    short = fn.replace("\\", "/").split("/NOOSE-Website/")[-1]
    print(f"{v - c:5} miss  {c:5}/{v:5} {100 * c / v if v else 0:5.1f}%  {short}")
