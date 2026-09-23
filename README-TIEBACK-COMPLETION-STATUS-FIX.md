# Tie-back completion status fix

The tie-back upload and compliance/task checks now use one canonical unit matcher.

## Fixed
- A stored unit such as `S0490 - Unit Name` matches `S0490`, `Unit Name`, or the full display value.
- My Tasks clears the daily tie-back item immediately after a successful upload.
- Administrator Units Needing Attention no longer marks a completed tie-back as outstanding.
- Compliance dashboard tie-back percentages recognise the same upload.
- Daily report rows and download authorisation use the same unit matching rule.
- Re-uploading for the same date/unit replaces the prior record instead of creating a duplicate when the unit representation differs.
- Login now also carries a `unitCode` claim for future requests.
