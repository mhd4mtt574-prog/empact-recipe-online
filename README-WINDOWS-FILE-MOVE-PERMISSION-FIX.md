# Windows File.Move permission fix

The application no longer replaces `app-data.json` with `File.Move(..., overwrite: true)`.
Some Windows security, antivirus, OneDrive, and controlled-folder-access configurations allow the temporary file to be written but deny the overwrite move.

The data store now:

- writes JSON directly to the selected data file;
- clears the read-only attribute before saving;
- flushes the file to disk before returning;
- retries automatically under `%LOCALAPPDATA%\\EmpactRecipeOnline\\App_Data\\app-data.json` when the selected destination denies access;
- keeps packaged recipe and APL seed files in the project `App_Data` directory.
