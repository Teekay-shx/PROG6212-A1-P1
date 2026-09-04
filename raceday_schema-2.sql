/* =========================================================================
   RaceDay System - Database Schema and Seed Data
   Target: SQL Server (SSMS)
   
   Entities (7): Users, Organisers, Participants, Events, Categories,
                 Enrolments, Results
   =========================================================================
   Run this script on a clean SQL Server instance from top to bottom.
   ========================================================================= */

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

/* -------------------------------------------------------------------------
   Drop tables if they already exist (in FK-safe order) so the script is
   re-runnable during testing.
   ------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Participants', 'U') IS NOT NULL DROP TABLE dbo.Participants;
IF OBJECT_ID('dbo.Organisers', 'U') IS NOT NULL DROP TABLE dbo.Organisers;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

/* =========================================================================
   1. USERS
   Base table for authentication and role-based access. Every Organiser
   and every Participant has exactly one corresponding Users row.
   ========================================================================= */
CREATE TABLE dbo.Users (
    UserID          INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(100)   NOT NULL,
    Email           NVARCHAR(150)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(255)   NOT NULL,
    Role            NVARCHAR(20)    NOT NULL
                        CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin','Organiser','Participant')),
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

/* =========================================================================
   2. ORGANISERS
   One-to-one with Users. Only users with Role = 'Organiser' get a row here.
   ========================================================================= */
CREATE TABLE dbo.Organisers (
    OrganiserID       INT IDENTITY(1,1) PRIMARY KEY,
    UserID            INT             NOT NULL UNIQUE,
    OrganisationName  NVARCHAR(150)   NOT NULL,
    ContactPhone      NVARCHAR(20)    NULL,
    CreatedAt         DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Organisers_Users FOREIGN KEY (UserID)
        REFERENCES dbo.Users(UserID)
        ON DELETE CASCADE
);
GO

/* =========================================================================
   3. PARTICIPANTS
   One-to-one with Users. Only users with Role = 'Participant' get a row here.
   ========================================================================= */
CREATE TABLE dbo.Participants (
    ParticipantID           INT IDENTITY(1,1) PRIMARY KEY,
    UserID                  INT             NOT NULL UNIQUE,
    DateOfBirth             DATE            NOT NULL,
    Gender                  NVARCHAR(10)    NULL
                                CONSTRAINT CK_Participants_Gender CHECK (Gender IN ('Male','Female','Other')),
    EmergencyContactName    NVARCHAR(100)   NULL,
    EmergencyContactPhone   NVARCHAR(20)    NULL,
    CreatedAt               DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Participants_Users FOREIGN KEY (UserID)
        REFERENCES dbo.Users(UserID)
        ON DELETE CASCADE
);
GO

/* =========================================================================
   4. EVENTS
   Many-to-one with Organisers (one Organiser runs many Events).
   ========================================================================= */
CREATE TABLE dbo.Events (
    EventID       INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID   INT             NOT NULL,
    EventName     NVARCHAR(150)   NOT NULL,
    EventDate     DATE            NOT NULL,
    Location      NVARCHAR(150)   NOT NULL,
    Description   NVARCHAR(MAX)   NULL,
    Status        NVARCHAR(20)    NOT NULL DEFAULT 'Planned'
                        CONSTRAINT CK_Events_Status CHECK (Status IN ('Planned','Open','Closed','Completed','Cancelled')),
    CreatedAt     DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Organisers FOREIGN KEY (OrganiserID)
        REFERENCES dbo.Organisers(OrganiserID)
        ON DELETE CASCADE
);
GO

/* =========================================================================
   5. CATEGORIES
   Many-to-one with Events (one Event has many race Categories,
   e.g. 5K, 10K, Half Marathon).
   ========================================================================= */
CREATE TABLE dbo.Categories (
    CategoryID       INT IDENTITY(1,1) PRIMARY KEY,
    EventID          INT             NOT NULL,
    CategoryName     NVARCHAR(100)   NOT NULL,
    DistanceKm       DECIMAL(5,2)    NOT NULL,
    MinAge           INT             NOT NULL DEFAULT 0,
    MaxAge           INT             NULL,
    EntryFee         DECIMAL(8,2)    NOT NULL DEFAULT 0,
    MaxParticipants  INT             NULL,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventID)
        REFERENCES dbo.Events(EventID)
        ON DELETE CASCADE,
    CONSTRAINT UQ_Categories_EventName UNIQUE (EventID, CategoryName)
);
GO

