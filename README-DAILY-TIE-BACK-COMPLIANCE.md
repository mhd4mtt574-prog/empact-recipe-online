# Daily Tie-Back Compliance

- All authenticated users can upload one daily tie-back sheet for their unit from Reports & tie-back.
- Accepted formats: PDF, XLS, XLSX, XLSM and CSV, up to 25 MB.
- A replacement upload for the same unit and date supersedes the prior metadata entry.
- Administrators receive an in-app overdue alert after 09:00 and can track every unit by day.
- Statuses: Pending, Overdue, Uploaded on time and Uploaded late.
- Administrators can filter, print, download submitted files and export the daily report to CSV.
- Notifications are in-app and appear when an administrator opens the dashboard or reports area; no external email/SMS service is configured.

## Planned and actual GP dashboard values
- XLSX/XLSM tie-back uploads are read automatically when submitted.
- The extractor uses the repeated Financial Impact block in the supplied tie-back template and stores the total Planned GP % / Rand value and Actual GP % / Rand value on the daily submission record.
- The Daily tie-back dashboard shows the GP percentage first and the Rand value directly underneath for both Planned GP and Actual GP.
- Replacement uploads refresh all four GP values for that unit/date.
- Older/non-Excel uploads remain valid evidence; their GP dashboard values display as unavailable when the figures cannot be extracted.
