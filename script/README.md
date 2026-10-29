# Scripts

This stack-agnostic template does not ship a test runner. Add scripts here (for example `test.sh`, `lint.sh`) and document them in [AGENT_HANDOFF.md](../AGENT_HANDOFF.md) and [TEST_PLAN.md](../TEST_PLAN.md).

`Export-GeminiSnapshot.ps1` writes a gitignored XML dump of the repo for an external LLM. Focused packs still use [doc/gemini/](../doc/gemini/README.md).

`Export-SaveGameText.py` turns a Big Ambitions `.hsg` in `doc/gemini/dropzone/` into gitignored `doc/gemini/empire_summary.md`. The phrase is **saved game to text**.

`Slice-SaveGame.py` gunzips that save once into `doc/gemini/cache/save.bin` and writes a string index plus region and topic slices. Later questions read the cache. The map is `doc/gemini/cache/MAP.md`.

`statement_ledger.py` reads that index and writes `doc/gemini/cache/ledger/statements.csv` (`day, street, number, sales, goods, wages, rent, marketing, theft, profit`), `portfolio.csv` (`business, family, street, number, tco`), `routes.csv` (`warehouse, destination, item, day, truck`), `warehouses.csv` (`warehouse, street, number, item, amount`), and `REPORT.md` (bottom 10 by average daily profit, top offices, streets with 3+ of one family, warehouses that mix families). Version `1.2.2`.
