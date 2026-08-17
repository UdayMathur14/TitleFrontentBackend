USE [SalesDataDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE [type] = 'PK'
      AND parent_object_id = OBJECT_ID(N'dbo.TBL_TITLES')
)
BEGIN
    ALTER TABLE dbo.TBL_TITLES
        ADD CONSTRAINT PK_TBL_TITLES PRIMARY KEY CLUSTERED (Id);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TBL_TITLES') AND name = N'IX_TBL_TITLES_ReferenceTitle')
BEGIN
    CREATE NONCLUSTERED INDEX IX_TBL_TITLES_ReferenceTitle
        ON dbo.TBL_TITLES (ReferenceTitle)
        INCLUDE (InvoiceNumber, CodeReference);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TBL_TITLES') AND name = N'IX_TBL_TITLES_TitleYear_Id')
BEGIN
    CREATE NONCLUSTERED INDEX IX_TBL_TITLES_TitleYear_Id
        ON dbo.TBL_TITLES (TitleYear, Id DESC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TBL_TITLES') AND name = N'IX_TBL_TITLES_InvoiceNumber_Id')
BEGIN
    CREATE NONCLUSTERED INDEX IX_TBL_TITLES_InvoiceNumber_Id
        ON dbo.TBL_TITLES (InvoiceNumber, Id DESC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TBL_TITLES') AND name = N'IX_TBL_TITLES_CodeReference_Id')
BEGIN
    CREATE NONCLUSTERED INDEX IX_TBL_TITLES_CodeReference_Id
        ON dbo.TBL_TITLES (CodeReference, Id DESC);
END;
GO

UPDATE STATISTICS dbo.TBL_TITLES WITH FULLSCAN;
GO
