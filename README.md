# Empact Recipe Online — MVP

A .NET 8 ASP.NET Core MVC web version of the Food Recipe & Costing Management prototype.

## Included

- Browser-based login and self-registration
- Administrator, Head Office, Unit Manager and Unit User roles
- Shared JSON recipe storage for MVP testing
- Recipe library with search and category filtering
- Add and Edit Recipe for Administrator, Head Office and Unit Manager
- Ingredient costing, waste calculations, batch cost and cost per portion
- Automatic allergen detection plus manual confirmation
- Still-image uploads with moving zoom effects
- GIF, MP4 and WebM uploads for genuinely moving meal visuals
- Animated login background and interactive recipe cards
- Printable standardised recipe sheet
- Responsive desktop, tablet and mobile layout

## Run locally

1. Install **Visual Studio 2022** with **ASP.NET and web development**, or install the .NET 8 SDK.
2. Extract the ZIP to a normal folder.
3. Open `EmpactRecipeOnline.sln`.
4. Press **F5**, or run `dotnet run` from the project folder.
5. Accept the local HTTPS certificate prompt when shown.

### Demo accounts

- Administrator: `admin@empact.local` / `admin123`
- Unit Manager: `manager@empact.local` / `manager123`

## Data and media

- MVP data is stored in `App_Data/app-data.json` after first run.
- Uploaded media is stored in `wwwroot/uploads`.
- Back up both folders during testing.

## Important production work

This source is a functional MVP, not yet a hardened production deployment. Before public or company-wide hosting, replace JSON storage with SQL Server/PostgreSQL, add administrator approval for registrations, password reset/email confirmation, audit logs, anti-malware scanning for uploads, cloud media storage, backups, privacy controls and deployment secrets.

Recipe allergens generated from ingredient names must be verified against supplier labels and specifications.

## Fix included in this package
The shared recipe-card partial is now included under `Views/Shared/_RecipeCard.cshtml`, and all card links explicitly target `RecipesController`. This resolves the `_RecipeCard` partial-view error shown after login.
