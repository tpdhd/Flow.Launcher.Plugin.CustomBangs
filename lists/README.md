# Bang list

Import [`tpdhd-custom-bangs-v6.json`](./tpdhd-custom-bangs-v6.json) (234 entries, including the `shop1` search group) from **Flow Settings → Plugins → Custom Bangs → Import**. Import replaces the current list after confirmation, so export a backup first.

This is a Flow Launcher list, not a Chrome or browser-extension list — it just happens to use the same version 6 JSON format as the [Custom Bang Search](https://github.com/psidex/CustomBangSearch) browser extension, so importing an export from that extension also works.

## Differences from the upstream list

The list starts from Custom Bang Search's own [Top 250 DuckDuckGo bang list](https://github.com/psidex/CustomBangSearch#lists):

- All one-character shortcuts are removed, since with an empty activator they could intercept ordinary Flow searches.
- `cults`, `mmf`, `thangs`, and `thing` were added for 3D-print model search sites.
- `mw` now points to MakerWorld instead of Merriam-Webster.
- The `shop1` search group was added, spanning Amazon, eBay, and AliExpress.

The public file is generated from the maintainer's working catalog with [`tools/sanitize-bang-export.py`](../tools/sanitize-bang-export.py).

## More lists

Custom Bang Search also publishes ready-to-import DuckDuckGo subsets:

- [Top 10](https://raw.githubusercontent.com/psidex/CustomBangSearch/master/ddg/ddg-top-10.json)
- [Top 25](https://raw.githubusercontent.com/psidex/CustomBangSearch/master/ddg/ddg-top-25.json)
- [Top 50](https://raw.githubusercontent.com/psidex/CustomBangSearch/master/ddg/ddg-top-50.json)
- [Top 100](https://raw.githubusercontent.com/psidex/CustomBangSearch/master/ddg/ddg-top-100.json)
- [Top 150](https://raw.githubusercontent.com/psidex/CustomBangSearch/master/ddg/ddg-top-150.json)
- [Top 200](https://raw.githubusercontent.com/psidex/CustomBangSearch/master/ddg/ddg-top-200.json)
- [Top 250](https://raw.githubusercontent.com/psidex/CustomBangSearch/master/ddg/ddg-top-250.json)

These files contain regular bangs only. Search groups can be added after import.
