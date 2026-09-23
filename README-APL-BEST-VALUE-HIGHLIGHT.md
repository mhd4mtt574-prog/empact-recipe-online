# APL best-value highlighting

The recipe ingredient picker now normalizes APL pack sizes before comparing prices:

- Grams and kilograms are compared as cost per kilogram.
- Millilitres and litres are compared as cost per litre.
- Count packs are compared as cost per item.
- Multipacks such as `24X500ML` are calculated using the full pack contents (12 litres).

The lowest comparable unit cost in each search-result measurement group is highlighted in green and labelled **Best value**. Administrators may still select any approved product. Products whose pack-size text cannot be normalized remain selectable and display **Not comparable**.
