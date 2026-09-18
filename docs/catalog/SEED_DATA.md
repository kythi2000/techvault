# Four-device proof catalog — Phase 5, with Phase 7 comparison metadata

The explicit `--seed-catalog` command uses the existing Device/typed-specification model and public read APIs. It inserts missing samples only; it is not an importer or editorial synchronization system. No schema or API contract changed in Phase 5.

## Dataset and classification

| Slug | Variant represented | Classification | Release information |
| --- | --- | --- | --- |
| `nokia-3310` | Original 2000 GSM handset; existing seed unchanged | Nokia; Phones → Feature Phones | Year 2000; exact retail date unknown |
| `nokia-3210` | Original 1999 GSM handset, not the 2024 reissue | Nokia; Phones → Feature Phones | Year 1999; exact retail date unknown |
| `macintosh-128k` | Original factory configuration, not a third-party RAM upgrade | Apple; Computers → All-in-One Computers | 1984-01-24 |
| `imac-g3` | First 1998 233 MHz configuration, not later G3 revisions | Apple; Computers → All-in-One Computers | 1998-08-15 |

Both computers use the same form-factor category, directly under `computers` as in SPEC. No unused desktop/laptop categories, parallel computer tables, or family/relationship records are created. Phase 7 adds explicit `phone` and `all_in_one` comparison groups; compatibility is no longer deferred and is not inferred from category names at query time.

On a fresh migrated/seeded database there are 4 Published devices, 2 brands, 4 categories, 2 comparison groups, 10 specification groups, 29 shared definitions, and 44 device-specific values. The 28 technical definitions are comparable; `announcement_date` is not. These counts are fixture expectations, not enforced production limits. Existing editorial data can legitimately change public counts and content.

## Sources and precision

The stored descriptions/history/SEO text are original summaries, not copied manuals or press releases. Source references live here rather than introducing a provenance schema in this phase.

### Nokia 3310

