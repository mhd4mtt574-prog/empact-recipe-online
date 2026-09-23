# Dashboard resale preview fix

The Food-brand dashboard resale preview now reads directly from `ResalePricingService` / `App_Data/resale-pricing.json`.

- Recipe records are no longer shown inside the Resale Pricing dashboard card.
- The signed-in unit code is matched to its resale Category A/B allocation where available.
- The preview shows real resale item description, item code / pack, current nett each cost and selling price.
- Administrator accounts without a mapped resale unit receive a Category A management preview, containing resale products only.
- The full Resale Pricing module remains unchanged and continues to enforce its ID-based access.
