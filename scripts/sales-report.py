#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
KamCo sales report generator.

Reads the KamCo SQLite database (kamco.db) DIRECTLY — no API server needed —
and prints a complete sales report as JSON on stdout (default) and/or writes
CSV files, ready to be analyzed by an AI agent (Gemini CLI, Qwen Code, ...) or
by a human.

Python 3.8+ — standard library only, no pip installs required.

Usage:
    python scripts/sales-report.py                       # JSON to stdout
    python scripts/sales-report.py --format both         # JSON + CSV files
    python scripts/sales-report.py --format csv --outdir reports
    python scripts/sales-report.py --db path/to/kamco.db --months 6
    python scripts/sales-report.py --compact             # single-line JSON

JSON keys are English (stable for tooling); ask your AI agent to analyze /
translate the output — it is designed to be self-explanatory.
"""

import argparse
import csv
import json
import os
import re
import sqlite3
import sys
from datetime import datetime, timedelta, timezone
from decimal import Decimal

# Make stdout UTF-8 safe on Windows consoles (cp1252 etc.).
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

VALID_STATUSES = ("Pending", "Paid", "Shipped", "Cancelled")


# ---------------------------------------------------------------- helpers --

def parse_dt(value):
    """Parse the ISO-8601 variants Microsoft.Data.Sqlite writes for DateTime.
    Naive values are treated as UTC (the app always stores DateTime.UtcNow)."""
    s = str(value).strip()
    dt = None
    try:
        dt = datetime.fromisoformat(s.replace("Z", "+00:00"))
    except ValueError:
        pass
    if dt is None:
        for fmt in (
            "%Y-%m-%d %H:%M:%S.%f%z",
            "%Y-%m-%d %H:%M:%S%z",
            "%Y-%m-%d %H:%M:%S.%f",
            "%Y-%m-%d %H:%M:%S",
            "%Y-%m-%dT%H:%M:%S.%f",
            "%Y-%m-%dT%H:%M:%S",
        ):
            try:
                dt = datetime.strptime(s, fmt)
                break
            except ValueError:
                continue
    if dt is None:
        m = re.match(r"(\d{4})-(\d{2})-(\d{2})", s)
        if m:
            dt = datetime(int(m.group(1)), int(m.group(2)), int(m.group(3)))
    if dt is not None and dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt


def dec(value):
    """SQLite stores EF decimal columns as TEXT — convert safely."""
    return Decimal(str(value))


def money(d):
    return float(round(d, 2))


def find_db(arg_value):
    """Default locations: repo's KamCo.Api/kamco.db, then ./kamco.db."""
    if arg_value:
        return arg_value
    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_default = os.path.join(script_dir, os.pardir, "KamCo.Api", "kamco.db")
    if os.path.isfile(repo_default):
        return os.path.normpath(repo_default)
    if os.path.isfile("kamco.db"):
        return os.path.abspath("kamco.db")
    return repo_default


# ------------------------------------------------------------------ query --

def load_data(db_path):
    if not os.path.isfile(db_path):
        sys.stderr.write(
            f"ERROR: database not found: {db_path}\n"
            "Run the API once (cd KamCo.Api && dotnet run) to create it, "
            "or pass --db /path/to/kamco.db\n"
        )
        sys.exit(2)

    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    try:
        orders = [dict(r) for r in conn.execute(
            "SELECT Id, UserId, Status, TotalAmount, CreatedAt FROM Orders")]
        items = [dict(r) for r in conn.execute(
            "SELECT OrderId, ProductId, Quantity, UnitPrice FROM OrderItems")]
        products = {r["Id"]: dict(r) for r in conn.execute(
            "SELECT Id, Name, CategoryId, StockQuantity, IsActive, Price "
            "FROM Products")}
        categories = {r["Id"]: dict(r) for r in conn.execute(
            "SELECT Id, Name FROM Categories")}
        users_count = conn.execute("SELECT COUNT(*) FROM Users").fetchone()[0]
    finally:
        conn.close()

    for o in orders:
        o["_dt"] = parse_dt(o["CreatedAt"])
    return orders, items, products, categories, users_count


