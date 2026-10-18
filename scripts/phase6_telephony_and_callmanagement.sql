BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE TABLE [crm].[CallRecords] (
        [Id] uniqueidentifier NOT NULL,
        [UniqueId] nvarchar(100) NOT NULL,
        [LinkedId] nvarchar(100) NOT NULL,
        [Direction] int NOT NULL,
        [Status] int NOT NULL,
        [CallerNumber] nvarchar(50) NOT NULL,
        [NormalizedCallerNumber] nvarchar(50) NOT NULL,
        [CallerName] nvarchar(200) NULL,
        [DestinationNumber] nvarchar(50) NOT NULL,
        [Extension] nvarchar(50) NULL,
        [Channel] nvarchar(200) NULL,
        [DestinationChannel] nvarchar(200) NULL,
        [StartTimeUtc] datetime2 NOT NULL,
        [AnswerTimeUtc] datetime2 NULL,
        [EndTimeUtc] datetime2 NULL,
        [DurationSeconds] int NULL,
        [BillableSeconds] int NULL,
        [PartyId] uniqueidentifier NULL,
        [ContactId] uniqueidentifier NULL,
        [IsUnknownCaller] bit NOT NULL,
        [MatchConfidence] nvarchar(20) NOT NULL,
        [RecordingFileName] nvarchar(250) NULL,
        [RecordingFilePath] nvarchar(500) NULL,
        [RecordingDurationSeconds] int NULL,
        [RecordingFileSize] bigint NULL,
        [HasRecording] bit NOT NULL,
        [CdrId] nvarchar(100) NULL,
        [ReconciliationStatus] int NOT NULL,
        [DisconnectionCause] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CallRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallRecords_Parties_PartyId] FOREIGN KEY ([PartyId]) REFERENCES [Parties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CallRecords_PartyContacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [PartyContacts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE TABLE [crm].[UserExtensions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Username] nvarchar(100) NOT NULL,
        [Extension] nvarchar(50) NOT NULL,
        [DeviceType] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [Description] nvarchar(250) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_UserExtensions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserExtensions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE TABLE [crm].[CallAudits] (
        [Id] uniqueidentifier NOT NULL,
        [CallRecordId] uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [PerformedBy] nvarchar(100) NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [DetailsJson] nvarchar(4000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CallAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallAudits_CallRecords_CallRecordId] FOREIGN KEY ([CallRecordId]) REFERENCES [crm].[CallRecords] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE TABLE [crm].[CallNotes] (
        [Id] uniqueidentifier NOT NULL,
        [CallRecordId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Username] nvarchar(100) NOT NULL,
        [NoteText] nvarchar(4000) NOT NULL,
        [Outcome] nvarchar(50) NOT NULL,
        [FollowUpRequired] bit NOT NULL,
        [FollowUpDueDateUtc] datetime2 NULL,
        [FollowUpAssignedTo] nvarchar(100) NULL,
        [FollowUpStatus] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CallNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallNotes_CallRecords_CallRecordId] FOREIGN KEY ([CallRecordId]) REFERENCES [crm].[CallRecords] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallAudits_CallRecordId] ON [crm].[CallAudits] ([CallRecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallAudits_TimestampUtc] ON [crm].[CallAudits] ([TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallNotes_CallRecordId] ON [crm].[CallNotes] ([CallRecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallNotes_FollowUpStatus] ON [crm].[CallNotes] ([FollowUpStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_ContactId] ON [crm].[CallRecords] ([ContactId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_Extension] ON [crm].[CallRecords] ([Extension]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_LinkedId] ON [crm].[CallRecords] ([LinkedId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_NormalizedCallerNumber] ON [crm].[CallRecords] ([NormalizedCallerNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_PartyId] ON [crm].[CallRecords] ([PartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_StartTimeUtc] ON [crm].[CallRecords] ([StartTimeUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_Status] ON [crm].[CallRecords] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CallRecords_UniqueId] ON [crm].[CallRecords] ([UniqueId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserExtensions_Extension] ON [crm].[UserExtensions] ([Extension]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_UserExtensions_UserId] ON [crm].[UserExtensions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018042000_Phase6_TelephonyAndCallManagement', N'8.0.10');
END;
GO

COMMIT;
GO

