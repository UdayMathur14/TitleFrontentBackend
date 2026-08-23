USE [SalesDataDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.TBL_TITLE_PUBLICATIONS', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TBL_TITLE_PUBLICATIONS
    (
        Id int IDENTITY(1,1) NOT NULL,
        RowNumber int NOT NULL CONSTRAINT DF_TBL_TITLE_PUBLICATIONS_RowNumber DEFAULT (0),
        InvoiceNumber varchar(250) NULL,
        PaperId varchar(250) NULL,
        CodeReference varchar(220) NULL,
        Title nvarchar(1200) NULL,
        CREATED_BY nvarchar(240) NULL,
        CREATED_ON date NULL,
        Status nvarchar(300) NULL,
        ReferenceTitle varchar(700) NULL,
        TitleYear varchar(204) NULL,
        UpdatedTitle nvarchar(1200) NULL,
        UpdatedReferenceTitle varchar(700) NULL,
        UpdatedTitleBy nvarchar(240) NULL,
        CONSTRAINT PK_TBL_TITLE_PUBLICATIONS PRIMARY KEY CLUSTERED (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TBL_TITLE_PUBLICATIONS') AND name = N'IX_TITLE_PUBLICATIONS_PaperId_InvoiceNumber')
    CREATE NONCLUSTERED INDEX IX_TITLE_PUBLICATIONS_PaperId_InvoiceNumber
        ON dbo.TBL_TITLE_PUBLICATIONS (PaperId, InvoiceNumber);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TBL_TITLE_PUBLICATIONS') AND name = N'IX_TITLE_PUBLICATIONS_ReferenceTitle')
    CREATE NONCLUSTERED INDEX IX_TITLE_PUBLICATIONS_ReferenceTitle
        ON dbo.TBL_TITLE_PUBLICATIONS (ReferenceTitle);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TBL_TITLE_PUBLICATIONS') AND name = N'IX_TITLE_PUBLICATIONS_UpdatedReferenceTitle')
    CREATE NONCLUSTERED INDEX IX_TITLE_PUBLICATIONS_UpdatedReferenceTitle
        ON dbo.TBL_TITLE_PUBLICATIONS (UpdatedReferenceTitle);
GO