/* =========================================================================
   6. ENROLMENTS
   Resolves the many-to-many between Participants and Categories:
   one Participant can enrol in many Categories, and one Category can
   have many Participants enrolled.
   ========================================================================= */
CREATE TABLE dbo.Enrolments (
    EnrolmentID     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID   INT             NOT NULL,
    CategoryID      INT             NOT NULL,
    BibNumber       NVARCHAR(10)    NULL,
    EnrolmentDate   DATETIME        NOT NULL DEFAULT GETDATE(),
    PaymentStatus   NVARCHAR(20)    NOT NULL DEFAULT 'Pending'
                        CONSTRAINT CK_Enrolments_PaymentStatus CHECK (PaymentStatus IN ('Pending','Paid','Refunded')),
    CONSTRAINT FK_Enrolments_Participants FOREIGN KEY (ParticipantID)
        REFERENCES dbo.Participants(ParticipantID)
        ON DELETE CASCADE,
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryID)
        REFERENCES dbo.Categories(CategoryID)
        ON DELETE NO ACTION,
    CONSTRAINT UQ_Enrolments_ParticipantCategory UNIQUE (ParticipantID, CategoryID)
);
GO

/* =========================================================================
   7. RESULTS
   One-to-one with Enrolments: each enrolment has at most one result record.
   ========================================================================= */
CREATE TABLE dbo.Results (
    ResultID     INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID  INT             NOT NULL UNIQUE,
    FinishTime   TIME            NULL,
    Position     INT             NULL,
    Status       NVARCHAR(20)    NOT NULL DEFAULT 'Registered'
                     CONSTRAINT CK_Results_Status CHECK (Status IN ('Registered','Finished','DNF','DNS','DQ')),
    RecordedAt   DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID)
        REFERENCES dbo.Enrolments(EnrolmentID)
        ON DELETE CASCADE
);
GO


/* =========================================================================
   SEED DATA
   ========================================================================= */

-- ---- Users (2 Organisers + 2 Participants) --------------------------------
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role) VALUES
('Sarah Mitchell',  'sarah.mitchell@raceday.co.za',  'hashed_pw_001', 'Organiser'),
('David Nkosi',     'david.nkosi@raceday.co.za',     'hashed_pw_002', 'Organiser'),
('Thabo Mokoena',   'thabo.mokoena@example.com',     'hashed_pw_003', 'Participant'),
('Emma van Wyk',    'emma.vanwyk@example.com',       'hashed_pw_004', 'Participant');
GO

-- ---- Organisers -------------------------------------------------------------
INSERT INTO dbo.Organisers (UserID, OrganisationName, ContactPhone) VALUES
((SELECT UserID FROM dbo.Users WHERE Email = 'sarah.mitchell@raceday.co.za'), 'Johannesburg Road Runners',    '0114567890'),
((SELECT UserID FROM dbo.Users WHERE Email = 'david.nkosi@raceday.co.za'),     'Cape Peninsula Athletics Club', '0219876543');
GO

-- ---- Participants -------------------------------------------------------------
INSERT INTO dbo.Participants (UserID, DateOfBirth, Gender, EmergencyContactName, EmergencyContactPhone) VALUES
((SELECT UserID FROM dbo.Users WHERE Email = 'thabo.mokoena@example.com'), '1995-03-14', 'Male',   'Lindiwe Mokoena', '0821234567'),
((SELECT UserID FROM dbo.Users WHERE Email = 'emma.vanwyk@example.com'),   '1998-07-22', 'Female', 'Pieter van Wyk',  '0837654321');
GO

-- ---- Events (3) -------------------------------------------------------------
INSERT INTO dbo.Events (OrganiserID, EventName, EventDate, Location, Description, Status) VALUES
((SELECT OrganiserID FROM dbo.Organisers WHERE OrganisationName = 'Johannesburg Road Runners'),
    'Joburg City Marathon', '2026-10-18', 'Johannesburg CBD', 'Annual road running event through the Johannesburg city centre.', 'Open'),