The unchanged seed uses [Nokia's 1 September 2000 announcement](https://www.globenewswire.com/js/news-release/2000/09/01/1845525/0/en/Nokia-introduces-mobile-chat-with-the-Nokia-3310.html). It has nine specifications, spanning text, number, boolean, and date. Announcement and retail release are not interchangeable: only the year is stored as release information, while `announcement_date` holds 2000-09-01. Exact retail/discontinuation dates and dimensions remain unset.

### Nokia 3210

Hardware/features come from the [original Nokia 3210 User's Guide, issue 2, document 9352002](https://www.instructionsmanuals.com/sites/default/files/2019-05/Nokia-3210-en.pdf), preserved by a third-party manual archive: printed pages 10 (GSM bands), 18 (antenna), 36–43 (SMS/predictive text/pictures), 58 (games), 64 (composer), 68–69 (covers), and 71–72 (NiMH battery).

The launch date and planned second-quarter availability come from [a contemporaneous reproduction of Nokia's 18 March 1999 CeBIT press release](https://groups.google.com/g/dk.teknik.telefoni.mobil/c/JST31lCgE3k), which identifies its original Nokia press-release URL. This is an archived copy, not a currently available Nokia-hosted page. The seed keeps the announcement date separate from the unknown exact retail date. Weight, dimensions, battery capacity, advertised talk/standby time, and discontinuation remain unset rather than borrowing unverified values or information from the modern reissue.

### Macintosh 128K

[Apple's technical specification](https://support.apple.com/en-us/112190) supplies the CPU, memory/slots, storage, display, Ethernet absence, physical measurements, introduction, and discontinuation dates. This sample describes factory hardware only. The screen note explicitly says 512 × 342; it takes precedence here over the contradictory generic 512 × 384 resolution row. The legacy software compatibility list is not used to invent an original OS version, so this sample has no `operating_system` value.

### iMac G3

[Apple's original iMac technical specification](https://support.apple.com/en-us/112281) supplies the 233 MHz CPU, base memory, RAM slots, disk/CD drive, display resolutions, USB, Ethernet, physical measurements, and 1998-08-15 introduction. [Apple's 6 May 1998 launch announcement](https://www.apple.com/ca/fr/press/1998/05/iMac.html) establishes the separate announcement date, original Mac OS 8.1 configuration, and Internet-focused positioning.

The technical page includes some revision/upgrade alternatives. The seed uses the base launch configuration and omits ambiguous maximum RAM, GPU/video-memory variants, and modem speed; it does not combine later upgrades with launch specifications. The source has no discontinuation date, so that remains null.

### Shared units and missing values

- Computer RAM uses the same numeric `ram_capacity` definition in **MiB**: 128 KiB becomes `0.125`, while the iMac's factory value is `32`. This normalizes the binary memory quantities historically labeled KB/MB.
- `cpu_clock` uses MHz on both computers. The iMac disk uses the advertised `4 GB`, without claiming an exact formatted byte count. The Macintosh floppy format stays textual (`400K`).
- Apple measurements are converted from inches/pounds using 25.4 mm/in and 453.59237 g/lb, then rounded to whole millimetres and 100 grams. Stored H/W/D/weight values are 345/244/277 mm and 7500 g for Macintosh, 401/386/447 mm and 18100 g for iMac. They are approximate conversions, not extra measurement precision.
- `ram_slots = 0`, `internal_hard_disk = false`, and `ethernet = false` on Macintosh are known values, not missing data. An absent specification remains unknown or outside this sample's documented scope.
- Definitions are reused by key and unit; only values assigned to a device appear in its response. Phone battery/messaging definitions do not leak into computer responses. Existing Nokia 3310 records are not backfilled with newly introduced definitions.

## Seed behavior and upgrade path

1. Verify `DATABASE_URL` targets the intended dedicated TechVault database. Apply all current migrations explicitly, including `AddCatalogComparisons`; there was no Phase 5 migration.
2. Run `dotnet run --project apps/api/src/TechVault.Api --launch-profile http -- --seed-catalog` from the repository root.
3. The command checks each device slug before loading its reference data. Existing devices are skipped wholesale, including Draft/Archived, reclassified, or partially populated records.
4. Missing devices reuse existing references or stage the required missing references. Type/unit or category-parent conflicts fail explicitly; labels, descriptions, display order, and other editorial metadata are never reset.
5. Each missing device and its new references/specifications are saved together once. Earlier device saves remain if a later device fails. After reviewing/fixing the reference conflict, rerun in a fresh process; existing devices are skipped and remaining ones are inserted.

Run one seed process at a time. Do not catch a seed failure and reuse that failed DbContext for more saves: it may still track unsaved references. There is no concurrent-seed retry, upsert, reconciliation, automatic startup seed, or automatic migration. A deleted sample slug will be inserted again on the next explicit seed; deleting only a specification from an existing device does not restore it.

For an existing Phase 3/4 database, this command adds only the three missing samples and their references. It never republishes or repairs Nokia 3310. The seed process exits after completion; run the API normally afterward. Local database mutation is an explicit operator step, not part of the automated test suite.

Phase 7's migration, not the seed command, initializes new compatibility/comparability metadata for existing recognized samples. It does not change old editorial fields or values. Fresh inserts use those defaults; reseeding never reassigns an existing device or resets an editor's `IsComparable` selection or group label. See [comparison migration details](../api/COMPARISON.md#persistence-migration-and-seed-safety), including how reclassified records remain unassigned.

## Verification

With Docker running:

```sh
dotnet build apps/api/TechVault.slnx
dotnet test apps/api/TechVault.slnx
dotnet ef migrations has-pending-model-changes --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
```

Mixed-catalog tests use disposable PostgreSQL databases. They exercise all four detail/specification responses, both browse types, category/brand/year intersections, cross-category pagination, shared units/definitions, and preservation of all stored rows during reseeding after editorial changes. Separate cases cover upgrading a Nokia-only database, existing incomplete drafts, conflicting reference metadata, and hidden computer visibility. Phase 3/4 regression fixtures remain intentionally narrow to preserve their empty-catalog and synthetic-data checks.

Use [the HTTP request examples](../../apps/api/src/TechVault.Api/TechVault.Api.http) against a deliberately seeded running API. Expected fresh-data results include two phones, two computers, four total devices, and the iMac/Nokia 3210 pair for the 1990s filter. API shape and handlers remain unchanged; search, timeline, comparison, admin writes, media, and further catalog expansion are out of scope.