# ------------------------------------------------------------------ report --

def build_report(orders, items, products, categories, users_count,
                 top_n, months, low_stock_threshold):
    now = datetime.now(timezone.utc)

    def is_valid(o):  # revenue = non-cancelled orders only
        return o["Status"] != "Cancelled"

    def month_key(o):
        return o["_dt"].strftime("%Y-%m") if o["_dt"] else "unknown"

    items_by_order = {}
    for it in items:
        items_by_order.setdefault(it["OrderId"], []).append(it)

    valid_orders = [o for o in orders if is_valid(o)]
    cancelled_orders = [o for o in orders if o["Status"] == "Cancelled"]

    revenue_items = [it for it in items
                     if any(o["Id"] == it["OrderId"] for o in valid_orders)]
    units_sold = sum(int(it["Quantity"]) for it in revenue_items)
    revenue = sum(dec(it["UnitPrice"]) * int(it["Quantity"])
                  for it in revenue_items)

    def window_revenue(start, end):
        return sum(
            dec(it["UnitPrice"]) * int(it["Quantity"])
            for o in valid_orders if o["_dt"] and start <= o["_dt"] < end
            for it in items_by_order.get(o["Id"], [])
        )

    last_30 = window_revenue(now - timedelta(days=30), now + timedelta(days=1))
    prev_30 = window_revenue(now - timedelta(days=60), now - timedelta(days=30))
    trend = (float((last_30 - prev_30) / prev_30 * 100)
             if prev_30 > 0 else None)

    aov = (revenue / len(valid_orders)) if valid_orders else Decimal(0)

    # ---- monthly breakdown
    monthly = {}
    for o in orders:
        bucket = monthly.setdefault(month_key(o), {
            "orders": 0, "cancelled": 0, "units": 0, "revenue": 0.0})
        bucket["orders"] += 1
        if is_valid(o):
            for it in items_by_order.get(o["Id"], []):
                bucket["units"] += int(it["Quantity"])
                bucket["revenue"] += float(
                    round(dec(it["UnitPrice"]) * int(it["Quantity"]), 2))
        else:
            bucket["cancelled"] += 1
    monthly_rows = [{"month": m, **v, "revenue": money(Decimal(str(v["revenue"])))}
                    for m, v in sorted(monthly.items())]
    if months and months > 0:
        monthly_rows = monthly_rows[-months:]

    # ---- status distribution
    status_rows = []
    total = len(orders)
    for s in VALID_STATUSES:
        n = sum(1 for o in orders if o["Status"] == s)
        if n:
            status_rows.append({
                "status": s,
                "orders": n,
                "share_pct": round(n / total * 100, 1) if total else 0.0,
            })

    # ---- product / category performance (non-cancelled only)
    prod_stats, cat_stats = {}, {}
    for it in revenue_items:
        p = products.get(it["ProductId"], {})
        pname = p.get("Name", f"Product {it['ProductId']}")
        cname = categories.get(p.get("CategoryId"), {}).get("Name", "Unknown")
        qty = int(it["Quantity"])
        rev = dec(it["UnitPrice"]) * qty
        for stats, key in ((prod_stats, pname), (cat_stats, cname)):
            acc = stats.setdefault(key, {"units": 0, "revenue": Decimal(0)})
            acc["units"] += qty
            acc["revenue"] += rev

    def ranked(stats, name_key):
        rows = [{"name": k, "units": v["units"], "revenue": money(v["revenue"])}
                for k, v in stats.items()]
        rows.sort(key=lambda r: (-r["revenue"], -r["units"]))
        return rows

    top_products = ranked(prod_stats, "product")[:top_n]
    top_categories = ranked(cat_stats, "category")

    # ---- low stock alerts (active products only)
    low_stock = sorted(
        ({"product": p["Name"], "stock": int(p["StockQuantity"]),
          "price": money(dec(p["Price"]))}
         for p in products.values()
         if p.get("IsActive") and int(p["StockQuantity"]) < low_stock_threshold),
        key=lambda r: r["stock"])

    kpis = {
        "total_orders": total,
        "valid_orders": len(valid_orders),
        "cancelled_orders": len(cancelled_orders),
        "cancellation_rate_pct": round(
            len(cancelled_orders) / total * 100, 1) if total else 0.0,
        "total_revenue": money(revenue),
        "average_order_value": money(aov),
        "units_sold": units_sold,
        "unique_customers": len({o["UserId"] for o in valid_orders}),
        "registered_users": users_count,
        "revenue_last_30d": money(last_30),
        "revenue_prev_30d": money(prev_30),
        "revenue_trend_pct": round(trend, 1) if trend is not None else None,
    }

    return {
        "report": "KamCo sales report",
        "generated_at": now.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "currency": "USD",
        "kpis": kpis,
        "monthly": monthly_rows,
        "order_status": status_rows,
        "top_products": top_products,
        "top_categories": top_categories,
        "low_stock": low_stock,
    }