((SELECT OrganiserID FROM dbo.Organisers WHERE OrganisationName = 'Johannesburg Road Runners'),
    'Sandton Fun Run', '2026-11-08', 'Sandton, Johannesburg', 'Family-friendly fun run in support of local charities.', 'Open'),
((SELECT OrganiserID FROM dbo.Organisers WHERE OrganisationName = 'Cape Peninsula Athletics Club'),
    'Table Mountain Trail Challenge', '2026-09-27', 'Cape Town', 'Off-road trail race around the base of Table Mountain.', 'Planned');
GO

-- ---- Categories (2 per event) -------------------------------------------------------------
INSERT INTO dbo.Categories (EventID, CategoryName, DistanceKm, MinAge, MaxAge, EntryFee, MaxParticipants) VALUES
((SELECT EventID FROM dbo.Events WHERE EventName = 'Joburg City Marathon'), '10K',            10.0, 16, NULL, 150.00, 2000),
((SELECT EventID FROM dbo.Events WHERE EventName = 'Joburg City Marathon'), 'Half Marathon',  21.1, 18, NULL, 250.00, 1500),
((SELECT EventID FROM dbo.Events WHERE EventName = 'Sandton Fun Run'),      '5K Fun Run',      5.0, 0,  NULL,  80.00, 3000),
((SELECT EventID FROM dbo.Events WHERE EventName = 'Sandton Fun Run'),      '5K Timed',        5.0, 12, NULL, 100.00, 1000),
((SELECT EventID FROM dbo.Events WHERE EventName = 'Table Mountain Trail Challenge'), '15K Trail', 15.0, 18, NULL, 200.00, 800),
((SELECT EventID FROM dbo.Events WHERE EventName = 'Table Mountain Trail Challenge'), '25K Trail', 25.0, 18, 60,   300.00, 500);
GO

-- ---- Enrolments -------------------------------------------------------------
INSERT INTO dbo.Enrolments (ParticipantID, CategoryID, BibNumber, PaymentStatus) VALUES
((SELECT ParticipantID FROM dbo.Participants p JOIN dbo.Users u ON p.UserID = u.UserID WHERE u.Email = 'thabo.mokoena@example.com'),
    (SELECT CategoryID FROM dbo.Categories WHERE CategoryName = 'Half Marathon'), 'JHB-1001', 'Paid'),
((SELECT ParticipantID FROM dbo.Participants p JOIN dbo.Users u ON p.UserID = u.UserID WHERE u.Email = 'thabo.mokoena@example.com'),
    (SELECT CategoryID FROM dbo.Categories WHERE CategoryName = '15K Trail'), 'CT-2001', 'Pending'),
((SELECT ParticipantID FROM dbo.Participants p JOIN dbo.Users u ON p.UserID = u.UserID WHERE u.Email = 'emma.vanwyk@example.com'),
    (SELECT CategoryID FROM dbo.Categories WHERE CategoryName = '5K Timed'), 'SDT-3001', 'Paid');
GO

-- ---- Results (sample, for the completed/paid enrolment) -------------------------------------------------------------
INSERT INTO dbo.Results (EnrolmentID, FinishTime, Position, Status) VALUES
((SELECT EnrolmentID FROM dbo.Enrolments WHERE BibNumber = 'JHB-1001'), '01:38:22', 42, 'Finished'),
((SELECT EnrolmentID FROM dbo.Enrolments WHERE BibNumber = 'SDT-3001'), NULL, NULL, 'Registered');
GO

/* =========================================================================
   Quick sanity checks (optional - comment out before final submission if
   your marker only wants the CREATE/INSERT statements)
   ========================================================================= */
-- SELECT * FROM dbo.Users;
-- SELECT * FROM dbo.Organisers;
-- SELECT * FROM dbo.Participants;
-- SELECT * FROM dbo.Events;
-- SELECT * FROM dbo.Categories;
-- SELECT * FROM dbo.Enrolments;
-- SELECT * FROM dbo.Results;
