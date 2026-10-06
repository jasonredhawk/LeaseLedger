# LeaseLedger

Ontario landlord and tenant records: a website and an Android, iOS, and Windows app that share one set of screens. Landlords keep a rent ledger, maintenance timeline, and N1/N4 calculation helpers. Tenants see their balance, receipts, and maintenance updates.

Open [LeaseLedger.sln](LeaseLedger.sln) in Visual Studio.

## Website

1. Set **LeaseLedger.Web** as the startup project.
2. Press F5. The site runs at `https://localhost:7208`.

Development accounts, also linked from the sign-in page:

- Landlord: `landlord@demo.local` / `Demo123!`
- Tenant: `tenant@demo.local` / `Demo123!`

The database is SQL Server LocalDB, database name `LeaseLedger`.

## Windows app

1. Start **LeaseLedger.Web** and leave it running.
2. Set **LeaseLedger.Mobile** as the startup project.
3. Choose the **Windows Machine** target and press F5.

The app calls `https://localhost:7208`. Debug builds accept the local ASP.NET development certificate. Trust it once if Windows still blocks the call:

```powershell
dotnet dev-certs https --trust
```

## Android

Run **LeaseLedger.Mobile** on an Android emulator while the website is running. The debug app calls `https://10.0.2.2:7208`, which is the emulator's address for this computer.

## iOS

The iOS target is in the same MAUI project. Building it needs Visual Studio's Pair to Mac with Xcode. It does not compile on Windows alone.

## Notice helpers

N1, N2, and N4 screens calculate dates, guideline percentages, and arrears. They do not serve an official Landlord and Tenant Board form and they are not legal advice. Copy the figures onto the current form from Tribunals Ontario. A valid worksheet can be saved on the tenancy. N2 is only for units exempt from the guideline.

The ledger also has a printable year-end receipt, and each unit has an inspection log the tenant can read.

Guideline rows live in the database. The seed includes 2026 at 2.1% and 2027 at 1.9%. For a notice dated on or after September 21, 2026, the N4 termination date is 7 days later. Earlier monthly or yearly notices use 14 days.
