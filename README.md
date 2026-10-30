# Hotel Room Booking System

ASP.NET Core MVC (.NET 9) hotel booking application with SQL Server, Entity Framework Core 9,
ASP.NET Core Identity and JWT authentication stored in HttpOnly cookies.

## Features (implemented)

**Guests**
- Register, log in, log out (passwords hashed by ASP.NET Core Identity; lockout after 5 failed attempts)
- Browse active rooms, search by check-in date, check-out date and number of guests
- Room details with live availability and server-calculated total price
- Book an available room (status starts as *Pending*), booking confirmation with a unique reference
- "My bookings": list, details, cancel own Pending/Confirmed bookings before the check-in date
- Guests can only see and change their own bookings (other bookings return 404)

**Administrators**
- Dashboard: occupancy tonight, check-ins/outs today, bookings by status, booked value, upcoming arrivals
- Manage rooms: add, edit, view, activate/deactivate (rooms are never deleted)
- Manage bookings: list, filter by status, search, details, change status following defined rules

## Not implemented / known limitations
- **Payments:** the `Payments` table exists in the schema, but no payment flow (real or simulated) is implemented.
- **Room types and amenities** are seeded reference data; there is no admin screen to edit them.
- **Image uploads** are not implemented; rooms use an optional image URL (https or `/images/...`).
- **Automated tests:** no automated test project is included; testing was manual (see below).
- If two browser tabs refresh an expired session at exactly the same moment, refresh-token reuse
  detection may log the user out (deliberate security trade-off).
- The home page image is loaded from an external URL and may not be licensed for reuse.

## Technology
ASP.NET Core MVC (.NET 9), C#, Entity Framework Core 9, SQL Server, ASP.NET Core Identity,
JWT bearer (read from an HttpOnly cookie), Razor Views, Bootstrap 5.

## Setup

### Prerequisites
.NET SDK 9, SQL Server (Developer edition or similar), Visual Studio 2022 17.14+ (optional).

### 1. Database
Either:
- **Restore** `database/HotelBookingDB.bak` in SSMS (Databases > Restore Database), **or**
- Create an empty database `HotelBookingDB`, then in Visual Studio's Package Manager Console run `Update-Database`.

If your SQL Server instance is not `DESKTOP-224HQJ9`, change `ConnectionStrings:DefaultConnection`
in `src/HotelBookingSystem/appsettings.json`.

### 2. Secrets (never committed)
From `src/HotelBookingSystem` run (PowerShell):

```powershell
dotnet user-secrets init
$bytes = New-Object byte[] 64; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($bytes))

# Development admin account (created at startup in Development only; choose your own password)
dotnet user-secrets set "SeedAdmin:Email" "reviewer@hotel.local"
dotnet user-secrets set "SeedAdmin:FullName" "Reviewer Admin"
dotnet user-secrets set "SeedAdmin:Password" 'YourOwnPassword1'
```

The app refuses to start if `Jwt:Key` is missing or shorter than 256 bits.

### 3. Run
Open `src/HotelBookingSystem.sln`, press Ctrl+F5, browse to `https://localhost:7218`.
In Development, roles, room types, amenities and 7 sample rooms are seeded automatically.

## Test accounts
| Role  | Email | Password |
|-------|-------|----------|
| Guest | guest1@test.com | Guest1234 |
| Admin | the `SeedAdmin:Email` you configured | the password you configured |

(The guest account exists only if you restored the database backup; otherwise register a guest and configure the admin as above.)

## Key design decisions
- **Identity tables** (`AspNetUsers`, `AspNetRoles`, ...) replace custom Users/Roles tables; `ApplicationUser` adds FullName, IsActive, CreatedAtUtc.
- **Authentication:** short-lived JWT (15 min) + rotating refresh token (7 days), both in `__Host-` HttpOnly, Secure, SameSite=Strict cookies. Only SHA-256 hashes of refresh tokens are stored; reuse of a revoked token revokes all of the user's tokens.
- **Antiforgery** validation on every POST (global filter).
- **Money** is `decimal(10,2)`; each booking stores a snapshot of the price per night; totals are calculated on the server.
- **Dates** are SQL `date` (DateOnly); "today" uses the hotel time zone (India Standard Time).
- **Overlap rule:** `ExistingCheckIn < RequestedCheckOut AND ExistingCheckOut > RequestedCheckIn`, counting only Pending/Confirmed bookings. Strict comparisons allow same-day turnover.
- **Double-booking prevention:** booking creation runs in a transaction that takes a per-room SQL Server application lock (`sp_getapplock`) and re-checks availability inside the lock.
- **Admin status changes** follow explicit rules (Pending → Confirmed/Cancelled; Confirmed → Cancelled, or Completed once the stay has started) and use `RowVersion` optimistic concurrency.
- Database CHECK constraints and unique indexes back up the business rules.

## Manual test results
| Area | Result |
|------|--------|
| Registration, duplicate email, weak password, login, wrong password | Passed |
| JWT in HttpOnly/Secure/SameSite=Strict cookies; logout revokes refresh token | Passed |
| Refresh-token rotation (old token revoked and linked to replacement) | Passed |
| Guest vs Admin access (Access Denied for guests on admin pages) | Passed |
| Room add/edit/activate/deactivate, duplicate room number, unsafe image URL | Passed |
| Search: invalid dates, capacity, inactive rooms hidden | Passed |
| Overlap rule incl. same-day turnover; cancelled bookings free the room | Passed |
| Booking creation with server-side price; booking ownership (404 for others) | Passed |
| Guest cancellation | Passed |
| **Simultaneous booking test** (Development-only admin page, N parallel requests): exactly 1 succeeded | Passed |
| Admin status changes and optimistic concurrency | Passed |
| Not formally tested: refresh-token reuse detection, tampered token, deactivated user, lockout, antiforgery rejection | Not tested |

## Repository layout
```
src/            Visual Studio solution and project
database/       HotelBookingDB.bak, HotelBookingDB.sql, ER-Diagram.png
screenshots/    Desktop and mobile screenshots
```
