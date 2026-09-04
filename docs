 

  

IF DB_ID('RaceDayDB') IS NOT NULL 

BEGIN 

ALTER DATABASE RaceDayDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE; 

DROP DATABASE RaceDayDB; 

END 

GO 

  

CREATE DATABASE RaceDayDB; 

GO 

  

USE RaceDayDB; 

GO 

  

/* --------------------------------------------------------- 

1. Users (both Organisers and Participants) 

--------------------------------------------------------- */ 

CREATE TABLE Users ( 

UserId INT IDENTITY(1,1) PRIMARY KEY, 

FullName NVARCHAR(100) NOT NULL, 

Email NVARCHAR(150) NOT NULL UNIQUE, 

PasswordHash NVARCHAR(255) NOT NULL, 

Role VARCHAR(20) NOT NULL CHECK (Role IN ('Organiser','Participant')), 

PhoneNumber VARCHAR(20) NULL, 

CreatedAt DATETIME NOT NULL DEFAULT GETDATE() 

); 

GO 

  

/* --------------------------------------------------------- 

2. RefreshTokens (supports JWT auth in Part 2) 

--------------------------------------------------------- */ 

CREATE TABLE RefreshTokens ( 

TokenId INT IDENTITY(1,1) PRIMARY KEY, 

UserId INT NOT NULL, 

Token NVARCHAR(255) NOT NULL, 

ExpiresAt DATETIME NOT NULL, 

CreatedAt DATETIME NOT NULL DEFAULT GETDATE(), 

CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE 

); 

GO 

  

/* --------------------------------------------------------- 

3. Events 

--------------------------------------------------------- */ 

CREATE TABLE Events ( 

EventId INT IDENTITY(1,1) PRIMARY KEY, 

OrganiserId INT NOT NULL, 

Name NVARCHAR(150) NOT NULL, 

Description NVARCHAR(1000) NULL, 

EventDate DATETIME NOT NULL, 

EventType VARCHAR(20) NOT NULL CHECK (EventType IN ('Run','Walk','Cycle')), 

Status VARCHAR(20) NOT NULL DEFAULT 'Published' CHECK (Status IN ('Draft','Published','Cancelled','Completed')), 

CreatedAt DATETIME NOT NULL DEFAULT GETDATE(), 

CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserId) REFERENCES Users(UserId) 

); 

GO 

  

/* --------------------------------------------------------- 

4. Venues (one venue per event - start/finish location) 

--------------------------------------------------------- */ 

CREATE TABLE Venues ( 

VenueId INT IDENTITY(1,1) PRIMARY KEY, 

EventId INT NOT NULL UNIQUE, 

AddressLine NVARCHAR(200) NOT NULL, 

City NVARCHAR(100) NOT NULL, 

Province NVARCHAR(100) NOT NULL, 

Latitude DECIMAL(9,6) NULL, 

Longitude DECIMAL(9,6) NULL, 

CONSTRAINT FK_Venues_Events FOREIGN KEY (EventId) REFERENCES Events(EventId) ON DELETE CASCADE 

); 

GO 

  

/* --------------------------------------------------------- 

5. Categories (distance categories within an event) 

--------------------------------------------------------- */ 

CREATE TABLE Categories ( 

CategoryId INT IDENTITY(1,1) PRIMARY KEY, 

EventId INT NOT NULL, 

Name NVARCHAR(50) NOT NULL, 

DistanceKm DECIMAL(5,2) NOT NULL, 

MaxParticipants INT NOT NULL, 

EntryFee DECIMAL(8,2) NOT NULL DEFAULT 0, 

CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES Events(EventId) ON DELETE CASCADE 

); 

GO 

  

/* --------------------------------------------------------- 

6. Enrolments (Participant enters a Category) 

--------------------------------------------------------- */ 

CREATE TABLE Enrolments ( 

EnrolmentId INT IDENTITY(1,1) PRIMARY KEY, 

ParticipantId INT NOT NULL, 

CategoryId INT NOT NULL, 

BibNumber VARCHAR(10) NULL, 

EnrolmentDate DATETIME NOT NULL DEFAULT GETDATE(), 

Status VARCHAR(20) NOT NULL DEFAULT 'Confirmed' CHECK (Status IN ('Pending','Confirmed','Cancelled')), 

CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantId) REFERENCES Users(UserId), 

CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId), 

CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantId, CategoryId) 

); 

GO 

  

/* --------------------------------------------------------- 

7. Results (one result per Enrolment) 

--------------------------------------------------------- */ 

CREATE TABLE Results ( 

ResultId INT IDENTITY(1,1) PRIMARY KEY, 

EnrolmentId INT NOT NULL UNIQUE, 

FinishTime TIME NULL, 

OverallPosition INT NULL, 

CategoryPosition INT NULL, 

RaceStatus VARCHAR(15) NOT NULL DEFAULT 'Finished' CHECK (RaceStatus IN ('Finished','DNF','DQ')), 

RecordedAt DATETIME NOT NULL DEFAULT GETDATE(), 

CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId) ON DELETE CASCADE 

); 

GO 

  

/* ============================================================ 

SEED DATA 

============================================================ */ 

  

-- Organisers (2) and Participants (2) 

INSERT INTO Users (FullName, Email, PasswordHash, Role, PhoneNumber) VALUES 

('Thandiwe Nkosi', 'thandiwe.nkosi@raceday.co.za', 'HASHED_PW_1', 'Organiser', '0821234567'), 

('Johan van der Merwe','johan.vdm@raceday.co.za', 'HASHED_PW_2', 'Organiser', '0837654321'), 

('Lindiwe Dube', 'lindiwe.dube@example.com', 'HASHED_PW_3', 'Participant', '0791112222'), 

('Pieter Botha', 'pieter.botha@example.com', 'HASHED_PW_4', 'Participant', '0824445555'); 

GO 

  

-- Events (3) 

INSERT INTO Events (OrganiserId, Name, Description, EventDate, EventType, Status) VALUES 

(1, 'Pretoria Park Run Challenge', 'A community 5km/10km run through Pretoria parks.', '2026-10-10 07:00:00', 'Run', 'Published'), 

(1, 'Tshwane Cycle Classic', 'Road cycling event around Tshwane.', '2026-11-01 06:30:00', 'Cycle', 'Published'), 

(2, 'Cape Winelands Marathon', 'Scenic marathon and half-marathon through the Winelands.', '2026-09-20 06:00:00', 'Run', 'Published'); 

GO 
