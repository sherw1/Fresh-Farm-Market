-- Fresh Farm Market Database Setup Script
-- Run this after creating migrations if you need to manually create tables

-- Create Members Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Members')
BEGIN
    CREATE TABLE Members (
        Id INT PRIMARY KEY IDENTITY(1,1),
        FullName NVARCHAR(100) NOT NULL,
        EncryptedCreditCard NVARCHAR(256) NOT NULL,
        Gender NVARCHAR(50) NOT NULL,
        MobileNo NVARCHAR(20) NOT NULL,
        DeliveryAddress NVARCHAR(200) NOT NULL,
        Email NVARCHAR(256) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(512) NOT NULL,
        PhotoPath NVARCHAR(260) NULL,
        AboutMe NVARCHAR(1000) NULL,
        FailedLoginAttempts INT NOT NULL DEFAULT 0,
        LockoutEndTime DATETIME2 NULL,
        PreviousPasswordHash1 NVARCHAR(512) NULL,
        PreviousPasswordHash2 NVARCHAR(512) NULL,
        LastPasswordChangeDate DATETIME2 NULL
    );
    
    CREATE UNIQUE INDEX IX_Members_Email ON Members(Email);
END

-- Create AuditLogs Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE AuditLogs (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Email NVARCHAR(256) NOT NULL,
        Action NVARCHAR(100) NOT NULL,
        Timestamp DATETIME2 NOT NULL,
        IpAddress NVARCHAR(50) NULL,
        IsSuccess BIT NOT NULL,
        Details NVARCHAR(500) NULL
    );
    
    CREATE INDEX IX_AuditLogs_Email ON AuditLogs(Email);
    CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp);
END

-- Create LoginAttempts Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoginAttempts')
BEGIN
    CREATE TABLE LoginAttempts (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Email NVARCHAR(256) NOT NULL,
        AttemptTime DATETIME2 NOT NULL,
        IsSuccess BIT NOT NULL,
        IpAddress NVARCHAR(50) NULL
    );
    
    CREATE INDEX IX_LoginAttempts_Email ON LoginAttempts(Email);
    CREATE INDEX IX_LoginAttempts_AttemptTime ON LoginAttempts(AttemptTime);
END

-- Create UserSessions Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserSessions')
BEGIN
    CREATE TABLE UserSessions (
        Id INT PRIMARY KEY IDENTITY(1,1),
        MemberId INT NOT NULL,
        SessionId NVARCHAR(450) NOT NULL,
        LoginTime DATETIME2 NOT NULL,
        LastActivityTime DATETIME2 NOT NULL,
        IpAddress NVARCHAR(50) NULL,
        IsActive BIT NOT NULL,
        FOREIGN KEY (MemberId) REFERENCES Members(Id)
    );
    
    CREATE INDEX IX_UserSessions_MemberId ON UserSessions(MemberId);
    CREATE INDEX IX_UserSessions_SessionId ON UserSessions(SessionId);
    CREATE INDEX IX_UserSessions_IsActive ON UserSessions(IsActive);
END

-- Sample Queries for Monitoring

-- View all audit logs
-- SELECT * FROM AuditLogs ORDER BY Timestamp DESC;

-- View failed login attempts
-- SELECT Email, COUNT(*) as FailedAttempts, MAX(AttemptTime) as LastAttempt
-- FROM LoginAttempts 
-- WHERE IsSuccess = 0 
-- GROUP BY Email;

-- View locked accounts
-- SELECT Email, FullName, LockoutEndTime, FailedLoginAttempts 
-- FROM Members 
-- WHERE LockoutEndTime > GETUTCDATE();

-- View active sessions
-- SELECT m.Email, us.LoginTime, us.LastActivityTime, us.IpAddress
-- FROM UserSessions us
-- INNER JOIN Members m ON us.MemberId = m.Id
-- WHERE us.IsActive = 1;
