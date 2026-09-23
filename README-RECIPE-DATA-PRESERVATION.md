# Recipe data preservation fix

This build permanently removes the automatic startup migration that cleared recipes.

## Behaviour

- Existing `App_Data/app-data.json` recipe records are loaded and preserved.
- Application upgrades and restarts do not clear administrator-created recipes.
- A brand-new installation still starts with an empty recipe library.
- Recipe deletion remains an explicit administrator action only.

## Deployment note

When replacing an existing deployment, retain the current `App_Data/app-data.json` file. That file contains recipes created in the running application.
