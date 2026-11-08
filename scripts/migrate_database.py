"""Back up, rehearse, verify, then migrate the legacy Auditorium SQLite database.

Run after dotnet build Auditorium.slnx:
  python3 scripts/migrate_database.py --database Auditorium.Web/auditorium.db
Requires Python 3 and the installed dotnet-ef tool. The database must not be
written by another process while this maintenance command is running.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sqlite3
import subprocess
import tempfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
UTC_MIGRATION = "20261027191206_PreserveLegacyScheduleAndUseUtcTime"
MIGRATION = "20261108130929_MapRoomsByScheduleCode"
ARCHIVES = {"Audiences": "LegacyAudiences", "Lessons": "LegacyLessons"}
WORKING_TABLES = ("Rooms", "Users", "ScheduleEntries", "OccupancyMarks")


def connect(path):
    # mode=rw prevents accidentally creating a database at a wrong path.
    return sqlite3.connect(path.as_uri() + "?mode=rw", uri=True, timeout=30)


def table_fingerprint(db, table):
    digest = hashlib.sha256()
    count = 0
    columns = '"Id", "Number", "ScheduleDates"' if table == "Rooms" else "*"
    for row in db.execute(f'SELECT {columns} FROM "{table}" ORDER BY "Id"'):
        digest.update(json.dumps(row, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
        digest.update(b"\n")
        count += 1
    return count, digest.hexdigest()


def check_integrity(db):
    if db.execute("PRAGMA integrity_check").fetchall() != [("ok",)]:
        raise RuntimeError("SQLite integrity check failed")
    if db.execute("PRAGMA foreign_key_check").fetchall():
        raise RuntimeError("SQLite foreign key check failed")


def snapshot(db, has_utc_schema):
    if not has_utc_schema:
        return {table: table_fingerprint(db, table) for table in ARCHIVES}
    tables = tuple(ARCHIVES.values()) + WORKING_TABLES
    if db.execute("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'ScheduleImportState'").fetchone():
        tables += ("ScheduleImportState",)
    return {table: table_fingerprint(db, table) for table in tables}


def verify(path, expected, has_utc_schema):
    with connect(path) as db:
        check_integrity(db)
        for table, fingerprint in expected.items():
            target = table if has_utc_schema else ARCHIVES[table]
            if table_fingerprint(db, target) != fingerprint:
                raise RuntimeError(f"Data mismatch: {table}")
        if not has_utc_schema:
            if db.execute('SELECT count(*) FROM "Rooms"').fetchone()[0] != expected["Audiences"][0]:
                raise RuntimeError("Room count mismatch")
            if db.execute('SELECT "Id", "Name" FROM "LegacyAudiences" EXCEPT SELECT "Id", "Number" FROM "Rooms"').fetchall():
                raise RuntimeError("Audience identifiers or names changed")
            if db.execute("SELECT count(*) FROM Rooms WHERE ScheduleDates != '[]'").fetchone()[0]:
                raise RuntimeError("Migration invented schedule coverage dates")
            for table in ("Users", "ScheduleEntries", "OccupancyMarks"):
                if db.execute(f'SELECT count(*) FROM "{table}"').fetchone()[0]:
                    raise RuntimeError(f"Unexpected generated data in {table}")
        if db.execute("SELECT 1 FROM pragma_table_info('Rooms') WHERE name = 'Status'").fetchone():
            raise RuntimeError("Persisted room status was not removed")
        if db.execute("""
            SELECT 1 FROM Rooms JOIN LegacyAudiences USING (Id)
            WHERE SourceScheduleCode IS NOT Rooms.Id
        """).fetchone():
            raise RuntimeError("Legacy room codes were not preserved")
        db.execute('SELECT "LastSuccessfulUpdate", "UpdateFailed" FROM "ScheduleImportState"').fetchall()
        if not db.execute('SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId"=?', (MIGRATION,)).fetchone():
            raise RuntimeError("Migration history was not updated")
    return {"rooms": expected["Rooms" if has_utc_schema else "Audiences"][0],
            "archived_lessons": expected["LegacyLessons" if has_utc_schema else "Lessons"][0],
            "integrity": "ok", "foreign_keys": "ok", "verified_sha256": expected}


def migrate(path):
    subprocess.run(["dotnet", "ef", "database", "update", MIGRATION, "--project",
                    str(ROOT / "Auditorium.Infrastructure"), "--no-build", "--connection",
                    f"Data Source={path};Default Timeout=30"], cwd=ROOT, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--database", type=Path, required=True)
    args = parser.parse_args()
    database = args.database.resolve(strict=True)
    backup_dir = database.parent / "backups"
    backup_dir.mkdir(exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    backup = backup_dir / f"auditorium.before-schedule-storage.{stamp}.db"
    with connect(database) as source:
        check_integrity(source)
        if source.execute('SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId"=?', (MIGRATION,)).fetchone():
            print("Migration already applied; no changes made.")
            return
        has_utc_schema = source.execute(
            'SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId"=?', (UTC_MIGRATION,)
        ).fetchone() is not None
        expected = snapshot(source, has_utc_schema)
        with sqlite3.connect(backup) as target:
            source.backup(target)
    print(f"BACKUP: {backup}", flush=True)
    with tempfile.TemporaryDirectory(prefix="auditorium-migration-") as temp:
        rehearsal = Path(temp) / "auditorium.db"
        shutil.copy2(backup, rehearsal)
        migrate(rehearsal)
        print("REHEARSAL:", json.dumps(verify(rehearsal, expected, has_utc_schema), ensure_ascii=False), flush=True)
        # The second call must leave both data and migration history unchanged.
        migrate(rehearsal)
        verify(rehearsal, expected, has_utc_schema)
    with connect(database) as source:
        if snapshot(source, has_utc_schema) != expected:
            raise RuntimeError("Database changed during rehearsal; stop writers and retry")
    migrate(database)
    report = verify(database, expected, has_utc_schema)
    report.update(database=str(database), backup=str(backup), migration=MIGRATION)
    report_path = backup.with_suffix(".verification.json")
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("VERIFIED:", json.dumps(report, ensure_ascii=False), flush=True)
    print(f"REPORT: {report_path}", flush=True)


if __name__ == "__main__":
    main()