# ------------------------------------------------------------------- csv --

CSV_SECTIONS = {
    "kpis": ("kpis", ["key", "value"]),
    "sales_monthly": ("monthly", ["month", "orders", "cancelled", "units",
                                  "revenue"]),
    "order_status": ("order_status", ["status", "orders", "share_pct"]),
    "top_products": ("top_products", ["name", "units", "revenue"]),
    "top_categories": ("top_categories", ["name", "units", "revenue"]),
    "low_stock": ("low_stock", ["product", "stock", "price"]),
}


def write_csv(report, outdir):
    os.makedirs(outdir, exist_ok=True)
    written = []
    for fname, (key, columns) in CSV_SECTIONS.items():
        path = os.path.join(outdir, f"{fname}.csv")
        rows = report[key]
        if key == "kpis":
            rows = [{"key": k, "value": v} for k, v in rows.items()]
        with open(path, "w", newline="", encoding="utf-8-sig") as f:
            w = csv.DictWriter(f, fieldnames=columns)
            w.writeheader()
            w.writerows(rows)
        written.append(path)
    return written


# ------------------------------------------------------------------- main --

def main():
    ap = argparse.ArgumentParser(
        description="KamCo sales report from kamco.db (JSON / CSV).")
    ap.add_argument("--db", help="Path to kamco.db "
                                 "(default: <repo>/KamCo.Api/kamco.db)")
    ap.add_argument("--format", choices=("json", "csv", "both"), default="json",
                    help="json = stdout only (default); csv = write files; "
                         "both = write files AND print JSON")
    ap.add_argument("--outdir", default="reports",
                    help="Directory for CSV files (default: ./reports)")
    ap.add_argument("--months", type=int, default=0,
                    help="Limit the monthly table to the last N months")
    ap.add_argument("--top", type=int, default=10,
                    help="How many top products/categories to include")
    ap.add_argument("--low-stock", type=int, default=10,
                    help="Stock threshold for low-stock alerts (default: 10)")
    ap.add_argument("--compact", action="store_true",
                    help="Print single-line JSON")
    args = ap.parse_args()

    db_path = find_db(args.db)
    orders, items, products, categories, users_count = load_data(db_path)
    report = build_report(orders, items, products, categories, users_count,
                          top_n=max(args.top, 0), months=args.months,
                          low_stock_threshold=max(args.low_stock, 1))
    report["database"] = os.path.abspath(db_path)

    if args.format in ("csv", "both"):
        written = write_csv(report, args.outdir)
        for path in written:
            sys.stderr.write(f"Wrote {path}\n")

    if args.format in ("json", "both"):
        print(json.dumps(report, ensure_ascii=False,
                         indent=None if args.compact else 2,
                         separators=(",", ":") if args.compact else None))


if __name__ == "__main__":
    main()
